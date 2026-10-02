using Fleet.Application.Assignments;
using Fleet.Application.Checklists;
using Fleet.Application.Common;
using Fleet.Application.Documents;
using Fleet.Application.Occurrences;
using Fleet.Application.Vehicles;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Documents;
using Fleet.Domain.Drivers;
using Fleet.Domain.Fuel;
using Fleet.Domain.Tires;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Dashboard;

/// <summary>Phase 1 indicators, kept for compatibility.</summary>
public sealed record DashboardIndicators(
    int ActiveVehicles,
    int AvailableVehicles,
    int VehiclesUnderMaintenance,
    int ActiveDrivers);

/// <summary>Vehicles by derived operational status (ADR-018). The parts add up to Total.</summary>
public sealed record FleetStatusBreakdown(
    int Total, int Available, int Assigned, int OnTrip, int Unavailable, int UnderMaintenance, int Inactive);

/// <summary>Counters of the operational modules. Null = the caller cannot see that module.</summary>
public sealed record OperationsSummary(
    int? DocumentsExpiringSoon,
    int? DocumentsExpired,
    int? PendingChecklists,
    int? OpenOccurrences,
    int? CriticalOpenOccurrences,
    int? PendingMileageReviews,
    int VehiclesWithoutRecentMileage);

public sealed record HighestMileageVehicle(Guid Id, string LicensePlate, int OdometerKm);

public sealed record MileageSummary(
    int FleetKmThisMonth,
    int AverageKmPerVehicleThisMonth,
    HighestMileageVehicle? HighestMileageVehicle,
    int VehiclesWithoutRecentMileage,
    int StaleAfterDays);

/// <summary>Preventive maintenance due-counts across the fleet (seção 28) + the work order funnel.</summary>
public sealed record MaintenanceSummary(
    int DueToday, int DueSoon, int Overdue, int InProgress, int WaitingParts, int CompletedThisMonth, int VehiclesUnderMaintenance);

public enum AlertType
{
    LicenseExpired,
    LicenseExpiringSoon,
    DocumentExpired,
    DocumentExpiringSoon,
    CriticalOccurrence,
    MileagePendingReview,
    CriticalWorkOrder,
    /// <summary>Phase 4: a fueling with an anomaly nobody reviewed yet.</summary>
    FuelingPendingReview,
    /// <summary>Phase 5: an installed tire at the company's minimum tread or with damage recorded in its last inspection.</summary>
    TireCritical,
}

public enum AlertSeverity
{
    Warning,
    Critical,
}

/// <param name="EntityType">Where the alert leads: Driver, Vehicle, Implement, Company or Occurrence.</param>
/// <param name="Tab">Section of the target page (e.g. "documents", "mileage"), when relevant.</param>
public sealed record DashboardAlert(
    AlertType Type,
    AlertSeverity Severity,
    string Title,
    string Description,
    string EntityType,
    Guid EntityId,
    DateOnly DueDate,
    string? Tab = null);

public sealed record DashboardResponse(
    DashboardIndicators Indicators,
    FleetStatusBreakdown Fleet,
    OperationsSummary Operations,
    MileageSummary Mileage,
    MaintenanceSummary? Maintenance,
    IReadOnlyList<DashboardAlert> Alerts,
    int TotalAlerts);

/// <summary>
/// Each indicator/alert source is an independent method, so future KPIs (fuel, maintenance, costs) are added
/// alongside without touching the existing ones. Module data is shown only with that module's view permission.
/// </summary>
public sealed class DashboardService(IFleetDbContext db, IClock clock, ICurrentUser currentUser, ChecklistService checklists)
{
    public const int MaxAlerts = 10;

    private sealed record AlertSource(IReadOnlyList<DashboardAlert> Alerts, int Total);

    private static readonly AlertSource NoAlerts = new([], 0);

