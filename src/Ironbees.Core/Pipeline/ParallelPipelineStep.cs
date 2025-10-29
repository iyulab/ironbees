namespace Ironbees.Core.Pipeline;

/// <summary>
/// Represents a step that executes multiple agents in parallel
/// </summary>
public class ParallelPipelineStep
{
    /// <summary>
    /// Names of agents to execute in parallel
    /// </summary>
    public required List<string> AgentNames { get; set; }

    /// <summary>
    /// Options for parallel execution behavior
    /// </summary>
    public ParallelExecutionOptions ExecutionOptions { get; set; } = new();

    /// <summary>
    /// Strategy for aggregating parallel results
    /// </summary>
    public ICollaborationStrategy? CollaborationStrategy { get; set; }

    /// <summary>
    /// Options for collaboration strategy
    /// </summary>
    public CollaborationOptions CollaborationOptions { get; set; } = new();

    /// <summary>
    /// Optional input transformer applied before parallel execution
    /// </summary>
    public Func<PipelineContext, string>? InputTransformer { get; set; }

    /// <summary>
    /// Optional output transformer applied after aggregation
    /// </summary>
    public Func<PipelineContext, string, string>? OutputTransformer { get; set; }

    /// <summary>
    /// Optional condition to determine if this step should execute
    /// </summary>
    public Func<PipelineContext, bool>? Condition { get; set; }

    /// <summary>
    /// Custom metadata for this parallel step
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result from parallel pipeline step execution
/// </summary>
public class ParallelStepResult
{
    /// <summary>
    /// Names of agents that were executed
    /// </summary>
    public required List<string> AgentNames { get; set; }

    /// <summary>
    /// Individual results from each agent
    /// </summary>
    public required List<PipelineStepResult> IndividualResults { get; set; }

    /// <summary>
    /// Aggregated result from collaboration strategy
    /// </summary>
    public CollaborationResult? AggregatedResult { get; set; }

    /// <summary>
    /// Final output after aggregation
    /// </summary>
    public required string Output { get; set; }

    /// <summary>
    /// Whether the parallel step succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Total execution time for all parallel agents
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Longest execution time among all agents
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; }

    /// <summary>
    /// Shortest execution time among successful agents
    /// </summary>
    public TimeSpan MinExecutionTime { get; set; }

    /// <summary>
    /// Number of agents that succeeded
    /// </summary>
    public int SuccessfulAgents { get; set; }

    /// <summary>
    /// Number of agents that failed
    /// </summary>
    public int FailedAgents { get; set; }

    /// <summary>
    /// Error if parallel step failed
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// Additional metadata about parallel execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
