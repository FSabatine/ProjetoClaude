using Fleet.Application.Common;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Analytics;

public sealed class FleetPerformanceRequest : ListRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public VehicleType? Type { get; set; }
    /// <summary>Default: every vehicle except inactive ones.</summary>
    public bool IncludeInactive { get; set; }
}

public sealed class FleetPerformanceRequestValidator : AbstractValidator<FleetPerformanceRequest>
{
    public const int MaxDays = 731;

    public FleetPerformanceRequestValidator()
    {
        this.ValidPeriod(x => x.From, x => x.To, "to");
        RuleFor(x => x).Must(x => x.From is not { } f || x.To is not { } t || t.DayNumber - f.DayNumber <= MaxDays)
            .OverridePropertyName("from").WithMessage("Escolha um período de até 2 anos.");
    }
}

public sealed record PeriodRequest(DateOnly? From, DateOnly? To);

public sealed record RecurringProblemRow(
    Guid VehicleId, string LicensePlate, string Problem, int Occurrences, DateTime LastCompletedAt, IReadOnlyList<int> WorkOrderNumbers);

public sealed record BenchmarkRow(string Label, int VehicleCount, VehicleMetrics Average);

public sealed record VehicleComparisonResponse(
    DateOnly From, DateOnly To, IReadOnlyList<VehicleMetrics> Vehicles, IReadOnlyList<BenchmarkRow> Benchmarks);