    public async Task<DashboardResponse> GetAsync(CancellationToken ct)
    {
        var fleet = await GetFleetAsync(ct);
        var activeDrivers = await db.Drivers.CountAsync(d => d.Status == DriverStatus.Active, ct);
        var indicators = new DashboardIndicators(
            fleet.Total - fleet.Inactive, fleet.Available + fleet.Assigned, fleet.UnderMaintenance, activeDrivers);
        var mileage = await GetMileageAsync(ct);

        var sources = new List<AlertSource>
        {
            // Alerts name drivers (personal data), so they require drivers.view on top of dashboard.view.
            Can(Permissions.Drivers.View) ? await GetLicenseAlertsAsync(ct) : NoAlerts,
            Can(Permissions.Documents.View) ? await GetDocumentAlertsAsync(ct) : NoAlerts,
            Can(Permissions.Occurrences.View) ? await GetCriticalOccurrenceAlertsAsync(ct) : NoAlerts,
            Can(Permissions.Mileage.Manage) ? await GetMileageReviewAlertsAsync(ct) : NoAlerts,
            Can(Permissions.Maintenance.View) ? await GetCriticalWorkOrderAlertsAsync(ct) : NoAlerts,
            // Only who can act on it (review) sees it; the fuel dashboard shows the same queue to fuel.view.
            Can(Permissions.Fuel.ReviewAnomalies) ? await GetFuelingReviewAlertsAsync(ct) : NoAlerts,
            Can(Permissions.Tires.View) ? await GetTireAlertsAsync(ct) : NoAlerts,
        };
        var alerts = sources.SelectMany(s => s.Alerts)
            .OrderByDescending(a => a.Severity).ThenBy(a => a.DueDate)
            .Take(MaxAlerts).ToList();

        var maintenance = Can(Permissions.Maintenance.View) ? await GetMaintenanceAsync(fleet.UnderMaintenance, ct) : null;
        return new DashboardResponse(
            indicators, fleet, await GetOperationsAsync(mileage, ct), mileage, maintenance, alerts, sources.Sum(s => s.Total));
    }

    private bool Can(string permission) => currentUser.HasPermission(permission);

    private async Task<FleetStatusBreakdown> GetFleetAsync(CancellationToken ct)
    {
        var active = AssignmentService.Active(db);
        // Two small columns per vehicle; grouping in memory keeps the operational rule in one place (VehicleOperationalState).
        var rows = await db.Vehicles
            .Select(v => new { v.Status, Assigned = active.Any(a => a.VehicleId == v.Id) })
            .ToListAsync(ct);
        var counts = rows
            .GroupBy(r => VehicleOperationalState.From(r.Status, r.Assigned))
            .ToDictionary(g => g.Key, g => g.Count());
        int Count(VehicleOperationalStatus s) => counts.GetValueOrDefault(s);

        return new FleetStatusBreakdown(
            counts.Values.Sum(), Count(VehicleOperationalStatus.Available), Count(VehicleOperationalStatus.Assigned),
            Count(VehicleOperationalStatus.OnTrip), Count(VehicleOperationalStatus.Unavailable),
            Count(VehicleOperationalStatus.UnderMaintenance), Count(VehicleOperationalStatus.Inactive));
    }

    private async Task<OperationsSummary> GetOperationsAsync(MileageSummary mileage, CancellationToken ct)
    {
        var today = clock.Today;
        int? expiring = null, expired = null, pendingChecklists = null, open = null, critical = null, reviews = null;
        if (Can(Permissions.Documents.View))
        {
            expiring = await db.Documents.WhereStatus(DocumentStatus.ExpiringSoon, today).CountAsync(ct);
            expired = await db.Documents.WhereStatus(DocumentStatus.Expired, today).CountAsync(ct);
        }
        if (Can(Permissions.Checklists.View)) pendingChecklists = (await checklists.PendingAsync(ct)).Count;
        if (Can(Permissions.Occurrences.View))
        {
            open = await OccurrenceService.Open(db.Occurrences).CountAsync(ct);
            critical = await OccurrenceService.Open(db.Occurrences).CountAsync(o => o.Severity == OccurrenceSeverity.Critical, ct);
        }
        if (Can(Permissions.Mileage.Manage))
            reviews = await db.OdometerReadings.CountAsync(r => r.Status == OdometerReadingStatus.PendingReview, ct);

        return new OperationsSummary(expiring, expired, pendingChecklists, open, critical, reviews, mileage.VehiclesWithoutRecentMileage);
    }

    /// <summary>
    /// Kilometers driven this month = per vehicle, the highest valid reading of the month minus the highest valid
    /// reading before the month (or the first of the month). Only valid readings count: suspicious ones are pending.
    /// </summary>
    private async Task<MileageSummary> GetMileageAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var monthStart = clock.StartOfBusinessDayUtc(new DateOnly(today.Year, today.Month, 1));
        var valid = db.OdometerReadings.Where(r => r.Status == OdometerReadingStatus.Valid && r.Vehicle.Status != VehicleStatus.Inactive);

