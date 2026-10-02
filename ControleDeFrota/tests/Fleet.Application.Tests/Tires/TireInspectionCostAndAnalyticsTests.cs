using System.Diagnostics;
using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Tires;
using Fleet.Domain.Authorization;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Tires;

public class TireInspectionCostAndAnalyticsTests : TireTestBase
{
    private Task<TireInspectionResponse> InspectAsync(Guid tireId, decimal? tread = null, decimal? pressure = null,
        TireCondition condition = TireCondition.Good, TireWearPattern wear = TireWearPattern.Normal, TireDamageType[]? damages = null,
        IReadOnlyList<Guid>? files = null, DateTime? at = null) =>
        Inspections.InspectAsync(tireId, new TireInspectionRequest
        {
            TreadDepthMm = tread, Pressure = pressure, PressureUnit = TirePressureUnit.Psi, Condition = condition, WearPattern = wear,
            Damages = damages, FileIds = files, InspectedAt = at, Notes = damages?.Contains(TireDamageType.Other) == true ? "Outro dano" : null,
        }, default);

    // ---------- inspection (seções 16–21, 68) ----------

    [Fact]
    public async Task Inspect_RecordsTreadPressureDamageAndPosition()
    {
        await ArrangeAsync(allowedSteerSize: "295/80R22.5"); // also sets 110 psi as reference pressure on every axle
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");

        var inspection = await InspectAsync(tire.Id, tread: 9, pressure: 90, condition: TireCondition.Attention, damages: [TireDamageType.Cut]);

        inspection.PositionLabel.Should().Be("Eixo 1 — Esquerdo");
        inspection.OdometerKm.Should().Be(100_000);
        inspection.PressureCheck.Should().Be(TirePressureCheck.Low, "90 psi is more than 10% below the 110 psi reference");
        inspection.Damages.Should().Equal(TireDamageType.Cut);
        inspection.RequiresAction.Should().BeTrue();
        var current = await Tires.GetAsync(tire.Id, default);
        current.CurrentTreadDepthMm.Should().Be(9);
        current.Alerts.Select(a => a.Type).Should().Contain([TireAlertType.DamageReported, TireAlertType.PressureOutOfRange]);
        var events = await T.Db.OperationalEvents.Where(e => e.TireId == tire.Id).Select(e => e.Type).ToListAsync();
        events.Should().Contain([OperationalEventType.TireInspected, OperationalEventType.TireInspectionFailed, OperationalEventType.TirePressureLow]);
    }

