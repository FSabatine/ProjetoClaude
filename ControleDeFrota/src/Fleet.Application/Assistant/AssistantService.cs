using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fleet.Application.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Fleet.Application.Assistant;

/// <summary>Where the question was asked from — so "analise este veículo" needs no plate.</summary>
public sealed record AssistantContext(Guid? VehicleId = null, string? Page = null);

public sealed record AssistantRequest
{
    public string? Question { get; init; }
    public Guid? VehicleId { get; init; }
    /// <summary>"dashboard", "vehicle", "finance", "maintenance", "fuel", "tires", "alerts"…</summary>
    public string? Page { get; init; }
}

public enum AssistantMode
{
    /// <summary>Fixed templates over the tool results (no AI provider configured, or it failed).</summary>
    Calculated,
    /// <summary>Claude wrote the text from the tool results (numbers still computed by the system).</summary>
    AiExplained,
}

public sealed record AssistantResponse(
    AssistantMode Mode, string Answer, string? Reason, IReadOnlyList<string> Evidence, string? SuggestedAction,
    IReadOnlyList<SourceLink> Sources, IReadOnlyList<string> ToolsUsed, Guid? FocusVehicleId, bool InsufficientData,
    string? Notice, IReadOnlyList<string> Suggestions);

public sealed class AssistantRequestValidator : AbstractValidator<AssistantRequest>
{
    public const int MaxLength = 500;

    public AssistantRequestValidator()
    {
        RuleFor(x => x.Question).Required("Pergunta").MaxLen(MaxLength);
        RuleFor(x => x.Page).MaxLen(30);
    }
}

/// <summary>The language model behind the "AI explained" mode (ADR-050). Infrastructure implements it with Claude.</summary>
public interface IAssistantLanguageModel
{
    bool IsConfigured { get; }

    /// <summary>
    /// Runs the tool loop: the model may call the tools (executed by <paramref name="executeTool"/>, with the caller's
    /// permissions) and returns its final text. Returns null when the provider refused or failed — the caller falls back.
    /// </summary>
    Task<string?> AnswerAsync(string systemPrompt, string userMessage, IReadOnlyList<AssistantToolDefinition> tools,
        Func<string, JsonElement, CancellationToken, Task<string>> executeTool, CancellationToken ct);
}

/// <summary>Used when no provider is configured — the assistant answers with the calculated templates only.</summary>
public sealed class NoLanguageModel : IAssistantLanguageModel
{
    public bool IsConfigured => false;

    public Task<string?> AnswerAsync(string systemPrompt, string userMessage, IReadOnlyList<AssistantToolDefinition> tools,
        Func<string, JsonElement, CancellationToken, Task<string>> executeTool, CancellationToken ct) => Task.FromResult<string?>(null);
}

