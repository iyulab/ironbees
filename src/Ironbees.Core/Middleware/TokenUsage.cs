namespace Ironbees.Core.Middleware;

/// <summary>
/// Represents token usage data from an LLM API call.
/// Used for tracking costs and optimizing token consumption.
/// </summary>
public sealed record TokenUsage
{
    /// <summary>
    /// Unique identifier for this usage record.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The model ID used for this request.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Agent name that made the request (if applicable).
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Number of tokens in the input/prompt.
    /// </summary>
    public long InputTokens { get; init; }

    /// <summary>
    /// Number of tokens in the output/completion.
    /// </summary>
    public long OutputTokens { get; init; }

    /// <summary>
    /// Total tokens used (input + output).
    /// </summary>
    public long TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Timestamp when this usage was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional session or conversation identifier.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Optional metadata for additional tracking.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Estimated cost in USD for this request, calculated by TokenMeter if available.
    /// </summary>
    public decimal? EstimatedCost { get; init; }
}

/// <summary>
/// Aggregated token usage statistics.
/// </summary>
public sealed record TokenUsageStatistics
{
    /// <summary>
    /// Total number of requests tracked.
    /// </summary>
    public int TotalRequests { get; init; }

    /// <summary>
    /// Total input tokens across all requests.
    /// </summary>
    public long TotalInputTokens { get; init; }

    /// <summary>
    /// Total output tokens across all requests.
    /// </summary>
    public long TotalOutputTokens { get; init; }

    /// <summary>
    /// Total tokens (input + output).
    /// </summary>
    public long TotalTokens => TotalInputTokens + TotalOutputTokens;

    /// <summary>
    /// Average tokens per request.
    /// </summary>
    public double AverageTokensPerRequest =>
        TotalRequests > 0 ? (double)TotalTokens / TotalRequests : 0;

    /// <summary>
    /// Statistics grouped by model.
    /// </summary>
    public IReadOnlyDictionary<string, ModelUsageStatistics> ByModel { get; init; } =
        new Dictionary<string, ModelUsageStatistics>();

    /// <summary>
    /// Statistics grouped by agent.
    /// </summary>
    public IReadOnlyDictionary<string, long> ByAgent { get; init; } =
        new Dictionary<string, long>();

    /// <summary>
    /// Total estimated cost in USD across all requests.
    /// </summary>
    public decimal TotalEstimatedCost { get; init; }

    /// <summary>
    /// Time range start for these statistics.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Time range end for these statistics.
    /// </summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>
/// Token usage statistics for a specific model.
/// </summary>
public sealed record ModelUsageStatistics
{
    /// <summary>
    /// Number of requests for this model.
    /// </summary>
    public int Requests { get; init; }

    /// <summary>
    /// Total input tokens for this model.
    /// </summary>
    public long InputTokens { get; init; }

    /// <summary>
    /// Total output tokens for this model.
    /// </summary>
    public long OutputTokens { get; init; }

    /// <summary>
    /// Total tokens for this model.
    /// </summary>
    public long TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Estimated cost in USD for this model.
    /// </summary>
    public decimal EstimatedCost { get; init; }
}
