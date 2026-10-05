using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Fuel;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Validation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Analytics;

public enum InsightSentiment
{
    Neutral,
    Positive,
    Negative,
}

public sealed record Insight(
    string Key, FleetAlertCategory Category, InsightSentiment Sentiment, string Title, string Explanation, string Evidence,
    string? Link, decimal Magnitude, TrendDirection Direction = TrendDirection.Stable);

/// <summary>
/// A few high-value findings for the dashboard (spec §13–§15). Deterministic: every figure is computed here with
/// <see cref="TrendAnalysis"/> over real data; the wording is fixed and neutral. Each insight needs the permissions of
/// the data it quotes (money = every *.viewcosts). The AI assistant may explain these, never recompute them.
/// </summary>
public sealed class InsightService(IFleetDbContext db, IClock clock, ICurrentUser currentUser, CostAggregationService costs)
{
    public const int WindowDays = 30;
    public const int MaxInsights = 5;
    public const decimal MinChangePercent = 10m;
    public const decimal MinCostBase = 500m;

    private bool Can(params string[] permissions) => permissions.All(currentUser.HasPermission);

    public async Task<IReadOnlyList<Insight>> GetAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var current = (From: today.AddDays(-WindowDays + 1), To: today);
        var previous = (From: current.From.AddDays(-WindowDays), To: current.From.AddDays(-1));
        var insights = new List<Insight>();

        if (Can([.. AlertAudiences.RequiredPermissions(AlertAudience.FleetCosts)]))
        {
            insights.AddRange(await CostTrendsAsync(current, previous, ct));
            if (await MaintenanceConcentrationAsync(today, ct) is { } concentration) insights.Add(concentration);
        }
        if (Can(Permissions.Fuel.View))
        {
            if (await FleetConsumptionAsync(current, previous, ct) is { } consumption) insights.Add(consumption);
            if (await VehiclesAboveOwnAverageAsync(ct) is { } above) insights.Add(above);
        }
        if (Can(Permissions.Maintenance.View))
        {
            if (await DowntimeAsync(current, previous, ct) is { } downtime) insights.Add(downtime);
            if (await CorrectiveFrequencyAsync(current, previous, ct) is { } frequency) insights.Add(frequency);
        }
        if (Can(Permissions.Tires.View) && await TireReplacementsAsync(today, ct) is { } tires) insights.Add(tires);

