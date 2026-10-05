using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fleet.Domain.Common;

namespace Fleet.Application.Assistant;

public enum AssistantIntent
{
    RankingCost,
    RankingFuelCost,
    RankingConsumption,
    RankingAboveAverage,
    RankingCostPerKm,
    RankingMaintenanceCost,
    RankingWorkOrders,
    RankingTireCost,
    RankingTireReplacements,
    VehicleAnalysis,
    FleetCosts,
    CategoryIncrease,
    Budget,
    Attention,
    Insights,
    Maintenance,
    Tires,
    Fuel,
}

public sealed record ToolCall(string Tool, JsonElement Input);

public sealed record AssistantRoute(AssistantIntent Intent, IReadOnlyList<ToolCall> Calls);

/// <summary>
/// Calculated mode (no AI): recognizes the common questions of spec §4 by keywords and maps them to the same tools
/// the AI would call. Unknown questions get the list of what can be asked — never a guess.
/// </summary>
public static class AssistantRouter
{
    private static readonly Regex Plate = new(@"\b([A-Za-z]{3})-?(\d[A-Za-z0-9]\d{2})\b");

    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static ToolCall Call(string tool, object input) => new(tool, JsonSerializer.SerializeToElement(input));

    public static AssistantRoute? Route(string question, AssistantContext context)
    {
        var q = Normalize(question);
        bool Has(params string[] words) => words.Any(w => q.Contains(w, StringComparison.Ordinal));
        var period = Has("este mes", "neste mes", "do mes", "mes atual", "esse mes") ? "this_month"
            : Has("mes passado", "ultimo mes") ? "last_month"
            : Has("este ano", "neste ano", "do ano") ? "this_year"
            : Has("30 dias", "ultimas semanas") ? "last_30_days" : null;
        AssistantRoute R(AssistantIntent intent, params ToolCall[] calls) => new(intent, calls);
        ToolCall Ranking(string metric, string order = "desc", int limit = 5) => Call("get_fleet_ranking", new { metric, order, period, limit });

        var plate = Plate.Match(question);
        var aboutThisVehicle = context.VehicleId is not null && (Has("este veiculo", "esse veiculo", "deste veiculo", "desse veiculo", "analis", "por que", "porque") || !Has("frota", "veiculos", "qual veiculo", "quais"));
        if (plate.Success) return R(AssistantIntent.VehicleAnalysis, Call("get_vehicle_analysis", new { plate = plate.Value, period }));
        if (aboutThisVehicle) return R(AssistantIntent.VehicleAnalysis, Call("get_vehicle_analysis", new { plate = "current", period }));

        if (Has("pneu"))
        {
            if (Has("custo", "gast", "caro")) return R(AssistantIntent.RankingTireCost, Ranking("tire_cost"));
            if (Has("vida util", "trocas", "troca de pneu", "duram")) return R(AssistantIntent.RankingTireReplacements, Ranking("tire_replacements"));
            return R(AssistantIntent.Tires, Call("get_tire_overview", new { period }));
        }
        if (Has("orcament", "orcado")) return R(AssistantIntent.Budget, Call("get_budget_status", new { }));
        if (Has("custo por km", "custo/km", "custo por quilometro")) return R(AssistantIntent.RankingCostPerKm, Ranking("cost_per_km"));
        if (Has("manuten", "oficina", "conserto", "corretiva", "preventiva", "parado"))
        {
            if (Has("repet", "recorrent", "de novo")) return R(AssistantIntent.Maintenance, Call("get_maintenance_overview", new { period }));
            if (Has("custo", "caro", "gast")) return R(AssistantIntent.RankingMaintenanceCost, Ranking("maintenance_cost"));
            if (Has("mais manuten", "mais ordens", "quantas", "frequen")) return R(AssistantIntent.RankingWorkOrders, Ranking("work_orders"));
            return R(AssistantIntent.Maintenance, Call("get_maintenance_overview", new { period }));
        }
        if (Has("combust", "consum", "abastec", "diesel", "km/l", "litro"))
        {
            if (Has("gasto", "gastamos", "custo", "gastando mais", "valor")) return R(AssistantIntent.RankingFuelCost, Ranking("fuel_cost"));
            if (Has("pior", "mais consum", "consumindo mais", "consomem mais")) return R(AssistantIntent.RankingConsumption, Ranking("average_consumption", "asc"));
            return R(AssistantIntent.Fuel, Call("get_fuel_overview", new { period }));
        }
        if (Has("acima da media", "acima do normal", "mais caros que")) return R(AssistantIntent.RankingAboveAverage, Ranking("total_cost", limit: 10));
        if (Has("categoria") && Has("aument", "subiu", "cresceu")) return R(AssistantIntent.CategoryIncrease, Call("get_fleet_costs", new { period = period ?? "last_30_days" }));
        if (Has("quanto gast", "gastamos", "onde estamos gastando", "maiores custos", "gasto total", "custo da frota", "custos deste", "custos do mes", "explique os custos", "explicar os custos"))
            return R(AssistantIntent.FleetCosts, Call("get_fleet_costs", new { period = period ?? "this_month" }));
        if (Has("custo", "caro", "gastando mais", "mais gasta", "mais dinheiro") && Has("veiculo", "qual", "quais", "carro", "caminhao"))
            return R(AssistantIntent.RankingCost, Ranking("total_cost"));
        if (Has("atencao", "investigar", "prioridade", "urgente", "anormal", "problema", "fazer hoje", "merece"))
            return R(AssistantIntent.Attention, Call("get_open_alerts", new { limit = 5 }));
        if (Has("resumo", "acontecendo", "tendencia", "destaque", "novidade", "como esta a frota", "melhor", "desempenh"))
            return R(AssistantIntent.Insights, Call("get_fleet_insights", new { }), Call("get_open_alerts", new { limit = 3 }));
        if (context.Page == "finance") return R(AssistantIntent.FleetCosts, Call("get_fleet_costs", new { period = period ?? "this_month" }));
        return null;
    }
}

