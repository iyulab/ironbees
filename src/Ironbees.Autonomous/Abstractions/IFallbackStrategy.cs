namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Strategy interface for providing fallback behavior when task execution fails.
/// Lessons learned from TwentyQuestions: AI can return empty responses, need smart fallbacks.
/// </summary>
/// <typeparam name="TRequest">Task request type</typeparam>
/// <typeparam name="TResult">Task result type</typeparam>
public interface IFallbackStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    /// <summary>
    /// Attempt to provide a fallback result when the primary executor fails.
    /// </summary>
    /// <param name="context">Context about the failure and execution state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Fallback result, or null if no fallback is available</returns>
    Task<TResult?> GetFallbackAsync(
        FallbackContext<TRequest> context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this strategy can provide a fallback for the given context.
    /// Called before GetFallbackAsync to allow quick pre-checks.
    /// </summary>
    /// <param name="context">Context about the failure and execution state</param>
    /// <returns>True if fallback may be available</returns>
    bool CanProvideFallback(FallbackContext<TRequest> context);
}

/// <summary>
/// Context provided to fallback strategy when executor fails
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
public record FallbackContext<TRequest>
    where TRequest : ITaskRequest
{
    /// <summary>
    /// The original request that failed
    /// </summary>
    public required TRequest FailedRequest { get; init; }

    /// <summary>
    /// Current iteration number (1-based)
    /// </summary>
    public int Iteration { get; init; }

    /// <summary>
    /// Number of retry attempts made before calling fallback
    /// </summary>
    public int RetryAttempts { get; init; }

    /// <summary>
    /// Error message from the failed execution, if any
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Previous successful outputs in this execution chain.
    /// Useful for context-aware fallback selection.
    /// </summary>
    public IReadOnlyList<string> PreviousOutputs { get; init; } = [];

    /// <summary>
    /// Custom metadata that can be passed from executor to fallback strategy
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();
}

/// <summary>
/// Fallback strategy that does nothing (returns null).
/// Used as default when no fallback is configured.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public sealed class NoOpFallbackStrategy<TRequest, TResult> : IFallbackStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    /// <summary>
    /// Singleton instance
    /// </summary>
    public static readonly NoOpFallbackStrategy<TRequest, TResult> Instance = new();

    private NoOpFallbackStrategy() { }

    /// <inheritdoc />
    public bool CanProvideFallback(FallbackContext<TRequest> context) => false;

    /// <inheritdoc />
    public Task<TResult?> GetFallbackAsync(
        FallbackContext<TRequest> context,
        CancellationToken cancellationToken = default) => Task.FromResult<TResult?>(default);
}
