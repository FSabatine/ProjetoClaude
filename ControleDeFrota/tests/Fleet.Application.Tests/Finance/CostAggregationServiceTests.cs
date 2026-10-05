using Fleet.Application.Finance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Finance;
using Fleet.Domain.Fuel;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Tires;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Finance;

public class CostAggregationServiceTests : IDisposable
{
    private readonly TestDb T = new();

    private async Task<Guid> ArrangeVehicleAsync(string role = SystemRoles.FleetManager)
    {
        await Scenario.SignedInAsync(T, role);
        return (await Scenario.VehicleAsync(T)).Id;
    }

    private async Task AddFuelingAsync(Guid vehicleId, decimal total, DateOnly fueledOn)
    {
        var fuelTypes = await Services.FuelTypes(T).ListAsync(false, default);
        T.Db.Fuelings.Add(new Fueling
        {
            Id = Guid.NewGuid(), CompanyId = T.CurrentUser.CompanyId!.Value, VehicleId = vehicleId, FuelTypeId = fuelTypes[0].Id,
            FueledAt = fueledOn.ToDateTime(TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc), FueledOn = fueledOn,
            OdometerKm = 1000, Quantity = 100m, UnitPrice = total / 100m, TotalAmount = total,
            PaymentMethod = Fleet.Domain.Fuel.PaymentMethod.Cash, Status = FuelingStatus.Valid,
        });
        await T.Db.SaveChangesAsync();
    }

    private async Task AddWorkOrderAsync(Guid vehicleId, decimal totalCost, DateTime openedAt, WorkOrderStatus status = WorkOrderStatus.Completed)
    {
        var sequence = await T.Db.WorkOrders.CountAsync() + 1;
        T.Db.WorkOrders.Add(new WorkOrder
        {
            Id = Guid.NewGuid(), CompanyId = T.CurrentUser.CompanyId!.Value, Sequence = sequence, VehicleId = vehicleId,
            Description = "Troca de óleo", OpenedAt = openedAt, Status = status, TotalCost = totalCost,
        });
        await T.Db.SaveChangesAsync();
    }

    private async Task<Guid> AddTireAsync(string brand = "Michelin")
    {
        var model = new TireModel { Id = Guid.NewGuid(), CompanyId = T.CurrentUser.CompanyId!.Value, Brand = brand, Name = "X Multi", Size = "295/80R22.5" };
        T.Db.TireModels.Add(model);
        var sequence = await T.Db.Tires.CountAsync() + 1;
        var tire = new Tire
        {
            Id = Guid.NewGuid(), CompanyId = T.CurrentUser.CompanyId!.Value, Sequence = sequence, Code = $"PN-{sequence:D6}", TireModelId = model.Id,
        };
        T.Db.Tires.Add(tire);
        await T.Db.SaveChangesAsync();
        return tire.Id;
    }

    private async Task InstallTireAsync(Guid tireId, Guid vehicleId, DateTime installedAt, DateTime? removedAt)
    {
        T.Db.TireInstallations.Add(new TireInstallation
        {
            Id = Guid.NewGuid(), CompanyId = T.CurrentUser.CompanyId!.Value, TireId = tireId, VehicleId = vehicleId,
            PositionCode = "1E", PositionLabel = "Dianteiro esquerdo", AxleNumber = 1, InstalledAt = installedAt, RemovedAt = removedAt,
            InstallReason = TireInstallReason.Installation,
        });
        await T.Db.SaveChangesAsync();
    }

    private async Task AddTireCostAsync(Guid tireId, decimal amount, DateOnly incurredOn)
    {
        T.Db.TireCosts.Add(new TireCost
        {
            Id = Guid.NewGuid(), CompanyId = T.CurrentUser.CompanyId!.Value, TireId = tireId, Type = TireCostType.Repair,
            IncurredOn = incurredOn, Amount = amount,
        });
        await T.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetVehicleCostBreakdown_BlendsFuelMaintenanceAndManual_WithoutDuplicating()
    {
        var vehicleId = await ArrangeVehicleAsync();
        var today = T.Clock.Today;
        await AddFuelingAsync(vehicleId, 500m, today);
        await AddWorkOrderAsync(vehicleId, 300m, today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = categories.Single(c => c.Code == "TOLLS").Id, VehicleId = vehicleId,
            Description = "Pedágio BR-277", ExpenseDate = today, Amount = 50m,
        }, default);

        var breakdown = await Services.CostAggregation(T).GetVehicleCostBreakdownAsync(vehicleId, today.AddDays(-1), today, default);

        breakdown.FuelCost.Should().Be(500m);
        breakdown.MaintenanceCost.Should().Be(300m);
        breakdown.OtherCost.Should().Be(50m);
        breakdown.TireCost.Should().Be(0m);
        breakdown.TotalCost.Should().Be(850m);
        breakdown.IsPartial.Should().BeFalse();
    }

