using Fleet.Application.Common;
using Fleet.Domain.Common;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

public sealed record TireStatusCounts(int Total, int Installed, int InStock, int UnderInspection, int UnderRepair, int UnderRetread, int Disposed);

public sealed record TireAttentionCounts(int NearReplacement, int BelowMinimum, int InspectionOverdue, int DamageOrUnevenWear, int RequiresReview, int AgeExceeded);

public sealed record TireDashboardItem(Guid TireId, string Code, string Brand, string ModelName, string Size, TireStatus Status, string? AssetLabel,
    string? PositionLabel, decimal? CurrentTreadDepthMm, string Detail);

public sealed record TireCostRankingItem(Guid TireId, string Code, string Brand, string ModelName, string Size, decimal TotalCost, int Km, decimal CostPerKm);

public sealed record TireMovementItem(long Id, OperationalEventType Type, DateTime OccurredAt, string Summary, Guid? TireId);

public sealed record TireUpcomingInspection(Guid TireId, string Code, string? AssetLabel, string? PositionLabel, DateTime DueAt, bool Overdue);

public sealed record TireDashboardResponse(
    TireStatusCounts Status, TireAttentionCounts Attention, IReadOnlyList<TireDashboardItem> AbnormalWear,
    IReadOnlyList<TireCostRankingItem>? HighestCostPerKm, IReadOnlyList<TireMovementItem> RecentMovements,
    IReadOnlyList<TireUpcomingInspection> UpcomingInspections, bool CanSeeCosts, decimal MinTreadDepthMm, decimal TreadWarningDepthMm,
    int InspectionIntervalDays);

public sealed class TireReportRequest : ListRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? ImplementId { get; set; }
    public TireStatus? Status { get; set; }
    public string? Brand { get; set; }
    public string? Size { get; set; }
    public TireCondition? Condition { get; set; }
    /// <summary>Inspection report: only inspections that require action.</summary>
    public bool? RequiresAction { get; set; }
}

public sealed record TireReportPage<T>(DateOnly? From, DateOnly? To, bool CanSeeCosts, PagedResult<T> Rows);

public sealed record TireLifecycleReportRow(
    Guid InstallationId, Guid TireId, string Code, string Brand, string ModelName, string Size, string AssetLabel, string PositionLabel,
    DateTime InstalledAt, DateTime? RemovedAt, TireRemovalReason? RemovalReason, int? DistanceKm, int TireKm, int RepairCount, int RetreadCount,
    decimal? LifecycleCost, decimal? CostPerKm);

public sealed record TireInspectionReportRow(
    Guid InspectionId, Guid TireId, string Code, string? AssetLabel, string? PositionLabel, DateTime InspectedAt, TireInspectionSource Source,
    decimal? TreadDepthMm, decimal? Pressure, TirePressureUnit? PressureUnit, TirePressureCheck? PressureCheck, TireCondition Condition,
    TireWearPattern WearPattern, int DamageCount);

public sealed record TireCostReportRow(
    Guid TireId, string Code, string Brand, string ModelName, string Size, TireStatus Status, decimal? Purchase, decimal Repairs, decimal Retreads,
    decimal Other, decimal Total, int Km, bool HasUnmeasuredDistance, decimal? CostPerKm);

public sealed class TireReportRequestValidator : AbstractValidator<TireReportRequest>
{
    public TireReportRequestValidator()
    {
        this.ValidPeriod(x => x.From, x => x.To, "to");
        RuleFor(x => x).Must(x => x.From is not { } f || x.To is not { } t || t.DayNumber - f.DayNumber < DateRangeRules.MaxPeriodDays)
            .WithName("to").OverridePropertyName("to").WithMessage("Escolha um período de até 5 anos.");
    }
}

