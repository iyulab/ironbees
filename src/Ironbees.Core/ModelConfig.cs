namespace Ironbees.Core;

/// <summary>
/// LLM model configuration
/// </summary>
public record ModelConfig
{
    /// <summary>
    /// Provider name (e.g., "azure-openai", "openai")
    /// </summary>
    public string Provider { get; init; } = "azure-openai";

    /// <summary>
    /// Model deployment name (e.g., "gpt-4o", "gpt-4o-mini")
    /// </summary>
    public required string Deployment { get; init; }

    /// <summary>
    /// Temperature for response randomness (0.0 - 2.0)
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Maximum tokens in response
    /// </summary>
    public int MaxTokens { get; init; } = 4000;

    /// <summary>
    /// Top-p sampling parameter (0.0 - 1.0)
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// Frequency penalty (-2.0 - 2.0)
    /// </summary>
    public double? FrequencyPenalty { get; init; }

    /// <summary>
    /// Presence penalty (-2.0 - 2.0)
    /// </summary>
    public double? PresencePenalty { get; init; }
}
