using System.Text.Json;
using System.Text.Json.Serialization;
using Fleet.Application.Analytics;
using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Application.Intelligence;
using Fleet.Domain.Authorization;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Tires;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Assistant;

/// <summary>A tool the assistant can call. The schema is the JSON Schema of its input (object with properties/required).</summary>
public sealed record AssistantToolDefinition(string Name, string Description, JsonElement InputSchema);

public sealed record SourceLink(string Label, string Link);

/// <summary>What a tool returned: the facts (serialized to the model) and the screens where the user can check them.</summary>
public sealed record ToolOutcome(string Tool, object Facts, IReadOnlyList<SourceLink> Sources, Guid? FocusVehicleId = null);

public sealed record AssistantPeriod(DateOnly From, DateOnly To, string Label);

public static class AssistantPeriods
{
    public static readonly string[] Keys = ["this_month", "last_month", "last_30_days", "last_90_days", "this_year"];

    public static AssistantPeriod Resolve(string? key, DateOnly today, string fallback = "last_90_days")
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return (Keys.Contains(key) ? key : fallback) switch
        {
            "this_month" => new(monthStart, today, $"neste mês (desde {BrazilianDate(monthStart)})"),
            "last_month" => new(monthStart.AddMonths(-1), monthStart.AddDays(-1), "no mês passado"),
            "last_30_days" => new(today.AddDays(-29), today, "nos últimos 30 dias"),
            "this_year" => new(new DateOnly(today.Year, 1, 1), today, $"neste ano ({today.Year})"),
            _ => new(today.AddDays(-89), today, "nos últimos 90 dias"),
        };
    }

    /// <summary>Labels read after a verb ("gastou … nos últimos 90 dias"). The window of the same length right before (for "increased/decreased" questions).</summary>
    public static AssistantPeriod Previous(AssistantPeriod p)
    {
        var days = p.To.DayNumber - p.From.DayNumber + 1;
        return new(p.From.AddDays(-days), p.From.AddDays(-1), $"nos {days} dias anteriores");
    }

    private static string BrazilianDate(DateOnly d) => d.ToString("dd'/'MM'/'yyyy", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// The assistant's tools (ADR-050). Every number the assistant can say is computed HERE by the same services as the
/// screens, with the caller's permissions — so the AI never sees, and never needs to calculate, anything the user
/// could not see in the app. Percentages and differences are precomputed so the model only has to explain them.
/// </summary>
public sealed class AssistantToolbox(
    IFleetDbContext db, IClock clock, ICurrentUser currentUser, VehicleMetricsService metrics, InsightService insights,
    FleetAlertService alerts, BudgetService budgets, CostAggregationService costs, VehicleHealthService health, FleetReportsService reports)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly string[] RankingMetrics =
        ["total_cost", "cost_per_km", "fuel_cost", "maintenance_cost", "tire_cost", "other_cost", "average_consumption", "km_driven", "downtime_hours", "work_orders", "tire_replacements"];

    private static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);
    private static readonly object PeriodProperty = new
    {
        type = "string", @enum = AssistantPeriods.Keys,
        description = "Período: this_month (mês atual até hoje), last_month, last_30_days, last_90_days (padrão), this_year.",
    };

    public static IReadOnlyList<AssistantToolDefinition> Definitions { get; } =
    [
        new("get_fleet_ranking",
            "Ranking dos veículos ativos por uma métrica no período, com a média da frota e a diferença % de cada veículo para a média. Use para 'qual veículo mais caro', 'pior consumo', 'acima da média', 'mais manutenções', 'maior custo com pneus', 'custo por km'.",
            Schema(new
            {
                type = "object",
                properties = new
                {
                    metric = new { type = "string", @enum = RankingMetrics, description = "Métrica do ranking." },
                    order = new { type = "string", @enum = new[] { "desc", "asc" }, description = "desc = maiores primeiro (padrão); para consumo (km/l), asc = piores primeiro." },
                    period = PeriodProperty,
                    limit = new { type = "integer", minimum = 1, maximum = 10, description = "Quantos veículos (padrão 5)." },
                },
                required = new[] { "metric" },
            })),
        new("get_vehicle_analysis",
            "Análise de um veículo: métricas do período e do período anterior (com variação %), comparação com a média da frota e do mesmo tipo, principal fator de custo, saúde operacional, alertas abertos e problemas recorrentes. Use 'current' para o veículo da tela atual.",
            Schema(new
            {
                type = "object",
                properties = new
                {
                    plate = new { type = "string", description = "Placa (ABC1D23 ou ABC-1234) ou 'current' para o veículo aberto na tela." },
                    period = PeriodProperty,
                },
                required = new[] { "plate" },
            })),
        new("get_fleet_costs",
            "Custos da frota no período por fatia (combustível, manutenção, pneus, outras despesas) e por categoria, comparados com o período anterior de mesma duração, e os veículos com maior custo. Use para 'quanto gastamos', 'onde gastamos mais', 'qual categoria aumentou'.",
            Schema(new { type = "object", properties = new { period = PeriodProperty } })),
        new("get_budget_status",
            "Orçado x realizado do mês atual e do ano: percentual usado e situação de cada linha de orçamento. Use para 'estamos dentro do orçamento?'.",
            Schema(new { type = "object", properties = new { } })),
        new("get_open_alerts",
            "Alertas abertos mais importantes (já priorizados pelo sistema), com explicação. Use para 'o que merece atenção', 'o que investigar primeiro', 'existe algo anormal'.",
            Schema(new
            {
                type = "object",
                properties = new
                {
                    category = new { type = "string", @enum = Enum.GetNames<FleetAlertCategory>(), description = "Filtrar por área (opcional)." },
                    limit = new { type = "integer", minimum = 1, maximum = 10 },
                },
            })),
        new("get_fleet_insights",
            "Destaques calculados pelo sistema: tendências dos últimos 30 dias contra os 30 anteriores (custos, consumo, tempo parado, corretivas, trocas de pneu, concentração de custo).",
            Schema(new { type = "object", properties = new { } })),
        new("get_maintenance_overview",
            "Manutenção no período: veículos com mais ordens de serviço, corretivas, tempo parado e custo de manutenção, problemas recorrentes e preventivas atrasadas.",
            Schema(new { type = "object", properties = new { period = PeriodProperty } })),
        new("get_tire_overview",
            "Pneus: instalados no sulco mínimo ou no aviso (com veículo e posição), sinais de desgaste anormal em aberto e veículos com mais trocas e maior custo de pneus no período.",
            Schema(new { type = "object", properties = new { period = PeriodProperty } })),
        new("get_fuel_overview",
            "Combustível no período: consumo médio da frota (km/l) contra o período anterior, veículos com pior consumo, veículos com consumo fora do próprio padrão e gasto total.",
            Schema(new { type = "object", properties = new { period = PeriodProperty } })),
    ];

    private bool Can(params string[] permissions) => permissions.All(currentUser.HasPermission);
    private bool CanSeeAllCosts => Can([.. AlertAudiences.RequiredPermissions(AlertAudience.FleetCosts)]);

    public async Task<ToolOutcome> ExecuteAsync(string name, JsonElement input, AssistantContext context, CancellationToken ct)
    {
        string? Str(string key) => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        int Int(string key, int fallback, int min, int max) =>
            input.ValueKind == JsonValueKind.Object && input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
                ? Math.Clamp(i, min, max) : fallback;

        return name switch
        {
            "get_fleet_ranking" => await RankingAsync(Str("metric") ?? "total_cost", Str("order") ?? "desc", Str("period"), Int("limit", 5, 1, 10), ct),
            "get_vehicle_analysis" => await VehicleAnalysisAsync(Str("plate"), context, Str("period"), ct),
            "get_fleet_costs" => await FleetCostsAsync(Str("period"), ct),
            "get_budget_status" => await BudgetStatusAsync(ct),
            "get_open_alerts" => await OpenAlertsAsync(Enum.TryParse<FleetAlertCategory>(Str("category"), out var c) ? c : null, Int("limit", 5, 1, 10), ct),
            "get_fleet_insights" => await InsightsAsync(ct),
            "get_maintenance_overview" => await MaintenanceAsync(Str("period"), ct),
            "get_tire_overview" => await TiresAsync(Str("period"), ct),
            "get_fuel_overview" => await FuelAsync(Str("period"), ct),
            _ => new ToolOutcome(name, new { error = "Ferramenta desconhecida." }, []),
        };
    }

    // ---------------- Ranking

    public sealed record RankingRow(string Plate, string Model, string Type, decimal? Value, decimal? VsFleetAveragePercent, string? MainCostDriver);

    public sealed record RankingFacts(
        string Metric, string MetricLabel, string Unit, string Period, string Order, IReadOnlyList<RankingRow> Vehicles,
        decimal? FleetAverage, int VehiclesWithData, int ActiveVehicles, bool PartialCosts, string? Unavailable, string? Note);

    private static (string Label, string Unit, bool Money, Func<VehicleMetrics, decimal?> Value) Metric(string metric) => metric switch
    {
        "cost_per_km" => ("custo por km", "R$/km", true, m => m.CostPerKm),
        "fuel_cost" => ("custo de combustível", "R$", true, m => m.FuelCost),
        "maintenance_cost" => ("custo de manutenção", "R$", true, m => m.MaintenanceCost),
        "tire_cost" => ("custo de pneus", "R$", true, m => m.TireCost),
        "other_cost" => ("outras despesas", "R$", true, m => m.OtherCost),
        "average_consumption" => ("consumo médio", "km/l", false, m => m.AverageConsumption),
        "km_driven" => ("km rodados", "km", false, m => m.KmDriven),
        "downtime_hours" => ("tempo parado em manutenção", "h", false, m => m.DowntimeHours),
        "work_orders" => ("ordens de serviço concluídas", "OS", false, m => m.WorkOrdersCompleted),
        "tire_replacements" => ("trocas de pneu", "pneus", false, m => m.TireReplacements),
        _ => ("custo total", "R$", true, m => m.TotalCost),
    };

    private async Task<ToolOutcome> RankingAsync(string metric, string order, string? periodKey, int limit, CancellationToken ct)
    {
        if (!RankingMetrics.Contains(metric)) metric = "total_cost";
        var (label, unit, money, value) = Metric(metric);
        var period = AssistantPeriods.Resolve(periodKey, clock.Today);
        var sources = new List<SourceLink> { new("Relatório de desempenho da frota", $"/relatorios?de={period.From:yyyy-MM-dd}&ate={period.To:yyyy-MM-dd}") };
        if (money && !Can(Permissions.Finance.ViewCosts))
            return new ToolOutcome("get_fleet_ranking", new RankingFacts(metric, label, unit, period.Label, order, [], null, 0, 0, false,
                "Você não tem permissão para ver valores financeiros; não é possível responder sobre custos.", null), sources);

        var rows = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive), period.From, period.To, ct);
        var withData = rows.Where(r => value(r) is not null && (metric is not ("km_driven" or "total_cost") || value(r) > 0)).ToList();
        decimal? average = withData.Count == 0 ? null : Math.Round(withData.Average(r => value(r)!.Value), 2);
        var sorted = order == "asc" ? withData.OrderBy(r => value(r)) : withData.OrderByDescending(r => value(r));
        var top = sorted.Take(limit).Select(r => new RankingRow(
            LicensePlate.Format(r.LicensePlate), r.Model, r.Type.ToString(), value(r),
            average is > 0 ? Math.Round((value(r)!.Value - average.Value) / average.Value * 100m, 1) : null,
            money ? MainDriver(r) : null)).ToList();
        string? note = metric == "cost_per_km" ? "Custo por km só para veículos com km confiável (leitura de hodômetro antes do período e pelo menos 50 km) e total completo." : null;
        return new ToolOutcome("get_fleet_ranking",
            new RankingFacts(metric, label, unit, period.Label, order, top, average, withData.Count, rows.Count, rows.Any(r => r.IsPartial), null, note),
            sources, top.Count > 0 ? rows.First(r => LicensePlate.Format(r.LicensePlate) == top[0].Plate).VehicleId : null);
    }

    private static string? MainDriver(VehicleMetrics m)
    {
        var slices = new (string Name, decimal? Value)[] { ("combustível", m.FuelCost), ("manutenção", m.MaintenanceCost), ("pneus", m.TireCost), ("outras despesas", m.OtherCost) };
        var best = slices.Where(s => s.Value is > 0).OrderByDescending(s => s.Value).FirstOrDefault();
        return best.Name;
    }

    // ---------------- Vehicle analysis

    public sealed record MetricComparison(string Metric, decimal? Current, decimal? Previous, decimal? ChangePercent, decimal? FleetAverage, decimal? TypeAverage, decimal? VsTypeAveragePercent);

    public sealed record VehicleFacts(
        string Plate, string Model, string Type, string Status, string Period, string PreviousPeriod,
        IReadOnlyList<MetricComparison> Metrics, string? MainCostDriver, string? MainCostDriverExplanation,
        int? HealthScore, string? HealthLevel, IReadOnlyList<string> HealthFactors,
        IReadOnlyList<string> OpenAlerts, IReadOnlyList<string> RecurringProblems, bool PartialCosts, string? Unavailable);

    private async Task<ToolOutcome> VehicleAnalysisAsync(string? plate, AssistantContext context, string? periodKey, CancellationToken ct)
    {
        Guid? vehicleId = null;
        if (string.IsNullOrWhiteSpace(plate) || plate.Equals("current", StringComparison.OrdinalIgnoreCase)) vehicleId = context.VehicleId;
        else
        {
            var normalized = LicensePlate.Normalize(plate);
            vehicleId = await db.Vehicles.Where(v => v.LicensePlate == normalized).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct);
        }
        if (vehicleId is null)
            return new ToolOutcome("get_vehicle_analysis", new { unavailable = "Veículo não encontrado nesta empresa (confira a placa) ou nenhum veículo aberto na tela." }, []);

        var vehicle = await db.Vehicles.Where(v => v.Id == vehicleId).Select(v => new { v.Id, v.LicensePlate, v.Model, v.Type, v.Status }).SingleAsync(ct);
        var period = AssistantPeriods.Resolve(periodKey, clock.Today);
        var previous = AssistantPeriods.Previous(period);
        var current = (await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Id == vehicle.Id), period.From, period.To, ct)).Single();
        var before = (await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Id == vehicle.Id), previous.From, previous.To, ct)).Single();
        var fleet = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive), period.From, period.To, ct);
        var peers = fleet.Where(f => f.Type == vehicle.Type && f.VehicleId != vehicle.Id).ToList();
        var others = fleet.Where(f => f.VehicleId != vehicle.Id).ToList();

        static decimal? Avg(IEnumerable<decimal?> values) { var l = values.Where(v => v.HasValue).Select(v => v!.Value).ToList(); return l.Count == 0 ? null : Math.Round(l.Average(), 2); }
        static decimal? Pct(decimal? a, decimal? b) => a is { } x && b is { } y && y != 0 ? Math.Round((x - y) / y * 100m, 1) : null;

        MetricComparison Compare(string name, Func<VehicleMetrics, decimal?> f)
        {
            var typeAvg = Avg(peers.Select(f));
            return new MetricComparison(name, f(current), f(before), Pct(f(current), f(before)), Avg(others.Select(f)), typeAvg, Pct(f(current), typeAvg));
        }
        var list = new List<MetricComparison>
        {
            Compare("km_rodados", m => m.KmDriven),
            Compare("consumo_km_l", m => m.AverageConsumption),
            Compare("custo_total", m => m.TotalCost),
            Compare("custo_combustivel", m => m.FuelCost),
            Compare("custo_manutencao", m => m.MaintenanceCost),
            Compare("custo_pneus", m => m.TireCost),
            Compare("outras_despesas", m => m.OtherCost),
            Compare("custo_por_km", m => m.CostPerKm),
            Compare("os_concluidas", m => m.WorkOrdersCompleted),
            Compare("tempo_parado_h", m => m.DowntimeHours),
            Compare("trocas_de_pneu", m => m.TireReplacements),
        };
        // The slice that moved most against the previous period explains a cost change better than the largest slice.
        var slices = list.Where(c => c.Metric is "custo_combustivel" or "custo_manutencao" or "custo_pneus" or "outras_despesas" && c.Current is not null).ToList();
        var driver = slices.OrderByDescending(s => (s.Current ?? 0) - (s.Previous ?? 0)).FirstOrDefault();
        string? driverExplanation = driver is null ? null
            : $"{driver.Metric}: {driver.Current} no período contra {driver.Previous ?? 0} no período anterior" + (driver.TypeAverage is { } ta ? $"; média do tipo {ta}" : "") + ".";

        var h = await health.GetAsync(vehicle.Id, ct);
        var openAlerts = (await alerts.ListAsync(new FleetAlertListRequest { VehicleId = vehicle.Id, PageSize = 5 }, ct)).Items.Select(a => $"{a.Title} — {a.Explanation}").ToList();
        var recurring = Can(Permissions.Maintenance.View)
            ? (await reports.RecurringProblemsAsync(new PeriodRequest(period.From, period.To), ct)).Where(r => r.VehicleId == vehicle.Id).Select(r => $"{r.Problem} ({r.Occurrences} vezes)").ToList()
            : [];
        var plateText = LicensePlate.Format(vehicle.LicensePlate);
        var facts = new VehicleFacts(plateText, vehicle.Model, vehicle.Type.ToString(), vehicle.Status.ToString(), period.Label, previous.Label, list,
            driver?.Metric, driverExplanation, h.Score, h.Level.ToString(),
            h.Factors.Where(f => f.Status != HealthFactorStatus.NotVisible).Select(f => $"{f.Area}: {f.Status} — {f.Explanation}").ToList(),
            openAlerts, recurring, current.IsPartial, Can(Permissions.Finance.ViewCosts) ? null : "Sem permissão para valores financeiros: custos omitidos.");
        return new ToolOutcome("get_vehicle_analysis", facts,
            [new($"Veículo {plateText}", $"/veiculos/{vehicle.Id}"), new($"Financeiro de {plateText}", $"/veiculos/{vehicle.Id}?aba=financeiro")], vehicle.Id);
    }

    // ---------------- Fleet costs

    public sealed record SliceFacts(string Slice, decimal Current, decimal Previous, decimal? ChangePercent);
    public sealed record TopVehicleCost(string Plate, decimal TotalCost, decimal SharePercent, string? MainCostDriver);

    private async Task<ToolOutcome> FleetCostsAsync(string? periodKey, CancellationToken ct)
    {
        var period = AssistantPeriods.Resolve(periodKey, clock.Today, "this_month");
        var previous = AssistantPeriods.Previous(period);
        var sources = new List<SourceLink> { new("Painel financeiro", "/financeiro"), new("Relatórios financeiros", "/financeiro/relatorios") };
        if (!Can(Permissions.Finance.ViewCosts))
            return new ToolOutcome("get_fleet_costs", new { unavailable = "Você não tem permissão para ver valores financeiros." }, sources);

        var (nowCats, partial) = await costs.GetFleetCostByCategoryAsync(period.From, period.To, ct);
        var (beforeCats, _) = await costs.GetFleetCostByCategoryAsync(previous.From, previous.To, ct);
        decimal Sum(IReadOnlyList<CategoryCost> cats, string name) => cats.Where(c => c.CategoryName == name).Sum(c => c.Amount);
        static decimal? Pct(decimal a, decimal b) => b > 0 ? Math.Round((a - b) / b * 100m, 1) : null;
        var slices = new[] { "Combustível", "Manutenção", "Pneus" }
            .Select(n => new SliceFacts(n, Sum(nowCats, n), Sum(beforeCats, n), Pct(Sum(nowCats, n), Sum(beforeCats, n)))).ToList();
        var otherNow = nowCats.Sum(c => c.Amount) - slices.Sum(s => s.Current);
        var otherBefore = beforeCats.Sum(c => c.Amount) - slices.Sum(s => s.Previous);
        slices.Add(new SliceFacts("Outras despesas", otherNow, otherBefore, Pct(otherNow, otherBefore)));
        var total = slices.Sum(s => s.Current);
        var totalBefore = slices.Sum(s => s.Previous);

        var byCategory = nowCats.OrderByDescending(c => c.Amount).Take(8).Select(c =>
        {
            var before = beforeCats.Where(b => b.CategoryName == c.CategoryName).Sum(b => b.Amount);
            return new SliceFacts(c.CategoryName, c.Amount, before, Pct(c.Amount, before));
        }).ToList();
        var vehicles = await metrics.ForVehiclesAsync(db.Vehicles, period.From, period.To, ct);
        var topVehicles = vehicles.Where(v => v.TotalCost is > 0).OrderByDescending(v => v.TotalCost).Take(3)
            .Select(v => new TopVehicleCost(LicensePlate.Format(v.LicensePlate), v.TotalCost!.Value,
                total > 0 ? Math.Round(v.TotalCost!.Value / total * 100m, 1) : 0, MainDriver(v))).ToList();

        return new ToolOutcome("get_fleet_costs", new
        {
            period = period.Label, previousPeriod = previous.Label, total, totalPrevious = totalBefore, totalChangePercent = Pct(total, totalBefore),
            slices, categories = byCategory, topVehicles,
            partial, note = partial ? "Totais parciais: você não vê o custo de todas as fontes." : null,
        }, sources);
    }

    private async Task<ToolOutcome> BudgetStatusAsync(CancellationToken ct)
    {
        var sources = new List<SourceLink> { new("Orçamentos", "/financeiro/orcamentos"), new("Orçado x realizado", "/financeiro/relatorios?relatorio=orcado") };
        if (!Can(Permissions.Finance.ViewCosts))
            return new ToolOutcome("get_budget_status", new { unavailable = "Você não tem permissão para ver valores financeiros." }, sources);
        var today = clock.Today;
        var lines = (await budgets.ListVsActualAsync(today.Year, today.Month, ct)).Concat(await budgets.ListVsActualAsync(today.Year, null, ct))
            .Select(l => new
            {
                category = l.Budget.CategoryName, scope = l.Budget.LicensePlate is { } p ? LicensePlate.Format(p) : l.Budget.CostCenterName,
                period = l.Budget.Month is { } m ? $"{m:D2}/{l.Budget.Year}" : $"{l.Budget.Year} (anual)",
                budgeted = l.Budget.Amount, actual = l.Actual, usedPercent = l.UtilizationPercent, status = l.Status.ToString(), partial = l.IsPartial,
            }).ToList();
        return new ToolOutcome("get_budget_status", new
        {
            lines, linesOverBudget = lines.Count(l => l.usedPercent > 100), linesAbove90Percent = lines.Count(l => l.usedPercent >= 90),
            note = lines.Count == 0 ? "Nenhum orçamento cadastrado para o mês atual ou para o ano." : null,
        }, sources);
    }

    private async Task<ToolOutcome> OpenAlertsAsync(FleetAlertCategory? category, int limit, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.Alerts.View))
            return new ToolOutcome("get_open_alerts", new { unavailable = "Você não tem acesso à central de alertas." }, []);
        var page = await alerts.ListAsync(new FleetAlertListRequest { Category = category, PageSize = limit }, ct);
        var summary = await alerts.SummaryAsync(ct);
        return new ToolOutcome("get_open_alerts", new
        {
            openTotal = summary.Open, critical = summary.Critical,
            alerts = page.Items.Select(a => new { a.Title, severity = a.Severity.ToString(), area = a.Category.ToString(), a.Explanation, basis = a.Evidence, a.RecommendedAction, plate = a.LicensePlate is null ? null : LicensePlate.Format(a.LicensePlate) }),
            lastCheckedAt = summary.LastCheckedAt is { } at ? clock.FormatDateTime(at) : null,
        }, [new("Central de alertas", "/alertas")], page.Items.FirstOrDefault()?.VehicleId);
    }

    private async Task<ToolOutcome> InsightsAsync(CancellationToken ct)
    {
        var items = await insights.GetAsync(ct);
        return new ToolOutcome("get_fleet_insights", new
        {
            insights = items.Select(i => new { i.Title, i.Explanation, basis = i.Evidence, sentiment = i.Sentiment.ToString() }),
            note = items.Count == 0 ? "Nenhuma variação relevante nos últimos 30 dias, ou sem permissão para os dados envolvidos." : null,
        }, [new("Painel", "/")]);
    }

    private async Task<ToolOutcome> MaintenanceAsync(string? periodKey, CancellationToken ct)
    {
        if (!Can(Permissions.Maintenance.View))
            return new ToolOutcome("get_maintenance_overview", new { unavailable = "Você não tem acesso à manutenção." }, []);
        var period = AssistantPeriods.Resolve(periodKey, clock.Today);
        var rows = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive), period.From, period.To, ct);
        var recurring = await reports.RecurringProblemsAsync(new PeriodRequest(period.From, period.To), ct);
        var overdue = await db.FleetAlerts.CountAsync(a => a.Trigger == AutomationTrigger.MaintenanceOverdue && FleetAlertWorkflow.OpenStatuses.Contains(a.Status), ct);
        var withOrders = rows.Where(r => r.WorkOrdersCompleted > 0).ToList();
        return new ToolOutcome("get_maintenance_overview", new
        {
            period = period.Label,
            workOrdersCompleted = withOrders.Sum(r => r.WorkOrdersCompleted ?? 0),
            correctiveWorkOrders = withOrders.Sum(r => r.CorrectiveWorkOrders ?? 0),
            downtimeHours = withOrders.Sum(r => r.DowntimeHours ?? 0),
            maintenanceCost = Can(Permissions.Finance.ViewCosts, Permissions.Maintenance.ViewCosts) ? rows.Sum(r => r.MaintenanceCost ?? 0) : (decimal?)null,
            vehiclesWithMostWorkOrders = withOrders.OrderByDescending(r => r.WorkOrdersCompleted).Take(5)
                .Select(r => new { plate = LicensePlate.Format(r.LicensePlate), r.WorkOrdersCompleted, r.CorrectiveWorkOrders, r.DowntimeHours, r.MaintenanceCost }),
            recurringProblems = recurring.Take(10).Select(r => new { plate = LicensePlate.Format(r.LicensePlate), r.Problem, r.Occurrences }),
            overduePreventiveAlerts = overdue,
        }, [new("Relatório de manutenção", "/relatorios?relatorio=manutencao"), new("Ordens de serviço", "/ordens-servico")]);
    }

    private async Task<ToolOutcome> TiresAsync(string? periodKey, CancellationToken ct)
    {
        if (!Can(Permissions.Tires.View))
            return new ToolOutcome("get_tire_overview", new { unavailable = "Você não tem acesso a pneus." }, []);
        var period = AssistantPeriods.Resolve(periodKey, clock.Today);
        var settings = await db.TireSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? TireSettings.Defaults();
        var low = await db.TireInstallations
            .Where(i => i.RemovedAt == null && i.Tire.Status == TireStatus.Installed && i.Tire.CurrentTreadDepthMm != null && i.Tire.CurrentTreadDepthMm <= settings.TreadWarningDepthMm)
            .OrderBy(i => i.Tire.CurrentTreadDepthMm).Take(15)
            .Select(i => new { i.Tire.Code, Tread = i.Tire.CurrentTreadDepthMm, i.PositionLabel, Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement!.LicensePlate })
            .ToListAsync(ct);
        var anomalies = await db.FleetAlerts.Where(a => a.Audience == AlertAudience.Tires && FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .OrderByDescending(a => a.Priority).Take(5).Select(a => a.Title).ToListAsync(ct);
        var openTireAnomalies = await db.TireAnomalies.CountAsync(a => a.ReviewedAt == null, ct);
        var rows = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive), period.From, period.To, ct);
        return new ToolOutcome("get_tire_overview", new
        {
            period = period.Label, warningTreadMm = settings.TreadWarningDepthMm, minimumTreadMm = settings.MinTreadDepthMm,
            tiresNearReplacement = low.Select(t => new { t.Code, treadMm = t.Tread, atMinimum = t.Tread <= settings.MinTreadDepthMm, plate = LicensePlate.Format(t.Plate), position = t.PositionLabel }),
            openTireAlerts = anomalies, tiresRequiringReview = openTireAnomalies,
            vehiclesWithMostReplacements = rows.Where(r => r.TireReplacements > 0).OrderByDescending(r => r.TireReplacements).Take(5)
                .Select(r => new { plate = LicensePlate.Format(r.LicensePlate), r.TireReplacements, r.TireCost }),
            vehiclesWithHighestTireCost = Can(Permissions.Finance.ViewCosts, Permissions.Tires.ViewCosts)
                ? rows.Where(r => r.TireCost > 0).OrderByDescending(r => r.TireCost).Take(5).Select(r => new { plate = LicensePlate.Format(r.LicensePlate), r.TireCost })
                : null,
        }, [new("Painel de pneus", "/pneus/painel"), new("Relatórios de pneus", "/pneus/relatorios")]);
    }

    private async Task<ToolOutcome> FuelAsync(string? periodKey, CancellationToken ct)
    {
        if (!Can(Permissions.Fuel.View))
            return new ToolOutcome("get_fuel_overview", new { unavailable = "Você não tem acesso a combustível." }, []);
        var period = AssistantPeriods.Resolve(periodKey, clock.Today, "last_30_days");
        var previous = AssistantPeriods.Previous(period);
        var rows = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive), period.From, period.To, ct);
        var before = await metrics.ForVehiclesAsync(db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive), previous.From, previous.To, ct);
        static decimal? Fleet(IEnumerable<VehicleMetrics> r) { var l = r.Where(x => x.AverageConsumption.HasValue).Select(x => x.AverageConsumption!.Value).ToList(); return l.Count == 0 ? null : Math.Round(l.Average(), 2); }
        var now = Fleet(rows);
        var then = Fleet(before);
        var abnormal = await db.FleetAlerts.Where(a => a.Trigger == AutomationTrigger.FuelConsumptionAbnormal && FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .Select(a => a.Title).Take(10).ToListAsync(ct);
        var pendingReview = await db.Fuelings.CountAsync(f => f.Status == Domain.Fuel.FuelingStatus.PendingReview, ct);
        return new ToolOutcome("get_fuel_overview", new
        {
            period = period.Label, previousPeriod = previous.Label,
            fleetAverageConsumptionKmL = now, previousFleetAverageConsumptionKmL = then,
            consumptionChangePercent = now is { } a && then is { } b && b > 0 ? Math.Round((a - b) / b * 100m, 1) : (decimal?)null,
            note = "Consumo em km/l: maior é melhor. Média simples dos veículos com trechos medidos (tanque cheio a tanque cheio).",
            worstConsumption = rows.Where(r => r.AverageConsumption.HasValue).OrderBy(r => r.AverageConsumption).Take(5)
                .Select(r => new { plate = LicensePlate.Format(r.LicensePlate), r.Model, kmPerLiter = r.AverageConsumption }),
            highestFuelCost = Can(Permissions.Finance.ViewCosts, Permissions.Fuel.ViewCosts)
                ? rows.Where(r => r.FuelCost > 0).OrderByDescending(r => r.FuelCost).Take(5).Select(r => new { plate = LicensePlate.Format(r.LicensePlate), r.FuelCost, r.FuelQuantity })
                : null,
            fuelCostTotal = Can(Permissions.Finance.ViewCosts, Permissions.Fuel.ViewCosts) ? rows.Sum(r => r.FuelCost ?? 0) : (decimal?)null,
            vehiclesWithAbnormalConsumption = abnormal, fuelingsPendingReview = pendingReview,
        }, [new("Painel de combustível", "/combustivel"), new("Relatórios de combustível", "/combustivel/relatorios")]);
    }
}
