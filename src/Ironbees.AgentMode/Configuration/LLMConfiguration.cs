namespace Ironbees.AgentMode.Configuration;

/// <summary>
/// Configuration for LLM provider and model.
/// </summary>
public record LLMConfiguration
{
    /// <summary>
    /// LLM provider type.
    /// </summary>
    public LLMProvider Provider { get; init; } = LLMProvider.AzureOpenAI;

    /// <summary>
    /// Model name or deployment name.
    /// - Azure OpenAI: deployment name (e.g., "gpt-4o")
    /// - OpenAI: model name (e.g., "gpt-4o-mini")
    /// - Anthropic: model name (e.g., "claude-sonnet-4-20250514")
    /// - OpenAI-compatible: model name from provider
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// API endpoint URL.
    /// - Azure OpenAI: https://{resource}.openai.azure.com
    /// - OpenAI: https://api.openai.com/v1 (default, can be omitted)
    /// - Anthropic: https://api.anthropic.com/v1 (default, can be omitted)
    /// - OpenAI-compatible: custom endpoint (e.g., http://172.30.1.53:8080/v1)
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Temperature for response generation (0.0 - 2.0).
    /// Default: 0.0 (deterministic)
    /// </summary>
    public float Temperature { get; init; }

    /// <summary>
    /// Maximum output tokens.
    /// Default: 4096
    /// </summary>
    public int MaxOutputTokens { get; init; } = 4096;

    /// <summary>
    /// Enable prompt caching (if supported by provider).
    /// Currently supported: Anthropic Claude
    /// </summary>
    public bool EnablePromptCaching { get; init; }

    /// <summary>
    /// Additional provider-specific options.
    /// </summary>
    public Dictionary<string, string>? AdditionalOptions { get; init; }
}
