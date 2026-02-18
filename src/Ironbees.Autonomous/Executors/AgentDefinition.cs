namespace Ironbees.Autonomous.Executors;

/// <summary>
/// Agent definition loaded from YAML configuration.
/// Follows Ironbees filesystem convention: agents/{name}/agent.yaml
/// </summary>
public record AgentDefinition
{
    /// <summary>Agent identifier</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable name</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Agent description</summary>
    public string? Description { get; init; }

    /// <summary>Role of this agent (questioner, answerer, validator, etc.)</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>System prompt (loaded from system-prompt.md)</summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>Output format specification</summary>
    public OutputFormat Output { get; init; } = new();

    /// <summary>LLM settings override for this agent</summary>
    public AgentLlmSettings? Llm { get; init; }

    /// <summary>Resilience settings for retry and backoff</summary>
    public ResilienceConfig? Resilience { get; init; }

    /// <summary>Fallback configuration</summary>
    public FallbackConfig? Fallback { get; init; }

    /// <summary>Guess deduction rules for fallback scenarios</summary>
    public List<GuessRule>? GuessRules { get; init; }

    /// <summary>Variables that can be substituted in prompts</summary>
    public Dictionary<string, string> Variables { get; init; } = [];
}

/// <summary>
/// Output format specification for agent responses
/// </summary>
public record OutputFormat
{
    /// <summary>Expected format: json, text, markdown</summary>
    public string Type { get; init; } = "json";

    /// <summary>JSON schema or template for structured output</summary>
    public string? Schema { get; init; }

    /// <summary>Example output for few-shot learning</summary>
    public string? Example { get; init; }
}

/// <summary>
/// LLM settings specific to an agent (overrides global settings)
/// </summary>
public record AgentLlmSettings
{
    public int? MaxOutputTokens { get; init; }
    public float? Temperature { get; init; }
    public float? TopP { get; init; }
}

/// <summary>
/// Fallback configuration for when AI fails.
/// Supports sequential, random, and context-aware strategies.
/// </summary>
public record FallbackConfig
{
    /// <summary>Enable fallback behavior</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Strategy: sequential, random, context-aware</summary>
    public string Strategy { get; init; } = "sequential";

    /// <summary>Simple list of fallback items (for sequential/random)</summary>
    public List<string> Items { get; init; } = [];

    /// <summary>Default fallback pool (used when no context matches)</summary>
    public List<string> Default { get; init; } = [];

    /// <summary>Context-aware fallback pools</summary>
    public List<FallbackPool>? Pools { get; init; }

    /// <summary>Get all fallback items (combines Items and Default for compatibility)</summary>
    public IReadOnlyList<string> GetAllItems() =>
        Items.Count > 0 ? Items : Default;
}

/// <summary>
/// Context-aware fallback pool
/// </summary>
public record FallbackPool
{
    /// <summary>Context keywords that activate this pool</summary>
    public List<string> Context { get; init; } = [];

    /// <summary>Priority: normal, high (high-priority pools are checked first)</summary>
    public string Priority { get; init; } = "normal";

    /// <summary>Questions in this pool</summary>
    public List<string> Questions { get; init; } = [];
}

/// <summary>
/// Rule for deducing a guess based on conversation context
/// </summary>
public record GuessRule
{
    /// <summary>Conditions that must be present in conversation (keywords)</summary>
    public List<string> Conditions { get; init; } = [];

    /// <summary>The guess to make if conditions match</summary>
    public string? Guess { get; init; }

    /// <summary>Default guess if no conditions specified</summary>
    public string? Default { get; init; }

    /// <summary>Check if this rule matches the given context</summary>
    public bool Matches(IEnumerable<string> contextKeywords)
    {
        if (Conditions.Count == 0)
            return Default != null;

        var keywords = contextKeywords
            .Select(k => k.ToLowerInvariant())
            .ToHashSet();

        return Conditions.All(c => keywords.Any(k => k.Contains(c, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Get the guess value</summary>
    public string GetGuess() => Guess ?? Default ?? "unknown";
}