/// <summary>
/// Tire dashboard and reports (seções 44, 45). Counts and sums are SQL; the cost/km ranking needs the running km of open
/// stints, so it is computed over three narrow aggregated queries (one row per tire), never by loading histories.
/// </summary>
public sealed class TireAnalyticsService(
    IFleetDbContext db,
    IClock clock,
    TireLifecycle lifecycle,
    TireService tires,
    TireSettingsService settingsService,
    IValidator<TireReportRequest> reportValidator)
{
    private const int ListSize = 8;

    public async Task<TireDashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var settings = await settingsService.CurrentAsync(ct);
        var byStatus = await db.Tires.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Of(TireStatus s) => byStatus.SingleOrDefault(x => x.Key == s)?.Count ?? 0;
        var status = new TireStatusCounts(byStatus.Where(x => x.Key != TireStatus.Disposed).Sum(x => x.Count), Of(TireStatus.Installed),
            Of(TireStatus.InStock), Of(TireStatus.UnderInspection), Of(TireStatus.UnderRepair), Of(TireStatus.UnderRetread), Of(TireStatus.Disposed));

        Task<int> Count(TireAlertFilter f) => tires.WhereAlert(db.Tires, f, settings).CountAsync(ct);
        var attention = new TireAttentionCounts(await Count(TireAlertFilter.NearReplacement), await Count(TireAlertFilter.BelowMinimum),
            await Count(TireAlertFilter.InspectionOverdue), await Count(TireAlertFilter.DamageOrUnevenWear), await Count(TireAlertFilter.RequiresReview),
            await Count(TireAlertFilter.AgeExceeded));

        var abnormal = await AbnormalWearAsync(settings, ct);
        var ranking = lifecycle.CanSeeCosts ? await CostRankingAsync(ct) : null;
        var movementTypes = TireOperationsService.TireEventTypes;
        var recent = await db.OperationalEvents
            .Where(e => e.TireId != null && movementTypes.Contains(e.Type))
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Take(ListSize)
            .Select(e => new TireMovementItem(e.Id, e.Type, e.OccurredAt, e.Summary, e.TireId)).ToListAsync(ct);

        return new TireDashboardResponse(status, attention, abnormal, ranking, recent, await UpcomingInspectionsAsync(settings, ct),
            lifecycle.CanSeeCosts, settings.MinTreadDepthMm, settings.TreadWarningDepthMm, settings.InspectionIntervalDays);
    }

    /// <summary>Tires whose last inspection recorded damage/irregular wear, or with an unreviewed wear anomaly.</summary>
    private async Task<IReadOnlyList<TireDashboardItem>> AbnormalWearAsync(TireSettings settings, CancellationToken ct)
    {
        var flagged = tires.WhereAlert(db.Tires, TireAlertFilter.DamageOrUnevenWear, settings).Select(t => t.Id)
            .Concat(db.TireAnomalies.Where(a => a.ReviewedAt == null && a.Type == TireAnomalyType.RapidTreadLoss).Select(a => a.TireId));
        var rows = await db.Tires.Include(t => t.Model).Where(t => t.Status != TireStatus.Disposed && flagged.Contains(t.Id))
            .OrderByDescending(t => t.LastInspectedAt).Take(ListSize).ToListAsync(ct);
        var items = await tires.ToListItemsAsync(rows, settings, ct);
        var rapid = (await db.TireAnomalies.Where(a => a.ReviewedAt == null && a.Type == TireAnomalyType.RapidTreadLoss)
            .Select(a => a.TireId).ToListAsync(ct)).ToHashSet();
        return items.Select(i =>
        {
            var tire = rows.Single(r => r.Id == i.Id);
            var detail = tire.LastInspectionHasDamage ? "Dano registrado na última inspeção"
                : tire.LastWearPattern is { } w && w is not (TireWearPattern.Normal or TireWearPattern.Unknown) ? "Desgaste irregular registrado"
                : rapid.Contains(i.Id) ? "Perda de sulco acima da referência" : "Requer revisão";
            return new TireDashboardItem(i.Id, i.Code, i.Brand, i.ModelName, i.Size, i.Status, i.Location?.AssetLabel, i.Location?.PositionLabel,
                i.CurrentTreadDepthMm, detail);
        }).ToList();
    }

    /// <summary>Highest cost/km among tires in use with enough measured km (seção 25: no misleading figures).</summary>
    private async Task<IReadOnlyList<TireCostRankingItem>> CostRankingAsync(CancellationToken ct)
    {
        var candidates = await db.Tires.Where(t => t.Status != TireStatus.Disposed && !t.HasUnmeasuredDistance)
            .Select(t => new { t.Id, t.Code, t.Model.Brand, t.Model.Name, t.Model.Size, t.PurchasePrice, t.AccumulatedKm }).ToListAsync(ct);
        var running = (await db.TireInstallations.Where(i => i.RemovedAt == null && i.VehicleId != null && !i.IsSpare)
                .Select(i => new { i.TireId, i.InstalledOdometerKm, i.Vehicle!.CurrentOdometerKm }).ToListAsync(ct))
            .ToDictionary(x => x.TireId, x => TireMileage.StintDistance(false, x.InstalledOdometerKm, x.CurrentOdometerKm) ?? 0);
        var costs = await db.TireCosts.GroupBy(c => c.TireId).Select(g => new { g.Key, Total = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return candidates
            .Select(t =>
            {
                var km = t.AccumulatedKm + running.GetValueOrDefault(t.Id);
                var total = (t.PurchasePrice ?? 0) + costs.GetValueOrDefault(t.Id);
                return (t, km, total, perKm: TireCostPolicy.CostPerKm(total, km, false));
            })
            .Where(x => x.perKm is not null)
            .OrderByDescending(x => x.perKm).Take(5)
            .Select(x => new TireCostRankingItem(x.t.Id, x.t.Code, x.t.Brand, x.t.Name, x.t.Size, x.total, x.km, x.perKm!.Value)).ToList();
    }

    /// <summary>Installed tires due for inspection in the next 7 days, overdue ones first.</summary>
    private async Task<IReadOnlyList<TireUpcomingInspection>> UpcomingInspectionsAsync(TireSettings settings, CancellationToken ct)
    {
        if (settings.InspectionIntervalDays <= 0) return [];
        var horizon = clock.UtcNow.AddDays(7 - settings.InspectionIntervalDays);
        var due = await db.Tires.Where(t => t.Status == TireStatus.Installed && t.InspectionReferenceAt != null && t.InspectionReferenceAt < horizon)
            .OrderBy(t => t.InspectionReferenceAt).Take(ListSize)
            .Select(t => new { t.Id, t.Code, t.InspectionReferenceAt }).ToListAsync(ct);
        var ids = due.Select(d => d.Id).ToList();
        var where = await db.TireInstallations.Where(i => ids.Contains(i.TireId) && i.RemovedAt == null)
            .Select(i => new { i.TireId, i.PositionLabel, Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement!.LicensePlate })
            .ToDictionaryAsync(x => x.TireId, ct);
        return due.Select(d =>
        {
            var dueAt = d.InspectionReferenceAt!.Value.AddDays(settings.InspectionIntervalDays);
            var place = where.GetValueOrDefault(d.Id);
            return new TireUpcomingInspection(d.Id, d.Code, place is null ? null : LicensePlate.Format(place.Plate), place?.PositionLabel, dueAt,
                dueAt < clock.UtcNow);
        }).ToList();
    }

    // ---------- reports (seção 45) ----------

    public async Task<TireReportPage<TireListItemResponse>> InventoryAsync(TireReportRequest request, CancellationToken ct)
    {
        await reportValidator.ValidateAndThrowAsync(request, ct);
        var list = await tires.ListAsync(new TireListRequest
        {
            Status = request.Status, Brand = request.Brand, Size = request.Size, VehicleId = request.VehicleId, ImplementId = request.ImplementId,
            Search = request.Search, Page = request.Page, PageSize = request.PageSize, SortBy = request.SortBy, SortDirection = request.SortDirection,
        }, ct);
        return new TireReportPage<TireListItemResponse>(null, null, lifecycle.CanSeeCosts, list);
    }

    /// <summary>Stints overlapping the period, with the tire's lifecycle figures (cost columns need tires.viewcosts).</summary>
    public async Task<TireReportPage<TireLifecycleReportRow>> LifecycleAsync(TireReportRequest request, CancellationToken ct)
    {
        await reportValidator.ValidateAndThrowAsync(request, ct);
        var query = db.TireInstallations.AsQueryable();
        if (request.From is { } from)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            query = query.Where(i => i.RemovedAt == null || i.RemovedAt >= start);
        }
        if (request.To is { } to)
        {
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            query = query.Where(i => i.InstalledAt < end);
        }
        if (request.VehicleId is { } vid) query = query.Where(i => i.VehicleId == vid);
        if (request.ImplementId is { } iid) query = query.Where(i => i.ImplementId == iid);
        if (request.Status is { } status) query = query.Where(i => i.Tire.Status == status);
        if (request.Brand.TrimToNull() is { } brand) query = query.Where(i => i.Tire.Model.Brand == brand);
        if (request.Size.TrimToNull() is { } size)
        {
            var normalized = TireSizes.Normalize(size);
            query = query.Where(i => i.Tire.Model.Size.Contains(normalized));
        }
        if (request.SearchTerm is { } term)
        {
            var upper = term.ToUpperInvariant();
            query = query.Where(i => i.Tire.Code.Contains(upper));
        }
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "code" => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(i => i.Tire.Code) : query.OrderBy(i => i.Tire.Code),
            "distance" => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(i => i.DistanceKm) : query.OrderBy(i => i.DistanceKm),
            _ => request.SortDirection == SortDirection.Asc ? query.OrderBy(i => i.InstalledAt) : query.OrderByDescending(i => i.InstalledAt),
        };
        var page = await ordered.ThenBy(i => i.Id).ToPagedResultAsync(request, i => new
        {
            Stint = i,
            i.Tire.Code, i.Tire.Model.Brand, ModelName = i.Tire.Model.Name, i.Tire.Model.Size, i.Tire.AccumulatedKm, i.Tire.HasUnmeasuredDistance,
            i.Tire.RepairCount, i.Tire.RetreadCount, i.Tire.PurchasePrice,
            Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement!.LicensePlate,
            VehicleKm = i.Vehicle != null ? (int?)i.Vehicle.CurrentOdometerKm : null,
        }, ct);
        var tireIds = page.Items.Select(x => x.Stint.TireId).Distinct().ToList();
        var openRunning = (await db.TireInstallations.Where(i => tireIds.Contains(i.TireId) && i.RemovedAt == null && i.VehicleId != null && !i.IsSpare)
                .Select(i => new { i.TireId, i.InstalledOdometerKm, i.Vehicle!.CurrentOdometerKm }).ToListAsync(ct))
            .ToDictionary(x => x.TireId, x => TireMileage.StintDistance(false, x.InstalledOdometerKm, x.CurrentOdometerKm) ?? 0);
        var costs = lifecycle.CanSeeCosts
            ? await db.TireCosts.Where(c => tireIds.Contains(c.TireId)).GroupBy(c => c.TireId)
                .Select(g => new { g.Key, Total = g.Sum(c => c.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct)
            : [];

        var rows = page.Items.Select(x =>
        {
            var tireKm = x.AccumulatedKm + openRunning.GetValueOrDefault(x.Stint.TireId);
            var distance = x.Stint.IsOpen ? TireMileage.StintDistance(x.Stint.IsSpare, x.Stint.InstalledOdometerKm, x.VehicleKm) : x.Stint.DistanceKm;
            decimal? total = lifecycle.CanSeeCosts ? (x.PurchasePrice ?? 0) + costs.GetValueOrDefault(x.Stint.TireId) : null;
            return new TireLifecycleReportRow(x.Stint.Id, x.Stint.TireId, x.Code, x.Brand, x.ModelName, x.Size, LicensePlate.Format(x.Plate),
                x.Stint.PositionLabel, x.Stint.InstalledAt, x.Stint.RemovedAt, x.Stint.RemovalReason, distance, tireKm, x.RepairCount, x.RetreadCount,
                total, total is { } t ? TireCostPolicy.CostPerKm(t, tireKm, x.HasUnmeasuredDistance) : null);
        }).ToList();
        return new TireReportPage<TireLifecycleReportRow>(request.From, request.To, lifecycle.CanSeeCosts,
            new PagedResult<TireLifecycleReportRow>(rows, page.Page, page.PageSize, page.TotalCount));
    }

    public async Task<TireReportPage<TireInspectionReportRow>> InspectionsAsync(TireReportRequest request, CancellationToken ct)
    {
        await reportValidator.ValidateAndThrowAsync(request, ct);
        var query = db.TireInspections.AsQueryable();
        if (request.From is { } from)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            query = query.Where(i => i.InspectedAt >= start);
        }
        if (request.To is { } to)
        {
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            query = query.Where(i => i.InspectedAt < end);
        }
        if (request.VehicleId is { } vid) query = query.Where(i => i.VehicleId == vid);
        if (request.ImplementId is { } iid) query = query.Where(i => i.ImplementId == iid);
        if (request.Condition is { } condition) query = query.Where(i => i.Condition == condition);
        if (request.RequiresAction == true)
            query = query.Where(i => i.Condition != TireCondition.Good || i.Damages.Any() ||
                                     (i.WearPattern != TireWearPattern.Normal && i.WearPattern != TireWearPattern.Unknown));
        if (request.Brand.TrimToNull() is { } brand) query = query.Where(i => i.Tire.Model.Brand == brand);
        if (request.SearchTerm is { } term)
        {
            var upper = term.ToUpperInvariant();
            query = query.Where(i => i.Tire.Code.Contains(upper));
        }
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "code" => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(i => i.Tire.Code) : query.OrderBy(i => i.Tire.Code),
            "tread" => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(i => i.TreadDepthMm) : query.OrderBy(i => i.TreadDepthMm),
            _ => request.SortDirection == SortDirection.Asc ? query.OrderBy(i => i.InspectedAt) : query.OrderByDescending(i => i.InspectedAt),
        };
        var page = await ordered.ThenBy(i => i.Id).ToPagedResultAsync(request, i => new
        {
            i.Id, i.TireId, i.Tire.Code, Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement != null ? i.Implement.LicensePlate : null,
            i.PositionLabel, i.InspectedAt, i.Source, i.TreadDepthMm, i.Pressure, i.PressureUnit, i.PressureCheck, i.Condition, i.WearPattern,
            DamageCount = i.Damages.Count,
        }, ct);
        var rows = page.Items.Select(x => new TireInspectionReportRow(x.Id, x.TireId, x.Code, x.Plate is null ? null : LicensePlate.Format(x.Plate),
            x.PositionLabel, x.InspectedAt, x.Source, x.TreadDepthMm, x.Pressure, x.PressureUnit, x.PressureCheck, x.Condition, x.WearPattern,
            x.DamageCount)).ToList();
        return new TireReportPage<TireInspectionReportRow>(request.From, request.To, lifecycle.CanSeeCosts,
            new PagedResult<TireInspectionReportRow>(rows, page.Page, page.PageSize, page.TotalCount));
    }

    /// <summary>Lifecycle cost per tire (all-time). The period filters tires purchased in it. Sorting is by plain columns.</summary>
    public async Task<TireReportPage<TireCostReportRow>> CostsAsync(TireReportRequest request, CancellationToken ct)
    {
        await reportValidator.ValidateAndThrowAsync(request, ct);
        var query = db.Tires.AsQueryable();
        if (request.From is { } from) query = query.Where(t => t.PurchasedOn >= from);
        if (request.To is { } to) query = query.Where(t => t.PurchasedOn <= to);
        if (request.Status is { } status) query = query.Where(t => t.Status == status);
        if (request.Brand.TrimToNull() is { } brand) query = query.Where(t => t.Model.Brand == brand);
        if (request.Size.TrimToNull() is { } size)
        {
            var normalized = TireSizes.Normalize(size);
            query = query.Where(t => t.Model.Size.Contains(normalized));
        }
        if (request.VehicleId is { } vid)
            query = query.Where(t => db.TireInstallations.Any(i => i.TireId == t.Id && i.VehicleId == vid));
        if (request.SearchTerm is { } term)
        {
            var upper = term.ToUpperInvariant();
            query = query.Where(t => t.Code.Contains(upper));
        }
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "purchase" => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(t => t.PurchasePrice) : query.OrderBy(t => t.PurchasePrice),
            "km" => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(t => t.AccumulatedKm) : query.OrderBy(t => t.AccumulatedKm),
            "retreads" => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(t => t.RetreadCount) : query.OrderBy(t => t.RetreadCount),
            _ => request.SortDirection == SortDirection.Desc ? query.OrderByDescending(t => t.Code) : query.OrderBy(t => t.Code),
        };
        var page = await ordered.ThenBy(t => t.Id).ToPagedResultAsync(request, t => new
        {
            t.Id, t.Code, t.Model.Brand, t.Model.Name, t.Model.Size, t.Status, t.PurchasePrice, t.AccumulatedKm, t.HasUnmeasuredDistance,
        }, ct);
        var ids = page.Items.Select(x => x.Id).ToList();
        var byType = await db.TireCosts.Where(c => ids.Contains(c.TireId)).GroupBy(c => new { c.TireId, c.Type })
            .Select(g => new { g.Key.TireId, g.Key.Type, Total = g.Sum(c => c.Amount) }).ToListAsync(ct);
        var running = (await db.TireInstallations.Where(i => ids.Contains(i.TireId) && i.RemovedAt == null && i.VehicleId != null && !i.IsSpare)
                .Select(i => new { i.TireId, i.InstalledOdometerKm, i.Vehicle!.CurrentOdometerKm }).ToListAsync(ct))
            .ToDictionary(x => x.TireId, x => TireMileage.StintDistance(false, x.InstalledOdometerKm, x.CurrentOdometerKm) ?? 0);
        decimal Sum(Guid id, params TireCostType[] types) => byType.Where(c => c.TireId == id && types.Contains(c.Type)).Sum(c => c.Total);

        var rows = page.Items.Select(x =>
        {
            var repairs = Sum(x.Id, TireCostType.Repair);
            var retreads = Sum(x.Id, TireCostType.Retread);
            var other = Sum(x.Id, TireCostType.Installation, TireCostType.Other);
            var total = (x.PurchasePrice ?? 0) + repairs + retreads + other;
            var km = x.AccumulatedKm + running.GetValueOrDefault(x.Id);
            return new TireCostReportRow(x.Id, x.Code, x.Brand, x.Name, x.Size, x.Status, x.PurchasePrice, repairs, retreads, other, total, km,
                x.HasUnmeasuredDistance, TireCostPolicy.CostPerKm(total, km, x.HasUnmeasuredDistance));
        }).ToList();
        return new TireReportPage<TireCostReportRow>(request.From, request.To, true,
            new PagedResult<TireCostReportRow>(rows, page.Page, page.PageSize, page.TotalCount));
    }
}
