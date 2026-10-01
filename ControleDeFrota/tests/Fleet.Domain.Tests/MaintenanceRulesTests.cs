using FluentAssertions;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Tests;

public class MaintenanceSchedulePolicyTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    private static MaintenancePlanItem Item(int? graceKm = null, int? graceDays = null, decimal? graceHours = null) => new()
    {
        ServiceName = "Troca de óleo", GraceKm = graceKm, GraceDays = graceDays, GraceHours = graceHours,
    };

    [Theory]
    [InlineData(8_000, MaintenanceScheduleStatus.Scheduled)]
    [InlineData(9_600, MaintenanceScheduleStatus.DueSoon)]  // within the 500 km grace window before due
    [InlineData(10_000, MaintenanceScheduleStatus.Due)]
    [InlineData(10_400, MaintenanceScheduleStatus.Due)]     // within the 500 km grace window after due
    [InlineData(10_600, MaintenanceScheduleStatus.Overdue)]
    public void Evaluate_ByKm(int currentKm, MaintenanceScheduleStatus expected)
    {
        var due = new MaintenanceDueData(null, 10_000, null);
        MaintenanceSchedulePolicy.Evaluate(due, Item(graceKm: 500), Today, currentKm, null).Should().Be(expected);
    }

    [Theory]
    [InlineData(10, MaintenanceScheduleStatus.Scheduled)]  // due in 10 days, outside the 5-day grace window
    [InlineData(3, MaintenanceScheduleStatus.DueSoon)]     // due in 3 days, inside the window
    [InlineData(0, MaintenanceScheduleStatus.Due)]         // due today
    [InlineData(-5, MaintenanceScheduleStatus.Due)]        // 5 days overdue, still inside the grace window
    [InlineData(-10, MaintenanceScheduleStatus.Overdue)]   // 10 days overdue, past the grace window
    public void Evaluate_ByDate(int daysUntilDue, MaintenanceScheduleStatus expected)
    {
        var due = new MaintenanceDueData(Today.AddDays(daysUntilDue), null, null);
        MaintenanceSchedulePolicy.Evaluate(due, Item(graceDays: 5), Today, currentKm: 0, null).Should().Be(expected);
    }

    [Fact]
    public void Evaluate_NoHoursReading_IgnoresHoursAxis()
    {
        var due = new MaintenanceDueData(null, null, 500);
        MaintenanceSchedulePolicy.Evaluate(due, Item(), Today, currentKm: 0, currentHours: null)
            .Should().Be(MaintenanceScheduleStatus.Scheduled);
    }

    [Fact]
    public void Evaluate_CombinedAxes_MostUrgentWins()
    {
        // Km axis is far from due (Scheduled); date axis is already overdue — the worst one wins.
        var due = new MaintenanceDueData(Today.AddDays(-30), 50_000, null);
        MaintenanceSchedulePolicy.Evaluate(due, Item(graceKm: 500, graceDays: 5), Today, currentKm: 1_000, null)
            .Should().Be(MaintenanceScheduleStatus.Overdue);
    }

    [Fact]
    public void NextDue_FromBaseline_AppliesEachConfiguredInterval()
    {
        var item = new MaintenancePlanItem { IntervalKm = 10_000, IntervalMonths = 6, IntervalHours = 200 };
        var due = MaintenanceSchedulePolicy.NextDue(item, Today, 50_000, 1_000m);

        due.NextDueOn.Should().Be(Today.AddMonths(6));
        due.NextDueKm.Should().Be(60_000);
        due.NextDueHours.Should().Be(1_200m);
    }

    [Fact]
    public void NextDue_IntervalNotConfigured_AxisStaysNull()
    {
        var item = new MaintenancePlanItem { IntervalKm = 10_000 };
        var due = MaintenanceSchedulePolicy.NextDue(item, Today, 50_000, 1_000m);

        due.NextDueKm.Should().Be(60_000);
        due.NextDueOn.Should().BeNull();
        due.NextDueHours.Should().BeNull();
    }
}

public class MaintenancePlanResolverTests
{
    private static Vehicle Truck(Guid id) => new() { Id = id, Type = VehicleType.Truck };

    [Fact]
    public void ResolveFor_VehicleSpecificPlan_WinsOverTypeAndDefault()
    {
        var vehicle = Truck(Guid.NewGuid());
        var plans = new[]
        {
            new MaintenancePlan { Name = "Padrão" },
            new MaintenancePlan { Name = "Caminhões", VehicleType = VehicleType.Truck },
            new MaintenancePlan { Name = "Este veículo", VehicleId = vehicle.Id },
        };

        MaintenancePlanResolver.ResolveFor(vehicle, plans)!.Name.Should().Be("Este veículo");
    }

