namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Monitors context saturation and triggers actions when thresholds are exceeded.
/// Enables integration with external memory systems for context management.
/// </summary>
/// <remarks>
/// Design principle: Event-driven monitoring without implementation details.
/// External systems handle the actual eviction/compression logic.
/// </remarks>
public interface IContextSaturationMonitor
{
    /// <summary>
    /// Gets the current saturation state.
    /// </summary>
    SaturationState CurrentState { get; }

    /// <summary>
    /// Event raised when saturation level changes.
    /// </summary>
    event EventHandler<SaturationChangedEventArgs>? SaturationChanged;

    /// <summary>
    /// Event raised when action is required (threshold exceeded).
    /// </summary>
    event EventHandler<SaturationActionRequiredEventArgs>? ActionRequired;

    /// <summary>
    /// Records token usage for monitoring.
    /// </summary>
    /// <param name="tokens">Number of tokens used</param>
    /// <param name="source">Source of the tokens (e.g., "prompt", "response", "context")</param>
    void RecordUsage(int tokens, string source = "unknown");

    /// <summary>
    /// Updates the saturation state based on current usage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current saturation state</returns>
    Task<SaturationState> UpdateStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets usage tracking (e.g., for new iteration).
    /// </summary>
    void ResetIteration();

    /// <summary>
    /// Configures saturation thresholds.
    /// </summary>
    /// <param name="config">Threshold configuration</param>
    void Configure(SaturationConfig config);
}

/// <summary>
/// Current saturation state.
/// </summary>
public record SaturationState
{
    /// <summary>
    /// Current saturation level.
    /// </summary>
    public SaturationLevel Level { get; init; } = SaturationLevel.Normal;

    /// <summary>
    /// Saturation percentage (0-100).
    /// </summary>
    public float Percentage { get; init; }

    /// <summary>
    /// Current token usage.
    /// </summary>
    public int CurrentTokens { get; init; }

    /// <summary>
    /// Maximum token capacity.
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// Tokens available before threshold.
    /// </summary>
    public int AvailableTokens => MaxTokens - CurrentTokens;

    /// <summary>
    /// Usage breakdown by source.
    /// </summary>
    public IReadOnlyDictionary<string, int> UsageBySource { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Recommended action based on current state.
    /// </summary>
    public SaturationAction RecommendedAction { get; init; } = SaturationAction.None;

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Saturation level classification.
/// </summary>
public enum SaturationLevel
{
    /// <summary>
    /// Normal operation (0-60% usage).
    /// </summary>
    Normal,

    /// <summary>
    /// Elevated usage (60-75%), consider optimization.
    /// </summary>
    Elevated,

    /// <summary>
    /// High usage (75-85%), should take action.
    /// </summary>
    High,

    /// <summary>
    /// Critical usage (85-95%), immediate action required.
    /// </summary>
    Critical,

    /// <summary>
    /// Overflow (>95%), must evict before continuing.
    /// </summary>
    Overflow
}

/// <summary>
/// Recommended action based on saturation.
/// </summary>
public enum SaturationAction
{
    /// <summary>
    /// No action needed.
    /// </summary>
    None,

    /// <summary>
    /// Consider summarizing older context.
    /// </summary>
    ConsiderSummarization,

    /// <summary>
    /// Should page out some context.
    /// </summary>
    ShouldPageOut,

    /// <summary>
    /// Must evict before continuing.
    /// </summary>
    MustEvict,

    /// <summary>
    /// Emergency: truncate or abort.
    /// </summary>
    Emergency
}

/// <summary>
/// Event args for saturation level changes.
/// </summary>
public class SaturationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Previous saturation level.
    /// </summary>
    public required SaturationLevel PreviousLevel { get; init; }

    /// <summary>
    /// New saturation level.
    /// </summary>
    public required SaturationLevel NewLevel { get; init; }

    /// <summary>
    /// Current saturation state.
    /// </summary>
    public required SaturationState CurrentState { get; init; }
}

/// <summary>
/// Event args when action is required.
/// </summary>
public class SaturationActionRequiredEventArgs : EventArgs
{
    /// <summary>
    /// Required action.
    /// </summary>
    public required SaturationAction Action { get; init; }

    /// <summary>
    /// Current saturation state.
    /// </summary>
    public required SaturationState CurrentState { get; init; }

    /// <summary>
    /// Suggested number of tokens to free.
    /// </summary>
    public int SuggestedTokensToFree { get; init; }

    /// <summary>
    /// Reason for the action requirement.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Configuration for saturation thresholds.
/// </summary>
public record SaturationConfig
{
    /// <summary>
    /// Maximum token capacity.
    /// </summary>
    public int MaxTokens { get; init; } = 128_000;

    /// <summary>
    /// Threshold for Elevated level (percentage).
    /// </summary>
    public float ElevatedThreshold { get; init; } = 60f;

    /// <summary>
    /// Threshold for High level (percentage).
    /// </summary>
    public float HighThreshold { get; init; } = 75f;

    /// <summary>
    /// Threshold for Critical level (percentage).
    /// </summary>
    public float CriticalThreshold { get; init; } = 85f;

    /// <summary>
    /// Threshold for Overflow level (percentage).
    /// </summary>
    public float OverflowThreshold { get; init; } = 95f;

    /// <summary>
    /// Target level after eviction (percentage).
    /// </summary>
    public float TargetAfterEviction { get; init; } = 50f;

    /// <summary>
    /// Whether to auto-trigger actions when thresholds exceeded.
    /// </summary>
    public bool AutoTriggerActions { get; init; } = true;
}
