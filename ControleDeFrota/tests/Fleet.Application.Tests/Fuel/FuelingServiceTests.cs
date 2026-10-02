using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Fuel;
using Fleet.Application.Mileage;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Drivers;
using Fleet.Domain.Fuel;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Fuel;

/// <summary>Shared arrangement: a diesel truck registered with 100,000 km and a 400 L tank, the default fuel catalog.</summary>
public abstract class FuelTestBase : IDisposable
{
    protected readonly TestDb T = new();
    protected Company Company = null!;
    protected Guid VehicleId;
    protected Guid DieselId;
    protected Guid GasolineId;

    public void Dispose() => T.Dispose();

    protected FuelingService Fuelings => Services.Fuelings(T);

    protected async Task ArrangeAsync(string role = SystemRoles.FleetManager, decimal? expected = null, decimal? tank = 400,
        VehicleStatus status = VehicleStatus.Available)
    {
        Company = await Scenario.SignedInAsync(T, role == SystemRoles.FleetManager ? role : SystemRoles.FleetManager);
        var vehicle = await Scenario.VehicleAsync(T, change: r => r with
        {
            CurrentOdometerKm = 100_000, FuelTankCapacity = tank, ExpectedConsumption = expected, Status = status,
        });
        VehicleId = vehicle.Id;
        var types = await Services.FuelTypes(T).ListAsync(includeInactive: false, default);
        DieselId = types.Single(t => t.Code == "S10").Id;
        GasolineId = types.Single(t => t.Code == "GAS").Id;
        if (role != SystemRoles.FleetManager) T.SignInAs(Company, role);
    }

    protected FuelingRequest Request(int km, decimal quantity, decimal price = 6m, bool fullTank = true, DateTime? at = null,
        Guid? fuelTypeId = null, Guid? driverId = null) => new()
    {
        VehicleId = VehicleId, FuelTypeId = fuelTypeId ?? DieselId, OdometerKm = km, Quantity = quantity, UnitPrice = price,
        IsFullTank = fullTank, FueledAt = at, PaymentMethod = PaymentMethod.FuelCard, DriverId = driverId,
    };

    /// <summary>Moves the clock forward (default one day — plausible mileage, no frequency warning) and records a fueling now.</summary>
    protected async Task<FuelingResponse> FuelAsync(int km, decimal quantity, decimal price = 6m, bool fullTank = true, double hoursLater = 24,
        Guid? fuelTypeId = null)
    {
        T.Clock.UtcNow = T.Clock.UtcNow.AddHours(hoursLater);
        return await Fuelings.CreateAsync(Request(km, quantity, price, fullTank, fuelTypeId: fuelTypeId), default);
    }

    protected static FuelingCorrectionRequest Correction(FuelingResponse f, string? reason = "Cupom conferido") => new()
    {
        DriverId = f.DriverId, FuelStationId = f.FuelStationId, FuelTypeId = f.FuelTypeId, FueledAt = f.FueledAt, OdometerKm = f.OdometerKm,
        Quantity = f.Quantity, UnitPrice = f.UnitPrice, IsFullTank = f.IsFullTank, PaymentMethod = f.PaymentMethod,
        ReceiptNumber = f.ReceiptNumber, Notes = f.Notes, Reason = reason,
    };

    protected async Task<List<OperationalEventType>> EventsAsync() =>
        await T.NewContext().OperationalEvents.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => e.Type).ToListAsync();
}

public class FuelingServiceTests : FuelTestBase
{
    // ---------- creation and validation (seção 8) ----------

