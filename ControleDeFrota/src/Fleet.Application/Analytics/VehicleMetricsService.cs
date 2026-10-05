using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Domain.Authorization;
using Fleet.Domain.Finance;
using Fleet.Domain.Fuel;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Analytics;

/// <summary>
/// Cross-module figures of one vehicle in a period. A null field = the reader cannot see that module (or there is no
/// reliable data) — never a misleading zero. Money follows ADR-042: each slice needs its own *.viewcosts AND finance.viewcosts.
/// </summary>
public sealed record VehicleMetrics(
    Guid VehicleId, string LicensePlate, string Model, VehicleType Type, VehicleStatus Status,
    int? KmDriven, bool KmReliable,
    decimal? FuelQuantity, decimal? AverageConsumption,
    decimal? FuelCost, decimal? MaintenanceCost, decimal? TireCost, decimal? OtherCost, decimal? TotalCost, decimal? CostPerKm,
    int? WorkOrdersCompleted, int? CorrectiveWorkOrders, decimal? DowntimeHours, int? TireReplacements,
    bool IsPartial);

/// <summary>
/// One place that computes per-vehicle cross-module metrics, aggregated in the database (ADR-033/040): reports,
/// comparison, benchmarks and insights all read from here, so the same number is the same everywhere.
/// </summary>
public sealed class VehicleMetricsService(IFleetDbContext db, IClock clock, ICurrentUser currentUser, CostAggregationService costs)
{
    private bool Can(string permission) => currentUser.HasPermission(permission);

    public async Task<IReadOnlyList<VehicleMetrics>> ForVehiclesAsync(IQueryable<Vehicle> vehicles, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var list = await vehicles.Select(v => new { v.Id, v.LicensePlate, v.Model, v.Type, v.Status }).ToListAsync(ct);
        if (list.Count == 0) return [];
        var ids = list.Select(v => v.Id).ToList();
        var start = clock.StartOfBusinessDayUtc(from);
        var end = clock.StartOfBusinessDayUtc(to.AddDays(1));

        var km = await KmAsync(ids, start, end, ct);
        var fuel = Can(Permissions.Fuel.View) ? await FuelAsync(ids, from, to, ct) : null;
        var maintenance = Can(Permissions.Maintenance.View) ? await MaintenanceAsync(ids, start, end, ct) : null;
        var tireChanges = Can(Permissions.Tires.View) ? await TireChangesAsync(ids, start, end, ct) : null;

        var canMoney = Can(Permissions.Finance.ViewCosts);
        var money = canMoney ? await costs.GetVehicleCostsAsync(from, to, ct) : null;
        var canFuelMoney = canMoney && Can(Permissions.Fuel.ViewCosts);
        var canMaintenanceMoney = canMoney && Can(Permissions.Maintenance.ViewCosts);
        var canTireMoney = canMoney && Can(Permissions.Tires.ViewCosts);
        var isPartial = !(canFuelMoney && canMaintenanceMoney && canTireMoney);

        return list.Select(v =>
        {
            var k = km.GetValueOrDefault(v.Id);
            var f = fuel?.GetValueOrDefault(v.Id);
            var m = maintenance?.GetValueOrDefault(v.Id);
            var c = money?.GetValueOrDefault(v.Id);
            decimal? total = canMoney ? c?.TotalCost ?? 0m : null;
            return new VehicleMetrics(
                v.Id, v.LicensePlate, v.Model, v.Type, v.Status,
                k?.Km, k?.Reliable ?? false,
                fuel is null ? null : f?.Quantity ?? 0m, f?.Average,
                canFuelMoney ? c?.FuelCost ?? 0m : null, canMaintenanceMoney ? c?.MaintenanceCost ?? 0m : null,
                canTireMoney ? c?.TireCost ?? 0m : null, canMoney ? c?.OtherCost ?? 0m : null,
                total,
                // Cost/km only on a reliable distance and a complete total (ADR-044): otherwise null, never a guess.
                total is { } t && !isPartial && k is { Reliable: true } ? VehicleCostPolicy.CostPerKm(t, k.Km) : null,
                maintenance is null ? null : m?.Completed ?? 0, maintenance is null ? null : m?.Corrective ?? 0,
                maintenance is null ? null : Math.Round((m?.DowntimeMinutes ?? 0) / 60m, 1),
                tireChanges is null ? null : tireChanges.GetValueOrDefault(v.Id),
                canMoney && isPartial);
        }).ToList();
    }

