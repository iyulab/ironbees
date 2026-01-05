using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Strategies;

/// <summary>
/// Fallback strategy that uses a predefined list of fallback options.
/// Learns from TwentyQuestions: provides diverse fallbacks without repetition.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public abstract class ListBasedFallbackStrategy<TRequest, TResult> : IFallbackStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    private readonly List<string> _usedFallbacks = [];

    /// <summary>
    /// Get the list of available fallback values
    /// </summary>
    protected abstract IReadOnlyList<string> FallbackValues { get; }

    /// <summary>
    /// Create a result from the fallback value.
    /// Implement this to construct your specific TResult type.
    /// </summary>
    /// <param name="request">The original failed request</param>
    /// <param name="fallbackValue">The selected fallback value</param>
    /// <param name="context">Fallback context</param>
    /// <returns>Result constructed from fallback value</returns>
    protected abstract TResult CreateResultFromFallback(
        TRequest request,
        string fallbackValue,
        FallbackContext<TRequest> context);

    /// <summary>
    /// Extract key concepts from a value for duplicate detection.
    /// Override to customize duplicate detection logic.
    /// Default implementation uses simple lowercase contains.
    /// </summary>
    /// <param name="value">Value to extract concepts from</param>
    /// <returns>Set of concept keys</returns>
    protected virtual HashSet<string> ExtractConcepts(string value)
    {
        // Default: return the value itself as a single concept
        return [value.ToLowerInvariant()];
    }

    /// <summary>
    /// Check if two values are conceptually similar.
    /// Override for domain-specific similarity logic.
    /// </summary>
    /// <param name="value1">First value</param>
    /// <param name="value2">Second value</param>
    /// <returns>True if conceptually similar</returns>
    protected virtual bool AreConceptuallySimilar(string value1, string value2)
    {
        var concepts1 = ExtractConcepts(value1);
        var concepts2 = ExtractConcepts(value2);
        return concepts1.Overlaps(concepts2);
    }

    /// <inheritdoc />
    public bool CanProvideFallback(FallbackContext<TRequest> context)
    {
        return GetNextUnusedFallback(context) != null;
    }

    /// <inheritdoc />
    public Task<TResult?> GetFallbackAsync(
        FallbackContext<TRequest> context,
        CancellationToken cancellationToken = default)
    {
        var fallbackValue = GetNextUnusedFallback(context);

        if (fallbackValue == null)
        {
            return Task.FromResult<TResult?>(default);
        }

        _usedFallbacks.Add(fallbackValue);
        var result = CreateResultFromFallback(context.FailedRequest, fallbackValue, context);

        return Task.FromResult<TResult?>(result);
    }

    /// <summary>
    /// Find next fallback value that hasn't been used and isn't similar to previous outputs
    /// </summary>
    private string? GetNextUnusedFallback(FallbackContext<TRequest> context)
    {
        // Combine used fallbacks with previous outputs for similarity checking
        var usedConcepts = _usedFallbacks
            .Concat(context.PreviousOutputs)
            .SelectMany(ExtractConcepts)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var fallback in FallbackValues)
        {
            var fallbackConcepts = ExtractConcepts(fallback);

            // Check if any concept overlaps with used concepts
            if (!fallbackConcepts.Any(c => usedConcepts.Contains(c)))
            {
                return fallback;
            }
        }

        return null;
    }

    /// <summary>
    /// Reset the used fallbacks list.
    /// Call this when starting a new execution chain.
    /// </summary>
    public void Reset()
    {
        _usedFallbacks.Clear();
    }
}

/// <summary>
/// Simple string-based fallback strategy using a list of string values.
/// Useful for quick prototyping and simple use cases.
/// </summary>
/// <typeparam name="TRequest">Request type (must support setting Output)</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public class StringListFallbackStrategy<TRequest, TResult> : ListBasedFallbackStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : class, ITaskResult, new()
{
    private readonly IReadOnlyList<string> _fallbacks;
    private readonly Func<TRequest, string, TResult> _resultFactory;

    /// <summary>
    /// Create a string list fallback strategy
    /// </summary>
    /// <param name="fallbacks">List of fallback string values</param>
    /// <param name="resultFactory">Factory to create result from request and fallback value</param>
    public StringListFallbackStrategy(
        IReadOnlyList<string> fallbacks,
        Func<TRequest, string, TResult> resultFactory)
    {
        _fallbacks = fallbacks ?? throw new ArgumentNullException(nameof(fallbacks));
        _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> FallbackValues => _fallbacks;

    /// <inheritdoc />
    protected override TResult CreateResultFromFallback(
        TRequest request,
        string fallbackValue,
        FallbackContext<TRequest> context)
    {
        return _resultFactory(request, fallbackValue);
    }
}
