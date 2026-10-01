using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Maintenance;

public enum MaintenanceScheduleStatus
{
    Scheduled,
    DueSoon,
    Due,
    Overdue,
}

/// <summary>
/// "Fast read" of the last/next maintenance for one (vehicle, plan item) — same idea as
/// <see cref="Vehicle.CurrentOdometerKm"/>: it only exists once the item has been serviced at least once
/// (recalculated by WorkOrderService when a linked WorkOrderItem completes). Vehicles never serviced yet are
/// evaluated from their registration baseline instead of a row here (see MaintenanceScheduleService).
/// </summary>
public class MaintenanceSchedule : AuditableEntity, ITenantScoped, IAuditable
{
    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid MaintenancePlanItemId { get; set; }

    public DateOnly? LastPerformedOn { get; set; }
    public int? LastPerformedKm { get; set; }
    public decimal? LastPerformedHours { get; set; }
    public Guid? LastWorkOrderId { get; set; }

    public DateOnly? NextDueOn { get; set; }
    public int? NextDueKm { get; set; }
    public decimal? NextDueHours { get; set; }
}

/// <summary>Read-only projection used to evaluate a schedule without requiring a persisted row (seção 9/10).</summary>
public sealed record MaintenanceDueData(DateOnly? NextDueOn, int? NextDueKm, decimal? NextDueHours);

/// <summary>
/// The ONLY place that turns due dates/km/hours into a status (mirrors DocumentExpiryPolicy). Each configured axis
/// (date, km, hours) is evaluated independently with its own grace window, and the most urgent one wins — "whichever
/// condition is reached first" (seção 6).
/// </summary>
public static class MaintenanceSchedulePolicy
{
    public static MaintenanceScheduleStatus Evaluate(
        MaintenanceDueData due, MaintenancePlanItem item, DateOnly today, int currentKm, decimal? currentHours)
    {
        var statuses = new List<MaintenanceScheduleStatus>();
        if (due.NextDueOn is { } dueOn)
            statuses.Add(AxisStatus(today.DayNumber, dueOn.DayNumber, item.GraceDays ?? 0));
        if (due.NextDueKm is { } dueKm)
            statuses.Add(AxisStatus(currentKm, dueKm, item.GraceKm ?? 0));
        if (due.NextDueHours is { } dueHours && currentHours is { } hours)
            statuses.Add(AxisStatus((double)hours, (double)dueHours, (double)(item.GraceHours ?? 0)));

        return statuses.Count == 0 ? MaintenanceScheduleStatus.Scheduled : statuses.Max();
    }

    private static MaintenanceScheduleStatus AxisStatus(double current, double due, double grace)
    {
        if (current > due + grace) return MaintenanceScheduleStatus.Overdue;
        if (current >= due) return MaintenanceScheduleStatus.Due;
        if (current >= due - grace) return MaintenanceScheduleStatus.DueSoon;
        return MaintenanceScheduleStatus.Scheduled;
    }

    /// <summary>Next due point from a baseline (last performed, or the vehicle's registration) plus the item's intervals.</summary>
    public static MaintenanceDueData NextDue(MaintenancePlanItem item, DateOnly? baselineOn, int? baselineKm, decimal? baselineHours) => new(
        item.IntervalMonths is { } months && baselineOn is { } on ? on.AddMonths(months) : null,
        item.IntervalKm is { } km && baselineKm is { } bkm ? bkm + km : null,
        item.IntervalHours is { } hours && baselineHours is { } bh ? bh + hours : null);
}