/// <summary>
/// Fleet assistant (spec §3–§7, ADR-050). The SYSTEM computes every number through <see cref="AssistantToolbox"/>
/// (same services, same permissions as the screens); the AI — when configured — only chooses the tools and explains
/// their results. Without a provider (or if it fails/refuses) the same tools answer through fixed templates.
/// </summary>
public sealed class AssistantService(
    AssistantToolbox toolbox, IAssistantLanguageModel model, IValidator<AssistantRequest> validator, ILogger<AssistantService> logger)
{
    public const int MaxToolCalls = 6;

    public static readonly IReadOnlyList<string> DefaultSuggestions =
    [
        "Qual veículo está gerando mais custo?",
        "O que merece atenção agora?",
        "Quanto gastamos este mês?",
        "Quais veículos estão consumindo mais combustível?",
        "Quais pneus estão próximos de substituição?",
        "Estamos dentro do orçamento?",
    ];

    public const string SystemPrompt = """
        Você é o assistente de análise de um sistema de gestão de frotas. Responda em português do Brasil, de forma curta e clara, para um gestor de frota sem conhecimento técnico.

        Regras obrigatórias:
        - Use SOMENTE os dados devolvidos pelas ferramentas. Nunca invente veículos, valores, datas ou causas.
        - Não faça contas: os totais, médias e percentuais já vêm calculados nas ferramentas. Repita os números como vieram (arredondar é permitido).
        - Se a ferramenta disser que falta permissão ou dado ("unavailable", lista vazia, "note"), diga isso claramente e não especule.
        - Linguagem neutra: fale em "requer revisão" ou "merece análise", nunca em fraude, culpa ou erro de pessoas.
        - Você só explica e sugere. A decisão é sempre do gestor.
        - Responda exatamente nestas seções, cada uma começando na própria linha:
        Resposta: (uma ou duas frases com a conclusão)
        Motivo: (por que o sistema chegou a isso)
        Evidências:
        - (um número por linha, com período e comparação)
        Sugestão: (o que verificar ou fazer a seguir)
        """;

    public async Task<AssistantResponse> AskAsync(AssistantRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var question = request.Question!.Trim();
        var context = new AssistantContext(request.VehicleId, request.Page);
        var outcomes = new List<ToolOutcome>();

        if (model.IsConfigured)
        {
            try
            {
                var text = await model.AnswerAsync(SystemPrompt, BuildUserMessage(question, context), AssistantToolbox.Definitions,
                    async (name, input, token) =>
                    {
                        if (outcomes.Count >= MaxToolCalls) return JsonSerializer.Serialize(new { error = "Limite de consultas atingido nesta pergunta." });
                        var outcome = await toolbox.ExecuteAsync(name, input, context, token);
                        outcomes.Add(outcome);
                        return JsonSerializer.Serialize(outcome.Facts, AssistantToolbox.Json);
                    }, ct);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Only tool names and counts are logged — never the question or the data (may be personal/financial).
                    logger.LogInformation("Assistant answered with AI using tools {Tools}", string.Join(",", outcomes.Select(o => o.Tool)));
                    return FromModelText(text, outcomes);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Assistant AI provider failed; answering with calculated templates");
            }
            outcomes.Clear();
        }

        var route = AssistantRouter.Route(question, context);
        if (route is null) return AssistantTemplates.Help();
        foreach (var call in route.Calls)
            outcomes.Add(await toolbox.ExecuteAsync(call.Tool, call.Input, context, ct));
        return AssistantTemplates.Render(route, outcomes, model.IsConfigured);
    }

    private static string BuildUserMessage(string question, AssistantContext context)
    {
        var where = context.Page switch
        {
            "vehicle" when context.VehicleId is not null => "A pergunta foi feita na página de um veículo: use plate='current' para analisá-lo.",
            "finance" => "A pergunta foi feita no painel financeiro.",
            "maintenance" => "A pergunta foi feita em uma tela de manutenção.",
            "fuel" => "A pergunta foi feita em uma tela de combustível.",
            "tires" => "A pergunta foi feita em uma tela de pneus.",
            _ => null,
        };
        // The question is user content, kept apart from the operator rules in the system prompt.
        return (where is null ? "" : where + "\n\n") + "Pergunta do usuário:\n" + question;
    }

    private static readonly Regex Section = new(@"^\s*(Resposta|Motivo|Evid[êe]ncias|Sugest[ãa]o)\s*:\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>Splits the model text into the four sections and checks its numbers against the tool results.</summary>
    public static AssistantResponse FromModelText(string text, IReadOnlyList<ToolOutcome> outcomes)
    {
        string answer = "", reason = "", suggestion = "";
        var evidence = new List<string>();
        string? current = null;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim().Trim('*').Trim();
            var m = Section.Match(line);
            if (m.Success)
            {
                current = m.Groups[1].Value.ToLowerInvariant()[..3];
                var rest = m.Groups[2].Value.Trim();
                if (rest.Length > 0) Append(current, rest);
                continue;
            }
            if (line.Length > 0) Append(current ?? "res", line);
        }
        void Append(string section, string line)
        {
            switch (section)
            {
                case "evi": evidence.Add(line.TrimStart('-', '•', ' ')); break;
                case "mot": reason = (reason + " " + line).Trim(); break;
                case "sug": suggestion = (suggestion + " " + line).Trim(); break;
                default: answer = (answer + " " + line).Trim(); break;
            }
        }

        var facts = string.Join("\n", outcomes.Select(o => JsonSerializer.Serialize(o.Facts, AssistantToolbox.Json)));
        var unsupported = GroundingCheck.UnsupportedNumbers(text, facts);
        var insufficient = outcomes.Count == 0 || facts.Contains("\"unavailable\"", StringComparison.Ordinal);
        return new AssistantResponse(
            AssistantMode.AiExplained, answer.Length > 0 ? answer : text.Trim(), NullIfEmpty(reason), evidence, NullIfEmpty(suggestion),
            outcomes.SelectMany(o => o.Sources).DistinctBy(s => s.Link).ToList(), outcomes.Select(o => o.Tool).Distinct().ToList(),
            outcomes.Select(o => o.FocusVehicleId).FirstOrDefault(id => id is not null), insufficient,
            unsupported.Count > 0
                ? $"Atenção: {unsupported.Count} número(s) desta resposta não aparecem nos dados consultados ({string.Join(", ", unsupported.Take(3))}). Confira nas telas de origem."
                : null,
            DefaultSuggestions);
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}

/// <summary>
/// Hallucination guard for the AI mode (spec §31): every number in the answer must exist in the tool results
/// (tolerating rounding). Dates, plates and small counts/day numbers are ignored.
/// </summary>
public static class GroundingCheck
{
    private static readonly Regex Dates = new(@"\b\d{1,2}/\d{1,2}(/\d{2,4})?\b");
    private static readonly Regex Plates = new(@"\b[A-Za-z]{3}-?\d[A-Za-z0-9]\d{2}\b");
    private static readonly Regex Numbers = new(@"(?<![\w.,])\d{1,3}(?:\.\d{3})+(?:,\d+)?|(?<![\w.,])\d+(?:,\d+)?");
    private static readonly Regex JsonNumbers = new(@"-?\d+(?:\.\d+)?");

    public static IReadOnlyList<string> UnsupportedNumbers(string answer, string factsJson)
    {
        var known = JsonNumbers.Matches(factsJson)
            .Select(m => decimal.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? Math.Abs(d) : (decimal?)null)
            .OfType<decimal>().ToHashSet();
        var text = Plates.Replace(Dates.Replace(answer, " "), " ");
        var result = new List<string>();
        foreach (Match m in Numbers.Matches(text))
        {
            var value = decimal.Parse(m.Value.Replace(".", "").Replace(',', '.'), CultureInfo.InvariantCulture);
            var isPercent = text.AsSpan(m.Index + m.Length).TrimStart().StartsWith("%");
            if (value <= 31 && !m.Value.Contains(',') && !isPercent) continue; // counts, days, small integers
            if (value is >= 2000 and <= 2100 && !m.Value.Contains(',') && !m.Value.Contains('.')) continue; // years
            if (!known.Any(k => Close(k, value))) result.Add(m.Value);
        }
        return result.Distinct().ToList();
    }

    private static bool Close(decimal known, decimal said) =>
        Math.Abs(known - said) <= Math.Max(0.6m, Math.Abs(known) * 0.006m);
}
