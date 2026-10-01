using Fleet.Application.Common;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Maintenance;

public sealed record MaintenanceScheduleItemResponse(
    Guid MaintenancePlanItemId, string ServiceName, MaintenancePriority Priority, MaintenanceScheduleStatus Status,
    DateOnly? LastPerformedOn, int? LastPerformedKm, decimal? LastPerformedHours,
    DateOnly? NextDueOn, int? NextDueKm, decimal? NextDueHours);

/// <summary>
/// The preventive maintenance schedule of a vehicle (seção 9/10): resolves which plan applies
/// (<see cref="MaintenancePlanResolver"/>) and evaluates each item's status (<see cref="MaintenanceSchedulePolicy"/>).
/// Also owns recalculating a <see cref="MaintenanceSchedule"/> row when a linked WorkOrderItem completes.
/// </summary>
public sealed class MaintenanceScheduleService(IFleetDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<MaintenanceScheduleItemResponse>> ForVehicleAsync(Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        var plan = await ResolvePlanAsync(vehicle, ct);
        if (plan is null) return [];

        var itemIds = plan.Items.Select(i => i.Id).ToList();
        var schedules = await db.MaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId && itemIds.Contains(s.MaintenancePlanItemId))
            .ToDictionaryAsync(s => s.MaintenancePlanItemId, ct);
        var baseline = await BaselineAsync(vehicle, ct);
        var today = clock.Today;

        var results = new List<MaintenanceScheduleItemResponse>();
        foreach (var item in plan.Items)
        {
            var due = schedules.TryGetValue(item.Id, out var schedule)
                ? new MaintenanceDueData(schedule.NextDueOn, schedule.NextDueKm, schedule.NextDueHours)
                : MaintenanceSchedulePolicy.NextDue(item, baseline.On, baseline.Km, baseline.Hours);
            var status = MaintenanceSchedulePolicy.Evaluate(due, item, today, vehicle.CurrentOdometerKm, vehicle.HourMeter);
            results.Add(new MaintenanceScheduleItemResponse(
                item.Id, item.ServiceName, item.Priority, status,
                schedule?.LastPerformedOn, schedule?.LastPerformedKm, schedule?.LastPerformedHours,
                due.NextDueOn, due.NextDueKm, due.NextDueHours));
        }
        // Most urgent first (Overdue > Due > DueSoon > Scheduled).
        return results.OrderByDescending(r => r.Status).ThenBy(r => r.ServiceName).ToList();
    }

    /// <summary>
    /// Updates (or creates) the fast-read schedule row for one (vehicle, plan item) when its WorkOrderItem completes.
    /// Does NOT save — the caller (WorkOrderService) commits it together with the work order.
    /// </summary>
    public async Task RecalculateAsync(Guid vehicleId, Guid maintenancePlanItemId, WorkOrder workOrder, CancellationToken ct)
    {
        var item = await db.MaintenancePlans.SelectMany(p => p.Items).SingleOrDefaultAsync(i => i.Id == maintenancePlanItemId, ct);
        if (item is null) return; // the plan item no longer exists (guarded against on normal edits; defensive here)

        var schedule = await db.MaintenanceSchedules
            .SingleOrDefaultAsync(s => s.VehicleId == vehicleId && s.MaintenancePlanItemId == maintenancePlanItemId, ct);
        if (schedule is null)
        {
            schedule = new MaintenanceSchedule { VehicleId = vehicleId, MaintenancePlanItemId = maintenancePlanItemId };
            db.MaintenanceSchedules.Add(schedule);
        }

        var completedOn = clock.ToBusinessDate(workOrder.CompletedAt ?? clock.UtcNow);
        schedule.LastPerformedOn = completedOn;
        schedule.LastPerformedKm = workOrder.OdometerKm;
        schedule.LastPerformedHours = workOrder.HourMeter;
        schedule.LastWorkOrderId = workOrder.Id;

        var due = MaintenanceSchedulePolicy.NextDue(item, completedOn, workOrder.OdometerKm, workOrder.HourMeter);
        schedule.NextDueOn = due.NextDueOn;
        schedule.NextDueKm = due.NextDueKm;
        schedule.NextDueHours = due.NextDueHours;
    }

    private async Task<MaintenancePlan?> ResolvePlanAsync(Vehicle vehicle, CancellationToken ct)
    {
        var plans = await db.MaintenancePlans.Include(p => p.Items)
            .Where(p => p.IsActive && (p.VehicleId == vehicle.Id || p.VehicleId == null))
            .ToListAsync(ct);
        return MaintenancePlanResolver.ResolveFor(vehicle, plans);
    }

    /// <summary>
    /// Baseline for a vehicle never serviced yet: its registration reading (or, for vehicles older than the
    /// odometer history, the stored CurrentOdometerKm/CreatedAt — same fallback MileageService uses).
    /// </summary>
    private async Task<(DateOnly On, int Km, decimal? Hours)> BaselineAsync(Vehicle vehicle, CancellationToken ct)
    {
        var registration = await db.OdometerReadings
            .Where(r => r.VehicleId == vehicle.Id && r.Source == OdometerReadingSource.Registration)
            .Select(r => new { r.OdometerKm, r.ReadAt }).FirstOrDefaultAsync(ct);
        var on = clock.ToBusinessDate(registration?.ReadAt ?? vehicle.OdometerUpdatedAt ?? vehicle.CreatedAt);
        var km = registration?.OdometerKm ?? vehicle.CurrentOdometerKm;
        return (on, km, vehicle.HourMeter);
    }
}
