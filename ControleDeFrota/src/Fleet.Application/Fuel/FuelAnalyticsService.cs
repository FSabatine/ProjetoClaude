using System.Globalization;
using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Fuel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Fuel;

public enum FuelHistoryGranularity
{
    Day,
    Week,
    Month,
}

public enum FuelCostGrouping
{
    Vehicle,
    Driver,
    Station,
    FuelType,
}

public class FuelPeriodRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class FuelReportRequest : FuelPeriodRequest
{
    public Guid? VehicleId { get; set; }
    public Guid? FuelTypeId { get; set; }
    public Guid? FuelStationId { get; set; }
    public FuelCostGrouping GroupBy { get; set; } = FuelCostGrouping.Vehicle;
    public string? SortBy { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = ListRequest.DefaultPageSize;
}

public sealed class VehicleFuelRequest : FuelPeriodRequest
{
    public FuelHistoryGranularity Granularity { get; set; } = FuelHistoryGranularity.Month;
}

/// <summary>
/// Operational totals of a period (not accounting values). Consumption and cost/km use only measured segments
/// (tank-to-tank); totals use every non-cancelled fueling. Money is null without fuel.viewcosts.
/// </summary>
public sealed record FuelTotals(
    int Fuelings, decimal Liters, decimal OtherUnitsQuantity, decimal? TotalCost, decimal? AveragePricePerLiter,
    decimal? AverageConsumption, int MeasuredDistanceKm, decimal? CostPerKm);

public sealed record FuelAttentionItem(
    Guid FuelingId, Guid VehicleId, string LicensePlate, DateTime FueledAt, IReadOnlyList<FuelAnomalyType> AnomalyTypes, string Message);

public sealed record VehicleFuelMetric(
    Guid VehicleId, string LicensePlate, int Fuelings, decimal Quantity, decimal? Cost, int DistanceKm, decimal? Consumption,
    decimal? ExpectedConsumption, decimal? VariancePercent, decimal? CostPerKm);

public sealed record FuelHistoryPoint(
    DateOnly Start, DateOnly End, string Label, int Fuelings, decimal Quantity, decimal? Cost, int DistanceKm, decimal? Consumption);

public sealed record FuelTypeShare(Guid FuelTypeId, string Name, FuelUnit Unit, int Fuelings, decimal Quantity, decimal? Cost);

public sealed record FuelDashboardResponse(
    DateOnly From, DateOnly To, bool CanSeeCosts, FuelTotals Totals, int PendingReview, int ConsumptionAnomalies,
    IReadOnlyList<FuelAttentionItem> Attention, IReadOnlyList<VehicleFuelMetric> HighestCostVehicles,
    IReadOnlyList<VehicleFuelMetric> LowestConsumptionVehicles, IReadOnlyList<FuelingListItemResponse> Recent,
    IReadOnlyList<FuelHistoryPoint> Monthly, IReadOnlyList<FuelTypeShare> ByFuelType);

public sealed record VehicleFuelResponse(
    Guid VehicleId, DateOnly From, DateOnly To, bool CanSeeCosts, FuelUnit Unit, FuelTotals Totals,
    decimal? ExpectedConsumption, ConsumptionBaselineSource? BaselineSource, decimal? TankCapacity, int PendingReview,
    FuelingListItemResponse? LastFueling, FuelHistoryGranularity Granularity, IReadOnlyList<FuelHistoryPoint> History);

public sealed record ConsumptionReportRow(
    Guid VehicleId, string LicensePlate, FuelUnit Unit, int Segments, int DistanceKm, decimal Quantity, decimal? Consumption,
    decimal? ExpectedConsumption, decimal? VariancePercent);

public sealed record CostReportRow(
    Guid? Key, string Name, int Fuelings, decimal Liters, decimal Cost, int? DistanceKm, decimal? CostPerKm);

public sealed record StationReportRow(
    Guid? FuelStationId, string StationName, Guid FuelTypeId, string FuelTypeName, FuelUnit Unit, int Fuelings, decimal Quantity,
    decimal? AveragePrice, decimal? MinPrice, decimal? MaxPrice, decimal? TotalCost);

public sealed record PriceReportRow(
    int Year, int Month, Guid FuelTypeId, string FuelTypeName, FuelUnit Unit, int Fuelings, decimal AveragePrice, decimal MinPrice, decimal MaxPrice);

public sealed record FuelReportPage<T>(DateOnly From, DateOnly To, bool CanSeeCosts, PagedResult<T> Rows);

public sealed class FuelPeriodRequestValidator : AbstractValidator<FuelPeriodRequest>
{
    public const int MaxPeriodDays = 731;

