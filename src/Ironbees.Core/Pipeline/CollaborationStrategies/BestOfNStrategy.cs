namespace Ironbees.Core.Pipeline.CollaborationStrategies;

/// <summary>
/// Selects the best result from N parallel agent executions based on a scoring function
/// </summary>
public class BestOfNStrategy : ICollaborationStrategy
{
    private readonly Func<PipelineStepResult, double> _scoringFunction;
    private readonly CollaborationOptions _options;

    public string Name => "BestOfN";

    /// <summary>
    /// Create a Best-of-N strategy with custom scoring
    /// </summary>
    /// <param name="scoringFunction">Function to score each result (higher = better)</param>
    /// <param name="options">Collaboration options</param>
    public BestOfNStrategy(
        Func<PipelineStepResult, double> scoringFunction,
        CollaborationOptions? options = null)
    {
        _scoringFunction = scoringFunction ?? throw new ArgumentNullException(nameof(scoringFunction));
        _options = options ?? new CollaborationOptions();
    }

    /// <summary>
    /// Create a Best-of-N strategy that selects by output length
    /// </summary>
    public static BestOfNStrategy ByLength(CollaborationOptions? options = null) =>
        new(result => result.Output?.Length ?? 0, options);

    /// <summary>
    /// Create a Best-of-N strategy that selects by execution time (fastest)
    /// </summary>
    public static BestOfNStrategy BySpeed(CollaborationOptions? options = null) =>
        new(result => -result.ExecutionTime.TotalMilliseconds, options);

    /// <summary>
    /// Create a Best-of-N strategy that selects by metadata score
    /// </summary>
    public static BestOfNStrategy ByMetadata(
        string metadataKey,
        CollaborationOptions? options = null) =>
        new(result =>
        {
            if (result.Metadata.TryGetValue(metadataKey, out var value))
            {
                return value switch
                {
                    double d => d,
                    float f => f,
                    int i => i,
                    long l => l,
                    _ => 0.0
                };
            }
            return 0.0;
        }, options);

    public Task<CollaborationResult> AggregateAsync(
        IReadOnlyList<PipelineStepResult> results,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        // Filter results based on options
        var filteredResults = FilterResults(results);

        if (filteredResults.Count < _options.MinimumResults)
        {
            throw new InvalidOperationException(
                $"Insufficient results for aggregation. Required: {_options.MinimumResults}, Got: {filteredResults.Count}");
        }

        // Apply maximum results limit
        if (_options.MaximumResults.HasValue && filteredResults.Count > _options.MaximumResults.Value)
        {
            filteredResults = filteredResults.Take(_options.MaximumResults.Value).ToList();
        }

        // Score and select best result
        var scoredResults = filteredResults
            .Select(r => new { Result = r, Score = _scoringFunction(r) })
            .OrderByDescending(x => x.Score)
            .ToList();

        var best = scoredResults.First();

        var collaborationResult = new CollaborationResult
        {
            Output = best.Result.Output,
            Strategy = Name,
            ResultCount = filteredResults.Count,
            ConfidenceScore = scoredResults.Count > 1
                ? best.Score / scoredResults.Sum(x => x.Score) // Normalize score
                : 1.0
        };

        if (_options.IncludeIndividualResults)
        {
            collaborationResult.IndividualResults = filteredResults;
        }

        if (_options.CollectMetadata)
        {
            collaborationResult.Metadata["bestScore"] = best.Score;
            collaborationResult.Metadata["allScores"] = scoredResults.Select(x => x.Score).ToList();
            collaborationResult.Metadata["selectedAgentName"] = best.Result.AgentName;
        }

        return Task.FromResult(collaborationResult);
    }

    private List<PipelineStepResult> FilterResults(IReadOnlyList<PipelineStepResult> results)
    {
        var filtered = results.AsEnumerable();

        // Filter by success status
        if (!_options.IncludeFailedResults)
        {
            filtered = filtered.Where(r => r.Success);
        }

        // Apply custom filter
        if (_options.ResultFilter != null)
        {
            filtered = filtered.Where(_options.ResultFilter);
        }

        // Filter by confidence threshold
        if (_options.MinimumConfidenceThreshold.HasValue)
        {
            filtered = filtered.Where(r =>
            {
                if (r.Metadata.TryGetValue("confidence", out var confidence))
                {
                    return (confidence as double? ?? 0.0) >= _options.MinimumConfidenceThreshold.Value;
                }
                return false;
            });
        }

        // Apply custom ranker for sorting
        if (_options.ResultRanker != null)
        {
            filtered = filtered.OrderByDescending(_options.ResultRanker);
        }

        return filtered.ToList();
    }
}
