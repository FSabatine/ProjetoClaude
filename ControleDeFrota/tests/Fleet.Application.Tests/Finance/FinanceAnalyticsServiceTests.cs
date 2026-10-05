using Fleet.Application.Finance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Mileage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Finance;

public class FinanceAnalyticsServiceTests : IDisposable
{
    private readonly TestDb T = new();

    private async Task<Guid> ArrangeVehicleAsync()
    {
        await Scenario.SignedInAsync(T, SystemRoles.FleetManager);
        return (await Scenario.VehicleAsync(T)).Id;
    }

    private async Task AddReadingAsync(Guid vehicleId, int odometerKm, DateTime readAt)
    {
        var vehicle = await T.Db.Vehicles.SingleAsync(v => v.Id == vehicleId);
        await Services.Mileage(T).AddReadingAsync(vehicle, odometerKm, readAt, OdometerReadingSource.Manual, null, null, "odometerKm", default);
    }

    [Fact]
    public async Task CostPerKm_NoMileageHistory_HasInsufficientData()
    {
        var vehicleId = await ArrangeVehicleAsync();
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = categories.Single(c => c.Code == "TOLLS").Id, VehicleId = vehicleId,
            Description = "Pedágio", ExpenseDate = T.Clock.Today, Amount = 100m,
        }, default);

        var result = await Services.FinanceAnalytics(T).GetVehicleCostPerKmAsync(vehicleId, T.Clock.Today.AddDays(-30), T.Clock.Today, default);

        result.HasSufficientData.Should().BeFalse();
        result.CostPerKm.Should().BeNull();
        result.TotalCost.Should().Be(100m); // the cost itself is still shown — only the ratio is withheld
    }

    [Fact]
    public async Task CostPerKm_SufficientMileage_Divides()
    {
        var vehicleId = await ArrangeVehicleAsync();
        // Registration itself wrote a reading at vehicle creation (ADR-019, odometer 1_000). Advance 10 days
        // before the jump to 10_000 km so it is both unambiguously later AND under the 1.500 km/day suspicious-jump
        // threshold (OdometerPolicy) — otherwise it would be saved as PendingReview and excluded from the history.
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(10);
        await AddReadingAsync(vehicleId, 10_000, T.Clock.UtcNow);
        var periodStart = T.Clock.Today.AddDays(1);
        T.Clock.UtcNow = T.Clock.UtcNow.AddDays(30);
        await AddReadingAsync(vehicleId, 10_500, T.Clock.UtcNow);
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = categories.Single(c => c.Code == "TOLLS").Id, VehicleId = vehicleId,
            Description = "Pedágio", ExpenseDate = T.Clock.Today, Amount = 1000m,
        }, default);

        var result = await Services.FinanceAnalytics(T).GetVehicleCostPerKmAsync(vehicleId, periodStart, T.Clock.Today, default);

        result.DistanceKm.Should().Be(500);
        result.HasSufficientData.Should().BeTrue();
        result.CostPerKm.Should().Be(2.0000m);
    }

    [Fact]
    public async Task Tco_IncludesAcquisitionValueAndOperatingCost()
    {
        await Scenario.SignedInAsync(T, SystemRoles.FleetManager);
        var vehicle = await Scenario.VehicleAsync(T, change: r => r with { AcquisitionDate = new DateOnly(2025, 1, 1), AcquisitionValue = 200_000m });
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = categories.Single(c => c.Code == "INSURANCE").Id, VehicleId = vehicle.Id,
            Description = "Seguro", ExpenseDate = T.Clock.Today, Amount = 5_000m,
        }, default);

        var tco = await Services.FinanceAnalytics(T).GetVehicleTcoAsync(vehicle.Id, default);

        tco.AcquisitionValue.Should().Be(200_000m);
        tco.OperatingCost.Should().Be(5_000m);
        tco.TotalCost.Should().Be(205_000m);
    }

    public void Dispose() => T.Dispose();
}
