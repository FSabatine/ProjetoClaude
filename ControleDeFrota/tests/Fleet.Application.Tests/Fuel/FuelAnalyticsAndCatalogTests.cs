using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Fuel;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Fuel;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Fuel;

public class FuelAnalyticsTests : FuelTestBase
{
    private FuelAnalyticsService Analytics => Services.FuelAnalytics(T);

    /// <summary>Two full tanks: 300 L × 6.00 then 1,000 km later 200 L × 6.50 (5.0 km/L).</summary>
    private async Task TwoFullTanksAsync()
    {
        await FuelAsync(100_000, 300, 6.00m);
        await FuelAsync(101_000, 200, 6.50m);
    }

    [Fact]
    public async Task Dashboard_TotalsAndMeasuredMetrics()
    {
        await ArrangeAsync();
        await TwoFullTanksAsync();

        var dashboard = await Analytics.GetDashboardAsync(new FuelPeriodRequest(), default);

        dashboard.Totals.Should().BeEquivalentTo(new FuelTotals(
            Fuelings: 2, Liters: 500, OtherUnitsQuantity: 0, TotalCost: 3_100m, AveragePricePerLiter: 6.2m,
            AverageConsumption: 5.0m, MeasuredDistanceKm: 1_000,
            CostPerKm: 1.30m));  // cost of the measured segment (200 L × 6.50) / 1,000 km
        dashboard.HighestCostVehicles.Should().ContainSingle().Which.Cost.Should().Be(3_100m);
        dashboard.LowestConsumptionVehicles.Should().ContainSingle().Which.Consumption.Should().Be(5.0m);
        dashboard.Recent.Should().HaveCount(2);
        dashboard.Monthly.Should().HaveCount(12);
        dashboard.ByFuelType.Should().ContainSingle().Which.Quantity.Should().Be(500);
    }

    [Fact]
    public async Task Dashboard_WithoutCostPermission_ShowsNoMoney()
    {
        await ArrangeAsync();
        await TwoFullTanksAsync();
        T.SignInAs(Company, SystemRoles.Maintenance);

        var dashboard = await Analytics.GetDashboardAsync(new FuelPeriodRequest(), default);

        dashboard.CanSeeCosts.Should().BeFalse();
        dashboard.Totals.TotalCost.Should().BeNull();
        dashboard.Totals.AveragePricePerLiter.Should().BeNull();
        dashboard.Totals.CostPerKm.Should().BeNull();
        dashboard.Totals.AverageConsumption.Should().Be(5.0m, "consumption is operational, not financial");
        dashboard.HighestCostVehicles.Should().BeEmpty();
        dashboard.Monthly.Should().OnlyContain(p => p.Cost == null);
        dashboard.Recent.Should().OnlyContain(r => r.TotalAmount == null);
    }

    [Fact]
    public async Task Dashboard_CancelledFuelings_DoNotCount()
    {
        await ArrangeAsync();
        await TwoFullTanksAsync();
        var duplicate = await FuelAsync(101_100, 50);
        await Fuelings.CancelAsync(duplicate.Id, new FuelingReasonRequest { Reason = "Duplicado" }, default);

        (await Analytics.GetDashboardAsync(new FuelPeriodRequest(), default)).Totals.Fuelings.Should().Be(2);
    }