        var thisMonth = await valid.Where(r => r.ReadAt >= monthStart)
            .GroupBy(r => r.VehicleId)
            .Select(g => new { VehicleId = g.Key, Max = g.Max(r => r.OdometerKm), Min = g.Min(r => r.OdometerKm) })
            .ToListAsync(ct);
        var movedIds = thisMonth.Select(m => m.VehicleId).ToList();
        var before = await valid.Where(r => r.ReadAt < monthStart && movedIds.Contains(r.VehicleId))
            .GroupBy(r => r.VehicleId)
            .Select(g => new { VehicleId = g.Key, Max = g.Max(r => r.OdometerKm) })
            .ToDictionaryAsync(b => b.VehicleId, b => b.Max, ct);
        var fleetKm = thisMonth.Sum(m => Math.Max(0, m.Max - before.GetValueOrDefault(m.VehicleId, m.Min)));

        var activeVehicles = await db.Vehicles.CountAsync(v => v.Status != VehicleStatus.Inactive, ct);
        var highest = await db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive)
            .OrderByDescending(v => v.CurrentOdometerKm)
            .Select(v => new HighestMileageVehicle(v.Id, v.LicensePlate, v.CurrentOdometerKm))
            .FirstOrDefaultAsync(ct);
        var stale = await VehicleService.WhereStaleMileage(db.Vehicles, clock.UtcNow).CountAsync(ct);

        return new MileageSummary(fleetKm, activeVehicles == 0 ? 0 : fleetKm / activeVehicles, highest, stale, OdometerPolicy.StaleAfterDays);
    }

    /// <summary>
    /// Due counts only cover vehicles serviced at least once (MaintenanceSchedule rows): a never-serviced vehicle's
    /// due status is evaluated live on its own maintenance tab, not aggregated here (keeps this query cheap).
    /// </summary>
    private async Task<MaintenanceSummary> GetMaintenanceAsync(int vehiclesUnderMaintenance, CancellationToken ct)
    {
        var today = clock.Today;
        var rows = await db.MaintenanceSchedules
            .Where(s => s.Vehicle.Status != VehicleStatus.Inactive)
            .Join(db.MaintenancePlans.SelectMany(p => p.Items), s => s.MaintenancePlanItemId, i => i.Id, (s, i) => new
            {
                s.NextDueOn, s.NextDueKm, s.NextDueHours, i.GraceDays, i.GraceKm, i.GraceHours,
                s.Vehicle.CurrentOdometerKm, s.Vehicle.HourMeter,
            })
            .ToListAsync(ct);
        var statuses = rows.Select(r => MaintenanceSchedulePolicy.Evaluate(
            new MaintenanceDueData(r.NextDueOn, r.NextDueKm, r.NextDueHours),
            new MaintenancePlanItem { GraceDays = r.GraceDays, GraceKm = r.GraceKm, GraceHours = r.GraceHours },
            today, r.CurrentOdometerKm, r.HourMeter)).ToList();

        var monthStart = clock.StartOfBusinessDayUtc(new DateOnly(today.Year, today.Month, 1));
        var inProgress = await db.WorkOrders.CountAsync(w => w.Status == WorkOrderStatus.InProgress, ct);
        var waitingParts = await db.WorkOrders.CountAsync(w => w.Status == WorkOrderStatus.WaitingParts, ct);
        var completedThisMonth = await db.WorkOrders.CountAsync(w => w.Status == WorkOrderStatus.Completed && w.CompletedAt >= monthStart, ct);

        return new MaintenanceSummary(
            statuses.Count(s => s == MaintenanceScheduleStatus.Due), statuses.Count(s => s == MaintenanceScheduleStatus.DueSoon),
            statuses.Count(s => s == MaintenanceScheduleStatus.Overdue), inProgress, waitingParts, completedThisMonth, vehiclesUnderMaintenance);
    }

    private async Task<AlertSource> GetCriticalWorkOrderAlertsAsync(CancellationToken ct)
    {
        var query = db.WorkOrders.Where(w => w.Priority == MaintenancePriority.Critical &&
            w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.Rejected);
        var total = await query.CountAsync(ct);
        var orders = await query.OrderBy(w => w.OpenedAt).Take(MaxAlerts)
            .Select(w => new { w.Id, w.Sequence, w.Description, w.OpenedAt, Plate = w.Vehicle.LicensePlate })
            .ToListAsync(ct);
        var alerts = orders.Select(w => new DashboardAlert(
            AlertType.CriticalWorkOrder, AlertSeverity.Critical, "Manutenção crítica",
            $"{LicensePlate.Format(w.Plate)} — OS-{w.Sequence:D6}: {Short(w.Description)}",
            "WorkOrder", w.Id, clock.ToBusinessDate(w.OpenedAt), "work-orders")).ToList();
        return new AlertSource(alerts, total);
    }

    private async Task<AlertSource> GetLicenseAlertsAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var limit = today.AddDays(Driver.LicenseExpiryAlertDays);
        var query = db.Drivers.Where(d => d.Status != DriverStatus.Inactive && d.LicenseExpiresOn <= limit);

        var total = await query.CountAsync(ct);
        var drivers = await query
            .OrderBy(d => d.LicenseExpiresOn)
            .Take(MaxAlerts)
            .Select(d => new { d.Id, d.FullName, d.LicenseExpiresOn })
            .ToListAsync(ct);

        var alerts = drivers.Select(d =>
        {
            var expired = d.LicenseExpiresOn < today;
            return new DashboardAlert(
                expired ? AlertType.LicenseExpired : AlertType.LicenseExpiringSoon,
                expired ? AlertSeverity.Critical : AlertSeverity.Warning,
                expired ? "CNH vencida" : "CNH vencendo",
                DueText($"A CNH de {d.FullName}", d.LicenseExpiresOn, today),
                "Driver", d.Id, d.LicenseExpiresOn);
        }).ToList();
        return new AlertSource(alerts, total);
    }

    private async Task<AlertSource> GetDocumentAlertsAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var query = db.Documents.WhereAlert(today);
        var total = await query.CountAsync(ct);
        var companyId = currentUser.CompanyId;
        var documents = await query
            .OrderBy(d => d.ExpiresOn)
            .Take(MaxAlerts)
            .Select(d => new
            {
                d.Id, d.OwnerType, d.VehicleId, d.DriverId, d.ImplementId, d.ExpiresOn, TypeName = d.DocumentType.Name,
                // Driver names are personal data: shown only to who can see drivers.
                Owner = d.Vehicle != null ? d.Vehicle.LicensePlate
                    : d.Implement != null ? d.Implement.LicensePlate
                    : d.Driver != null ? d.Driver.FullName : null,
            })
            .ToListAsync(ct);

        var canSeeDrivers = Can(Permissions.Drivers.View);
        var alerts = documents.Select(d =>
        {
            var expired = d.ExpiresOn!.Value < today;
            var (entityType, entityId, owner) = d.OwnerType switch
            {
                DocumentOwnerType.Vehicle => ("Vehicle", d.VehicleId!.Value, $"do veículo {LicensePlate.Format(d.Owner ?? "")}"),
                DocumentOwnerType.Implement => ("Implement", d.ImplementId!.Value, $"do implemento {LicensePlate.Format(d.Owner ?? "")}"),
                DocumentOwnerType.Driver => ("Driver", d.DriverId!.Value, canSeeDrivers ? $"de {d.Owner}" : "de um motorista"),
                _ => ("Company", companyId ?? Guid.Empty, "da empresa"),
            };
            return new DashboardAlert(
                expired ? AlertType.DocumentExpired : AlertType.DocumentExpiringSoon,
                expired ? AlertSeverity.Critical : AlertSeverity.Warning,
                expired ? "Documento vencido" : "Documento vencendo",
                DueText($"{d.TypeName} {owner}", d.ExpiresOn.Value, today),
                entityType, entityId, d.ExpiresOn.Value, "documents");
        }).ToList();
        return new AlertSource(alerts, total);
    }

    private async Task<AlertSource> GetCriticalOccurrenceAlertsAsync(CancellationToken ct)
    {
        var query = OccurrenceService.Open(db.Occurrences).Where(o => o.Severity == OccurrenceSeverity.Critical);
        var total = await query.CountAsync(ct);
        var occurrences = await query.OrderBy(o => o.OccurredAt).Take(MaxAlerts)
            .Select(o => new { o.Id, o.Description, o.OccurredAt, Plate = o.Vehicle != null ? o.Vehicle.LicensePlate : null })
            .ToListAsync(ct);
        var alerts = occurrences.Select(o => new DashboardAlert(
            AlertType.CriticalOccurrence, AlertSeverity.Critical, "Ocorrência crítica em aberto",
            (o.Plate is null ? string.Empty : $"{LicensePlate.Format(o.Plate)}: ") + Short(o.Description),
            "Occurrence", o.Id, clock.ToBusinessDate(o.OccurredAt))).ToList();
        return new AlertSource(alerts, total);
    }

    private async Task<AlertSource> GetMileageReviewAlertsAsync(CancellationToken ct)
    {
        var query = db.OdometerReadings.Where(r => r.Status == OdometerReadingStatus.PendingReview);
        var total = await query.CountAsync(ct);
        var readings = await query.OrderBy(r => r.ReadAt).Take(MaxAlerts)
            .Select(r => new { r.VehicleId, r.Vehicle.LicensePlate, r.OdometerKm, r.ReadAt })
            .ToListAsync(ct);
        var alerts = readings.Select(r => new DashboardAlert(
            AlertType.MileagePendingReview, AlertSeverity.Warning, "Leitura de hodômetro suspeita",
            $"{LicensePlate.Format(r.LicensePlate)}: {BrazilianFormat.Number(r.OdometerKm)} km aguardando revisão.",
            "Vehicle", r.VehicleId, clock.ToBusinessDate(r.ReadAt), "mileage")).ToList();
        return new AlertSource(alerts, total);
    }

    private async Task<AlertSource> GetFuelingReviewAlertsAsync(CancellationToken ct)
    {
        var query = db.Fuelings.Where(f => f.Status == FuelingStatus.PendingReview);
        var total = await query.CountAsync(ct);
        var fuelings = await query.OrderBy(f => f.FueledAt).Take(MaxAlerts)
            .Select(f => new
            {
                f.Id, f.Vehicle.LicensePlate, f.FueledAt,
                Message = f.Anomalies.Where(a => a.ReviewedAt == null).OrderBy(a => a.DetectedAt).Select(a => a.Message).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var alerts = fuelings.Select(f => new DashboardAlert(
            AlertType.FuelingPendingReview, AlertSeverity.Warning, "Abastecimento requer revisão",
            $"{LicensePlate.Format(f.LicensePlate)}: {Short(f.Message ?? "alerta pendente")}",
            "Fueling", f.Id, clock.ToBusinessDate(f.FueledAt))).ToList();
        return new AlertSource(alerts, total);
    }

    /// <summary>Installed tires needing action now (company thresholds — the wording never says "legal").</summary>
    private async Task<AlertSource> GetTireAlertsAsync(CancellationToken ct)
    {
        var settings = await db.TireSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? TireSettings.Defaults();
        var query = db.Tires.Where(t => t.Status == TireStatus.Installed &&
                                        ((t.CurrentTreadDepthMm != null && t.CurrentTreadDepthMm <= settings.MinTreadDepthMm) || t.LastInspectionHasDamage));
        var total = await query.CountAsync(ct);
        var tires = await query.OrderBy(t => t.Code).Take(MaxAlerts)
            .Select(t => new { t.Id, t.Code, t.CurrentTreadDepthMm, t.LastInspectionHasDamage, t.TreadMeasuredAt, t.LastInspectedAt })
            .ToListAsync(ct);
        var ids = tires.Select(t => t.Id).ToList();
        var places = await db.TireInstallations.Where(i => ids.Contains(i.TireId) && i.RemovedAt == null)
            .Select(i => new { i.TireId, i.PositionLabel, Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement!.LicensePlate })
            .ToDictionaryAsync(i => i.TireId, ct);
        var alerts = tires.Select(t =>
        {
            var place = places.GetValueOrDefault(t.Id);
            var where = place is null ? "" : $" ({LicensePlate.Format(place.Plate)}, {place.PositionLabel})";
            var lowTread = t.CurrentTreadDepthMm is { } mm && mm <= settings.MinTreadDepthMm;
            return new DashboardAlert(AlertType.TireCritical, AlertSeverity.Critical,
                lowTread ? "Pneu no sulco mínimo" : "Pneu com dano registrado",
                lowTread
                    ? $"{t.Code}{where}: sulco de {BrazilianFormat.Compact(t.CurrentTreadDepthMm!.Value)} mm, no mínimo configurado pela empresa."
                    : $"{t.Code}{where}: dano registrado na última inspeção. Requer inspeção.",
                "Tire", t.Id, clock.ToBusinessDate((lowTread ? t.TreadMeasuredAt : t.LastInspectedAt) ?? clock.UtcNow));
        }).ToList();
        return new AlertSource(alerts, total);
    }

    private static string DueText(string subject, DateOnly dueDate, DateOnly today)
    {
        var days = dueDate.DayNumber - today.DayNumber;
        var date = BrazilianFormat.Date(dueDate);
        return days switch
        {
            < 0 => $"{subject} venceu em {date}.",
            0 => $"{subject} vence hoje.",
            1 => $"{subject} vence amanhã.",
            _ => $"{subject} vence em {days} dias ({date}).",
        };
    }

    private static string Short(string text) => text.Length <= 90 ? text : text[..89] + "…";
}
