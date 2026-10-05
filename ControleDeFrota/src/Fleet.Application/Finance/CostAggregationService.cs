using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Finance;
using Fleet.Domain.Fuel;
using Fleet.Domain.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Finance;

public sealed record VehicleCostBreakdown(
    Guid VehicleId, decimal FuelCost, decimal MaintenanceCost, decimal TireCost, decimal OtherCost, decimal TotalCost, bool IsPartial);

public sealed record CategoryCost(Guid? ExpenseCategoryId, string CategoryName, decimal Amount);

public sealed record MonthlyCost(int Year, int Month, decimal Amount);

public sealed record CostCenterCost(Guid? CostCenterId, string CostCenterName, decimal Amount);

/// <summary>A list plus whether permission gaps hid part of it — every blended report carries this (ADR-040).</summary>
public sealed record PartialList<T>(IReadOnlyList<T> Items, bool IsPartial);

/// <summary>
/// Reads cost data straight from Fuel/Maintenance/Tires — never duplicates it into Expense (ADR-040, spec §6).
/// Every method respects each source's OWN *.viewcosts permission, zeroing the slices the caller can't see and
/// reporting <see cref="VehicleCostBreakdown.IsPartial"/> so a blended total is never silently wrong.
/// </summary>
public sealed class CostAggregationService(IFleetDbContext db, ICurrentUser currentUser, IClock clock)
{
    private bool CanSeeFuel => currentUser.HasPermission(Permissions.Fuel.ViewCosts) && currentUser.HasPermission(Permissions.Finance.ViewCosts);
    private bool CanSeeMaintenance => currentUser.HasPermission(Permissions.Maintenance.ViewCosts) && currentUser.HasPermission(Permissions.Finance.ViewCosts);
    private bool CanSeeTires => currentUser.HasPermission(Permissions.Tires.ViewCosts) && currentUser.HasPermission(Permissions.Finance.ViewCosts);
    private bool CanSeeOther => currentUser.HasPermission(Permissions.Finance.ViewCosts);
    private bool IsPartial => !CanSeeFuel || !CanSeeMaintenance || !CanSeeTires || !CanSeeOther;

    /// <summary>Per-vehicle totals for the period, one row per vehicle that had any cost (used by ranking/dashboard).</summary>
    public async Task<IReadOnlyDictionary<Guid, VehicleCostBreakdown>> GetVehicleCostsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var empty = new Dictionary<Guid, decimal>();
        var fuel = CanSeeFuel ? await FuelByVehicleAsync(from, to, null, ct) : empty;
        var maintenance = CanSeeMaintenance ? await MaintenanceByVehicleAsync(from, to, null, ct) : empty;
        var tires = CanSeeTires ? await TiresByVehicleAsync(from, to, null, ct) : empty;
        var other = CanSeeOther ? await ManualByVehicleAsync(from, to, null, ct) : empty;
        var isPartial = IsPartial;

