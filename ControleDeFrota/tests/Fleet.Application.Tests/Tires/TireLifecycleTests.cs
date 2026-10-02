using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Mileage;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Tires;
using Fleet.Domain.Authorization;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Tires;

public class TireLifecycleTests : TireTestBase
{
    // ---------- registry ----------

    [Fact]
    public async Task Create_WithoutCode_GeneratesFireNumberAndStartsInStock()
    {
        await ArrangeAsync();

        var tire = await NewTireAsync();

        tire.Code.Should().Be("PN-000001");
        tire.Status.Should().Be(TireStatus.InStock);
        tire.CurrentTreadDepthMm.Should().Be(16, "a new tire starts with the model's original tread");
        tire.ManufacturedOn.Should().Be(new DateOnly(2024, 8, 26), "DOT 3524 = week 35 of 2024");
        tire.StorageLocation.Should().Be("Almoxarifado A");
        (await T.Db.OperationalEvents.CountAsync(e => e.TireId == tire.Id && e.Type == OperationalEventType.TireRegistered)).Should().Be(1);
    }

    [Fact]
    public async Task Create_DuplicateCode_Conflict()
    {
        await ArrangeAsync();
        await NewTireAsync("FOGO-10");

        var act = () => NewTireAsync("fogo-10");

        (await act.Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("code");
    }

    [Fact]
    public async Task Create_PurchasePriceWithoutCostPermission_Forbidden()
    {
        await ArrangeAsync();
        T.CurrentUser.PermissionSet = [Permissions.Tires.View, Permissions.Tires.Create];

        var act = () => NewTireAsync(price: 1_500m);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await NewTireAsync(price: null)).PurchasePrice.Should().BeNull();
    }

    [Fact]
    public async Task Delete_TireWithHistory_IsRefused()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");

        var act = () => Tires.DeleteAsync(tire.Id, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*Dê baixa*");
    }

    // ---------- installation (seções 11, 12, 66) ----------

    [Fact]
    public async Task Install_ValidPosition_RecordsVehicleKmAndUpdatesTheLayout()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();

        var installed = await InstallAsync(tire.Id, "2EE");