    [Fact]
    public async Task CreateAsync_ValidFueling_CalculatesTotalAndFeedsTheMileageHistory()
    {
        await ArrangeAsync();

        var fueling = await FuelAsync(100_850, 200m, 6.20m);

        fueling.TotalAmount.Should().Be(1_240.00m);
        fueling.Status.Should().Be(FuelingStatus.Valid);
        fueling.ConsumptionResult.Should().Be(ConsumptionResult.FirstFullTank);
        fueling.OdometerReadingStatus.Should().Be(OdometerReadingStatus.Valid);

        var db = T.NewContext();
        var reading = await db.OdometerReadings.SingleAsync(r => r.FuelingId == fueling.Id);
        reading.Source.Should().Be(OdometerReadingSource.Fueling);
        reading.OdometerKm.Should().Be(100_850);
        (await db.Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(100_850);
        (await EventsAsync()).Should().Contain([OperationalEventType.MileageRecorded, OperationalEventType.FuelingRecorded]);
    }

    [Fact]
    public async Task CreateAsync_ClientTotal_IsCheckedButNeverTrusted()
    {
        await ArrangeAsync();
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);

        var mismatch = () => Fuelings.CreateAsync(Request(100_500, 37.512m, 6.199m) with { TotalAmount = 250m }, default);
        (await mismatch.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "TotalAmount");

        // Within the pump tolerance: accepted, but the stored total is still quantity × price.
        var fueling = await Fuelings.CreateAsync(Request(100_500, 37.512m, 6.199m) with { TotalAmount = 232.58m }, default);
        fueling.TotalAmount.Should().Be(232.54m);
    }

    [Theory]
    [InlineData(0, 6.0)]
    [InlineData(-10, 6.0)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    [InlineData(100.0001, 6.0)]  // more than 3 decimals of quantity
    public async Task CreateAsync_InvalidQuantityOrPrice_IsRejected(decimal quantity, decimal price)
    {
        await ArrangeAsync();
        var act = () => Fuelings.CreateAsync(Request(100_100, quantity, price), default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_FutureDate_IsRejected()
    {
        await ArrangeAsync();
        var act = () => Fuelings.CreateAsync(Request(100_100, 100, at: T.Clock.UtcNow.AddHours(2)), default);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "FueledAt");
    }

    [Fact]
    public async Task CreateAsync_InactiveVehicle_IsRejected()
    {
        await ArrangeAsync(status: VehicleStatus.Inactive);
        var act = () => Fuelings.CreateAsync(Request(100_100, 100), default);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*inativo*");
    }

    [Theory]
    [InlineData(VehicleStatus.UnderMaintenance)]
    [InlineData(VehicleStatus.Unavailable)]
    [InlineData(VehicleStatus.OnTrip)]
    public async Task CreateAsync_NonInactiveConditions_AreAllowed(VehicleStatus status)
    {
        await ArrangeAsync(status: status);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
        (await Fuelings.CreateAsync(Request(100_100, 100), default)).Status.Should().Be(FuelingStatus.Valid);
    }

    [Theory]
    [InlineData(DriverStatus.OnLeave)]
    [InlineData(DriverStatus.Inactive)]
    public async Task CreateAsync_DriverNotActive_IsRejected(DriverStatus status)
    {
        await ArrangeAsync();
        var driver = await Scenario.DriverAsync(T, change: r => r with { Status = status });
        var act = () => Fuelings.CreateAsync(Request(100_100, 100, driverId: driver.Id), default);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "driverId");
    }

    [Fact]
    public async Task CreateAsync_DriverRequiredBySettings_IsEnforced()
    {
        await ArrangeAsync();
        await Services.FuelSettings(T).UpdateAsync(new FuelSettingsRequest
        {
            TankTolerancePercent = 5, PriceDeviationPercent = 20, ConsumptionDeviationPercent = 20, MinHoursBetweenFuelings = 2, RequireDriver = true,
        }, default);

        var act = () => Fuelings.CreateAsync(Request(100_100, 100), default);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "driverId");
    }

    // ---------- mileage integration (seções 9, 10, 35) ----------