    public FuelPeriodRequestValidator()
    {
        this.ValidPeriod(x => x.From, x => x.To, "to");
        RuleFor(x => x).Must(x => x.From is not { } f || x.To is not { } t || t.DayNumber - f.DayNumber < MaxPeriodDays)
            .WithName("to").OverridePropertyName("to")
            .WithMessage("Escolha um período de até 2 anos.");
    }
}

/// <summary>
/// Fuel dashboard, vehicle fuel tab and reports (seções 19, 23–25, 32, 47). Aggregation happens in SQL; the grouped rows
/// are bounded by days or vehicles of the period, never by the number of fuelings (seção 44).
/// </summary>
public sealed class FuelAnalyticsService(
    IFleetDbContext db, IClock clock, ICurrentUser currentUser, FuelingService fuelings, IValidator<FuelPeriodRequest> periodValidator)
{
    public const int DefaultPeriodDays = 30;

    private bool CanSeeCosts => currentUser.HasPermission(Permissions.Fuel.ViewCosts);

    public async Task<FuelDashboardResponse> GetDashboardAsync(FuelPeriodRequest request, CancellationToken ct)
    {
        var (from, to) = await PeriodAsync(request, ct);
        var period = InPeriod(from, to);
        var costs = CanSeeCosts;

        var totals = await TotalsAsync(period, ct);
        var pending = await db.Fuelings.CountAsync(f => f.Status == FuelingStatus.PendingReview, ct);
        var consumptionAnomalies = await db.FuelingAnomalies.CountAsync(a => a.ReviewedAt == null &&
            (a.Type == FuelAnomalyType.LowConsumption || a.Type == FuelAnomalyType.HighConsumption), ct);

        var attentionRows = await db.Fuelings.Where(f => f.Status == FuelingStatus.PendingReview)
            .OrderByDescending(f => f.FueledAt).Take(10)
            .Select(f => new
            {
                f.Id, f.VehicleId, f.Vehicle.LicensePlate, f.FueledAt,
                Anomalies = f.Anomalies.Where(a => a.ReviewedAt == null).OrderBy(a => a.DetectedAt).Select(a => new { a.Type, a.Message }).ToList(),
            })
            .ToListAsync(ct);
        var attention = attentionRows.Select(r => new FuelAttentionItem(r.Id, r.VehicleId, r.LicensePlate, r.FueledAt,
            r.Anomalies.Select(a => a.Type).ToList(), r.Anomalies.Select(a => a.Message).FirstOrDefault() ?? "")).ToList();

        var vehicles = await VehicleMetricsAsync(period, ct);
        var highestCost = costs
            ? vehicles.Where(v => v.Cost > 0).OrderByDescending(v => v.Cost).ThenBy(v => v.LicensePlate).Take(5).ToList()
            : [];
        var lowestConsumption = vehicles.Where(v => v.Consumption is not null)
            .OrderBy(v => v.VariancePercent ?? 0).ThenBy(v => v.Consumption).Take(5).ToList();

        var recent = await fuelings.ListAsync(new FuelingListRequest { From = from, To = to, PageSize = 8 }, ct);

        var monthsFrom = new DateOnly(to.Year, to.Month, 1).AddMonths(-11);
        var monthly = Bucket(await DailyAsync(InPeriod(monthsFrom, to), unit: FuelUnit.Liter, ct), FuelHistoryGranularity.Month, monthsFrom, to);

        var byType = await period.GroupBy(f => new { f.FuelTypeId, f.FuelType.Name, f.FuelType.Unit })
            .Select(g => new FuelTypeShare(g.Key.FuelTypeId, g.Key.Name, g.Key.Unit, g.Count(), g.Sum(f => f.Quantity),
                costs ? g.Sum(f => f.TotalAmount) : null))
            .ToListAsync(ct);

        return new FuelDashboardResponse(from, to, costs, totals, pending, consumptionAnomalies, attention, highestCost, lowestConsumption,
            recent.Items, monthly, byType.OrderByDescending(t => t.Fuelings).ToList());
    }

    public async Task<VehicleFuelResponse> GetVehicleAsync(Guid vehicleId, VehicleFuelRequest request, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        var (from, to) = await PeriodAsync(request, ct, defaultDays: request.Granularity == FuelHistoryGranularity.Month ? 365 : 90);
        var all = db.Fuelings.Where(f => f.VehicleId == vehicleId && f.Status != FuelingStatus.Cancelled);
        var period = all.Where(f => f.FueledOn >= from && f.FueledOn <= to);

        var unit = await all.OrderByDescending(f => f.FueledAt).Select(f => (FuelUnit?)f.FuelType.Unit).FirstOrDefaultAsync(ct) ?? FuelUnit.Liter;
        var totals = await TotalsAsync(period, ct, unit);
        var lastMeasured = await all.Where(f => f.ConsumptionResult == ConsumptionResult.Calculated && f.ExpectedConsumption != null)
            .OrderByDescending(f => f.FueledAt)
            .Select(f => new { f.ExpectedConsumption, f.BaselineSource }).FirstOrDefaultAsync(ct);
        var last = await fuelings.ListAsync(new FuelingListRequest { VehicleId = vehicleId, PageSize = 1 }, ct);
        var history = Bucket(await DailyAsync(period, unit, ct), request.Granularity, from, to);
        var tank = vehicle.FuelTankCapacity is null && vehicle.SecondaryFuelTankCapacity is null
            ? (decimal?)null : (vehicle.FuelTankCapacity ?? 0) + (vehicle.SecondaryFuelTankCapacity ?? 0);

        return new VehicleFuelResponse(vehicleId, from, to, CanSeeCosts, unit, totals,
            vehicle.ExpectedConsumption ?? lastMeasured?.ExpectedConsumption,
            vehicle.ExpectedConsumption is not null ? ConsumptionBaselineSource.Configured : lastMeasured?.BaselineSource,
            tank, await all.CountAsync(f => f.Status == FuelingStatus.PendingReview, ct),
            last.Items.FirstOrDefault(), request.Granularity, history);
    }

    public async Task<FuelReportPage<ConsumptionReportRow>> GetConsumptionReportAsync(FuelReportRequest request, CancellationToken ct)
    {
        var (from, to) = await PeriodAsync(request, ct);
        var measured = Measured(Filtered(InPeriod(from, to), request));
        // Only plain columns are summed (seção 44; SQLite in the tests refuses computed decimal aggregates). Segment columns
        // are null unless the segment was measured, and the expected fuel of each segment is a stored snapshot.
        var groups = await measured.GroupBy(f => new { f.VehicleId, f.Vehicle.LicensePlate, f.FuelType.Unit })
            .Select(g => new
            {
                g.Key.VehicleId, g.Key.LicensePlate, g.Key.Unit, Segments = g.Count(),
                Distance = g.Sum(f => f.SegmentDistanceKm) ?? 0, Quantity = g.Sum(f => f.SegmentQuantity) ?? 0,
                WithBaseline = g.Sum(f => f.SegmentExpectedQuantity != null ? f.SegmentDistanceKm : 0) ?? 0,
                ExpectedQuantity = g.Sum(f => f.SegmentExpectedQuantity),
            })
            .ToListAsync(ct);

        var rows = groups.Select(g =>
        {
            var actual = Ratio(g.Distance, g.Quantity);
            var expected = g.ExpectedQuantity is > 0 ? Ratio(g.WithBaseline, g.ExpectedQuantity.Value) : null;
            return new ConsumptionReportRow(g.VehicleId, g.LicensePlate, g.Unit, g.Segments, g.Distance, g.Quantity, actual, expected,
                actual is { } a && expected is { } e ? FuelAnomalyRules.DeviationPercent(a, e) : null);
        });
        var sorted = (request.SortBy?.ToLowerInvariant()) switch
        {
            "consumption" => Order(rows, r => r.Consumption, request.SortDirection),
            "variancepercent" => Order(rows, r => r.VariancePercent, request.SortDirection),
            "distancekm" => Order(rows, r => r.DistanceKm, request.SortDirection),
            _ => Order(rows, r => r.LicensePlate, request.SortDirection),
        };
        return new FuelReportPage<ConsumptionReportRow>(from, to, CanSeeCosts, Page(sorted, request));
    }

    /// <summary>Cost by vehicle, driver, station or fuel type (seção 24). Requires fuel.viewcosts (checked on the endpoint).</summary>
    public async Task<FuelReportPage<CostReportRow>> GetCostReportAsync(FuelReportRequest request, CancellationToken ct)
    {
        var (from, to) = await PeriodAsync(request, ct);
        var period = Filtered(InPeriod(from, to), request);

        List<CostReportRow> rows;
        if (request.GroupBy == FuelCostGrouping.Vehicle)
        {
            rows = (await VehicleMetricsAsync(period, ct)).Select(v => new CostReportRow(
                v.VehicleId, v.LicensePlate, v.Fuelings, v.Quantity, v.Cost ?? 0, v.DistanceKm, v.CostPerKm)).ToList();
        }
        else
        {
            // Grouped by key and unit in SQL (litres are only summed with litres), folded per key here.
            var grouped = request.GroupBy switch
            {
                FuelCostGrouping.Driver => period.GroupBy(f => new { Key = f.DriverId, Name = f.Driver != null ? f.Driver.FullName : null, f.FuelType.Unit })
                    .Select(g => new CostGroup(g.Key.Key, g.Key.Name, g.Key.Unit, g.Count(), g.Sum(f => f.Quantity), g.Sum(f => f.TotalAmount))),
                FuelCostGrouping.Station => period.GroupBy(f => new { Key = f.FuelStationId, Name = f.FuelStation != null ? f.FuelStation.Name : null, f.FuelType.Unit })
                    .Select(g => new CostGroup(g.Key.Key, g.Key.Name, g.Key.Unit, g.Count(), g.Sum(f => f.Quantity), g.Sum(f => f.TotalAmount))),
                _ => period.GroupBy(f => new { Key = (Guid?)f.FuelTypeId, Name = (string?)f.FuelType.Name, f.FuelType.Unit })
                    .Select(g => new CostGroup(g.Key.Key, g.Key.Name, g.Key.Unit, g.Count(), g.Sum(f => f.Quantity), g.Sum(f => f.TotalAmount))),
            };
            var missing = request.GroupBy == FuelCostGrouping.Driver ? "Sem motorista informado" : "Sem posto informado";
            rows = (await grouped.ToListAsync(ct)).GroupBy(g => g.Key)
                .Select(g => new CostReportRow(g.Key, g.First().Name ?? missing, g.Sum(x => x.Count),
                    g.Where(x => x.Unit == FuelUnit.Liter).Sum(x => x.Quantity), g.Sum(x => x.Cost), null, null))
                .ToList();
        }
        var sorted = (request.SortBy?.ToLowerInvariant()) switch
        {
            "cost" => Order(rows, r => r.Cost, request.SortDirection),
            "costperkm" => Order(rows, r => r.CostPerKm, request.SortDirection),
            "fuelings" => Order(rows, r => r.Fuelings, request.SortDirection),
            _ => Order(rows, r => r.Name, request.SortDirection),
        };
        return new FuelReportPage<CostReportRow>(from, to, true, Page(sorted, request));
    }

    public async Task<FuelReportPage<StationReportRow>> GetStationReportAsync(FuelReportRequest request, CancellationToken ct)
    {
        var (from, to) = await PeriodAsync(request, ct);
        var costs = CanSeeCosts;
        var rows = await Filtered(InPeriod(from, to), request)
            .GroupBy(f => new { f.FuelStationId, Station = f.FuelStation != null ? f.FuelStation.Name : null, f.FuelTypeId, Type = f.FuelType.Name, f.FuelType.Unit })
            .Select(g => new
            {
                g.Key.FuelStationId, g.Key.Station, g.Key.FuelTypeId, g.Key.Type, g.Key.Unit, Count = g.Count(),
                Quantity = g.Sum(f => f.Quantity), Total = g.Sum(f => f.TotalAmount), Min = g.Min(f => f.UnitPrice), Max = g.Max(f => f.UnitPrice),
            })
            .ToListAsync(ct);
        var mapped = rows.Select(r => new StationReportRow(r.FuelStationId, r.Station ?? "Sem posto informado", r.FuelTypeId, r.Type, r.Unit,
            r.Count, r.Quantity, costs ? Price(r.Total, r.Quantity) : null, costs ? r.Min : null, costs ? r.Max : null, costs ? r.Total : null));
        var sorted = (request.SortBy?.ToLowerInvariant()) switch
        {
            "fuelings" => Order(mapped, r => r.Fuelings, request.SortDirection),
            "quantity" => Order(mapped, r => r.Quantity, request.SortDirection),
            "averageprice" when costs => Order(mapped, r => r.AveragePrice, request.SortDirection),
            "totalcost" when costs => Order(mapped, r => r.TotalCost, request.SortDirection),
            _ => Order(mapped, r => r.StationName + "|" + r.FuelTypeName, request.SortDirection),
        };
        return new FuelReportPage<StationReportRow>(from, to, costs, Page(sorted, request));
    }

    /// <summary>Price over time: weighted average (paid total / quantity), min and max per product and month (seção 23).</summary>
    public async Task<FuelReportPage<PriceReportRow>> GetPriceReportAsync(FuelReportRequest request, CancellationToken ct)
    {
        var (from, to) = await PeriodAsync(request, ct);
        var daily = await Filtered(InPeriod(from, to), request)
            .GroupBy(f => new { f.FueledOn, f.FuelTypeId, f.FuelType.Name, f.FuelType.Unit })
            .Select(g => new
            {
                g.Key.FueledOn, g.Key.FuelTypeId, g.Key.Name, g.Key.Unit, Count = g.Count(),
                Quantity = g.Sum(f => f.Quantity), Total = g.Sum(f => f.TotalAmount), Min = g.Min(f => f.UnitPrice), Max = g.Max(f => f.UnitPrice),
            })
            .ToListAsync(ct);
        var rows = daily.GroupBy(d => new { d.FueledOn.Year, d.FueledOn.Month, d.FuelTypeId, d.Name, d.Unit })
            .Select(g => new PriceReportRow(g.Key.Year, g.Key.Month, g.Key.FuelTypeId, g.Key.Name, g.Key.Unit, g.Sum(d => d.Count),
                Price(g.Sum(d => d.Total), g.Sum(d => d.Quantity)) ?? 0, g.Min(d => d.Min), g.Max(d => d.Max)))
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ThenBy(r => r.FuelTypeName);
        return new FuelReportPage<PriceReportRow>(from, to, true, Page(rows, request));
    }

    // ---------- building blocks ----------

    private async Task<(DateOnly From, DateOnly To)> PeriodAsync(FuelPeriodRequest request, CancellationToken ct, int defaultDays = DefaultPeriodDays)
    {
        await periodValidator.ValidateAndThrowAsync(request, ct);
        var to = request.To ?? clock.Today;
        var from = request.From ?? to.AddDays(-(defaultDays - 1));
        return (from, to);
    }

    private IQueryable<Fueling> InPeriod(DateOnly from, DateOnly to) =>
        db.Fuelings.Where(f => f.Status != FuelingStatus.Cancelled && f.FueledOn >= from && f.FueledOn <= to);

    private static IQueryable<Fueling> Filtered(IQueryable<Fueling> query, FuelReportRequest r)
    {
        if (r.VehicleId is { } vehicleId) query = query.Where(f => f.VehicleId == vehicleId);
        if (r.FuelTypeId is { } typeId) query = query.Where(f => f.FuelTypeId == typeId);
        if (r.FuelStationId is { } stationId) query = query.Where(f => f.FuelStationId == stationId);
        return query;
    }

    private static IQueryable<Fueling> Measured(IQueryable<Fueling> query) =>
        query.Where(f => f.ConsumptionResult == ConsumptionResult.Calculated && f.SegmentDistanceKm != null && f.SegmentQuantity != null);

    private sealed record CostGroup(Guid? Key, string? Name, FuelUnit Unit, int Count, decimal Quantity, decimal Cost);

    private sealed record UnitTotals(FuelUnit Unit, int Count, decimal Quantity, decimal Cost, int Distance, decimal SegmentQuantity, decimal SegmentCost);

    /// <summary>
    /// One grouped query by unit, summing plain columns only. Segment columns are null unless the segment was measured,
    /// so their sums are exactly the measured distance, fuel and cost of the period.
    /// </summary>
    private async Task<FuelTotals> TotalsAsync(IQueryable<Fueling> period, CancellationToken ct, FuelUnit consumptionUnit = FuelUnit.Liter)
    {
        var byUnit = await period.GroupBy(f => f.FuelType.Unit).Select(g => new UnitTotals(
            g.Key, g.Count(), g.Sum(f => f.Quantity), g.Sum(f => f.TotalAmount),
            g.Sum(f => f.SegmentDistanceKm) ?? 0, g.Sum(f => f.SegmentQuantity) ?? 0, g.Sum(f => f.SegmentCost) ?? 0)).ToListAsync(ct);

        var costs = CanSeeCosts;
        var liters = byUnit.Where(u => u.Unit == FuelUnit.Liter).ToList();
        var unit = byUnit.Where(u => u.Unit == consumptionUnit).ToList();
        var literQuantity = liters.Sum(u => u.Quantity);
        var distance = byUnit.Sum(u => u.Distance);
        var unitSegmentQuantity = unit.Sum(u => u.SegmentQuantity);
        return new FuelTotals(
            byUnit.Sum(u => u.Count), literQuantity, byUnit.Where(u => u.Unit != FuelUnit.Liter).Sum(u => u.Quantity),
            costs ? byUnit.Sum(u => u.Cost) : null,
            costs && literQuantity > 0 ? Price(liters.Sum(u => u.Cost), literQuantity) : null,
            unitSegmentQuantity > 0 ? Ratio(unit.Sum(u => u.Distance), unitSegmentQuantity) : null,
            distance,
            costs && distance > 0 ? Math.Round(byUnit.Sum(u => u.SegmentCost) / distance, 2, MidpointRounding.AwayFromZero) : null);
    }

    /// <summary>Per vehicle: every fueling for cost/volume, measured segments for consumption and cost/km (rows: vehicles x units).</summary>
    private async Task<List<VehicleFuelMetric>> VehicleMetricsAsync(IQueryable<Fueling> period, CancellationToken ct)
    {
        var costs = CanSeeCosts;
        var rows = await period.GroupBy(f => new { f.VehicleId, f.Vehicle.LicensePlate, f.FuelType.Unit })
            .Select(g => new
            {
                g.Key.VehicleId, g.Key.LicensePlate, g.Key.Unit, Count = g.Count(), Quantity = g.Sum(f => f.Quantity), Cost = g.Sum(f => f.TotalAmount),
                Distance = g.Sum(f => f.SegmentDistanceKm) ?? 0, SegmentQuantity = g.Sum(f => f.SegmentQuantity) ?? 0,
                SegmentCost = g.Sum(f => f.SegmentCost) ?? 0,
                WithBaseline = g.Sum(f => f.SegmentExpectedQuantity != null ? f.SegmentDistanceKm : 0) ?? 0,
                ExpectedQuantity = g.Sum(f => f.SegmentExpectedQuantity) ?? 0,
            })
            .ToListAsync(ct);

        return rows.GroupBy(r => new { r.VehicleId, r.LicensePlate }).Select(v =>
        {
            var liters = v.Where(r => r.Unit == FuelUnit.Liter).ToList();
            var distance = v.Sum(r => r.Distance);
            var literSegmentQuantity = liters.Sum(r => r.SegmentQuantity);
            var literExpectedQuantity = liters.Sum(r => r.ExpectedQuantity);
            var consumption = literSegmentQuantity > 0 ? Ratio(liters.Sum(r => r.Distance), literSegmentQuantity) : null;
            var expected = literExpectedQuantity > 0 ? Ratio(liters.Sum(r => r.WithBaseline), literExpectedQuantity) : null;
            return new VehicleFuelMetric(v.Key.VehicleId, v.Key.LicensePlate, v.Sum(r => r.Count), liters.Sum(r => r.Quantity),
                costs ? v.Sum(r => r.Cost) : null, distance, consumption, expected,
                consumption is { } a && expected is { } x ? FuelAnomalyRules.DeviationPercent(a, x) : null,
                costs && distance > 0 ? Math.Round(v.Sum(r => r.SegmentCost) / distance, 2, MidpointRounding.AwayFromZero) : null);
        }).ToList();
    }

    private sealed record DailyRow(DateOnly Day, int Count, decimal Quantity, decimal Cost, int Distance, decimal SegmentQuantity);

    /// <summary>One row per day with fuelings — the only rows that reach memory for history charts.</summary>
    private async Task<List<DailyRow>> DailyAsync(IQueryable<Fueling> period, FuelUnit unit, CancellationToken ct)
    {
        var rows = await period.GroupBy(f => new { f.FueledOn, f.FuelType.Unit }).Select(g => new
        {
            g.Key.FueledOn, g.Key.Unit, Count = g.Count(), Quantity = g.Sum(f => f.Quantity), Cost = g.Sum(f => f.TotalAmount),
            Distance = g.Sum(f => f.SegmentDistanceKm) ?? 0, SegmentQuantity = g.Sum(f => f.SegmentQuantity) ?? 0,
        }).ToListAsync(ct);
        // Count and cost include every product; volume and consumption only the unit being charted.
        return rows.GroupBy(r => r.FueledOn).Select(d => new DailyRow(d.Key, d.Sum(r => r.Count),
            d.Where(r => r.Unit == unit).Sum(r => r.Quantity), d.Sum(r => r.Cost),
            d.Where(r => r.Unit == unit).Sum(r => r.Distance), d.Where(r => r.Unit == unit).Sum(r => r.SegmentQuantity))).ToList();
    }

    /// <summary>
    /// Folds daily rows into day/week/month buckets covering the whole period (empty buckets included, so the chart
    /// does not hide gaps). A bucket without a measured segment has no consumption: "—" is honest, an estimate is not.
    /// </summary>
    private List<FuelHistoryPoint> Bucket(IReadOnlyList<DailyRow> daily, FuelHistoryGranularity granularity, DateOnly from, DateOnly to)
    {
        var costs = CanSeeCosts;
        var points = new List<FuelHistoryPoint>();
        for (var start = BucketStart(from, granularity); start <= to; start = Next(start, granularity))
        {
            var end = Next(start, granularity).AddDays(-1);
            var rows = daily.Where(d => d.Day >= start && d.Day <= end).ToList();
            var distance = rows.Sum(r => r.Distance);
            var segmentQuantity = rows.Sum(r => r.SegmentQuantity);
            points.Add(new FuelHistoryPoint(start, end, Label(start, end, granularity), rows.Sum(r => r.Count), rows.Sum(r => r.Quantity),
                costs ? rows.Sum(r => r.Cost) : null, distance, segmentQuantity > 0 ? Ratio(distance, segmentQuantity) : null));
        }
        return points;
    }

    private static DateOnly BucketStart(DateOnly day, FuelHistoryGranularity g) => g switch
    {
        FuelHistoryGranularity.Month => new DateOnly(day.Year, day.Month, 1),
        // ISO-like weeks starting on Monday, the convention used by the checklist schedule.
        FuelHistoryGranularity.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        _ => day,
    };

    private static DateOnly Next(DateOnly start, FuelHistoryGranularity g) => g switch
    {
        FuelHistoryGranularity.Month => start.AddMonths(1),
        FuelHistoryGranularity.Week => start.AddDays(7),
        _ => start.AddDays(1),
    };

    private static readonly CultureInfo PtBr = new("pt-BR");

    private static string Label(DateOnly start, DateOnly end, FuelHistoryGranularity g) => g switch
    {
        FuelHistoryGranularity.Month => PtBr.TextInfo.ToTitleCase(start.ToString("MMM/yy", PtBr).Replace(".", "")),
        FuelHistoryGranularity.Week => $"{start:dd'/'MM}–{end:dd'/'MM}",
        _ => start.ToString("dd'/'MM", CultureInfo.InvariantCulture),
    };

    private static decimal? Ratio(decimal distance, decimal quantity) =>
        quantity > 0 ? Math.Round(distance / quantity, 2, MidpointRounding.AwayFromZero) : null;

    private static decimal? Price(decimal total, decimal quantity) =>
        quantity > 0 ? Math.Round(total / quantity, 3, MidpointRounding.AwayFromZero) : null;

    private static IEnumerable<T> Order<T, TKey>(IEnumerable<T> rows, Func<T, TKey> key, SortDirection direction) =>
        direction == SortDirection.Desc ? rows.OrderByDescending(key) : rows.OrderBy(key);

    private static PagedResult<T> Page<T>(IEnumerable<T> rows, FuelReportRequest request)
    {
        var list = rows.ToList();
        var size = Math.Clamp(request.PageSize, 1, ListRequest.MaxPageSize);
        var page = Math.Max(1, request.Page);
        return new PagedResult<T>(list.Skip((page - 1) * size).Take(size).ToList(), page, size, list.Count);
    }
}
