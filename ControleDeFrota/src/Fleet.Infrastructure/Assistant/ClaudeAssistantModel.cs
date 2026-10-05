using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Fleet.Application.Assistant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Infrastructure.Assistant;

public sealed class AssistantAiOptions
{
    public const string SectionName = "Assistant:Anthropic";

    /// <summary>Off by default: no fleet data leaves the server until an administrator turns this on.</summary>
    public bool Enabled { get; set; }
    /// <summary>Prefer the ANTHROPIC_API_KEY environment variable or a secret store — never commit a key.</summary>
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxToolRounds { get; set; } = 4;
    public int MaxTokens { get; set; } = 4096;

    public string? ResolvedApiKey => string.IsNullOrWhiteSpace(ApiKey) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : ApiKey;
}

/// <summary>
/// Claude behind the assistant (ADR-050). Manual tool loop on the Messages API: Claude picks tools, the app runs them
/// with the caller's permissions and returns the computed facts, Claude writes the explanation. Effort "low" (a short
/// explanation, not open-ended reasoning). System prompt and tools are frozen, so they are cached across questions.
/// Any refusal, error or timeout returns null and the service answers with its calculated templates instead.
/// </summary>
public sealed class ClaudeAssistantModel(IOptions<AssistantAiOptions> options, ILogger<ClaudeAssistantModel> logger) : IAssistantLanguageModel
{
    private readonly AssistantAiOptions _options = options.Value;
    private AnthropicClient? _client;

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ResolvedApiKey);

    private AnthropicClient Client => _client ??= new AnthropicClient
    {
        ApiKey = _options.ResolvedApiKey,
        Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 300)),
        MaxRetries = 1,
    };

    public async Task<string?> AnswerAsync(string systemPrompt, string userMessage, IReadOnlyList<AssistantToolDefinition> tools,
        Func<string, JsonElement, CancellationToken, Task<string>> executeTool, CancellationToken ct)
    {
        var toolUnions = tools.Select((t, i) => (ToolUnion)new Tool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = new()
            {
                Properties = t.InputSchema.TryGetProperty("properties", out var props)
                    ? props.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                    : new Dictionary<string, JsonElement>(),
                Required = t.InputSchema.TryGetProperty("required", out var req) ? req.EnumerateArray().Select(r => r.GetString()!).ToList() : [],
            },
            // Breakpoint on the last tool: tools + system prompt form a stable cached prefix.
            CacheControl = i == tools.Count - 1 ? new CacheControlEphemeral() : null,
        }).ToList();

        List<MessageParam> messages = [new() { Role = Role.User, Content = userMessage }];
        try
        {
            for (var round = 0; round <= _options.MaxToolRounds; round++)
            {
                var response = await Client.Messages.Create(new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokens,
                    System = new List<TextBlockParam> { new() { Text = systemPrompt, CacheControl = new CacheControlEphemeral() } },
                    Tools = toolUnions,
                    OutputConfig = new OutputConfig { Effort = Effort.Low },
                    Messages = messages,
                }, cancellationToken: ct);

                if (response.StopReason == "refusal")
                {
                    logger.LogWarning("Assistant model declined the request");
                    return null;
                }

                List<ContentBlockParam> assistantContent = [];
                List<ContentBlockParam> toolResults = [];
                var text = new List<string>();
                foreach (var block in response.Content)
                {
                    if (block.TryPickText(out TextBlock? t))
                    {
                        text.Add(t.Text);
                        assistantContent.Add(new TextBlockParam { Text = t.Text });
                    }
                    else if (block.TryPickThinking(out ThinkingBlock? thinking))
                    {
                        assistantContent.Add(new ThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
                    }
                    else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
                    {
                        assistantContent.Add(new RedactedThinkingBlockParam { Data = redacted.Data });
                    }
                    else if (block.TryPickToolUse(out ToolUseBlock? use))
                    {
                        assistantContent.Add(new ToolUseBlockParam { ID = use.ID, Name = use.Name, Input = use.Input });
                        var input = JsonSerializer.SerializeToElement(use.Input);
                        string result;
                        try
                        {
                            result = await executeTool(use.Name, input, ct);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "Assistant tool {Tool} failed", use.Name);
                            result = JsonSerializer.Serialize(new { error = "A consulta falhou; responda sem esse dado e avise o usuário." });
                        }
                        // One result per tool_use, all in a single user message.
                        toolResults.Add(new ToolResultBlockParam { ToolUseID = use.ID, Content = result });
                    }
                }

                if (toolResults.Count == 0)
                {
                    logger.LogInformation("Assistant model finished after {Rounds} round(s); cache read {CacheRead} tokens",
                        round + 1, response.Usage.CacheReadInputTokens);
                    return string.Join("\n", text);
                }
                messages.Add(new() { Role = Role.Assistant, Content = assistantContent });
                messages.Add(new() { Role = Role.User, Content = toolResults });
            }
            logger.LogWarning("Assistant model exceeded {Rounds} tool rounds", _options.MaxToolRounds);
            return null;
        }
        catch (AnthropicRateLimitException ex)
        {
            logger.LogWarning(ex, "Assistant provider rate limited");
            return null;
        }
        catch (AnthropicApiException ex)
        {
            logger.LogWarning(ex, "Assistant provider returned an error");
            return null;
        }
    }
}