    [Fact]
    public async Task CreateAsync_MileageDecrease_IsRejectedAndNothingIsSaved()
    {
        await ArrangeAsync();
        var act = () => Fuelings.CreateAsync(Request(99_900, 100), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "odometerKm");
        (await T.NewContext().Fuelings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_SuspiciousJump_GoesToReviewWithoutTouchingTheOdometer()
    {
        await ArrangeAsync();

        var fueling = await FuelAsync(190_000, 200);

        fueling.Status.Should().Be(FuelingStatus.PendingReview);
        fueling.OdometerReadingStatus.Should().Be(OdometerReadingStatus.PendingReview);
        fueling.Anomalies.Should().ContainSingle(a => a.Type == FuelAnomalyType.MileageJump);
        (await T.NewContext().Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(100_000);
        (await EventsAsync()).Should().Contain(OperationalEventType.FuelingMileageInconsistencyDetected);
    }

    [Fact]
    public async Task CreateAsync_EnteredAfterLaterReadings_MustFitTheHistoryAndCreatesNoReading()
    {
        await ArrangeAsync();
        var registeredAt = T.Clock.UtcNow;
        T.Clock.UtcNow = registeredAt.AddDays(2);
        await Services.Mileage(T).RecordAsync(VehicleId, new OdometerReadingRequest { OdometerKm = 101_000 }, default);

        var fueling = await Fuelings.CreateAsync(Request(100_500, 150, at: registeredAt.AddDays(1)), default);

        fueling.OdometerReadingStatus.Should().BeNull();
        var db = T.NewContext();
        (await db.OdometerReadings.CountAsync(r => r.VehicleId == VehicleId)).Should().Be(2);
        (await db.Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(101_000);

        var outOfRange = () => Fuelings.CreateAsync(Request(101_200, 150, at: registeredAt.AddDays(1).AddHours(5)), default);
        (await outOfRange.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("leitura seguinte");
    }

    [Fact]
    public async Task CreateAsync_TypedSecondsBeforeTheLatestReading_IsStillCurrent()
    {
        // The form has minute precision: "10:00" right after a reading taken at 10:00:30 must not count as entered late.
        await ArrangeAsync();

        var fueling = await Fuelings.CreateAsync(Request(100_300, 100, at: T.Clock.UtcNow.AddSeconds(-30)), default);

        fueling.OdometerReadingStatus.Should().Be(OdometerReadingStatus.Valid);
        (await T.NewContext().Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(100_300);
    }

    // ---------- consumption (seções 17, 18, 56) ----------

    [Fact]
    public async Task Consumption_TwoFullTanks_IsDistanceOverFuelAndStoredOnTheFueling()
    {
        await ArrangeAsync();
        await FuelAsync(100_000, 300);

        var second = await FuelAsync(101_000, 200);

        second.ConsumptionResult.Should().Be(ConsumptionResult.Calculated);
        second.SegmentDistanceKm.Should().Be(1_000);
        second.SegmentQuantity.Should().Be(200m);
        second.Consumption.Should().Be(5.0m);
        second.SegmentCost.Should().Be(1_200m);
    }

    [Fact]
    public async Task Consumption_PartialFill_IsAddedToTheNextFullTank()
    {
        await ArrangeAsync();
        await FuelAsync(100_000, 300);
        var partial = await FuelAsync(100_400, 80, fullTank: false);

        var full = await FuelAsync(101_000, 120);

        partial.ConsumptionResult.Should().Be(ConsumptionResult.PartialFill);
        full.SegmentQuantity.Should().Be(200m);
        full.Consumption.Should().Be(5.0m);
    }

    [Fact]
    public async Task Consumption_SingleFueling_HasNoFigure()
    {
        await ArrangeAsync(expected: 5.0m);
        var only = await FuelAsync(100_500, 100);

        only.Consumption.Should().BeNull();
        only.ConsumptionResult.Should().Be(ConsumptionResult.FirstFullTank);
        only.Anomalies.Should().BeEmpty("insufficient data never produces a consumption warning");
    }

    [Fact]
    public async Task Consumption_FarBelowTheBaseline_CreatesANeutralAnomaly()
    {
        // seção 54: expected 5.0 km/L, actual 1,000 km / 270 L = 3.70 km/L (−26%).
        await ArrangeAsync(expected: 5.0m);
        await FuelAsync(100_000, 300);

        var fueling = await FuelAsync(101_000, 270);

        fueling.Consumption.Should().Be(3.70m);
        fueling.ExpectedConsumption.Should().Be(5.0m);
        fueling.BaselineSource.Should().Be(ConsumptionBaselineSource.Configured);
        fueling.ConsumptionDeviationPercent.Should().Be(-26.0m);
        fueling.Status.Should().Be(FuelingStatus.PendingReview);
        fueling.Anomalies.Should().ContainSingle(a => a.Type == FuelAnomalyType.LowConsumption)
            .Which.Message.Should().Contain("Revisão recomendada");
        (await EventsAsync()).Should().Contain(OperationalEventType.FuelConsumptionAnomalyDetected);
        (await T.NewContext().WorkOrders.CountAsync()).Should().Be(0, "an anomaly never opens maintenance by itself");
    }

    [Fact]
    public async Task Consumption_StoredSnapshot_DoesNotChangeWhenTheBaselineChangesLater()
    {
        await ArrangeAsync(expected: 5.0m);
        await FuelAsync(100_000, 300);
        var fueling = await FuelAsync(101_000, 270);

        var vehicle = await Services.Vehicles(T).GetAsync(VehicleId, default);
        await Services.Vehicles(T).UpdateAsync(VehicleId, Requests.Vehicle() with
        {
            CurrentOdometerKm = null, ExpectedConsumption = 3.0m, FuelTankCapacity = vehicle.FuelTankCapacity,
        }, default);
        await Services.FuelSettings(T).UpdateAsync(new FuelSettingsRequest
        {
            TankTolerancePercent = 5, PriceDeviationPercent = 20, ConsumptionDeviationPercent = 90, MinHoursBetweenFuelings = 2,
        }, default);

        var reloaded = await Fuelings.GetAsync(fueling.Id, default);
        reloaded.ExpectedConsumption.Should().Be(5.0m);
        reloaded.Consumption.Should().Be(3.70m);
        reloaded.Status.Should().Be(FuelingStatus.PendingReview);
    }

    [Fact]
    public async Task Consumption_WithoutConfiguredBaseline_UsesTheVehicleHistory()
    {
        await ArrangeAsync();
        await FuelAsync(100_000, 300);
        await FuelAsync(101_000, 200);  // 5.0
        await FuelAsync(102_000, 200);  // 5.0
        await FuelAsync(103_000, 200);  // 5.0 — three measured segments

        var fourth = await FuelAsync(104_000, 400);  // 2.5 km/L

        fourth.BaselineSource.Should().Be(ConsumptionBaselineSource.VehicleHistory);
        fourth.ExpectedConsumption.Should().Be(5.0m);
        fourth.Anomalies.Should().ContainSingle(a => a.Type == FuelAnomalyType.LowConsumption);
    }

    // ---------- record anomalies (seções 11, 12) ----------

    [Fact]
    public async Task Anomaly_QuantityAboveTankCapacity_RequiresReviewButIsSaved()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(100_500, 500);

        fueling.Status.Should().Be(FuelingStatus.PendingReview);
        fueling.Anomalies.Should().ContainSingle().Which.Type.Should().Be(FuelAnomalyType.ExcessiveQuantity);
        (await EventsAsync()).Should().Contain(OperationalEventType.FuelingMarkedForReview);
    }

    [Fact]
    public async Task Anomaly_TankTolerance_IsConfigurable()
    {
        await ArrangeAsync();
        await Services.FuelSettings(T).UpdateAsync(new FuelSettingsRequest
        {
            TankTolerancePercent = 30, PriceDeviationPercent = 20, ConsumptionDeviationPercent = 20, MinHoursBetweenFuelings = 2,
        }, default);

        (await FuelAsync(100_500, 500)).Status.Should().Be(FuelingStatus.Valid);
    }

    [Fact]
    public async Task Anomaly_PriceFarFromTheRecentAverage_IsFlagged()
    {
        await ArrangeAsync();
        await FuelAsync(100_500, 100, 6.00m);
        await FuelAsync(101_000, 100, 6.00m);
        await FuelAsync(101_500, 100, 6.00m);

        var fueling = await FuelAsync(102_000, 100, 9.50m);

        fueling.Anomalies.Should().ContainSingle(a => a.Type == FuelAnomalyType.AbnormalPrice);
    }

    [Fact]
    public async Task Anomaly_TwoFuelingsCloseTogether_IsFlagged()
    {
        await ArrangeAsync();
        await FuelAsync(100_500, 100);
        var second = await FuelAsync(100_510, 50, hoursLater: 0.5);
        second.Anomalies.Should().ContainSingle(a => a.Type == FuelAnomalyType.HighFrequency);
    }

    [Fact]
    public async Task Anomaly_ProductNotMatchingTheEngine_IsFlagged()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(100_500, 100, fuelTypeId: GasolineId);
        fueling.Anomalies.Should().ContainSingle(a => a.Type == FuelAnomalyType.FuelTypeMismatch);
    }

    // ---------- correction (seção 14) ----------

    [Fact]
    public async Task CorrectAsync_Quantity_RecalculatesTotalAndConsumptionAndKeepsHistory()
    {
        await ArrangeAsync();
        await FuelAsync(100_000, 300);
        var second = await FuelAsync(101_000, 200);

        var corrected = await Fuelings.CorrectAsync(second.Id, Correction(second) with { Quantity = 250, Reason = "Digitado errado" }, default);

        corrected.TotalAmount.Should().Be(1_500m);
        corrected.Consumption.Should().Be(4.0m);
        var correction = corrected.Corrections.Should().ContainSingle().Subject;
        correction.Reason.Should().Be("Digitado errado");
        correction.Changes.Should().ContainEquivalentOf(new FuelingChange("quantity", "Quantidade", "200,00 L", "250,00 L"));
        correction.Changes.Should().Contain(c => c.Field == "totalAmount");
        (await EventsAsync()).Should().Contain(OperationalEventType.FuelingCorrected);
        (await T.NewContext().AuditLogs.CountAsync(a => a.EntityName == nameof(Fueling) && a.EntityId == second.Id.ToString()))
            .Should().BeGreaterThan(1, "the audit trail keeps the creation and the change");
    }

    [Fact]
    public async Task CorrectAsync_RequiresReasonAndAChange()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(100_500, 100);

        await FluentActions.Invoking(() => Fuelings.CorrectAsync(fueling.Id, Correction(fueling, reason: " ") with { Quantity = 90 }, default))
            .Should().ThrowAsync<ValidationException>();
        await FluentActions.Invoking(() => Fuelings.CorrectAsync(fueling.Id, Correction(fueling), default))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("Nenhum dado*");
    }

    [Fact]
    public async Task CorrectAsync_OdometerAlreadyApplied_NeedsMileageManageAndBecomesAnAuditedCorrection()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(100_900, 100);

        T.CurrentUser.PermissionSet.Remove(Permissions.Mileage.Manage);
        await FluentActions.Invoking(() => Fuelings.CorrectAsync(fueling.Id, Correction(fueling) with { OdometerKm = 100_090 }, default))
            .Should().ThrowAsync<ForbiddenException>();
        // Each HTTP request has its own DbContext; here the failed attempt's in-memory edits must be discarded by hand.
        T.Db.ChangeTracker.Clear();

        T.CurrentUser.PermissionSet.Add(Permissions.Mileage.Manage);
        var corrected = await Fuelings.CorrectAsync(fueling.Id, Correction(fueling) with { OdometerKm = 100_090 }, default);

        corrected.OdometerKm.Should().Be(100_090);
        var db = T.NewContext();
        (await db.OdometerReadings.SingleAsync(r => r.FuelingId == fueling.Id && r.Source == OdometerReadingSource.Correction))
            .OdometerKm.Should().Be(100_090);
        (await db.Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(100_090);
        (await EventsAsync()).Should().Contain(OperationalEventType.MileageCorrected);
    }

    [Fact]
    public async Task CorrectAsync_DateOfAFuelingWithReading_IsLocked()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(100_500, 100);

        var act = () => Fuelings.CorrectAsync(fueling.Id, Correction(fueling) with { FueledAt = fueling.FueledAt.AddHours(-3) }, default);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "fueledAt");
    }

    [Fact]
    public async Task CorrectAsync_TypoInASuspiciousOdometer_ReplacesThePendingReading()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(190_000, 200);

        var corrected = await Fuelings.CorrectAsync(fueling.Id, Correction(fueling) with { OdometerKm = 100_900, Reason = "Dígito a mais" }, default);

        corrected.Status.Should().Be(FuelingStatus.Valid);
        corrected.Anomalies.Should().BeEmpty();
        corrected.OdometerReadingStatus.Should().Be(OdometerReadingStatus.Valid);
        var db = T.NewContext();
        (await db.OdometerReadings.Where(r => r.FuelingId == fueling.Id).Select(r => r.Status).ToListAsync())
            .Should().BeEquivalentTo([OdometerReadingStatus.Rejected, OdometerReadingStatus.Valid]);
        (await db.Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(100_900);
    }

    // ---------- cancellation and review (seções 13, 54, 55) ----------

    [Fact]
    public async Task CancelAsync_RemovesTheFuelingFromConsumptionAndKeepsTheRecord()
    {
        await ArrangeAsync();
        await FuelAsync(100_000, 300);
        var middle = await FuelAsync(101_000, 200);
        var last = await FuelAsync(102_000, 250);
        last.Consumption.Should().Be(4.0m);

        var cancelled = await Fuelings.CancelAsync(middle.Id, new FuelingReasonRequest { Reason = "Lançado em duplicidade" }, default);

        cancelled.Status.Should().Be(FuelingStatus.Cancelled);
        cancelled.Actions.Should().Be(new FuelingActions(false, false, false));
        // A cancelled fueling did not happen: the next segment spans both distances, with only the fuel that really went in.
        var recalculated = await Fuelings.GetAsync(last.Id, default);
        recalculated.SegmentDistanceKm.Should().Be(2_000);
        recalculated.SegmentQuantity.Should().Be(250m);
        recalculated.Consumption.Should().Be(8.0m);
        (await T.NewContext().Fuelings.CountAsync()).Should().Be(3);
        await FluentActions.Invoking(() => Fuelings.CancelAsync(middle.Id, new FuelingReasonRequest { Reason = "de novo" }, default))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CancelAsync_SuspiciousOdometer_IsRejectedWithTheRecord()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(190_000, 200);

        (await Fuelings.CancelAsync(fueling.Id, new FuelingReasonRequest { Reason = "Erro" }, default))
            .OdometerReadingStatus.Should().Be(OdometerReadingStatus.Rejected);
    }

    [Fact]
    public async Task ReviewAsync_ConfirmsTheRecordAndKeepsWhoReviewed()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(100_500, 500);

        await FluentActions.Invoking(() => Fuelings.ReviewAsync(fueling.Id, new FuelingReasonRequest(), default))
            .Should().ThrowAsync<ValidationException>();
        var reviewed = await Fuelings.ReviewAsync(fueling.Id, new FuelingReasonRequest { Reason = "Dois tanques abastecidos, conferido" }, default);

        reviewed.Status.Should().Be(FuelingStatus.Valid);
        reviewed.ReviewNotes.Should().Be("Dois tanques abastecidos, conferido");
        reviewed.Anomalies.Should().ContainSingle().Which.ReviewedAt.Should().NotBeNull();
        (await EventsAsync()).Should().Contain(OperationalEventType.FuelingReviewed);
    }