        installed.Status.Should().Be(TireStatus.Installed);
        installed.StorageLocation.Should().BeNull();
        installed.Location!.PositionCode.Should().Be("2EE");
        installed.Location.PositionLabel.Should().Be("Eixo 2 — Esquerdo externo");
        installed.Location.InstalledOdometerKm.Should().Be(100_000, "installation km = vehicle current km (seção 38)");
        var asset = await Operations.GetAssetAsync(VehicleId, null, default);
        asset.Positions.Single(p => p.Position.Code == "2EE").Tire!.Code.Should().Be(tire.Code);
        asset.InstalledCount.Should().Be(1);
        asset.EmptyRequiredCount.Should().Be(5, "6 required positions, the spare is optional");
        (await T.Db.OperationalEvents.CountAsync(e => e.TireId == tire.Id && e.VehicleId == VehicleId && e.Type == OperationalEventType.TireInstalled))
            .Should().Be(1, "the installation appears in the tire and in the vehicle timelines");
    }

    [Fact]
    public async Task Install_OccupiedPosition_ConflictWithoutReplacingSilently()
    {
        await ArrangeAsync();
        var first = await NewTireAsync();
        var second = await NewTireAsync();
        await InstallAsync(first.Id, "1E");

        var act = () => InstallAsync(second.Id, "1E");

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("Substituir");
        (await ReloadAsync(second.Id)).Status.Should().Be(TireStatus.InStock);
    }

    [Fact]
    public async Task Install_PositionNotInLayout_ValidationError()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();

        var act = () => InstallAsync(tire.Id, "3EE");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("positionCode");
    }

    [Fact]
    public async Task Install_SizeNotAllowedOnAxle_IsRefused()
    {
        await ArrangeAsync(allowedSteerSize: "275/80R22.5");
        var tire = await NewTireAsync();

        var act = () => InstallAsync(tire.Id, "1E");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("275/80R22.5");
        (await InstallAsync(tire.Id, "2EE")).Status.Should().Be(TireStatus.Installed, "the drive axle has no size configured");
    }

    [Fact]
    public async Task Compatibility_PositionWithoutSize_SaysItCouldNotBeVerified()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();

        var result = await Tires.CompatibilityAsync(tire.Id, VehicleId, null, "1E", default);

        result.Status.Should().Be(TireCompatibilityStatus.NotVerified);
        result.Messages.Single().Should().Contain("não pôde ser verificada automaticamente");
    }

    [Fact]
    public async Task Install_TireAlreadyInstalled_IsRefused()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");

        var act = () => InstallAsync(tire.Id, "1D");

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*já está instalado*");
    }

    [Fact]
    public async Task Install_VehicleWithoutLayout_IsRefused()
    {
        await ArrangeAsync();
        var other = await Scenario.VehicleAsync(T, 1);
        var tire = await NewTireAsync();

        var act = () => InstallAsync(tire.Id, "1E", other.Id);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*configuração de eixos*");
    }

    [Fact]
    public async Task Install_WithInformedOdometer_RecordsTheReadingThroughTheMileageHistory()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(2);

        var installed = await InstallAsync(tire.Id, "1E", km: 100_600);

        installed.Location!.InstalledOdometerKm.Should().Be(100_600);
        var reading = await T.Db.OdometerReadings.OrderByDescending(r => r.ReadAt).FirstAsync(r => r.VehicleId == VehicleId);
        reading.Source.Should().Be(OdometerReadingSource.TireService);
        (await T.Db.Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(100_600);
    }

    [Fact]
    public async Task Install_SuspiciousOdometer_IsRefusedAndNothingIsSaved()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();

        var act = () => InstallAsync(tire.Id, "1E", km: 180_000);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("odometerKm");
        T.Db.ChangeTracker.Clear();
        (await ReloadAsync(tire.Id)).Status.Should().Be(TireStatus.InStock);
        (await T.Db.OdometerReadings.CountAsync(r => r.Source == OdometerReadingSource.TireService)).Should().Be(0);
    }

    [Fact]
    public async Task Install_TypedLater_UsesTheOdometerTheHistoryHadThen()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        var installedAt = T.Clock.UtcNow.AddHours(1);
        await DriveToAsync(104_000);

        var installed = await InstallAsync(tire.Id, "1E", at: installedAt);

        installed.Location!.InstalledOdometerKm.Should().Be(100_000, "the vehicle had 100,000 km at that time");
        installed.CurrentKm.Should().Be(4_000, "it has run since then");
    }

    [Fact]
    public async Task Install_BeforeTheTiresLastMovement_IsRefused()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
        await RemoveAsync(tire.Id);

        var act = () => InstallAsync(tire.Id, "1D", at: T.Clock.UtcNow.AddDays(-2));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("performedAt");
    }

    // ---------- removal, mileage and reinstallation ----------

    [Fact]
    public async Task Remove_ToStock_AccumulatesTheKmOfTheStint()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(105_000);

        var removed = await RemoveAsync(tire.Id, tread: 13);

        removed.Status.Should().Be(TireStatus.InStock);
        removed.StorageLocation.Should().Be("Almoxarifado B");
        removed.AccumulatedKm.Should().Be(5_000);
        removed.CurrentTreadDepthMm.Should().Be(13, "the measurement at removal joins the tread history");
        var stint = await T.Db.TireInstallations.SingleAsync(i => i.TireId == tire.Id);
        stint.RemovedOdometerKm.Should().Be(105_000);
        stint.DistanceKm.Should().Be(5_000);
        (await T.Db.TireInspections.CountAsync(i => i.TireId == tire.Id && i.Source == TireInspectionSource.Removal)).Should().Be(1);
    }

    [Fact]
    public async Task Reinstall_AccumulatesKmAcrossInstallationsOnDifferentVehicles()
    {
        await ArrangeAsync();
        var other = await Scenario.VehicleAsync(T, 1, r => r with { CurrentOdometerKm = 50_000 });
        await Operations.SetLayoutAsync(other.Id, null, new TireLayoutAssignmentRequest { LayoutId = LayoutId }, default);
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(103_000);
        await RemoveAsync(tire.Id);

        await InstallAsync(tire.Id, "2DE", other.Id);
        await DriveToAsync(52_500, other.Id);
        var current = await Tires.GetAsync(tire.Id, default);

        current.AccumulatedKm.Should().Be(3_000);
        current.CurrentKm.Should().Be(5_500, "3,000 km on the first truck + 2,500 km running on the second");
        (await Tires.InstallationsAsync(tire.Id, new ListRequest(), default)).TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Spare_DoesNotAccumulateKm()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "EST1");
        await DriveToAsync(104_000);

        (await Tires.GetAsync(tire.Id, default)).CurrentKm.Should().Be(0);
        (await RemoveAsync(tire.Id)).AccumulatedKm.Should().Be(0);
    }

    [Fact]
    public async Task Remove_ToDisposal_ClosesTheStintAndIsFinal()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");

        var disposed = await RemoveAsync(tire.Id, TireRemovalDestination.Disposal, TireRemovalReason.EndOfLife, disposal: TireDisposalReason.EndOfLife);

        disposed.Status.Should().Be(TireStatus.Disposed);
        disposed.Location.Should().BeNull("a disposed tire never remains installed (seção 42)");
        disposed.DisposalReason.Should().Be(TireDisposalReason.EndOfLife);
        var act = () => InstallAsync(tire.Id, "1E");
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Dispose_InstalledTire_IsRefused()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");

        var act = () => Operations.DisposeAsync(tire.Id, new TireDisposalRequest { Reason = TireDisposalReason.IrreparableDamage }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*Remova-o*");
    }

    [Fact]
    public async Task Dispose_FromStock_RecordsReasonDestinationAndShortLifeAnomaly()
    {
        await ArrangeAsync();
        await TireServices.Settings(T).UpdateAsync(new TireSettingsRequest { MinExpectedLifeKm = 60_000 }, default);
        var tire = await NewTireAsync();

        var disposed = await Operations.DisposeAsync(tire.Id, new TireDisposalRequest
        {
            Reason = TireDisposalReason.IrreparableDamage, Destination = "Reciclagem Ecopneus", Notes = "Corte lateral",
        }, default);

        disposed.Status.Should().Be(TireStatus.Disposed);
        disposed.DisposalDestination.Should().Be("Reciclagem Ecopneus");
        disposed.Anomalies.Single().Type.Should().Be(TireAnomalyType.ShortLifecycle);
        disposed.Actions.CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_DestinationDisposalWithoutDisposePermission_Forbidden()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        T.CurrentUser.PermissionSet = [Permissions.Tires.View, Permissions.Tires.Remove];

        var act = () => RemoveAsync(tire.Id, TireRemovalDestination.Disposal, disposal: TireDisposalReason.EndOfLife);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Evaluation_AndReturnToStock()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        (await RemoveAsync(tire.Id, TireRemovalDestination.Evaluation, TireRemovalReason.Damage)).Status.Should().Be(TireStatus.UnderInspection);

        var back = await Operations.ReturnToStockAsync(tire.Id, new TireStockRequest { StorageLocation = "Prateleira 3", Notes = "Sem dano" }, default);

        back.Status.Should().Be(TireStatus.InStock);
        back.StorageLocation.Should().Be("Prateleira 3");
    }

    // ---------- replacement, transfer (seções 32, 70) ----------

    [Fact]
    public async Task Replace_RemovesTheCurrentTireAndInstallsTheNewOneInOneTransaction()
    {
        await ArrangeAsync();
        var old = await NewTireAsync();
        var replacement = await NewTireAsync();
        await InstallAsync(old.Id, "1E");
        await DriveToAsync(102_000);

        var result = await Operations.ReplaceAsync(old.Id, new TireReplaceRequest
        {
            Reason = TireRemovalReason.Damage, Destination = TireRemovalDestination.Evaluation, ReplacementTireId = replacement.Id, TreadDepthMm = 9,
        }, default);

        result.Id.Should().Be(replacement.Id);
        result.Location!.PositionCode.Should().Be("1E");
        result.Location.InstalledOdometerKm.Should().Be(102_000);
        var previous = await Tires.GetAsync(old.Id, default);
        previous.Status.Should().Be(TireStatus.UnderInspection);
        previous.AccumulatedKm.Should().Be(2_000);
        (await T.Db.TireInstallations.SingleAsync(i => i.TireId == old.Id)).RemovalReason.Should().Be(TireRemovalReason.Damage);
    }

    [Fact]
    public async Task Replace_WithIncompatibleTire_KeepsEverythingAsItWas()
    {
        await ArrangeAsync(allowedSteerSize: "295/80R22.5");
        var smaller = (await TireServices.Models(T).CreateAsync(new TireModelRequest { Brand = "Pirelli", Name = "FR85", Size = "275/80R22.5" }, default)).Id;
        var old = await NewTireAsync();
        var replacement = await NewTireAsync(modelId: smaller);
        await InstallAsync(old.Id, "1E");

        var act = () => Operations.ReplaceAsync(old.Id, new TireReplaceRequest
        {
            Reason = TireRemovalReason.Replacement, Destination = TireRemovalDestination.Stock, ReplacementTireId = replacement.Id,
        }, default);

        await act.Should().ThrowAsync<ValidationException>();
        T.Db.ChangeTracker.Clear();
        (await ReloadAsync(old.Id)).Status.Should().Be(TireStatus.Installed);
        (await ReloadAsync(replacement.Id)).Status.Should().Be(TireStatus.InStock);
    }

    [Fact]
    public async Task Transfer_MovesTheTireToAnotherVehicle()
    {
        await ArrangeAsync();
        var other = await Scenario.VehicleAsync(T, 1, r => r with { CurrentOdometerKm = 20_000 });
        await Operations.SetLayoutAsync(other.Id, null, new TireLayoutAssignmentRequest { LayoutId = LayoutId }, default);
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(101_500);

        var moved = await Operations.TransferAsync(tire.Id, new TireTransferRequest { VehicleId = other.Id, PositionCode = "2DI" }, default);

        moved.Location!.VehicleId.Should().Be(other.Id);
        moved.Location.InstalledOdometerKm.Should().Be(20_000);
        moved.AccumulatedKm.Should().Be(1_500);
        (await T.Db.TireInstallations.CountAsync(i => i.TireId == tire.Id && i.RemovedAt == null)).Should().Be(1);
    }

    // ---------- repair and retread (seções 22, 23, 69) ----------

    [Fact]
    public async Task Retread_FullScenario_RemoveSendCompleteAndInstallAgain()
    {
        await ArrangeAsync();
        var workshop = await Services.Workshops(T).CreateAsync(Requests.Workshop("Recapadora Sul"), default);
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "2EE");
        await DriveToAsync(160_000);

        var removed = await Operations.RemoveAsync(tire.Id, new TireRemovalRequest
        {
            Reason = TireRemovalReason.Retread, Destination = TireRemovalDestination.Retread, TreadDepthMm = 4, WorkshopId = workshop.Id,
        }, default);
        removed.Status.Should().Be(TireStatus.UnderRetread);
        removed.OpenServiceOrder!.RetreadNumber.Should().Be(1);
        removed.OpenServiceOrder.ProviderName.Should().Be("Recapadora Sul");

        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(10);
        var order = await ServiceOrders.CompleteAsync(removed.OpenServiceOrder.Id, new TireServiceCompletionRequest
        {
            Result = TireServiceResult.Approved, NewTreadDepthMm = 14, TreadPattern = "Banda BDR", Cost = 600m, WarrantyUntil = T.Clock.Today.AddMonths(6),
        }, default);
        order.Status.Should().Be(TireServiceStatus.Completed);

        var back = await Tires.GetAsync(tire.Id, default);
        back.Status.Should().Be(TireStatus.InStock);
        back.RetreadCount.Should().Be(1);
        back.CurrentTreadDepthMm.Should().Be(14);
        back.Costs!.Retreads.Should().Be(600m);
        (await InstallAsync(tire.Id, "2DE")).Status.Should().Be(TireStatus.Installed);
        (await Tires.HistoryAsync(tire.Id, new Fleet.Application.Operations.HistoryRequest { PageSize = 100 }, default)).Items.Select(e => e.Type).Should()
            .Contain([OperationalEventType.TireRetreadStarted, OperationalEventType.TireRetreadCompleted, OperationalEventType.TireInstalled]);
    }

    [Fact]
    public async Task Retread_RejectedByTheProvider_TireWaitsForADecision()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        var order = await ServiceOrders.SendAsync(tire.Id, new TireServiceRequest { Kind = TireServiceKind.Retread, ProviderName = "Recapadora X" }, default);

        await ServiceOrders.CompleteAsync(order.Id, new TireServiceCompletionRequest
        {
            Result = TireServiceResult.Rejected, ResultNotes = "Carcaça sem condição de recapagem",
        }, default);

        var tireNow = await Tires.GetAsync(tire.Id, default);
        tireNow.Status.Should().Be(TireStatus.UnderInspection);
        tireNow.RetreadCount.Should().Be(0, "eligibility is the provider's decision, recorded — not assumed");
        tireNow.Actions.CanDispose.Should().BeTrue();
    }

    [Fact]
    public async Task Repair_FromStock_ReturnsToStockWithCostAndRepeatedRepairsAnomaly()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        for (var i = 0; i < 3; i++)
        {
            T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
            var order = await ServiceOrders.SendAsync(tire.Id, new TireServiceRequest { Kind = TireServiceKind.Repair, RepairType = TireRepairType.Vulcanization }, default);
            (await Tires.GetAsync(tire.Id, default)).Status.Should().Be(TireStatus.UnderRepair);
            await ServiceOrders.CompleteAsync(order.Id, new TireServiceCompletionRequest { Result = TireServiceResult.Approved, Cost = 80m }, default);
        }

        var repaired = await Tires.GetAsync(tire.Id, default);
        repaired.Status.Should().Be(TireStatus.InStock);
        repaired.RepairCount.Should().Be(3);
        repaired.Costs!.Repairs.Should().Be(240m);
        repaired.Anomalies.Should().ContainSingle(a => a.Type == TireAnomalyType.RepeatedRepairs);
    }

    [Fact]
    public async Task Repair_InPlace_KeepsTheTireInstalled()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1D");

        var order = await ServiceOrders.SendAsync(tire.Id, new TireServiceRequest
        {
            Kind = TireServiceKind.Repair, RepairType = TireRepairType.Puncture, Cost = 45m,
        }, default);

        order.InPlace.Should().BeTrue();
        order.Status.Should().Be(TireServiceStatus.Completed);
        var tireNow = await Tires.GetAsync(tire.Id, default);
        tireNow.Status.Should().Be(TireStatus.Installed);
        tireNow.Location!.PositionCode.Should().Be("1D");
        tireNow.RepairCount.Should().Be(1);
    }

    [Fact]
    public async Task Retread_OnAnInstalledTire_MustBeRemovedFirst()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1D");

        var act = () => ServiceOrders.SendAsync(tire.Id, new TireServiceRequest { Kind = TireServiceKind.Retread }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*remova-o*");
    }

    [Fact]
    public async Task CancelService_SendsTheTireBackToEvaluation()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        var order = await ServiceOrders.SendAsync(tire.Id, new TireServiceRequest { Kind = TireServiceKind.Repair }, default);

        await ServiceOrders.CancelAsync(order.Id, new TireServiceCancelRequest { Reason = "Enviado por engano" }, default);

        (await Tires.GetAsync(tire.Id, default)).Status.Should().Be(TireStatus.UnderInspection);
    }

    // ---------- controlled correction ----------

    [Fact]
    public async Task CorrectInstallation_AdjustsAccumulatedKmAndRecordsTheCorrection()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(105_000);
        await RemoveAsync(tire.Id);
        var stint = await T.Db.TireInstallations.SingleAsync(i => i.TireId == tire.Id);

        var corrected = await Operations.CorrectInstallationAsync(stint.Id, new TireInstallationCorrectionRequest
        {
            InstalledOdometerKm = 101_000, Reason = "Pneu instalado depois do que foi lançado",
        }, default);

        corrected.DistanceKm.Should().Be(4_000);
        (await Tires.GetAsync(tire.Id, default)).AccumulatedKm.Should().Be(4_000);
        (await T.Db.OperationalEvents.SingleAsync(e => e.TireId == tire.Id && e.Type == OperationalEventType.TireHistoryCorrected))
            .Summary.Should().Contain("100.000 km → 101.000 km");
    }

    // ---------- layout assignment ----------

    [Fact]
    public async Task SetLayout_WithoutTheOccupiedPositions_IsRefused()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "2EI");
        var car = (await TireServices.Layouts(T).ListAsync(TireLayoutTarget.Vehicle, false, default)).Single(l => l.Name.StartsWith("Carro"));

        var act = () => Operations.SetLayoutAsync(VehicleId, null, new TireLayoutAssignmentRequest { LayoutId = car.Id }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*2EI*");
    }

    [Fact]
    public async Task UpdateLayout_RemovingAnOccupiedAxle_IsRefused()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "2DE");
        var layout = await TireServices.Layouts(T).GetAsync(LayoutId, default);

        var act = () => TireServices.Layouts(T).UpdateAsync(LayoutId, new TireLayoutRequest
        {
            Name = layout.Name, Target = layout.Target, SpareCount = 1,
            Axles = [new TireLayoutAxleRequest { Type = AxleType.Steer }, new TireLayoutAxleRequest { Type = AxleType.Drive, IsDual = false }],
        }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*2DE*");
    }

    [Fact]
    public async Task Layouts_EveryCompanyStartsWithTheDefaults()
    {
        await ArrangeAsync();

        var all = await TireServices.Layouts(T).ListAsync(null, false, default);

        all.Should().HaveCount(TireLayoutDefaults.All.Count);
        all.Single(l => l.Name == "Semirreboque 3 eixos").Positions.Should().HaveCount(13, "3 dual axles + 1 spare");
    }

    // ---------- tenant isolation ----------

    [Fact]
    public async Task OtherCompany_CannotSeeOrOperateTheTires()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        var other = await T.AddCompanyAsync("12ABC34501DE35", "Outra");
        T.SignInAs(other, SystemRoles.FleetManager);

        await FluentActions.Awaiting(() => Tires.GetAsync(tire.Id, default)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => InstallAsync(tire.Id, "1E")).Should().ThrowAsync<NotFoundException>();
        (await Tires.ListAsync(new TireListRequest(), default)).TotalCount.Should().Be(0);
        await FluentActions.Awaiting(() => Operations.GetAssetAsync(VehicleId, null, default)).Should().ThrowAsync<NotFoundException>();
    }
}
