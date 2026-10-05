using Fleet.Application.Common;
using Fleet.Application.Mileage;
using Fleet.Domain.Authorization;
using Fleet.Domain.Finance;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Finance;

public sealed record FinanceDashboardResponse(
    decimal TotalFleetCostYtd, decimal MonthlyCost, decimal? FleetCostPerKm, bool CostPerKmHasSufficientData,
    decimal FuelCost, decimal MaintenanceCost, decimal TireCost, decimal OtherCost,
    int OverdueExpenseCount, decimal? OverdueExpenseAmount, int DueSoonRecurringCount,
    IReadOnlyList<MonthlyCost> MonthlyEvolution, IReadOnlyList<CategoryCost> CostByCategory, bool IsPartial, bool CanSeeCosts);

public sealed record VehicleCostPerKmResponse(
    Guid VehicleId, decimal TotalCost, int? DistanceKm, decimal? CostPerKm, bool HasSufficientData, bool IsPartial);

public sealed record VehicleTcoResponse(
    Guid VehicleId, decimal? AcquisitionValue, DateOnly SinceDate, decimal OperatingCost, decimal TotalCost,
    decimal CostPerMonth, int? DistanceKm, decimal? CostPerKm, bool HasSufficientMileageData,
    decimal FuelCost, decimal MaintenanceCost, decimal TireCost, decimal OtherCost, bool IsPartial);

public sealed class VehicleRankingRequest : ListRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public VehicleType? Type { get; set; }
    public VehicleStatus? Status { get; set; }
}

public sealed record VehicleRankingRow(
    Guid VehicleId, string LicensePlate, string Model, decimal TotalCost, decimal? CostPerKm,
    decimal FuelCost, decimal MaintenanceCost, decimal TireCost, int ExpenseCount);

