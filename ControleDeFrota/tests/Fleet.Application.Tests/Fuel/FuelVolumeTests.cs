using System.Diagnostics;
using FluentAssertions;
using Fleet.Application.Dashboard;
using Fleet.Application.Fuel;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Fuel;
using Fleet.Domain.Vehicles;
using Xunit.Abstractions;

namespace Fleet.Application.Tests.Fuel;

public class FuelDashboardAlertTests : FuelTestBase
{
    [Fact]
    public async Task MainDashboard_ShowsFuelingsToReview_OnlyToWhoCanReview()
    {
        await ArrangeAsync();
        await FuelAsync(100_500, 500);  // above the 400 L tank

        (await Services.Dashboard(T).GetAsync(default)).Alerts
            .Should().ContainSingle(a => a.Type == AlertType.FuelingPendingReview).Which.EntityType.Should().Be("Fueling");

        T.SignInAs(Company, SystemRoles.Operations);
        (await Services.Dashboard(T).GetAsync(default)).Alerts.Should().NotContain(a => a.Type == AlertType.FuelingPendingReview);
    }
}

/// <summary>
/// seção 44/58: totals over a large history must be aggregated by the database. 12,000 fuelings of 300 vehicles are
/// inserted directly (bypassing the service, which is not what is measured) and every analytics entry point is timed.
/// The limit is generous on purpose (shared CI machines); loading the rows into memory or querying per row breaks it.
/// </summary>
public class FuelVolumeTests(ITestOutputHelper output) : FuelTestBase
{
    private const int Vehicles = 300;
    private const int FuelingsPerVehicle = 40;

    [Fact]
    public async Task Analytics_OverTwelveThousandFuelings_StayFast()
    {
        await ArrangeAsync();
        var start = T.Clock.UtcNow.AddDays(-FuelingsPerVehicle * 3);
        for (var v = 0; v < Vehicles; v++)
        {
            var vehicle = new Vehicle
            {
                LicensePlate = $"VOL{v:0000}", Renavam = $"{v:00000000000}", Chassis = $"9BWVOLUME{v:00000000}", Manufacturer = "Volvo",
                Model = "FH", ManufacturingYear = 2022, ModelYear = 2022, Type = VehicleType.TruckTractor, FuelType = VehicleFuelType.DieselS10,
                CurrentOdometerKm = 100_000 + FuelingsPerVehicle * 1_000,
            };
            T.Db.Vehicles.Add(vehicle);
            for (var i = 0; i < FuelingsPerVehicle; i++)
            {
                var at = start.AddDays(i * 3);
                T.Db.Fuelings.Add(new Fueling
                {
                    VehicleId = vehicle.Id, Vehicle = vehicle, FuelTypeId = DieselId, FueledAt = at, FueledOn = T.Clock.ToBusinessDate(at),
                    OdometerKm = 100_000 + i * 1_000, Quantity = 400, UnitPrice = 6.1m, TotalAmount = 2_440m, PaymentMethod = PaymentMethod.FuelCard,
                    ConsumptionResult = i == 0 ? ConsumptionResult.FirstFullTank : ConsumptionResult.Calculated,
                    SegmentDistanceKm = i == 0 ? null : 1_000, SegmentQuantity = i == 0 ? null : 400, SegmentCost = i == 0 ? null : 2_440m,
                    Consumption = i == 0 ? null : 2.5m, ExpectedConsumption = i == 0 ? null : 2.6m, SegmentExpectedQuantity = i == 0 ? null : 384.615m,
                });
            }
        }
        await T.Db.SaveChangesAsync();
        T.Db.ChangeTracker.Clear();

        var analytics = Services.FuelAnalytics(T);
        var period = new FuelPeriodRequest { From = T.Clock.Today.AddDays(-150), To = T.Clock.Today };
        var watch = Stopwatch.StartNew();

        var dashboard = await analytics.GetDashboardAsync(period, default);
        var dashboardTime = watch.Elapsed;
        var consumption = await analytics.GetConsumptionReportAsync(new FuelReportRequest { From = period.From, To = period.To }, default);
        var costs = await analytics.GetCostReportAsync(new FuelReportRequest { From = period.From, To = period.To, GroupBy = FuelCostGrouping.Station }, default);
        var page = await Services.Fuelings(T).ListAsync(new FuelingListRequest { From = period.From, To = period.To, PageSize = 50 }, default);
        watch.Stop();

        output.WriteLine($"dashboard {dashboardTime.TotalMilliseconds:0} ms; all four calls {watch.Elapsed.TotalMilliseconds:0} ms");
        dashboard.Totals.Fuelings.Should().Be(Vehicles * FuelingsPerVehicle);
        dashboard.Totals.TotalCost.Should().Be(Vehicles * FuelingsPerVehicle * 2_440m);
        dashboard.Totals.AverageConsumption.Should().Be(2.5m);
        consumption.Rows.TotalCount.Should().Be(Vehicles);
        consumption.Rows.Items.Should().HaveCount(20);
        costs.Rows.Items.Should().ContainSingle().Which.Name.Should().Be("Sem posto informado");
        page.Items.Should().HaveCount(50);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
    }
}