/// <summary>Fixed pt-BR answers over the tool results — the numbers are exactly the tools' numbers.</summary>
public static class AssistantTemplates
{
    private static JsonElement Facts(ToolOutcome o) => JsonSerializer.SerializeToElement(o.Facts, AssistantToolbox.Json);

    private static string? Str(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static decimal? Dec(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
    private static IEnumerable<JsonElement> Arr(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    private static string Cap(string? s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Money(decimal? v) => v is { } d ? BrazilianFormat.Currency(d) : "—";
    private static string Num(decimal? v, int decimals = 0) => v is { } d ? BrazilianFormat.Number(d, decimals) : "—";
    private static string Pct(decimal? v) => v is { } d ? BrazilianFormat.SignedPercent(d) : "";
    private static string Value(decimal? v, string unit) => unit switch
    {
        "R$" => Money(v),
        "R$/km" => v is { } d ? "R$ " + BrazilianFormat.Number(d, 2) + "/km" : "—",
        "km/l" => v is { } c ? BrazilianFormat.Number(c, 2) + " km/l" : "—",
        "h" => v is { } h ? BrazilianFormat.Number(h, 1) + " h" : "—",
        _ => v is { } n ? BrazilianFormat.Number(n) + " " + unit : "—",
    };

    public static AssistantResponse Help() => new(
        AssistantMode.Calculated,
        "Não consegui identificar o que você quer saber. Posso responder sobre custos, consumo, manutenção, pneus, orçamento e alertas da frota.",
        null, [], "Tente uma das perguntas sugeridas ou cite a placa de um veículo (ex.: \"analise o ABC-1D23\").",
        [], [], null, true, null, AssistantService.DefaultSuggestions);

    public static AssistantResponse Render(AssistantRoute route, IReadOnlyList<ToolOutcome> outcomes, bool aiConfigured)
    {
        var first = outcomes[0];
        var f = Facts(first);
        var notice = aiConfigured ? "O provedor de IA não respondeu; esta resposta foi montada pelo sistema com os mesmos cálculos." : null;
        AssistantResponse Build(string answer, string? reason, IEnumerable<string> evidence, string? suggestion, bool insufficient = false) => new(
            AssistantMode.Calculated, answer, reason, evidence.ToList(), suggestion,
            outcomes.SelectMany(o => o.Sources).DistinctBy(s => s.Link).ToList(), outcomes.Select(o => o.Tool).ToList(),
            outcomes.Select(o => o.FocusVehicleId).FirstOrDefault(id => id is not null), insufficient, notice, AssistantService.DefaultSuggestions);

        if (Str(f, "unavailable") is { } unavailable)
            return Build(unavailable, null, [], "Se precisar dessa informação, peça acesso ao administrador do sistema.", true);

        switch (route.Intent)
        {
            case AssistantIntent.VehicleAnalysis:
                return Vehicle(f, Build);
            case AssistantIntent.FleetCosts:
            case AssistantIntent.CategoryIncrease:
                return Costs(f, route.Intent == AssistantIntent.CategoryIncrease, Build);
            case AssistantIntent.Budget:
                return Budget(f, Build);
            case AssistantIntent.Attention:
                return Attention(f, Build);
            case AssistantIntent.Insights:
                return InsightsAnswer(f, outcomes.Count > 1 ? Facts(outcomes[1]) : default, Build);
            case AssistantIntent.Maintenance:
                return Maintenance(f, Build);
            case AssistantIntent.Tires:
                return Tires(f, Build);
            case AssistantIntent.Fuel:
                return Fuel(f, Build);
            default:
                return Ranking(route.Intent, f, Build);
        }
    }

    private delegate AssistantResponse Builder(string answer, string? reason, IEnumerable<string> evidence, string? suggestion, bool insufficient = false);

    private static AssistantResponse Ranking(AssistantIntent intent, JsonElement f, Builder build)
    {
        var label = Str(f, "metricLabel") ?? "";
        var unit = Str(f, "unit") ?? "";
        var period = Str(f, "period") ?? "";
        var avg = Dec(f, "fleetAverage");
        var rows = Arr(f, "vehicles").ToList();
        if (intent == AssistantIntent.RankingAboveAverage) rows = rows.Where(r => Dec(r, "vsFleetAveragePercent") >= 15).ToList();
        if (rows.Count == 0)
            return build(intent == AssistantIntent.RankingAboveAverage
                    ? $"Nenhum veículo está 15% ou mais acima da média de {label} da frota {period}."
                    : $"Não há dados suficientes de {label} {period} para responder.",
                null, [], "Confira se os registros do período (abastecimentos, ordens de serviço, despesas, hodômetro) estão lançados.", intent != AssistantIntent.RankingAboveAverage);

        var top = rows[0];
        var plate = Str(top, "plate");
        var vs = Dec(top, "vsFleetAveragePercent");
        var worse = intent == AssistantIntent.RankingConsumption ? "pior consumo" : null;
        var answer = intent switch
        {
            AssistantIntent.RankingAboveAverage => $"{rows.Count} veículo(s) com custo total 15% ou mais acima da média da frota {period}: {string.Join(", ", rows.Select(r => Str(r, "plate")))}.",
            AssistantIntent.RankingConsumption => $"{plate} tem o {worse} da frota {period}: {Value(Dec(top, "value"), unit)}" + (vs is { } c ? $", {BrazilianFormat.Compact(Math.Abs(c))}% {(c < 0 ? "abaixo" : "acima")} da média ({Value(avg, unit)})." : "."),
            _ => $"{plate}. {Cap(period)}, {label} de {Value(Dec(top, "value"), unit)}" + (vs is { } p ? $", {BrazilianFormat.Compact(Math.Abs(p))}% {(p >= 0 ? "acima" : "abaixo")} da média da frota ({Value(avg, unit)})." : "."),
        };
        var driver = Str(top, "mainCostDriver");
        var reason = $"Comparação dos {Num(Dec(f, "vehiclesWithData"))} veículos ativos com dado de {label} no período." +
                     (driver is not null && unit == "R$" ? $" No {plate}, a maior parte do custo foi {driver}." : "") +
                     (Str(f, "note") is { } note ? " " + note : "");
        var evidence = rows.Take(5).Select(r => $"{Str(r, "plate")} ({Str(r, "model")}): {Value(Dec(r, "value"), unit)}" +
                                               (Dec(r, "vsFleetAveragePercent") is { } d ? $" — {Pct(d)} em relação à média" : "")).ToList();
        evidence.Add($"Média da frota: {Value(avg, unit)}.");
        if (f.TryGetProperty("partialCosts", out var partial) && partial.GetBoolean()) evidence.Add("Totais parciais: você não vê o custo de todas as fontes.");
        var suggestion = intent switch
        {
            AssistantIntent.RankingConsumption => "Confira os abastecimentos, a calibragem dos pneus e a rota/carga desse veículo; se persistir, peça avaliação mecânica.",
            AssistantIntent.RankingWorkOrders or AssistantIntent.RankingMaintenanceCost => "Veja o histórico de manutenção do veículo e se há problemas recorrentes (Relatórios › Manutenção).",
            AssistantIntent.RankingTireCost or AssistantIntent.RankingTireReplacements => "Verifique alinhamento, calibragem e a posição dos pneus desse veículo; compare marcas e recapagens no relatório de pneus.",
            _ => "Abra a aba Financeiro do veículo para ver os lançamentos que compõem o custo e compare com veículos do mesmo tipo.",
        };
        return build(answer, reason, evidence, suggestion);
    }

    private static readonly Dictionary<string, string> MetricNames = new()
    {
        ["km_rodados"] = "Km rodados", ["consumo_km_l"] = "Consumo (km/l)", ["custo_total"] = "Custo total", ["custo_combustivel"] = "Combustível",
        ["custo_manutencao"] = "Manutenção", ["custo_pneus"] = "Pneus", ["outras_despesas"] = "Outras despesas", ["custo_por_km"] = "Custo por km",
        ["os_concluidas"] = "OS concluídas", ["tempo_parado_h"] = "Tempo parado (h)", ["trocas_de_pneu"] = "Trocas de pneu",
    };

    private static AssistantResponse Vehicle(JsonElement f, Builder build)
    {
        var plate = Str(f, "plate");
        var period = Str(f, "period");
        var metrics = Arr(f, "metrics").ToDictionary(m => Str(m, "metric")!, m => m);
        string Fmt(string key, decimal? v) => key switch
        {
            "custo_total" or "custo_combustivel" or "custo_manutencao" or "custo_pneus" or "outras_despesas" => Money(v),
            "custo_por_km" => v is { } d ? "R$ " + BrazilianFormat.Number(d, 2) + "/km" : "—",
            "consumo_km_l" => v is { } c ? BrazilianFormat.Number(c, 2) + " km/l" : "—",
            "tempo_parado_h" => v is { } h ? BrazilianFormat.Number(h, 1) + " h" : "—",
            "km_rodados" => v is { } k ? BrazilianFormat.Number(k) + " km" : "—",
            _ => Num(v),
        };
        var total = metrics.GetValueOrDefault("custo_total");
        var score = Dec(f, "healthScore");
        string answer;
        if (total.ValueKind == JsonValueKind.Object && Dec(total, "current") is { } cost)
        {
            var change = Dec(total, "changePercent");
            var vsType = Dec(total, "vsTypeAveragePercent");
            answer = $"{plate}: custo total de {Money(cost)} {period}" +
                     (change is { } c ? $", {BrazilianFormat.Compact(Math.Abs(c))}% {(c >= 0 ? "acima" : "abaixo")} do período anterior" : "") +
                     (vsType is { } t ? $" e {BrazilianFormat.Compact(Math.Abs(t))}% {(t >= 0 ? "acima" : "abaixo")} da média do mesmo tipo" : "") + ".";
        }
        else
        {
            var km = metrics.GetValueOrDefault("km_rodados");
            answer = $"{plate}: {Fmt("km_rodados", km.ValueKind == JsonValueKind.Object ? Dec(km, "current") : null)} rodados {period}.";
        }
        if (score is { } s) answer += $" Saúde operacional {Num(s)}/100.";

        var driver = Str(f, "mainCostDriver");
        var reason = driver is not null && MetricNames.TryGetValue(driver, out var dn) && metrics.TryGetValue(driver, out var dm) && Dec(dm, "current") is { } dc
            ? $"A fatia que mais variou foi {dn.ToLowerInvariant()}: {Money(dc)} no período contra {Money(Dec(dm, "previous") ?? 0)} no anterior" +
              (Dec(dm, "typeAverage") is { } ta ? $" (média do tipo: {Money(ta)})." : ".")
            : "Comparação do veículo com o período anterior de mesma duração e com a média dos veículos do mesmo tipo.";
        var evidence = metrics.Where(kv => kv.Value.TryGetProperty("current", out var cur) && cur.ValueKind == JsonValueKind.Number)
            .Select(kv => $"{MetricNames.GetValueOrDefault(kv.Key, kv.Key)}: {Fmt(kv.Key, Dec(kv.Value, "current"))}" +
                          (Dec(kv.Value, "previous") is { } p ? $" (anterior {Fmt(kv.Key, p)})" : "") +
                          (Dec(kv.Value, "typeAverage") is { } t ? $" · média do tipo {Fmt(kv.Key, t)}" : ""))
            .ToList();
        evidence.AddRange(Arr(f, "healthFactors").Select(h => h.GetString()!).Where(h => !h.Contains(": Good", StringComparison.Ordinal)).Take(3));
        var alerts = Arr(f, "openAlerts").Select(a => a.GetString()!).ToList();
        var recurring = Arr(f, "recurringProblems").Select(a => a.GetString()!).ToList();
        evidence.AddRange(recurring.Select(r => "Problema recorrente: " + r));
        var suggestion = alerts.Count > 0 ? $"Comece pelo alerta aberto: {alerts[0]}"
            : recurring.Count > 0 ? "Investigue o problema recorrente com a oficina antes da próxima ordem de serviço."
            : "Nenhum alerta aberto para este veículo; acompanhe os próximos lançamentos.";
        if (Str(f, "unavailable") is { } u) evidence.Add(u);
        return build(answer, reason, evidence, suggestion);
    }

    private static AssistantResponse Costs(JsonElement f, bool categoryIncrease, Builder build)
    {
        var period = Str(f, "period");
        var slices = Arr(f, "slices").ToList();
        var categories = Arr(f, "categories").ToList();
        var total = Dec(f, "total");
        var change = Dec(f, "totalChangePercent");
        var evidence = slices.Select(s => $"{Str(s, "slice")}: {Money(Dec(s, "current"))} (anterior {Money(Dec(s, "previous"))}{(Dec(s, "changePercent") is { } c ? ", " + Pct(c) : "")})").ToList();
        evidence.AddRange(Arr(f, "topVehicles").Select(v => $"{Str(v, "plate")}: {Money(Dec(v, "totalCost"))} ({Num(Dec(v, "sharePercent"), 1)}% do total)"));
        if (Str(f, "note") is { } note) evidence.Add(note);

        if (categoryIncrease)
        {
            var up = categories.Where(c => Dec(c, "changePercent") > 0).OrderByDescending(c => Dec(c, "current") - Dec(c, "previous")).FirstOrDefault();
            if (up.ValueKind != JsonValueKind.Object)
                return build($"Nenhuma categoria aumentou {period} em relação ao período anterior.", null, evidence, null);
            return build($"{Str(up, "slice")} foi a categoria que mais aumentou: {Money(Dec(up, "current"))} {period}, {Pct(Dec(up, "changePercent"))} em relação ao período anterior de mesma duração.",
                "Comparação de cada categoria com o período anterior de mesma duração (maior aumento em reais).", evidence,
                "Abra a lista de despesas e os relatórios financeiros filtrando essa categoria para ver o que mudou.");
        }
        var biggest = slices.OrderByDescending(s => Dec(s, "current")).FirstOrDefault();
        return build($"{Cap(period)} a frota gastou {Money(total)}" + (change is { } c ? $" ({Pct(c)} em relação ao período anterior de mesma duração)." : "."),
            biggest.ValueKind == JsonValueKind.Object ? $"A maior fatia foi {Str(biggest, "slice")?.ToLowerInvariant()}, com {Money(Dec(biggest, "current"))}." : null,
            evidence, "Veja o painel financeiro e o ranking de veículos para entender quem puxou o custo.");
    }

    private static AssistantResponse Budget(JsonElement f, Builder build)
    {
        var lines = Arr(f, "lines").ToList();
        if (lines.Count == 0) return build(Str(f, "note") ?? "Não há orçamentos cadastrados.", null, [], "Cadastre orçamentos em Configurações › Orçamentos para acompanhar o realizado.", true);
        var over = lines.Where(l => Dec(l, "usedPercent") > 100).ToList();
        var near = lines.Where(l => Dec(l, "usedPercent") is >= 90 and <= 100).ToList();
        var answer = over.Count == 0
            ? $"Sim. Nenhuma das {lines.Count} linhas de orçamento passou do limite" + (near.Count > 0 ? $"; {near.Count} já usaram 90% ou mais." : ".")
            : $"Não totalmente: {over.Count} de {lines.Count} linhas de orçamento passaram do limite.";
        var evidence = lines.OrderByDescending(l => Dec(l, "usedPercent")).Take(6)
            .Select(l => $"{Str(l, "category")}{(Str(l, "scope") is { } s ? " · " + s : "")} ({Str(l, "period")}): {Num(Dec(l, "usedPercent"), 1)}% — {Money(Dec(l, "actual"))} de {Money(Dec(l, "budgeted"))}");
        return build(answer, "Realizado de cada linha de orçamento do mês atual e do ano, calculado a partir dos lançamentos de cada módulo.", evidence,
            over.Count > 0 ? "Revise os lançamentos das categorias acima do orçamento e ajuste o orçamento se o gasto for justificado." : "Acompanhe as linhas acima de 90% até o fim do período.");
    }

    private static AssistantResponse Attention(JsonElement f, Builder build)
    {
        var alerts = Arr(f, "alerts").ToList();
        if (alerts.Count == 0) return build("Nenhum alerta em aberto no momento.", "As regras de automação conferem a frota a cada hora.", [], null);
        var top = alerts[0];
        return build($"Há {Num(Dec(f, "openTotal"))} alerta(s) em aberto ({Num(Dec(f, "critical"))} crítico(s)). O mais importante agora: {Str(top, "title")}.",
            Str(top, "explanation"),
            alerts.Select(a => $"[{Severity(Str(a, "severity"))}] {Str(a, "title")} — {Str(a, "basis")}"),
            Str(top, "recommendedAction"));
    }

    private static string Severity(string? s) => s switch { "Critical" => "Crítico", "Warning" => "Atenção", _ => "Informativo" };

    private static AssistantResponse InsightsAnswer(JsonElement f, JsonElement alerts, Builder build)
    {
        var items = Arr(f, "insights").ToList();
        var evidence = items.Select(i => $"{Str(i, "title")}: {Str(i, "basis")}").ToList();
        if (alerts.ValueKind == JsonValueKind.Object)
            evidence.AddRange(Arr(alerts, "alerts").Select(a => $"Alerta: {Str(a, "title")}"));
        if (items.Count == 0)
            return build("Nenhuma variação relevante nos últimos 30 dias em relação aos 30 anteriores.", Str(f, "note"), evidence, "Veja a área \"Requer atenção\" do painel para as pendências do dia.");
        return build(Str(items[0], "title") + ".", Str(items[0], "explanation"), evidence, "Clique nos destaques do painel para abrir os registros envolvidos.");
    }

    private static AssistantResponse Maintenance(JsonElement f, Builder build)
    {
        var recurring = Arr(f, "recurringProblems").ToList();
        var most = Arr(f, "vehiclesWithMostWorkOrders").ToList();
        var answer = recurring.Count > 0
            ? $"Há {recurring.Count} problema(s) se repetindo {Str(f, "period")}. O mais frequente: {Str(recurring[0], "problem")} no {Str(recurring[0], "plate")} ({Num(Dec(recurring[0], "occurrences"))} vezes)."
            : $"Nenhum problema corretivo se repetiu no mesmo veículo {Str(f, "period")}.";
        var evidence = new List<string>
        {
            $"OS concluídas: {Num(Dec(f, "workOrdersCompleted"))} ({Num(Dec(f, "correctiveWorkOrders"))} corretivas); tempo parado: {Num(Dec(f, "downtimeHours"), 1)} h.",
            $"Preventivas atrasadas (alertas abertos): {Num(Dec(f, "overduePreventiveAlerts"))}.",
        };
        if (Dec(f, "maintenanceCost") is { } c) evidence.Add($"Custo de manutenção: {Money(c)}.");
        evidence.AddRange(most.Select(m => $"{Str(m, "plate")}: {Num(Dec(m, "workOrdersCompleted"))} OS, {Num(Dec(m, "downtimeHours"), 1)} h parado"));
        evidence.AddRange(recurring.Skip(1).Take(4).Select(r => $"Recorrente: {Str(r, "problem")} · {Str(r, "plate")} ({Num(Dec(r, "occurrences"))}x)"));
        return build(answer, "Ordens de serviço concluídas no período; recorrente = o mesmo serviço corretivo feito 2+ vezes no mesmo veículo.", evidence,
            recurring.Count > 0 ? "Discuta com a oficina a causa do problema recorrente (peça, diagnóstico ou uso) antes de repetir o reparo." : "Mantenha as preventivas em dia para reduzir corretivas.");
    }

    private static AssistantResponse Tires(JsonElement f, Builder build)
    {
        var low = Arr(f, "tiresNearReplacement").ToList();
        var atMin = low.Count(t => t.TryGetProperty("atMinimum", out var m) && m.GetBoolean());
        var answer = low.Count == 0
            ? "Nenhum pneu instalado está no limite de aviso de sulco configurado pela empresa."
            : $"{low.Count} pneu(s) instalado(s) no limite de aviso de sulco, {atMin} já no mínimo para substituição.";
        var evidence = low.Take(8).Select(t => $"{Str(t, "code")} · {Str(t, "plate")} ({Str(t, "position")}): {Num(Dec(t, "treadMm"), 1)} mm").ToList();
        evidence.Add($"Limites da empresa: aviso {Num(Dec(f, "warningTreadMm"), 1)} mm; mínimo {Num(Dec(f, "minimumTreadMm"), 1)} mm.");
        evidence.AddRange(Arr(f, "openTireAlerts").Select(a => "Alerta: " + a.GetString()));
        if (Dec(f, "tiresRequiringReview") is > 0 and var r) evidence.Add($"{Num(r)} sinal(is) de \"requer revisão\" (desgaste/anomalia) em aberto.");
        return build(answer, "Sulco da última medição de cada pneu instalado comparado aos limites configurados em Configurações › Pneus.", evidence,
            atMin > 0 ? "Programe a substituição ou recapagem dos pneus no mínimo." : "Programe a inspeção dos pneus no limite de aviso.");
    }

    private static AssistantResponse Fuel(JsonElement f, Builder build)
    {
        var now = Dec(f, "fleetAverageConsumptionKmL");
        var change = Dec(f, "consumptionChangePercent");
        var abnormal = Arr(f, "vehiclesWithAbnormalConsumption").Select(a => a.GetString()!).ToList();
        var answer = now is null ? $"Não há trechos de consumo medidos {Str(f, "period")}."
            : $"A frota fez em média {BrazilianFormat.Number(now.Value, 2)} km/l {Str(f, "period")}" +
              (change is { } c ? $", {BrazilianFormat.Compact(Math.Abs(c))}% {(c >= 0 ? "melhor" : "pior")} que no período anterior de mesma duração." : ".");
        var evidence = Arr(f, "worstConsumption").Select(w => $"{Str(w, "plate")} ({Str(w, "model")}): {Num(Dec(w, "kmPerLiter"), 2)} km/l").ToList();
        evidence.AddRange(abnormal.Select(a => "Fora do padrão do próprio veículo: " + a));
        if (Dec(f, "fuelCostTotal") is { } total) evidence.Add($"Gasto com combustível no período: {Money(total)}.");
        if (Dec(f, "fuelingsPendingReview") is > 0 and var p) evidence.Add($"{Num(p)} abastecimento(s) aguardando revisão.");
        return build(answer, Str(f, "note"), evidence,
            abnormal.Count > 0 ? "Comece pelos veículos com consumo fora do próprio padrão: confira abastecimentos, pneus e rota." : "Acompanhe os veículos com pior consumo no relatório de combustível.");
    }
}