/// <summary>
/// Vehicle/fleet-level analysis built on CostAggregationService: cost/km, TCO, ranking and the finance dashboard
/// feed (ADR-040). Methodology: cost/km needs >= VehicleCostPolicy.MinKmForCostPerKm reliable km in the period
/// (MileageService's valid-reading history) or it returns null with HasSufficientData=false — never a misleading number.
/// </summary>
public sealed class FinanceAnalyticsService(
    IFleetDbContext db, ICurrentUser currentUser, IClock clock, CostAggregationService costs, MileageService mileage)
{
    private bool CanSeeCosts => currentUser.HasPermission(Permissions.Finance.ViewCosts);

    public async Task<FinanceDashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var yearStart = new DateOnly(today.Year, 1, 1);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var ytd = await costs.GetVehicleCostsAsync(yearStart, today, ct);
        var ytdTotal = ytd.Values.Sum(v => v.TotalCost);
        var monthlyBreakdown = await SumAllAsync(monthStart, today, ct);
        var (evolution, evolutionPartial) = await costs.GetMonthlyCostEvolutionAsync(yearStart.AddYears(-1).AddDays(1), today, null, ct);
        var (byCategory, categoryPartial) = await costs.GetFleetCostByCategoryAsync(yearStart, today, ct);
        var (costPerKm, hasKmData) = await FleetCostPerKmAsync(monthStart, today, ct);

        var overdueQuery = db.Expenses.Where(e => e.CancelledAt == null && e.DueDate != null && e.DueDate < today && e.PaidAmount < e.Amount);
        var overdueCount = await overdueQuery.CountAsync(ct);
        var overdueAmount = CanSeeCosts ? await overdueQuery.SumAsync(e => e.Amount - e.PaidAmount, ct) : (decimal?)null;
        var dueSoonHorizon = today.AddDays(RecurringExpenseGenerationScanner.HorizonDays);
        var dueSoonRecurring = await db.Expenses.CountAsync(e => e.IsRecurring && e.CancelledAt == null &&
            e.DueDate != null && e.DueDate >= today && e.DueDate <= dueSoonHorizon && e.PaidAmount < e.Amount, ct);

        return new FinanceDashboardResponse(
            ytdTotal, monthlyBreakdown.TotalCost, costPerKm, hasKmData,
            monthlyBreakdown.FuelCost, monthlyBreakdown.MaintenanceCost, monthlyBreakdown.TireCost, monthlyBreakdown.OtherCost,
            overdueCount, overdueAmount, dueSoonRecurring,
            evolution, byCategory, evolutionPartial || categoryPartial || monthlyBreakdown.IsPartial, CanSeeCosts);
    }

    public async Task<VehicleCostPerKmResponse> GetVehicleCostPerKmAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var breakdown = await costs.GetVehicleCostBreakdownAsync(vehicleId, from, to, ct);
        var distance = await DistanceInPeriodAsync(vehicleId, from, to, ct);
        var costPerKm = VehicleCostPolicy.CostPerKm(breakdown.TotalCost, distance);
        return new VehicleCostPerKmResponse(vehicleId, breakdown.TotalCost, distance, costPerKm,
            distance is >= VehicleCostPolicy.MinKmForCostPerKm, breakdown.IsPartial);
    }

    /// <summary>
    /// TCO since the vehicle's acquisition date (operational management analysis, not accounting — spec §16).
    /// Falls back to the registration date when there is no acquisition date, and is capped to the valid
    /// mileage history for cost/km the same way the period figure is.
    /// </summary>
    public async Task<VehicleTcoResponse> GetVehicleTcoAsync(Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado.");
        var since = vehicle.AcquisitionDate ?? DateOnly.FromDateTime(vehicle.CreatedAt);
        var today = clock.Today;
        var breakdown = await costs.GetVehicleCostBreakdownAsync(vehicleId, since, today, ct);
        var distance = await DistanceInPeriodAsync(vehicleId, since, today, ct);
        var months = Math.Max(1, ((today.Year - since.Year) * 12) + today.Month - since.Month);
        var acquisitionValue = CanSeeCosts ? vehicle.AcquisitionValue ?? 0m : 0m;
        var totalCost = breakdown.TotalCost + acquisitionValue;

        return new VehicleTcoResponse(vehicleId, CanSeeCosts ? vehicle.AcquisitionValue : null, since, breakdown.TotalCost, totalCost,
            Math.Round(totalCost / months, 2), distance, VehicleCostPolicy.CostPerKm(totalCost, distance),
            distance is >= VehicleCostPolicy.MinKmForCostPerKm, breakdown.FuelCost, breakdown.MaintenanceCost, breakdown.TireCost,
            breakdown.OtherCost, breakdown.IsPartial);
    }

    public async Task<PagedResult<VehicleRankingRow>> GetVehicleRankingAsync(VehicleRankingRequest request, CancellationToken ct)
    {
        var to = request.To ?? clock.Today;
        var from = request.From ?? new DateOnly(to.Year, to.Month, 1);
        var vehicleCosts = await costs.GetVehicleCostsAsync(from, to, ct);

        var query = db.Vehicles.AsQueryable();
        if (request.Type is { } type) query = query.Where(v => v.Type == type);
        if (request.Status is { } status) query = query.Where(v => v.Status == status);
        var vehicles = await query.Select(v => new { v.Id, v.LicensePlate, Model = v.Manufacturer + " " + v.Model }).ToListAsync(ct);

        var rows = new List<VehicleRankingRow>();
        foreach (var vehicle in vehicles)
        {
            if (vehicleCosts.GetValueOrDefault(vehicle.Id) is not { } cost) continue; // no cost at all in the period — not a meaningful ranking row
            var distance = await DistanceInPeriodAsync(vehicle.Id, from, to, ct);
            var expenseCount = await db.Expenses.CountAsync(e => e.VehicleId == vehicle.Id && e.CancelledAt == null &&
                e.ExpenseDate >= from && e.ExpenseDate <= to, ct);
            rows.Add(new VehicleRankingRow(vehicle.Id, vehicle.LicensePlate, vehicle.Model, cost.TotalCost,
                VehicleCostPolicy.CostPerKm(cost.TotalCost, distance), cost.FuelCost, cost.MaintenanceCost, cost.TireCost, expenseCount));
        }

        IEnumerable<VehicleRankingRow> sorted = (request.SortBy?.ToLowerInvariant()) switch
        {
            "costperkm" => Order(rows, r => r.CostPerKm ?? -1, request.SortDirection),
            "fuelcost" => Order(rows, r => r.FuelCost, request.SortDirection),
            "maintenancecost" => Order(rows, r => r.MaintenanceCost, request.SortDirection),
            "tirecost" => Order(rows, r => r.TireCost, request.SortDirection),
            "expensecount" => Order(rows, r => r.ExpenseCount, request.SortDirection),
            _ => Order(rows, r => r.TotalCost, request.SortDirection),
        };
        var ordered = sorted.ToList();
        var page = ordered.Skip((request.SafePage - 1) * request.SafePageSize).Take(request.SafePageSize).ToList();
        return new PagedResult<VehicleRankingRow>(page, request.SafePage, request.SafePageSize, ordered.Count);
    }

    private static IEnumerable<T> Order<T, TKey>(IEnumerable<T> source, Func<T, TKey> key, SortDirection direction) =>
        direction == SortDirection.Desc ? source.OrderByDescending(key) : source.OrderBy(key);

    private async Task<(decimal FuelCost, decimal MaintenanceCost, decimal TireCost, decimal OtherCost, decimal TotalCost, bool IsPartial)>
        SumAllAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var all = await costs.GetVehicleCostsAsync(from, to, ct);
        var isPartial = all.Values.Any(v => v.IsPartial);
        return (all.Values.Sum(v => v.FuelCost), all.Values.Sum(v => v.MaintenanceCost), all.Values.Sum(v => v.TireCost),
            all.Values.Sum(v => v.OtherCost), all.Values.Sum(v => v.TotalCost), isPartial);
    }

    private async Task<(decimal? CostPerKm, bool HasSufficientData)> FleetCostPerKmAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var all = await costs.GetVehicleCostsAsync(from, to, ct);
        var totalCost = all.Values.Sum(v => v.TotalCost);
        var totalDistance = 0;
        var anyMissing = false;
        foreach (var vehicleId in all.Keys)
        {
            var distance = await DistanceInPeriodAsync(vehicleId, from, to, ct);
            if (distance is null) { anyMissing = true; continue; }
            totalDistance += distance.Value;
        }
        var hasSufficientData = !anyMissing && totalDistance >= VehicleCostPolicy.MinKmForCostPerKm;
        return (VehicleCostPolicy.CostPerKm(totalCost, hasSufficientData ? totalDistance : null), hasSufficientData);
    }

    /// <summary>
    /// Distance from the odometer history (MileageService), period-end capped at "now" so it reads the live
    /// odometer instead of requiring a reading dated exactly today.
    /// </summary>
    private async Task<int?> DistanceInPeriodAsync(Guid vehicleId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct);
        if (vehicle is null) return null;
        var fromUtc = clock.StartOfBusinessDayUtc(from);
        var toUtc = clock.StartOfBusinessDayUtc(to.AddDays(1));
        var endReading = toUtc >= clock.UtcNow ? vehicle.CurrentOdometerKm : await mileage.OdometerAtAsync(vehicle, toUtc, ct);
        var startReading = await mileage.OdometerAtAsync(vehicle, fromUtc, ct);
        if (endReading is null || startReading is null) return null;
        var distance = endReading.Value - startReading.Value;
        return distance > 0 ? distance : null;
    }
}
