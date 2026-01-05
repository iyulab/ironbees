namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Strategy for handling the final iteration when max iterations is about to be reached.
/// This allows enforcing completion actions (like forcing a guess in 20 Questions).
/// </summary>
/// <typeparam name="TRequest">Task request type</typeparam>
/// <typeparam name="TResult">Task result type</typeparam>
public interface IFinalIterationStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    /// <summary>
    /// Called before the final iteration begins.
    /// Return a modified request to enforce completion behavior.
    /// </summary>
    /// <param name="context">Current execution context</param>
    /// <returns>Modified request for final iteration, or null to use original</returns>
    Task<TRequest?> BeforeFinalIterationAsync(FinalIterationContext<TRequest, TResult> context);

    /// <summary>
    /// Called after the final iteration completes if no completion was achieved.
    /// Return a forced result to use as the final output.
    /// </summary>
    /// <param name="context">Final execution context with last result</param>
    /// <returns>Forced completion result, or null to use actual result</returns>
    Task<TResult?> ForceCompletionAsync(FinalIterationContext<TRequest, TResult> context);
}

/// <summary>
/// Context provided to final iteration strategy
/// </summary>
/// <typeparam name="TRequest">Task request type</typeparam>
/// <typeparam name="TResult">Task result type</typeparam>
public record FinalIterationContext<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    /// <summary>Current iteration number (1-based)</summary>
    public required int CurrentIteration { get; init; }

    /// <summary>Maximum configured iterations</summary>
    public required int MaxIterations { get; init; }

    /// <summary>Number of remaining iterations (including current)</summary>
    public int RemainingIterations => MaxIterations - CurrentIteration + 1;

    /// <summary>Whether this is the absolute last iteration</summary>
    public bool IsLastIteration => RemainingIterations <= 1;

    /// <summary>Whether we're in the final phase (last 3 iterations)</summary>
    public bool IsInFinalPhase => RemainingIterations <= 3;

    /// <summary>Original request for this iteration</summary>
    public required TRequest OriginalRequest { get; init; }

    /// <summary>Result from execution (only available in ForceCompletionAsync)</summary>
    public TResult? LastResult { get; init; }

    /// <summary>Previous outputs from all iterations</summary>
    public required IReadOnlyList<string> PreviousOutputs { get; init; }

    /// <summary>Session ID</summary>
    public required string SessionId { get; init; }
}

/// <summary>
/// Default no-op final iteration strategy
/// </summary>
public class NoOpFinalIterationStrategy<TRequest, TResult> : IFinalIterationStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    public Task<TRequest?> BeforeFinalIterationAsync(FinalIterationContext<TRequest, TResult> context)
        => Task.FromResult(default(TRequest));

    public Task<TResult?> ForceCompletionAsync(FinalIterationContext<TRequest, TResult> context)
        => Task.FromResult(default(TResult));
}

/// <summary>
/// Final iteration strategy that modifies the prompt to encourage completion
/// </summary>
public class PromptEnforcementFinalIterationStrategy<TRequest, TResult> : IFinalIterationStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    private readonly string _finalIterationWarning;
    private readonly Func<FinalIterationContext<TRequest, TResult>, TRequest> _requestModifier;
    private readonly Func<FinalIterationContext<TRequest, TResult>, TResult>? _completionEnforcer;

    public PromptEnforcementFinalIterationStrategy(
        string finalIterationWarning = "⚠️ This is your final iteration. You MUST provide a complete answer now.",
        Func<FinalIterationContext<TRequest, TResult>, TRequest>? requestModifier = null,
        Func<FinalIterationContext<TRequest, TResult>, TResult>? completionEnforcer = null)
    {
        _finalIterationWarning = finalIterationWarning;
        _requestModifier = requestModifier ?? (ctx => ctx.OriginalRequest);
        _completionEnforcer = completionEnforcer;
    }

    public Task<TRequest?> BeforeFinalIterationAsync(FinalIterationContext<TRequest, TResult> context)
    {
        if (!context.IsLastIteration)
            return Task.FromResult(default(TRequest));

        return Task.FromResult<TRequest?>(_requestModifier(context));
    }

    public Task<TResult?> ForceCompletionAsync(FinalIterationContext<TRequest, TResult> context)
    {
        if (_completionEnforcer == null)
            return Task.FromResult(default(TResult));

        return Task.FromResult<TResult?>(_completionEnforcer(context));
    }
}