        return insights.OrderByDescending(i => i.Sentiment == InsightSentiment.Negative).ThenByDescending(i => i.Magnitude)
            .Take(MaxInsights).ToList();
    }

    private static string Pct(decimal change) => BrazilianFormat.Compact(Math.Abs(change)) + "%";

    private async Task<IEnumerable<Insight>> CostTrendsAsync((DateOnly From, DateOnly To) current, (DateOnly From, DateOnly To) previous, CancellationToken ct)
    {
        var now = await costs.GetVehicleCostsAsync(current.From, current.To, ct);
        var before = await costs.GetVehicleCostsAsync(previous.From, previous.To, ct);
        // Fleet totals by category include expenses not tied to a vehicle; the per-vehicle figures only name who moved.
        var totalsNow = await SliceTotalsAsync(current.From, current.To, ct);
        var totalsBefore = await SliceTotalsAsync(previous.From, previous.To, ct);
        var plates = await db.Vehicles.ToDictionaryAsync(v => v.Id, v => v.LicensePlate, ct);
        var slices = new (string Key, string Name, Func<VehicleCostBreakdown, decimal> Value)[]
        {
            ("maintenance", "manutenção", c => c.MaintenanceCost),
            ("fuel", "combustível", c => c.FuelCost),
            ("tires", "pneus", c => c.TireCost),
            ("other", "outras despesas", c => c.OtherCost),
        };
        var result = new List<Insight>();
        foreach (var (key, name, value) in slices)
        {
            var a = totalsNow[key];
            var b = totalsBefore[key];
            var trend = TrendAnalysis.Compare(a, b, MinCostBase, MinChangePercent);
            if (!trend.IsSignificant) continue;
            var up = trend.Direction == TrendDirection.Up;
            // Who drove the change: the vehicles with the largest variation in this slice.
            var drivers = now.Keys.Union(before.Keys)
                .Select(id => (Id: id, Delta: value(now.GetValueOrDefault(id) ?? Empty(id)) - value(before.GetValueOrDefault(id) ?? Empty(id))))
                .Where(d => up ? d.Delta > 0 : d.Delta < 0)
                .OrderByDescending(d => Math.Abs(d.Delta)).Take(3)
                .Select(d => LicensePlate.Format(plates.GetValueOrDefault(d.Id, "?"))).ToList();
            result.Add(new Insight(
                $"cost-{key}", FleetAlertCategory.Finance, up ? InsightSentiment.Negative : InsightSentiment.Positive,
                $"Custos de {name} {(up ? "aumentaram" : "caíram")} {Pct(trend.ChangePercent!.Value)}",
                $"Nos últimos {WindowDays} dias a frota gastou {BrazilianFormat.Currency(a)} com {name}, contra {BrazilianFormat.Currency(b)} nos {WindowDays} dias anteriores." +
                (drivers.Count > 0 ? $" Principais veículos na variação: {string.Join(", ", drivers)}." : ""),
                $"{BrazilianFormat.Date(current.From)} a {BrazilianFormat.Date(current.To)}: {BrazilianFormat.Currency(a)}; {BrazilianFormat.Date(previous.From)} a {BrazilianFormat.Date(previous.To)}: {BrazilianFormat.Currency(b)}.",
                "/financeiro", Math.Abs(trend.ChangePercent.Value), trend.Direction));
        }
        return result;
    }

    private static VehicleCostBreakdown Empty(Guid id) => new(id, 0, 0, 0, 0, 0, false);

    /// <summary>System categories carry the module slices (ADR-040, same name mapping as BudgetService); the rest is "other".</summary>
    private async Task<Dictionary<string, decimal>> SliceTotalsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var (categories, _) = await costs.GetFleetCostByCategoryAsync(from, to, ct);
        decimal Sum(string category) => categories.Where(c => c.CategoryName == category).Sum(c => c.Amount);
        var fuel = Sum("Combustível");
        var maintenance = Sum("Manutenção");
        var tires = Sum("Pneus");
        return new Dictionary<string, decimal>
        {
            ["fuel"] = fuel, ["maintenance"] = maintenance, ["tires"] = tires,
            ["other"] = categories.Sum(c => c.Amount) - fuel - maintenance - tires,
        };
    }

    /// <summary>One vehicle concentrating a large share of the maintenance spending (90 days).</summary>
    private async Task<Insight?> MaintenanceConcentrationAsync(DateOnly today, CancellationToken ct)
    {
        var from = today.AddDays(-89);
        var byVehicle = (await costs.GetVehicleCostsAsync(from, today, ct)).Values.Where(v => v.MaintenanceCost > 0).ToList();
        var total = byVehicle.Sum(v => v.MaintenanceCost);
        if (byVehicle.Count < 4 || total < MinCostBase) return null;
        var top = byVehicle.MaxBy(v => v.MaintenanceCost)!;
        var share = Math.Round(top.MaintenanceCost / total * 100m, 0);
        if (share < 25) return null;
        var plate = await db.Vehicles.Where(v => v.Id == top.VehicleId).Select(v => v.LicensePlate).SingleAsync(ct);
        return new Insight("maintenance-concentration", FleetAlertCategory.Maintenance, InsightSentiment.Negative,
            $"{LicensePlate.Format(plate)} concentra {share:0}% do custo de manutenção",
            $"Nos últimos 90 dias, {LicensePlate.Format(plate)} respondeu por {share:0}% de todo o custo de manutenção da frota ({byVehicle.Count} veículos com manutenção).",
            $"{BrazilianFormat.Currency(top.MaintenanceCost)} de {BrazilianFormat.Currency(total)}.",
            $"/veiculos/{top.VehicleId}?aba=financeiro", share, TrendDirection.Up);
    }

    private async Task<(long Km, decimal Quantity, int Segments)> FleetSegmentsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var segments = db.Fuelings
            .Where(f => f.Status != FuelingStatus.Cancelled && f.ConsumptionResult == ConsumptionResult.Calculated &&
                        f.FuelType.Unit == FuelUnit.Liter && f.FueledOn >= from && f.FueledOn <= to);
        // ADR-033: sum columns separately; the ratio is taken in memory.
        var km = await segments.SumAsync(f => f.SegmentDistanceKm, ct) ?? 0;
        var quantity = await segments.SumAsync(f => f.SegmentQuantity, ct) ?? 0m;
        return (km, quantity, await segments.CountAsync(ct));
    }

    /// <summary>Fleet consumption (km/l, weighted) — for consumption "up" is good.</summary>
    private async Task<Insight?> FleetConsumptionAsync((DateOnly From, DateOnly To) current, (DateOnly From, DateOnly To) previous, CancellationToken ct)
    {
        var a = await FleetSegmentsAsync(current.From, current.To, ct);
        var b = await FleetSegmentsAsync(previous.From, previous.To, ct);
        if (a.Segments < 5 || b.Segments < 5) return null;
        var now = new ConsumptionSample(a.Km, a.Quantity, a.Segments).Average;
        var before = new ConsumptionSample(b.Km, b.Quantity, b.Segments).Average;
        if (now is null || before is null) return null;
        var trend = TrendAnalysis.Compare(now.Value, before.Value, 0.1m, 5m);
        if (!trend.IsSignificant) return null;
        var better = trend.Direction == TrendDirection.Up;
        return new Insight("fleet-consumption", FleetAlertCategory.Fuel, better ? InsightSentiment.Positive : InsightSentiment.Negative,
            $"Consumo médio da frota {(better ? "melhorou" : "piorou")} {Pct(trend.ChangePercent!.Value)}",
            $"A frota fez {BrazilianFormat.Compact(now.Value)} km/l nos últimos {WindowDays} dias, contra {BrazilianFormat.Compact(before.Value)} km/l nos {WindowDays} dias anteriores (média ponderada, tanque cheio a tanque cheio).",
            $"{a.Segments} trechos medidos no período e {b.Segments} no anterior.",
            "/combustivel", Math.Abs(trend.ChangePercent.Value), trend.Direction);
    }

    private async Task<Insight?> VehiclesAboveOwnAverageAsync(CancellationToken ct)
    {
        var count = await db.FleetAlerts.CountAsync(a => a.Trigger == AutomationTrigger.FuelConsumptionAbnormal &&
                                                         FleetAlertWorkflow.OpenStatuses.Contains(a.Status), ct);
        if (count < 2) return null;
        return new Insight("vehicles-consumption-above", FleetAlertCategory.Fuel, InsightSentiment.Negative,
            $"{count} veículos consumindo acima da própria média",
            $"{count} veículos estão com consumo pior que a média histórica de cada um, acima do limite configurado na regra de automação.",
            "Comparação de cada veículo com ele mesmo (180 dias anteriores).",
            $"/alertas?trigger={AutomationTrigger.FuelConsumptionAbnormal}", count * 5, TrendDirection.Up);
    }

    private async Task<Insight?> DowntimeAsync((DateOnly From, DateOnly To) current, (DateOnly From, DateOnly To) previous, CancellationToken ct)
    {
        async Task<decimal> HoursAsync(DateOnly from, DateOnly to)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            var minutes = await db.WorkOrders.Where(w => w.Status == WorkOrderStatus.Completed && w.CompletedAt >= start && w.CompletedAt < end)
                .SumAsync(w => w.DowntimeMinutes, ct) ?? 0;
            return minutes / 60m;
        }
        var a = await HoursAsync(current.From, current.To);
        var b = await HoursAsync(previous.From, previous.To);
        var trend = TrendAnalysis.Compare(a, b, 8m, 15m);
        if (!trend.IsSignificant) return null;
        var up = trend.Direction == TrendDirection.Up;
        return new Insight("downtime", FleetAlertCategory.Maintenance, up ? InsightSentiment.Negative : InsightSentiment.Positive,
            $"Tempo parado em manutenção {(up ? "aumentou" : "diminuiu")} {Pct(trend.ChangePercent!.Value)}",
            $"As ordens de serviço concluídas nos últimos {WindowDays} dias somaram {BrazilianFormat.Number(a, 0)} h de veículo parado, contra {BrazilianFormat.Number(b, 0)} h nos {WindowDays} dias anteriores.",
            "Soma do tempo de indisponibilidade informado nas ordens de serviço concluídas.",
            "/relatorios?relatorio=manutencao", Math.Abs(trend.ChangePercent.Value), trend.Direction);
    }

    private async Task<Insight?> CorrectiveFrequencyAsync((DateOnly From, DateOnly To) current, (DateOnly From, DateOnly To) previous, CancellationToken ct)
    {
        async Task<int> CountAsync(DateOnly from, DateOnly to)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            return await db.WorkOrders.CountAsync(w => w.Type == MaintenanceType.Corrective && w.OpenedAt >= start && w.OpenedAt < end &&
                                                       w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.Rejected, ct);
        }
        var a = await CountAsync(current.From, current.To);
        var b = await CountAsync(previous.From, previous.To);
        var trend = TrendAnalysis.Compare(a, b, 3m, 25m);
        if (!trend.IsSignificant) return null;
        var up = trend.Direction == TrendDirection.Up;
        return new Insight("corrective-frequency", FleetAlertCategory.Maintenance, up ? InsightSentiment.Negative : InsightSentiment.Positive,
            $"Manutenções corretivas {(up ? "aumentaram" : "diminuíram")} {Pct(trend.ChangePercent!.Value)}",
            $"Foram abertas {a} ordens de serviço corretivas nos últimos {WindowDays} dias, contra {b} nos {WindowDays} dias anteriores.",
            "Ordens corretivas abertas (exceto canceladas e rejeitadas).",
            "/ordens-servico", Math.Abs(trend.ChangePercent.Value), trend.Direction);
    }

    private async Task<Insight?> TireReplacementsAsync(DateOnly today, CancellationToken ct)
    {
        async Task<int> CountAsync(DateOnly from, DateOnly to)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            return await db.TireInstallations.CountAsync(i => i.VehicleId != null && !i.IsSpare && i.RemovedAt >= start && i.RemovedAt < end, ct);
        }
        var a = await CountAsync(today.AddDays(-89), today);
        var b = await CountAsync(today.AddDays(-179), today.AddDays(-90));
        var trend = TrendAnalysis.Compare(a, b, 4m, 25m);
        if (!trend.IsSignificant) return null;
        var up = trend.Direction == TrendDirection.Up;
        return new Insight("tire-replacements", FleetAlertCategory.Tires, up ? InsightSentiment.Negative : InsightSentiment.Positive,
            $"Trocas de pneu {(up ? "aumentaram" : "diminuíram")} {Pct(trend.ChangePercent!.Value)} no trimestre",
            $"Foram retirados {a} pneus de veículos nos últimos 90 dias, contra {b} nos 90 dias anteriores.",
            "Remoções de pneus instalados (estepes não contam).",
            "/pneus/relatorios", Math.Abs(trend.ChangePercent.Value), trend.Direction);
    }
}