    private sealed record KmInfo(int Km, bool Reliable);

    /// <summary>
    /// Km of the period = highest valid reading up to the end minus the highest valid reading before the start.
    /// Without a reading before the period the distance is only from the first reading inside it, so it is flagged
    /// as not reliable (cost/km is then withheld).
    /// </summary>
    private async Task<Dictionary<Guid, KmInfo>> KmAsync(List<Guid> ids, DateTime start, DateTime end, CancellationToken ct)
    {
        var valid = db.OdometerReadings.Where(r => r.Status == OdometerReadingStatus.Valid && ids.Contains(r.VehicleId));
        var inPeriod = await valid.Where(r => r.ReadAt >= start && r.ReadAt < end)
            .GroupBy(r => r.VehicleId).Select(g => new { g.Key, Max = g.Max(r => r.OdometerKm), Min = g.Min(r => r.OdometerKm) })
            .ToListAsync(ct);
        var before = await valid.Where(r => r.ReadAt < start)
            .GroupBy(r => r.VehicleId).Select(g => new { g.Key, Max = g.Max(r => r.OdometerKm) })
            .ToDictionaryAsync(g => g.Key, g => g.Max, ct);
        return inPeriod.ToDictionary(p => p.Key, p =>
        {
            var hasBase = before.TryGetValue(p.Key, out var baseKm);
            var km = Math.Max(0, p.Max - (hasBase ? baseKm : p.Min));
            return new KmInfo(km, hasBase && km >= VehicleCostPolicy.MinKmForCostPerKm);
        });
    }

    private sealed record FuelInfo(decimal Quantity, decimal? Average);

    private async Task<Dictionary<Guid, FuelInfo>> FuelAsync(List<Guid> ids, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fuelings = db.Fuelings.Where(f => ids.Contains(f.VehicleId) && f.Status != FuelingStatus.Cancelled && f.FueledOn >= from && f.FueledOn <= to);
        var quantities = await fuelings.GroupBy(f => f.VehicleId)
            .Select(g => new { g.Key, Quantity = g.Sum(f => f.Quantity) }).ToDictionaryAsync(g => g.Key, g => g.Quantity, ct);
        // Consumption only from measured segments in liters (km/l); sums of columns, ratio in memory (ADR-033).
        var segments = await fuelings.Where(f => f.ConsumptionResult == ConsumptionResult.Calculated && f.FuelType.Unit == FuelUnit.Liter)
            .GroupBy(f => f.VehicleId)
            .Select(g => new { g.Key, Km = g.Sum(f => f.SegmentDistanceKm), Quantity = g.Sum(f => f.SegmentQuantity), Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, ct);
        return quantities.ToDictionary(q => q.Key, q =>
        {
            var s = segments.GetValueOrDefault(q.Key);
            var sample = s is null ? null : new ConsumptionSample(s.Km ?? 0, s.Quantity ?? 0m, s.Count);
            return new FuelInfo(q.Value, sample?.Average);
        });
    }

    private sealed record MaintenanceInfo(int Completed, int Corrective, int DowntimeMinutes);

    private async Task<Dictionary<Guid, MaintenanceInfo>> MaintenanceAsync(List<Guid> ids, DateTime start, DateTime end, CancellationToken ct) =>
        await db.WorkOrders
            .Where(w => ids.Contains(w.VehicleId) && w.Status == WorkOrderStatus.Completed && w.CompletedAt >= start && w.CompletedAt < end)
            .GroupBy(w => w.VehicleId)
            .Select(g => new
            {
                g.Key, Completed = g.Count(), Corrective = g.Count(w => w.Type == MaintenanceType.Corrective),
                Downtime = g.Sum(w => w.DowntimeMinutes) ?? 0,
            })
            .ToDictionaryAsync(g => g.Key, g => new MaintenanceInfo(g.Completed, g.Corrective, g.Downtime), ct);

    /// <summary>Tires taken off the vehicle in the period (spares excluded) — the "replacement frequency" of spec §12.</summary>
    private async Task<Dictionary<Guid, int>> TireChangesAsync(List<Guid> ids, DateTime start, DateTime end, CancellationToken ct) =>
        await db.TireInstallations
            .Where(i => i.VehicleId != null && ids.Contains(i.VehicleId.Value) && !i.IsSpare && i.RemovedAt >= start && i.RemovedAt < end)
            .GroupBy(i => i.VehicleId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
}