/// <summary>
/// Cross-module reports (spec §12), vehicle comparison (§17) and internal benchmarking (§18). Every number comes from
/// <see cref="VehicleMetricsService"/>; rows are at most one per vehicle, so sorting/paging the grouped result in
/// memory is bounded by the fleet size.
/// </summary>
public sealed class FleetReportsService(
    IFleetDbContext db, IClock clock, VehicleMetricsService metrics, IValidator<FleetPerformanceRequest> validator)
{
    public const int DefaultPeriodDays = 90;
    public const int MaxCompared = 6;

    private (DateOnly From, DateOnly To) Period(DateOnly? from, DateOnly? to)
    {
        var end = to ?? clock.Today;
        return (from ?? end.AddDays(-DefaultPeriodDays + 1), end);
    }

    public async Task<PagedResult<VehicleMetrics>> FleetPerformanceAsync(FleetPerformanceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var (from, to) = Period(request.From, request.To);
        var vehicles = db.Vehicles.AsQueryable();
        if (!request.IncludeInactive) vehicles = vehicles.Where(v => v.Status != VehicleStatus.Inactive);
        if (request.Type is { } type) vehicles = vehicles.Where(v => v.Type == type);
        if (request.SearchTerm is { } term)
        {
            var plate = LicensePlate.Normalize(term);
            vehicles = vehicles.Where(v => v.LicensePlate.Contains(plate) || v.Model.Contains(term));
        }

        var rows = await metrics.ForVehiclesAsync(vehicles, from, to, ct);
        var sorted = Sort(rows, request.SortBy, request.SortDirection == SortDirection.Desc);
        var page = sorted.Skip((request.SafePage - 1) * request.SafePageSize).Take(request.SafePageSize).ToList();
        return new PagedResult<VehicleMetrics>(page, request.SafePage, request.SafePageSize, rows.Count);
    }

    /// <summary>Whitelisted sort keys (A03); nulls always last so "no data" never looks like the best or the worst.</summary>
    private static IEnumerable<VehicleMetrics> Sort(IReadOnlyList<VehicleMetrics> rows, string? sortBy, bool desc)
    {
        Func<VehicleMetrics, decimal?> key = sortBy?.ToLowerInvariant() switch
        {
            "kmdriven" => r => r.KmDriven,
            "averageconsumption" => r => r.AverageConsumption,
            "fuelcost" => r => r.FuelCost,
            "maintenancecost" => r => r.MaintenanceCost,
            "tirecost" => r => r.TireCost,
            "totalcost" => r => r.TotalCost,
            "costperkm" => r => r.CostPerKm,
            "downtimehours" => r => r.DowntimeHours,
            "workorderscompleted" => r => r.WorkOrdersCompleted,
            "tirereplacements" => r => r.TireReplacements,
            _ => null!,
        };
        if (key is null) return desc ? rows.OrderByDescending(r => r.LicensePlate) : rows.OrderBy(r => r.LicensePlate);
        var withValue = rows.Where(r => key(r) is not null);
        var ordered = desc ? withValue.OrderByDescending(key).ThenBy(r => r.LicensePlate) : withValue.OrderBy(key).ThenBy(r => r.LicensePlate);
        return ordered.Concat(rows.Where(r => key(r) is null).OrderBy(r => r.LicensePlate));
    }

    /// <summary>
    /// The same problem fixed again on the same vehicle (spec §12 "recurring problems"): completed corrective work
    /// order items grouped by vehicle and description (case/accents/spacing-insensitive) appearing 2+ times.
    /// </summary>
    public async Task<IReadOnlyList<RecurringProblemRow>> RecurringProblemsAsync(PeriodRequest request, CancellationToken ct)
    {
        var (from, to) = Period(request.From, request.To);
        var start = clock.StartOfBusinessDayUtc(from);
        var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
        var orders = await db.WorkOrders
            .Where(w => w.Status == WorkOrderStatus.Completed && w.Type == MaintenanceType.Corrective && w.CompletedAt >= start && w.CompletedAt < end)
            .Select(w => new { w.Id, w.VehicleId, w.Vehicle.LicensePlate, w.Sequence, CompletedAt = w.CompletedAt!.Value, w.Description })
            .ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToList();
        var itemsByOrder = (await db.WorkOrders.Where(w => orderIds.Contains(w.Id)).SelectMany(w => w.Items)
                .Select(i => new { i.WorkOrderId, i.Description }).ToListAsync(ct))
            .ToLookup(i => i.WorkOrderId, i => i.Description);
        // Each item is a problem; a work order without items describes the problem in its own description.
        var problems = orders.SelectMany(o => (itemsByOrder[o.Id].Any() ? itemsByOrder[o.Id] : [o.Description])
            .Select(description => new { o.VehicleId, o.LicensePlate, o.Sequence, o.CompletedAt, Description = description }));

        return problems
            .GroupBy(x => (x.VehicleId, Key: ProblemKey(x.Description)))
            .Where(g => g.Select(x => x.Sequence).Distinct().Count() >= 2)
            .Select(g => new RecurringProblemRow(
                g.Key.VehicleId, g.First().LicensePlate, g.OrderByDescending(x => x.CompletedAt).First().Description,
                g.Select(x => x.Sequence).Distinct().Count(), g.Max(x => x.CompletedAt),
                g.Select(x => x.Sequence).Distinct().OrderBy(s => s).ToList()))
            .OrderByDescending(r => r.Occurrences).ThenByDescending(r => r.LastCompletedAt)
            .ToList();
    }

    public async Task<VehicleComparisonResponse> CompareAsync(IReadOnlyList<Guid> vehicleIds, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var ids = vehicleIds.Distinct().ToList();
        if (ids.Count is < 1 or > MaxCompared)
            throw new BusinessRuleException($"Escolha de 1 a {MaxCompared} veículos para comparar.");
        var (start, end) = Period(from, to);
        var chosen = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => ids.Contains(v.Id)), start, end, ct);
        if (chosen.Count != ids.Count) throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");

        // Internal benchmark (spec §18): active fleet average and, for each type compared, the type average.
        var fleet = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive), start, end, ct);
        var benchmarks = new List<BenchmarkRow> { new("Média da frota", fleet.Count, Average(fleet, "Média da frota")) };
        foreach (var type in chosen.Select(c => c.Type).Distinct())
        {
            var peers = fleet.Where(f => f.Type == type).ToList();
            if (peers.Count >= 2) benchmarks.Add(new($"Média do tipo {TypeLabel(type)}", peers.Count, Average(peers, TypeLabel(type))));
        }
        return new VehicleComparisonResponse(start, end, ids.Select(id => chosen.Single(c => c.VehicleId == id)).ToList(), benchmarks);
    }

    /// <summary>Average of each metric over the vehicles that HAVE that metric (a vehicle without data does not pull it to zero).</summary>
    private static VehicleMetrics Average(IReadOnlyList<VehicleMetrics> rows, string label)
    {
        static decimal? Avg(IEnumerable<decimal?> values)
        {
            var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            return list.Count == 0 ? null : Math.Round(list.Average(), 2);
        }
        var km = rows.Where(r => r.KmDriven.HasValue).Select(r => (decimal)r.KmDriven!.Value).ToList();
        return new VehicleMetrics(
            Guid.Empty, label, "", rows.FirstOrDefault()?.Type ?? VehicleType.Other, VehicleStatus.Available,
            km.Count == 0 ? null : (int)Math.Round(km.Average()), false,
            Avg(rows.Select(r => r.FuelQuantity)), Avg(rows.Select(r => r.AverageConsumption)),
            Avg(rows.Select(r => r.FuelCost)), Avg(rows.Select(r => r.MaintenanceCost)), Avg(rows.Select(r => r.TireCost)),
            Avg(rows.Select(r => r.OtherCost)), Avg(rows.Select(r => r.TotalCost)), Avg(rows.Select(r => r.CostPerKm)),
            (int?)Avg(rows.Select(r => (decimal?)r.WorkOrdersCompleted)), (int?)Avg(rows.Select(r => (decimal?)r.CorrectiveWorkOrders)),
            Avg(rows.Select(r => r.DowntimeHours)), (int?)Avg(rows.Select(r => (decimal?)r.TireReplacements)),
            rows.Any(r => r.IsPartial));
    }

    /// <summary>"Troca de pastilhas", "troca  de PASTILHAS." and "Troca de pastilhas" are the same problem.</summary>
    internal static string ProblemKey(string description)
    {
        var plain = new string(description.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray());
        return string.Join(' ', plain.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string TypeLabel(VehicleType type) => type switch
    {
        VehicleType.Truck => "Caminhão",
        VehicleType.TruckTractor => "Cavalo mecânico",
        VehicleType.Van => "Van",
        VehicleType.Pickup => "Picape",
        VehicleType.Car => "Carro",
        VehicleType.Motorcycle => "Moto",
        VehicleType.Bus => "Ônibus",
        _ => "Outro",
    };
}