    [Fact]
    public async Task Dashboard_PeriodLongerThanTwoYears_IsRejected()
    {
        await ArrangeAsync();
        var act = () => Analytics.GetDashboardAsync(new FuelPeriodRequest { From = new DateOnly(2023, 1, 1), To = new DateOnly(2026, 1, 1) }, default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task VehicleFuel_HistoryHasEmptyBucketsWithoutInventedConsumption()
    {
        await ArrangeAsync(expected: 5.0m);
        await TwoFullTanksAsync();

        var fuel = await Analytics.GetVehicleAsync(VehicleId, new VehicleFuelRequest { Granularity = FuelHistoryGranularity.Week,
            From = T.Clock.Today.AddDays(-20), To = T.Clock.Today }, default);

        fuel.ExpectedConsumption.Should().Be(5.0m);
        fuel.BaselineSource.Should().Be(ConsumptionBaselineSource.Configured);
        fuel.TankCapacity.Should().Be(400m);
        fuel.LastFueling!.OdometerKm.Should().Be(101_000);
        fuel.History.Should().HaveCountGreaterThan(2);
        fuel.History.Where(p => p.Fuelings == 0).Should().OnlyContain(p => p.Consumption == null && p.Quantity == 0);
        fuel.History.Should().ContainSingle(p => p.Consumption == 5.0m);
    }

    [Fact]
    public async Task ConsumptionReport_ComparesWithTheExpectedSnapshot()
    {
        await ArrangeAsync(expected: 5.0m);
        await FuelAsync(100_000, 300);
        await FuelAsync(101_000, 250);  // 4.0 km/L

        var report = await Analytics.GetConsumptionReportAsync(new FuelReportRequest(), default);

        report.Rows.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new ConsumptionReportRow(
            VehicleId, "ABC1D23", FuelUnit.Liter, 1, 1_000, 250, 4.0m, 5.0m, -20.0m));
    }

    [Fact]
    public async Task CostReport_ByDriver_UsesFactualNames()
    {
        await ArrangeAsync();
        var driver = await Scenario.DriverAsync(T);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
        await Fuelings.CreateAsync(Request(100_500, 100, 6m, driverId: driver.Id), default);
        await FuelAsync(101_000, 50, 6m);

        var report = await Analytics.GetCostReportAsync(new FuelReportRequest { GroupBy = FuelCostGrouping.Driver, SortBy = "cost", SortDirection = SortDirection.Desc }, default);

        report.Rows.Items.Select(r => (r.Name, r.Cost)).Should().Equal(("Motorista 1", 600m), ("Sem motorista informado", 300m));
    }

    [Fact]
    public async Task StationAndPriceReports_AverageMinMax()
    {
        await ArrangeAsync();
        var station = await Services.FuelStations(T).CreateAsync(new FuelStationRequest { Name = "Posto ABC" }, default);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
        await Fuelings.CreateAsync(Request(100_500, 100, 6.00m) with { FuelStationId = station.Id }, default);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
        await Fuelings.CreateAsync(Request(101_000, 300, 6.40m) with { FuelStationId = station.Id }, default);

        var stations = await Analytics.GetStationReportAsync(new FuelReportRequest(), default);
        var row = stations.Rows.Items.Should().ContainSingle().Subject;
        row.StationName.Should().Be("Posto ABC");
        row.Quantity.Should().Be(400);
        row.AveragePrice.Should().Be(6.3m);  // (600 + 1,920) / 400 L
        row.MinPrice.Should().Be(6.00m);
        row.MaxPrice.Should().Be(6.40m);

        // The clock crossed from September into October between the two fuelings: one row per product and month.
        var prices = await Analytics.GetPriceReportAsync(new FuelReportRequest(), default);
        prices.Rows.Items.Select(r => (r.Month, r.AveragePrice, r.Fuelings)).Should().Equal((10, 6.40m, 1), (9, 6.00m, 1));
    }
}

public class FuelCatalogTests : FuelTestBase
{
    [Fact]
    public async Task FuelTypes_DefaultCatalogIsCreatedOnce_AndCodesAreUnique()
    {
        await ArrangeAsync();
        var service = Services.FuelTypes(T);

        (await service.ListAsync(includeInactive: true, default)).Should().HaveCount(FuelTypeDefaults.All.Count);
        var duplicate = () => service.CreateAsync(new FuelTypeRequest { Name = "Outro diesel", Code = "s10", Category = FuelCategory.Diesel }, default);
        (await duplicate.Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("code");

        var arla = await service.CreateAsync(new FuelTypeRequest { Name = "Biodiesel B100", Code = "b100", Category = FuelCategory.Diesel }, default);
        arla.Code.Should().Be("B100");
        (await service.ListAsync(includeInactive: true, default)).Should().HaveCount(FuelTypeDefaults.All.Count + 1);
    }

    [Fact]
    public async Task FuelTypes_InUse_CannotBeDeletedNorChangeUnit()
    {
        await ArrangeAsync();
        await FuelAsync(100_500, 100);
        var service = Services.FuelTypes(T);

        await FluentActions.Invoking(() => service.DeleteAsync(DieselId, default)).Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Invoking(() => service.UpdateAsync(DieselId,
                new FuelTypeRequest { Name = "Diesel S10", Code = "S10", Category = FuelCategory.Diesel, Unit = FuelUnit.CubicMeter }, default))
            .Should().ThrowAsync<BusinessRuleException>();

        // Deactivating is the way out — and a deactivated type is refused on new fuelings.
        await service.UpdateAsync(DieselId, new FuelTypeRequest { Name = "Diesel S10", Code = "S10", Category = FuelCategory.Diesel, IsActive = false }, default);
        var act = () => FuelAsync(101_000, 100);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "fuelTypeId");
    }