    [Fact]
    public async Task ReviewAsync_SuspiciousOdometer_ApprovesTheReadingOnlyWithMileageManage()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(101_400, 200, hoursLater: 12);  // 1,400 km in 12 h counts as one day: plausible
        var jump = await FuelAsync(103_200, 200, hoursLater: 12);     // 1,800 km in a day: suspicious
        jump.OdometerReadingStatus.Should().Be(OdometerReadingStatus.PendingReview);
        jump.ConsumptionResult.Should().Be(ConsumptionResult.NotReliable);
        _ = fueling;

        T.CurrentUser.PermissionSet.Remove(Permissions.Mileage.Manage);
        await FluentActions.Invoking(() => Fuelings.ReviewAsync(jump.Id, new FuelingReasonRequest { Reason = "ok" }, default))
            .Should().ThrowAsync<ForbiddenException>();

        T.CurrentUser.PermissionSet.Add(Permissions.Mileage.Manage);
        var reviewed = await Fuelings.ReviewAsync(jump.Id, new FuelingReasonRequest { Reason = "Viagem longa com dois motoristas" }, default);

        reviewed.OdometerReadingStatus.Should().Be(OdometerReadingStatus.Valid);
        reviewed.ConsumptionResult.Should().Be(ConsumptionResult.Calculated, "a confirmed odometer makes the segment reliable");
        reviewed.Consumption.Should().Be(9.0m);
        (await T.NewContext().Vehicles.SingleAsync(v => v.Id == VehicleId)).CurrentOdometerKm.Should().Be(103_200);
    }

    // ---------- permissions and tenancy (seções 38, 39, 41) ----------

    [Fact]
    public async Task Costs_AreHiddenFromWhoCannotSeeThem_ExceptOnTheirOwnRecords()
    {
        await ArrangeAsync();
        var managers = await FuelAsync(100_500, 100, 6.10m);

        T.SignInAs(Company, SystemRoles.Operations);
        var own = await FuelAsync(101_000, 100, 6.20m);

        own.UnitPrice.Should().Be(6.20m);
        own.CanSeeCosts.Should().BeTrue();
        var other = await Fuelings.GetAsync(managers.Id, default);
        other.UnitPrice.Should().BeNull();
        other.TotalAmount.Should().BeNull();
        other.CanSeeCosts.Should().BeFalse();
        var list = await Fuelings.ListAsync(new FuelingListRequest(), default);
        list.Items.Single(i => i.Id == managers.Id).TotalAmount.Should().BeNull();
        list.Items.Single(i => i.Id == own.Id).TotalAmount.Should().Be(620m);

        await FluentActions.Invoking(() => Fuelings.ListAsync(new FuelingListRequest { MinUnitPrice = 6 }, default))
            .Should().ThrowAsync<ForbiddenException>();
        other.Actions.Should().Be(new FuelingActions(false, false, false));
    }

    [Fact]
    public async Task Fuelings_OfAnotherCompany_AreInvisible()
    {
        await ArrangeAsync();
        var fueling = await FuelAsync(100_500, 100);

        await Scenario.SignedInAsync(T, cnpj: "12ABC34501DE35");

        await FluentActions.Invoking(() => Fuelings.GetAsync(fueling.Id, default)).Should().ThrowAsync<NotFoundException>();
        (await Fuelings.ListAsync(new FuelingListRequest(), default)).TotalCount.Should().Be(0);
        await FluentActions.Invoking(() => Fuelings.CancelAsync(fueling.Id, new FuelingReasonRequest { Reason = "x" }, default))
            .Should().ThrowAsync<NotFoundException>();
        var create = () => Fuelings.CreateAsync(Request(100_600, 50), default);
        (await create.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "vehicleId");
    }

    // ---------- list filters and form defaults (seções 27, 31) ----------

    [Fact]
    public async Task ListAsync_FiltersOnTheServer()
    {
        await ArrangeAsync();
        await FuelAsync(100_500, 100);
        await FuelAsync(101_000, 500);  // excessive quantity

        (await Fuelings.ListAsync(new FuelingListRequest { Status = FuelingStatus.PendingReview }, default)).TotalCount.Should().Be(1);
        (await Fuelings.ListAsync(new FuelingListRequest { AnomalyType = FuelAnomalyType.ExcessiveQuantity }, default)).TotalCount.Should().Be(1);
        (await Fuelings.ListAsync(new FuelingListRequest { MinQuantity = 200 }, default)).TotalCount.Should().Be(1);
        (await Fuelings.ListAsync(new FuelingListRequest { Search = "abc1d23" }, default)).TotalCount.Should().Be(2);
        (await Fuelings.ListAsync(new FuelingListRequest { From = T.Clock.Today.AddDays(1) }, default)).TotalCount.Should().Be(0);
        var page = await Fuelings.ListAsync(new FuelingListRequest(), default);
        page.Items.Select(i => i.OdometerKm).Should().Equal(101_000, 100_500);
    }

    [Fact]
    public async Task GetFormDefaultsAsync_SuggestsWhatIsKnown()
    {
        await ArrangeAsync();
        var driver = await Scenario.DriverAsync(T);
        await Services.Assignments(T).AssignAsync(VehicleId, new Fleet.Application.Assignments.AssignmentCreateRequest { DriverId = driver.Id }, default);
        await FuelAsync(100_500, 100);

        var defaults = await Fuelings.GetFormDefaultsAsync(VehicleId, default);

        defaults.CurrentOdometerKm.Should().Be(100_500);
        defaults.CurrentDriverId.Should().Be(driver.Id);
        defaults.LastFuelTypeId.Should().Be(DieselId);
        defaults.TankCapacity.Should().Be(400m);
    }
}