    [Fact]
    public async Task Inspect_WithPhotos_AttachesThemToTheInspection()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        var photo = await Services.Files(T).UploadAsync(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4]), "pneu.jpg", default);

        var inspection = await InspectAsync(tire.Id, tread: 15, files: [photo.Id]);

        inspection.Files.Single().Id.Should().Be(photo.Id);
    }

    [Fact]
    public async Task Inspect_KeepsTheTreadHistory_AndAnOlderInspectionDoesNotOverrideTheCurrentTread()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(10);
        await InspectAsync(tire.Id, tread: 14);
        await InspectAsync(tire.Id, tread: 15, at: T.Clock.UtcNow.AddDays(-5));

        var history = await Inspections.ListAsync(tire.Id, new ListRequest(), default);

        history.TotalCount.Should().Be(2, "measurements are never overwritten (seção 18)");
        (await Tires.GetAsync(tire.Id, default)).CurrentTreadDepthMm.Should().Be(14, "the newest measurement is the current tread");
    }

    [Fact]
    public async Task Inspect_FastTreadLoss_RaisesARequiresReviewAnomalyOnce()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        await InspectAsync(tire.Id, tread: 16);
        await DriveToAsync(102_000);

        await InspectAsync(tire.Id, tread: 14); // 2 mm in 2,000 km = 1.0 mm/1,000 km > 0.5 reference
        await DriveToAsync(104_000);
        await InspectAsync(tire.Id, tread: 12);

        var anomaly = (await Tires.GetAsync(tire.Id, default)).Anomalies.Should().ContainSingle().Subject;
        anomaly.Type.Should().Be(TireAnomalyType.RapidTreadLoss);
        anomaly.Message.Should().Contain("Requer revisão").And.NotContainAny("suspensão", "alinhamento", "causa");

        var reviewed = await Inspections.ReviewAnomalyAsync(anomaly.Id, new TireAnomalyReviewRequest { Notes = "Alinhamento conferido" }, default);
        reviewed.Anomalies.Single().ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Inspect_RepeatedPunctures_RaisesAnomaly()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InspectAsync(tire.Id, damages: [TireDamageType.Puncture]);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(20);

        await InspectAsync(tire.Id, damages: [TireDamageType.Puncture]);

        (await Tires.GetAsync(tire.Id, default)).Anomalies.Should().ContainSingle(a => a.Type == TireAnomalyType.RepeatedPunctures);
    }

    [Fact]
    public async Task Inspect_Unfit_OpensAMaintenanceRequestOnlyWhenTheRuleIsOn()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "2DE");

        var withoutRule = await InspectAsync(tire.Id, condition: TireCondition.Unfit, damages: [TireDamageType.Bulge]);
        withoutRule.MaintenanceRequestId.Should().BeNull("no automatic work for every observation (seção 36)");

        await TireServices.Settings(T).UpdateAsync(new TireSettingsRequest { AutoMaintenanceRequestOnUnfit = true }, default);
        var withRule = await InspectAsync(tire.Id, condition: TireCondition.Unfit, damages: [TireDamageType.Bulge]);

        var request = await T.Db.MaintenanceRequests.SingleAsync();
        withRule.MaintenanceRequestId.Should().Be(request.Id);
        request.Source.Should().Be(MaintenanceRequestSource.AutomaticAlert);
        request.Description.Should().Contain(tire.Code).And.Contain("Eixo 2 — Direito externo");
        (await T.Db.WorkOrders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Inspect_TreadAtTheMinimum_AlertsWithCompanyWording()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");

        await InspectAsync(tire.Id, tread: 2.5m);

        var alert = (await Tires.GetAsync(tire.Id, default)).Alerts.Single(a => a.Type == TireAlertType.TreadBelowMinimum);
        alert.Message.Should().Contain("configurado pela empresa").And.NotContain("legal");
        (await Services.Dashboard(T).GetAsync(default)).Alerts.Should().Contain(a => a.EntityType == "Tire" && a.EntityId == tire.Id);
    }

    [Fact]
    public async Task InspectionOverdue_FollowsTheConfiguredInterval()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        await InstallAsync(tire.Id, "1E");
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(31);

        (await Tires.GetAsync(tire.Id, default)).Alerts.Should().Contain(a => a.Type == TireAlertType.InspectionOverdue);
        (await Tires.ListAsync(new TireListRequest { Inspection = TireInspectionFilter.Overdue }, default)).TotalCount.Should().Be(1);

        await InspectAsync(tire.Id, tread: 15);
        (await Tires.ListAsync(new TireListRequest { Inspection = TireInspectionFilter.Overdue }, default)).TotalCount.Should().Be(0);
    }

    // ---------- costs (seções 24–26) ----------

    [Fact]
    public async Task LifecycleCost_SumsPurchaseRepairsAndRetreads_AndCostPerKm()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync(price: 2_000m);
        var repair = await ServiceOrders.SendAsync(tire.Id, new TireServiceRequest { Kind = TireServiceKind.Repair }, default);
        await ServiceOrders.CompleteAsync(repair.Id, new TireServiceCompletionRequest { Result = TireServiceResult.Approved, Cost = 150m }, default);
        var retread = await ServiceOrders.SendAsync(tire.Id, new TireServiceRequest { Kind = TireServiceKind.Retread }, default);
        await ServiceOrders.CompleteAsync(retread.Id, new TireServiceCompletionRequest
        {
            Result = TireServiceResult.Approved, Cost = 600m, NewTreadDepthMm = 14,
        }, default);
        await ServiceOrders.AddCostAsync(tire.Id, new TireCostRequest { Type = TireCostType.Installation, IncurredOn = T.Clock.Today, Amount = 50m }, default);
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(200_000);

        var costs = (await Tires.GetAsync(tire.Id, default)).Costs!;

        costs.Purchase.Should().Be(2_000m);
        costs.Repairs.Should().Be(150m);
        costs.Retreads.Should().Be(600m);
        costs.Installation.Should().Be(50m);
        costs.Total.Should().Be(2_800m);
        costs.Km.Should().Be(100_000);
        costs.CostPerKm.Should().Be(0.028m);
    }

    [Fact]
    public async Task CostPerKm_WithLittleKm_IsNotShown()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync(price: 2_000m);
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(103_000);

        var costs = (await Tires.GetAsync(tire.Id, default)).Costs!;

        costs.CostPerKm.Should().BeNull("3,000 km would produce a misleading figure (seção 25)");
        costs.CostPerKmNote.Should().Contain("5.000 km");
    }

    [Fact]
    public async Task CostPerKm_AfterAnImplementStint_IsNotShown()
    {
        await ArrangeAsync();
        var implement = await Services.Implements(T).CreateAsync(Requests.Implement(), default);
        var trailer = (await TireServices.Layouts(T).ListAsync(TireLayoutTarget.Implement, false, default)).First();
        await Operations.SetLayoutAsync(null, implement.Id, new TireLayoutAssignmentRequest { LayoutId = trailer.Id }, default);
        var tire = await NewTireAsync();
        await Operations.InstallAsync(tire.Id, new TireInstallRequest { ImplementId = implement.Id, PositionCode = "1EE" }, default);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(30);
        await RemoveAsync(tire.Id);
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(130_000);

        var detail = await Tires.GetAsync(tire.Id, default);

        detail.HasUnmeasuredDistance.Should().BeTrue();
        detail.Costs!.CostPerKm.Should().BeNull();
        detail.Costs.CostPerKmNote.Should().Contain("implemento");
    }

    [Fact]
    public async Task Costs_AreHiddenWithoutTheCostPermission()
    {
        await ArrangeAsync(role: SystemRoles.Operations);
        T.SignInAs(Company, SystemRoles.FleetManager);
        var tire = await NewTireAsync(price: 2_000m);
        T.SignInAs(Company, SystemRoles.Operations);

        var detail = await Tires.GetAsync(tire.Id, default);

        detail.CanSeeCosts.Should().BeFalse();
        detail.PurchasePrice.Should().BeNull();
        detail.Costs.Should().BeNull();
        var act = () => ServiceOrders.AddCostAsync(tire.Id, new TireCostRequest { Type = TireCostType.Other, IncurredOn = T.Clock.Today, Amount = 10 }, default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ServiceCost_CannotBeDeletedAsAManualCost()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync();
        var order = await ServiceOrders.SendAsync(tire.Id, new TireServiceRequest { Kind = TireServiceKind.Repair }, default);
        await ServiceOrders.CompleteAsync(order.Id, new TireServiceCompletionRequest { Result = TireServiceResult.Approved, Cost = 70m }, default);
        var cost = (await ServiceOrders.ListCostsAsync(tire.Id, default)).Single();

        cost.CanDelete.Should().BeFalse();
        var act = () => ServiceOrders.DeleteCostAsync(tire.Id, cost.Id, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // ---------- dashboard, reports, filters ----------

    [Fact]
    public async Task Dashboard_CountsByStatusAndAttention()
    {
        await ArrangeAsync();
        var installed = await NewTireAsync();
        var worn = await NewTireAsync();
        await NewTireAsync();
        await InstallAsync(installed.Id, "1E");
        await InstallAsync(worn.Id, "1D");
        await InspectAsync(worn.Id, tread: 3.5m, wear: TireWearPattern.ShoulderWear);

        var dashboard = await Analytics.GetDashboardAsync(default);

        dashboard.Status.Total.Should().Be(3);
        dashboard.Status.Installed.Should().Be(2);
        dashboard.Status.InStock.Should().Be(1);
        dashboard.Attention.NearReplacement.Should().Be(1);
        dashboard.Attention.BelowMinimum.Should().Be(0);
        dashboard.Attention.DamageOrUnevenWear.Should().Be(1);
        dashboard.AbnormalWear.Single().TireId.Should().Be(worn.Id);
        dashboard.RecentMovements.Should().HaveCount(4, "two installations, the inspection and its \"requires attention\" event");
        dashboard.HighestCostPerKm.Should().NotBeNull();
    }

    [Fact]
    public async Task Dashboard_RankingRequiresTheCostPermission()
    {
        await ArrangeAsync(role: SystemRoles.Viewer);

        var dashboard = await Analytics.GetDashboardAsync(default);

        dashboard.CanSeeCosts.Should().BeFalse();
        dashboard.HighestCostPerKm.Should().BeNull();
    }

    [Fact]
    public async Task List_FiltersByVehiclePositionRetreadsAndManufacturingPeriod()
    {
        await ArrangeAsync();
        var a = await NewTireAsync(retreads: 2);
        var b = await NewTireAsync();
        await InstallAsync(a.Id, "2EI");

        (await Tires.ListAsync(new TireListRequest { VehicleId = VehicleId }, default)).Items.Single().Id.Should().Be(a.Id);
        (await Tires.ListAsync(new TireListRequest { PositionCode = "2ei" }, default)).Items.Single().Id.Should().Be(a.Id);
        (await Tires.ListAsync(new TireListRequest { MinRetreads = 1 }, default)).Items.Single().Id.Should().Be(a.Id);
        (await Tires.ListAsync(new TireListRequest { Status = TireStatus.InStock }, default)).Items.Single().Id.Should().Be(b.Id);
        (await Tires.ListAsync(new TireListRequest { ManufacturedFrom = new DateOnly(2024, 1, 1), ManufacturedTo = new DateOnly(2024, 12, 31) }, default))
            .TotalCount.Should().Be(2);
        (await Tires.ListAsync(new TireListRequest { Search = "pn-000002" }, default)).Items.Single().Id.Should().Be(b.Id);
        var row = (await Tires.ListAsync(new TireListRequest { VehicleId = VehicleId }, default)).Items.Single();
        row.Location!.PositionLabel.Should().Be("Eixo 2 — Esquerdo interno");
        row.Brand.Should().Be("Michelin");
    }

    [Fact]
    public async Task Reports_LifecycleInspectionsAndCosts()
    {
        await ArrangeAsync();
        var tire = await NewTireAsync(price: 1_800m);
        await InstallAsync(tire.Id, "1E");
        await DriveToAsync(110_000);
        await InspectAsync(tire.Id, tread: 12, pressure: 105);
        await RemoveAsync(tire.Id);

        var lifecycle = await Analytics.LifecycleAsync(new TireReportRequest { From = T.Clock.Today.AddDays(-60), To = T.Clock.Today }, default);
        var row = lifecycle.Rows.Items.Single();
        row.DistanceKm.Should().Be(10_000);
        row.LifecycleCost.Should().Be(1_800m);
        row.CostPerKm.Should().Be(0.18m);

        var inspections = await Analytics.InspectionsAsync(new TireReportRequest(), default);
        inspections.Rows.Items.Should().Contain(i => i.TreadDepthMm == 12 && i.Pressure == 105);

        var costs = await Analytics.CostsAsync(new TireReportRequest(), default);
        costs.Rows.Items.Single().Total.Should().Be(1_800m);
        costs.Rows.Items.Single().Km.Should().Be(10_000);
    }

    /// <summary>Seção 53: the dashboard and the list stay fast with thousands of tires (server-side aggregation).</summary>
    [Fact]
    public async Task Volume_ThreeThousandTires_DashboardAndListStayFast()
    {
        await ArrangeAsync();
        var model = await T.Db.TireModels.SingleAsync();
        for (var i = 1; i <= 3_000; i++)
            T.Db.Tires.Add(new Tire
            {
                Sequence = i, Code = Tire.FormatCode(i), TireModelId = model.Id, Status = i % 3 == 0 ? TireStatus.InStock : TireStatus.UnderInspection,
                CurrentTreadDepthMm = i % 16, PurchasePrice = 1_500, AccumulatedKm = i * 40, LastInspectedAt = T.Clock.UtcNow,
            });
        await T.Db.SaveChangesAsync();
        T.Db.ChangeTracker.Clear();

        var watch = Stopwatch.StartNew();
        var dashboard = await Analytics.GetDashboardAsync(default);
        var page = await Tires.ListAsync(new TireListRequest { Alert = TireAlertFilter.NearReplacement, SortBy = "treadDepth" }, default);
        watch.Stop();

        dashboard.Status.Total.Should().Be(3_000);
        page.Items.Should().HaveCount(ListRequest.DefaultPageSize);
        watch.ElapsedMilliseconds.Should().BeLessThan(5_000);
    }
}