    [Fact]
    public async Task Stations_ValidateCnpjAndRejectDuplicates()
    {
        await ArrangeAsync();
        var service = Services.FuelStations(T);

        await FluentActions.Invoking(() => service.CreateAsync(new FuelStationRequest { Name = "X", Cnpj = "11.111.111/1111-11" }, default))
            .Should().ThrowAsync<ValidationException>();
        var created = await service.CreateAsync(new FuelStationRequest { Name = "Posto Centro", Cnpj = "11.222.333/0001-81" }, default);
        created.Cnpj.Should().Be("11222333000181");
        (await FluentActions.Invoking(() => service.CreateAsync(new FuelStationRequest { Name = "Outro", Cnpj = "11222333000181" }, default))
            .Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("cnpj");
    }

    [Fact]
    public async Task Stations_WithFuelings_AreDeactivatedNotDeleted_AndShowTheirHistory()
    {
        await ArrangeAsync();
        var service = Services.FuelStations(T);
        var station = await service.CreateAsync(new FuelStationRequest { Name = "Posto ABC" }, default);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
        await Fuelings.CreateAsync(Request(100_500, 100) with { FuelStationId = station.Id }, default);

        await FluentActions.Invoking(() => service.DeleteAsync(station.Id, default)).Should().ThrowAsync<BusinessRuleException>();
        (await service.GetAsync(station.Id, default)).FuelingCount.Should().Be(1);
        (await Fuelings.ListAsync(new FuelingListRequest { FuelStationId = station.Id }, default)).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Prices_AreHistoryAndRecordAnEvent_WithoutRewritingFuelings()
    {
        await ArrangeAsync();
        var service = Services.FuelStations(T);
        var station = await service.CreateAsync(new FuelStationRequest { Name = "Posto ABC" }, default);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(1);
        var fueling = await Fuelings.CreateAsync(Request(100_500, 100, 6.20m) with { FuelStationId = station.Id }, default);

        await service.AddPriceAsync(station.Id, new FuelPriceRequest { FuelTypeId = DieselId, Price = 6.45m, EffectiveFrom = T.Clock.Today }, default);
        await service.AddPriceAsync(station.Id, new FuelPriceRequest { FuelTypeId = DieselId, Price = 6.10m, EffectiveFrom = T.Clock.Today.AddDays(1) }, default);

        (await service.ListPricesAsync(station.Id, default)).Select(p => p.Price).Should().Equal(6.10m, 6.45m);
        (await Fuelings.GetAsync(fueling.Id, default)).UnitPrice.Should().Be(6.20m, "the price paid never changes");
        (await Fuelings.GetPriceHintAsync(station.Id, DieselId, default))!.Price.Should().Be(6.45m);
        (await EventsAsync()).Count(e => e == OperationalEventType.FuelPriceChanged).Should().Be(2);
    }

    [Fact]
    public async Task Stations_OfAnotherCompany_AreInvisible()
    {
        await ArrangeAsync();
        var station = await Services.FuelStations(T).CreateAsync(new FuelStationRequest { Name = "Posto ABC" }, default);

        await Scenario.SignedInAsync(T, cnpj: "12ABC34501DE35");

        (await Services.FuelStations(T).ListAsync(new FuelStationListRequest(), default)).TotalCount.Should().Be(0);
        await FluentActions.Invoking(() => Services.FuelStations(T).GetAsync(station.Id, default)).Should().ThrowAsync<NotFoundException>();
        // The other company gets its own default fuel catalog, never this one's.
        (await Services.FuelTypes(T).ListAsync(true, default)).Select(t => t.Id).Should().NotContain(DieselId);
    }

    [Fact]
    public async Task Settings_DefaultsUntilSaved_AndValidated()
    {
        await ArrangeAsync();
        var service = Services.FuelSettings(T);

        (await service.GetAsync(default)).Should().Be(new FuelSettingsResponse(5, 20, 20, 2, false));
        await FluentActions.Invoking(() => service.UpdateAsync(new FuelSettingsRequest { PriceDeviationPercent = 0, ConsumptionDeviationPercent = 20 }, default))
            .Should().ThrowAsync<ValidationException>();
        (await T.NewContext().FuelSettings.CountAsync()).Should().Be(0);
    }
}