    [Fact]
    public async Task GetVehicleCostBreakdown_CancelledFuelingAndRejectedWorkOrder_DoNotCount()
    {
        var vehicleId = await ArrangeVehicleAsync();
        var today = T.Clock.Today;
        await AddFuelingAsync(vehicleId, 500m, today);
        (await T.Db.Fuelings.SingleAsync()).Status = FuelingStatus.Cancelled;
        await T.Db.SaveChangesAsync();
        await AddWorkOrderAsync(vehicleId, 300m, today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), WorkOrderStatus.Rejected);

        var breakdown = await Services.CostAggregation(T).GetVehicleCostBreakdownAsync(vehicleId, today.AddDays(-1), today, default);

        breakdown.FuelCost.Should().Be(0m);
        breakdown.MaintenanceCost.Should().Be(0m);
    }

    [Fact]
    public async Task GetVehicleCostBreakdown_WithoutFuelViewCosts_ZeroesFuelSliceAndMarksPartial()
    {
        var vehicleId = await ArrangeVehicleAsync();
        var today = T.Clock.Today;
        await AddFuelingAsync(vehicleId, 500m, today);
        await AddWorkOrderAsync(vehicleId, 300m, today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        // Custom permission set: finance.viewcosts and maintenance.viewcosts, but NOT fuel.viewcosts.
        T.CurrentUser.PermissionSet = [Permissions.Finance.View, Permissions.Finance.ViewCosts, Permissions.Maintenance.ViewCosts];

        var breakdown = await Services.CostAggregation(T).GetVehicleCostBreakdownAsync(vehicleId, today.AddDays(-1), today, default);

        breakdown.FuelCost.Should().Be(0m);
        breakdown.MaintenanceCost.Should().Be(300m);
        breakdown.IsPartial.Should().BeTrue();
    }

    [Fact]
    public async Task GetFleetCostByCategory_GroupsSystemAndManualCategoriesSeparately()
    {
        var vehicleId = await ArrangeVehicleAsync();
        var today = T.Clock.Today;
        await AddFuelingAsync(vehicleId, 500m, today);
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = categories.Single(c => c.Code == "WASHING").Id, Description = "Lavagem", ExpenseDate = today, Amount = 40m,
        }, default);

        var result = await Services.CostAggregation(T).GetFleetCostByCategoryAsync(today.AddDays(-1), today, default);

        result.Items.Should().Contain(c => c.CategoryName == "Combustível" && c.Amount == 500m);
        result.Items.Should().Contain(c => c.CategoryName == "Lavagem" && c.Amount == 40m);
    }

    [Fact]
    public async Task TireCost_AttributedToVehicleInstalledAtTheTime_NotToAVehicleOutsideTheInstallationWindow()
    {
        var vehicleA = await ArrangeVehicleAsync();
        var vehicleB = (await Scenario.VehicleAsync(T, index: 1)).Id;
        var tireId = await AddTireAsync();

        // Installed on vehicle A from day 1 to day 10, then on vehicle B from day 10 onwards.
        var baseDate = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        await InstallTireAsync(tireId, vehicleA, baseDate, baseDate.AddDays(9));
        await InstallTireAsync(tireId, vehicleB, baseDate.AddDays(9), null);

        await AddTireCostAsync(tireId, 200m, DateOnly.FromDateTime(baseDate.AddDays(3))); // while on vehicle A
        await AddTireCostAsync(tireId, 150m, DateOnly.FromDateTime(baseDate.AddDays(15))); // while on vehicle B

        var period = (DateOnly.FromDateTime(baseDate), DateOnly.FromDateTime(baseDate.AddDays(20)));
        var breakdownA = await Services.CostAggregation(T).GetVehicleCostBreakdownAsync(vehicleA, period.Item1, period.Item2, default);
        var breakdownB = await Services.CostAggregation(T).GetVehicleCostBreakdownAsync(vehicleB, period.Item1, period.Item2, default);

        breakdownA.TireCost.Should().Be(200m);
        breakdownB.TireCost.Should().Be(150m);
    }

    public void Dispose() => T.Dispose();
}