        var vehicleIds = fuel.Keys.Union(maintenance.Keys).Union(tires.Keys).Union(other.Keys);
        var result = new Dictionary<Guid, VehicleCostBreakdown>();
        foreach (var vehicleId in vehicleIds)
        {
            var f = fuel.GetValueOrDefault(vehicleId);
            var m = maintenance.GetValueOrDefault(vehicleId);
            var t = tires.GetValueOrDefault(vehicleId);
            var o = other.GetValueOrDefault(vehicleId);
            result[vehicleId] = new VehicleCostBreakdown(vehicleId, f, m, t, o, f + m + t + o, isPartial);
        }
        return result;
    }

    /// <summary>Breakdown for a single vehicle (vehicle hub "Financeiro" tab).</summary>
    public async Task<VehicleCostBreakdown> GetVehicleCostBreakdownAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fuel = CanSeeFuel ? (await FuelByVehicleAsync(from, to, vehicleId, ct)).GetValueOrDefault(vehicleId) : 0m;
        var maintenance = CanSeeMaintenance ? (await MaintenanceByVehicleAsync(from, to, vehicleId, ct)).GetValueOrDefault(vehicleId) : 0m;
        var tires = CanSeeTires ? (await TiresByVehicleAsync(from, to, vehicleId, ct)).GetValueOrDefault(vehicleId) : 0m;
        var other = CanSeeOther ? (await ManualByVehicleAsync(from, to, vehicleId, ct)).GetValueOrDefault(vehicleId) : 0m;
        var isPartial = IsPartial;
        return new VehicleCostBreakdown(vehicleId, fuel, maintenance, tires, other, fuel + maintenance + tires + other, isPartial);
    }

    /// <summary>Fleet total by category — system categories blended in, manual categories kept distinct (dashboard "Cost by category" chart).</summary>
    public async Task<PartialList<CategoryCost>> GetFleetCostByCategoryAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = new List<CategoryCost>();
        var isPartial = IsPartial;

        if (CanSeeFuel)
        {
            var total = await db.Fuelings.Where(f => f.Status != FuelingStatus.Cancelled && f.FueledOn >= from && f.FueledOn <= to)
                .SumAsync(f => f.TotalAmount, ct);
            if (total > 0) result.Add(new CategoryCost(null, "Combustível", total));
        }
        if (CanSeeMaintenance)
        {
            var total = await WorkOrderCostQuery(from, to, null).SumAsync(w => w.TotalCost, ct);
            if (total > 0) result.Add(new CategoryCost(null, "Manutenção", total));
        }
        if (CanSeeTires)
        {
            var total = await db.TireCosts.Where(c => c.IncurredOn >= from && c.IncurredOn <= to).SumAsync(c => c.Amount, ct);
            if (total > 0) result.Add(new CategoryCost(null, "Pneus", total));
        }
        if (CanSeeOther)
        {
            var manual = await ManualExpenseQuery(from, to, null, null)
                .GroupBy(e => new { e.ExpenseCategoryId, e.ExpenseCategory.Name })
                .Select(g => new CategoryCost(g.Key.ExpenseCategoryId, g.Key.Name, g.Sum(e => e.Amount)))
                .ToListAsync(ct);
            result.AddRange(manual);
        }
        return new PartialList<CategoryCost>(result.OrderByDescending(c => c.Amount).ToList(), isPartial);
    }

    /// <summary>Blended monthly totals for a column chart (vehicle optional = whole fleet).</summary>
    public async Task<PartialList<MonthlyCost>> GetMonthlyCostEvolutionAsync(
        DateOnly from, DateOnly to, Guid? vehicleId, CancellationToken ct)
    {
        var byMonth = new SortedDictionary<(int, int), decimal>();
        void Add(IEnumerable<(int Year, int Month, decimal Amount)> rows)
        {
            foreach (var (year, month, amount) in rows)
                byMonth[(year, month)] = byMonth.GetValueOrDefault((year, month)) + amount;
        }
        var isPartial = IsPartial;

        if (CanSeeFuel)
        {
            var rows = await FuelQuery(from, to, vehicleId)
                .GroupBy(f => new { f.FueledOn.Year, f.FueledOn.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(f => f.TotalAmount) }).ToListAsync(ct);
            Add(rows.Select(r => (r.Year, r.Month, r.Amount)));
        }
        if (CanSeeMaintenance)
        {
            var rows = await WorkOrderCostQuery(from, to, vehicleId)
                .GroupBy(w => new { w.OpenedAt.Year, w.OpenedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(w => w.TotalCost) }).ToListAsync(ct);
            Add(rows.Select(r => (r.Year, r.Month, r.Amount)));
        }
        if (CanSeeTires)
        {
            // Resolved in memory per vehicle (see TiresByVehicleAsync), then grouped by month here — the per-source
            // dictionary already has one entry per vehicle, so this re-reads the raw costs once more, grouped by month instead.
            var byVehicle = await TireCostsWithMonthAsync(from, to, vehicleId, ct);
            Add(byVehicle.GroupBy(x => (x.Year, x.Month)).Select(g => (g.Key.Year, g.Key.Month, g.Sum(x => x.Amount))));
        }
        if (CanSeeOther)
        {
            var rows = await ManualExpenseQuery(from, to, vehicleId, null)
                .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(e => e.Amount) }).ToListAsync(ct);
            Add(rows.Select(r => (r.Year, r.Month, r.Amount)));
        }
        return new PartialList<MonthlyCost>(byMonth.Select(kv => new MonthlyCost(kv.Key.Item1, kv.Key.Item2, kv.Value)).ToList(), isPartial);
    }

    // ---------- per-source queries (each sums IN THE DATABASE, grouped by vehicle — ADR-033/skill "agregue no banco") ----------

    private IQueryable<Fueling> FuelQuery(DateOnly from, DateOnly to, Guid? vehicleId)
    {
        var q = db.Fuelings.Where(f => f.Status != FuelingStatus.Cancelled && f.FueledOn >= from && f.FueledOn <= to);
        return vehicleId is { } id ? q.Where(f => f.VehicleId == id) : q;
    }

    private async Task<Dictionary<Guid, decimal>> FuelByVehicleAsync(DateOnly from, DateOnly to, Guid? vehicleId, CancellationToken ct) =>
        await FuelQuery(from, to, vehicleId).GroupBy(f => f.VehicleId)
            .Select(g => new { VehicleId = g.Key, Amount = g.Sum(f => f.TotalAmount) })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Amount, ct);

    private static readonly WorkOrderStatus[] NonCountingWorkOrderStatuses = [WorkOrderStatus.Cancelled, WorkOrderStatus.Rejected];

    /// <summary>
    /// Plain DateTime comparison against UTC day boundaries (same trick as IClock.StartOfBusinessDayUtc elsewhere) —
    /// avoids relying on DateOnly/DateTime translation support, which EF Core 8 does not guarantee on every provider.
    /// </summary>
    private IQueryable<WorkOrder> WorkOrderCostQuery(DateOnly from, DateOnly to, Guid? vehicleId)
    {
        var fromUtc = clock.StartOfBusinessDayUtc(from);
        var toUtcExclusive = clock.StartOfBusinessDayUtc(to.AddDays(1));
        var q = db.WorkOrders.Where(w => !NonCountingWorkOrderStatuses.Contains(w.Status) && w.OpenedAt >= fromUtc && w.OpenedAt < toUtcExclusive);
        return vehicleId is { } id ? q.Where(w => w.VehicleId == id) : q;
    }

    private async Task<Dictionary<Guid, decimal>> MaintenanceByVehicleAsync(DateOnly from, DateOnly to, Guid? vehicleId, CancellationToken ct) =>
        await WorkOrderCostQuery(from, to, vehicleId).GroupBy(w => w.VehicleId)
            .Select(g => new { VehicleId = g.Key, Amount = g.Sum(w => w.TotalCost) })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Amount, ct);

    /// <summary>
    /// Tire costs are resolved to "which vehicle had the tire installed on that date" in memory, not pure SQL
    /// (ADR-040): EF Core 8 does not reliably translate a DateOnly/DateTime correlation across providers, and the
    /// volume of tire cost records (repairs/retreads/purchases) is small enough that this stays proportionate —
    /// unlike Fuel/Maintenance, which are summed purely in SQL because they can be high-frequency.
    /// </summary>
    private async Task<IReadOnlyList<(Guid VehicleId, int Year, int Month, decimal Amount)>> TireCostsWithMonthAsync(
        DateOnly from, DateOnly to, Guid? vehicleId, CancellationToken ct)
    {
        var costs = await db.TireCosts.Where(c => c.IncurredOn >= from && c.IncurredOn <= to)
            .Select(c => new { c.TireId, c.Amount, c.IncurredOn }).ToListAsync(ct);
        if (costs.Count == 0) return [];
        var tireIds = costs.Select(c => c.TireId).Distinct().ToList();
        var installations = await db.TireInstallations.Where(i => tireIds.Contains(i.TireId) && i.VehicleId != null)
            .Select(i => new { i.TireId, i.VehicleId, i.InstalledAt, i.RemovedAt }).ToListAsync(ct);

        var result = new List<(Guid, int, int, decimal)>();
        foreach (var cost in costs)
        {
            var installation = installations.Where(i => i.TireId == cost.TireId &&
                DateOnly.FromDateTime(i.InstalledAt) <= cost.IncurredOn &&
                (i.RemovedAt == null || DateOnly.FromDateTime(i.RemovedAt.Value) >= cost.IncurredOn))
                .OrderByDescending(i => i.InstalledAt).FirstOrDefault();
            if (installation?.VehicleId is not { } vid) continue;
            if (vehicleId is { } filter && vid != filter) continue;
            result.Add((vid, cost.IncurredOn.Year, cost.IncurredOn.Month, cost.Amount));
        }
        return result;
    }

    private async Task<Dictionary<Guid, decimal>> TiresByVehicleAsync(DateOnly from, DateOnly to, Guid? vehicleId, CancellationToken ct) =>
        (await TireCostsWithMonthAsync(from, to, vehicleId, ct))
        .GroupBy(x => x.VehicleId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

    private IQueryable<Expense> ManualExpenseQuery(DateOnly from, DateOnly to, Guid? vehicleId, Guid? costCenterId)
    {
        var q = db.Expenses.Where(e => e.CancelledAt == null && e.ExpenseDate >= from && e.ExpenseDate <= to);
        if (vehicleId is { } id) q = q.Where(e => e.VehicleId == id);
        if (costCenterId is { } cc) q = q.Where(e => e.CostCenterId == cc);
        return q;
    }

    private async Task<Dictionary<Guid, decimal>> ManualByVehicleAsync(DateOnly from, DateOnly to, Guid? vehicleId, CancellationToken ct) =>
        await ManualExpenseQuery(from, to, vehicleId, null).Where(e => e.VehicleId != null).GroupBy(e => e.VehicleId!.Value)
            .Select(g => new { VehicleId = g.Key, Amount = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Amount, ct);

    /// <summary>
    /// By cost center — manual expenses only (Fuel/Maintenance/Tires records have no CostCenterId; a cost center
    /// report is necessarily about the discretionary spend that was actually tagged with one).
    /// </summary>
    public async Task<PartialList<CostCenterCost>> GetCostCenterBreakdownAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!CanSeeOther) return new PartialList<CostCenterCost>([], true);
        var rows = await ManualExpenseQuery(from, to, null, null)
            .GroupBy(e => new { e.CostCenterId, Name = e.CostCenter == null ? "Sem centro de custo" : e.CostCenter.Name })
            .Select(g => new CostCenterCost(g.Key.CostCenterId, g.Key.Name, g.Sum(e => e.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToListAsync(ct);
        return new PartialList<CostCenterCost>(rows, false);
    }
}
