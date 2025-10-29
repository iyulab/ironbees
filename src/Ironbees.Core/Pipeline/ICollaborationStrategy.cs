namespace Ironbees.Core.Pipeline;

/// <summary>
/// Strategy for aggregating results from multiple agents executed in parallel
/// </summary>
public interface ICollaborationStrategy
{
    /// <summary>
    /// Name of the collaboration strategy
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Aggregate multiple agent results into a single result
    /// </summary>
    /// <param name="results">Results from parallel agent execution</param>
    /// <param name="context">Pipeline context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated result</returns>
    Task<CollaborationResult> AggregateAsync(
        IReadOnlyList<PipelineStepResult> results,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of collaboration strategy aggregation
/// </summary>
public class CollaborationResult
{
    /// <summary>
    /// Final aggregated output
    /// </summary>
    public required string Output { get; set; }

    /// <summary>
    /// Strategy used for aggregation
    /// </summary>
    public required string Strategy { get; set; }

    /// <summary>
    /// Number of results that were aggregated
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Individual results that were considered
    /// </summary>
    public List<PipelineStepResult> IndividualResults { get; set; } = new();

    /// <summary>
    /// Additional metadata about the aggregation process
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Confidence score of the aggregated result (0.0 to 1.0)
    /// </summary>
    public double? ConfidenceScore { get; set; }
}

/// <summary>
/// Options for configuring collaboration strategy behavior
/// </summary>
public class CollaborationOptions
{
    /// <summary>
    /// Minimum number of results required for aggregation
    /// If fewer results available, aggregation will fail
    /// Default: 1
    /// </summary>
    public int MinimumResults { get; set; } = 1;

    /// <summary>
    /// Maximum number of results to consider for aggregation
    /// Useful for limiting processing when many agents run
    /// Default: unlimited (null)
    /// </summary>
    public int? MaximumResults { get; set; }

    /// <summary>
    /// Whether to include failed results in aggregation consideration
    /// Default: false (only successful results)
    /// </summary>
    public bool IncludeFailedResults { get; set; } = false;

    /// <summary>
    /// Function to filter results before aggregation
    /// Return true to include result, false to exclude
    /// </summary>
    public Func<PipelineStepResult, bool>? ResultFilter { get; set; }

    /// <summary>
    /// Function to sort/rank results before aggregation
    /// Higher score = higher priority
    /// </summary>
    public Func<PipelineStepResult, double>? ResultRanker { get; set; }

    /// <summary>
    /// Minimum confidence score required for a result to be considered
    /// Results with lower confidence will be filtered out
    /// Default: null (no filtering)
    /// </summary>
    public double? MinimumConfidenceThreshold { get; set; }

    /// <summary>
    /// Whether to include individual results in the final CollaborationResult
    /// Default: true
    /// </summary>
    public bool IncludeIndividualResults { get; set; } = true;

    /// <summary>
    /// Whether to collect detailed metadata about the aggregation process
    /// Default: true
    /// </summary>
    public bool CollectMetadata { get; set; } = true;

    /// <summary>
    /// Timeout for the aggregation process itself
    /// Default: no timeout (null)
    /// </summary>
    public TimeSpan? AggregationTimeout { get; set; }
}