    [Fact]
    public void ResolveFor_NoVehiclePlan_FallsBackToTypePlan()
    {
        var vehicle = Truck(Guid.NewGuid());
        var plans = new[] { new MaintenancePlan { Name = "Padrão" }, new MaintenancePlan { Name = "Caminhões", VehicleType = VehicleType.Truck } };

        MaintenancePlanResolver.ResolveFor(vehicle, plans)!.Name.Should().Be("Caminhões");
    }

    [Fact]
    public void ResolveFor_NoVehicleOrTypePlan_FallsBackToDefault()
    {
        var vehicle = Truck(Guid.NewGuid());
        var plans = new[] { new MaintenancePlan { Name = "Padrão" }, new MaintenancePlan { Name = "Vans", VehicleType = VehicleType.Van } };

        MaintenancePlanResolver.ResolveFor(vehicle, plans)!.Name.Should().Be("Padrão");
    }

    [Fact]
    public void ResolveFor_NoPlanAtAll_ReturnsNull() =>
        MaintenancePlanResolver.ResolveFor(Truck(Guid.NewGuid()), []).Should().BeNull();
}

public class HourMeterPolicyTests
{
    private static readonly DateTime Base = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly HourMeterBaseline Baseline = new(1_000m, Base);

    [Fact]
    public void Evaluate_NoBaseline_IsValid() => HourMeterPolicy.Evaluate(5, Base, null).Check.Should().Be(HourMeterCheck.Valid);

    [Theory]
    [InlineData(1_000, 1)]  // did not move
    [InlineData(1_016, 1)]  // under the 20h/day limit
    [InlineData(1_100, 5)]  // 20h/day exactly
    public void Evaluate_PlausibleIncrease_IsValid(double hours, int days) =>
        HourMeterPolicy.Evaluate((decimal)hours, Base.AddDays(days), Baseline).Check.Should().Be(HourMeterCheck.Valid);

    [Fact]
    public void Evaluate_LowerThanBaseline_IsDecrease() =>
        HourMeterPolicy.Evaluate(999, Base.AddDays(1), Baseline).Check.Should().Be(HourMeterCheck.Decrease);

    [Fact]
    public void Evaluate_ExtraDigitTypo_IsSuspicious() =>
        HourMeterPolicy.Evaluate(10_000, Base.AddDays(4), Baseline).Check.Should().Be(HourMeterCheck.SuspiciousJump);
}

public class WorkOrderWorkflowTests
{
    [Theory]
    [InlineData(WorkOrderStatus.Draft, WorkOrderStatus.Approved, true)]
    [InlineData(WorkOrderStatus.Draft, WorkOrderStatus.InProgress, false)]
    [InlineData(WorkOrderStatus.Approved, WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts, true)]
    [InlineData(WorkOrderStatus.WaitingParts, WorkOrderStatus.Completed, false)]
    [InlineData(WorkOrderStatus.WaitingParts, WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Completed, true)]
    [InlineData(WorkOrderStatus.Completed, WorkOrderStatus.InProgress, false)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.Approved, false)]
    public void CanTransition(WorkOrderStatus from, WorkOrderStatus to, bool expected) =>
        WorkOrderWorkflow.CanTransition(from, to).Should().Be(expected);

    [Theory]
    [InlineData(WorkOrderStatus.Draft, false)]
    [InlineData(WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.WaitingParts, true)]
    [InlineData(WorkOrderStatus.Completed, false)]
    public void OccupiesVehicle(WorkOrderStatus status, bool expected) => WorkOrderWorkflow.OccupiesVehicle(status).Should().Be(expected);

    [Fact]
    public void RequiresResolution_OnlyToComplete()
    {
        WorkOrderWorkflow.RequiresResolution(WorkOrderStatus.Completed).Should().BeTrue();
        WorkOrderWorkflow.RequiresResolution(WorkOrderStatus.Cancelled).Should().BeFalse();
    }

    [Fact]
    public void RequiresReason_ToCancelOrReject()
    {
        WorkOrderWorkflow.RequiresReason(WorkOrderStatus.Cancelled).Should().BeTrue();
        WorkOrderWorkflow.RequiresReason(WorkOrderStatus.Rejected).Should().BeTrue();
        WorkOrderWorkflow.RequiresReason(WorkOrderStatus.Completed).Should().BeFalse();
    }
}

public class MaintenanceRequestWorkflowTests
{
    [Theory]
    [InlineData(MaintenanceRequestStatus.Open, false)]
    [InlineData(MaintenanceRequestStatus.Converted, true)]
    [InlineData(MaintenanceRequestStatus.Rejected, true)]
    public void IsClosed(MaintenanceRequestStatus status, bool expected) => MaintenanceRequestWorkflow.IsClosed(status).Should().Be(expected);
}
