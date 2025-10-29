namespace Ironbees.Core.Pipeline;

/// <summary>
/// Represents a pipeline of agents that execute sequentially
/// </summary>
public interface IAgentPipeline
{
    /// <summary>
    /// Name of the pipeline
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Steps in the pipeline
    /// </summary>
    IReadOnlyList<PipelineStep> Steps { get; }

    /// <summary>
    /// Execute the pipeline with the given input
    /// </summary>
    /// <param name="input">Input to the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline execution result</returns>
    Task<PipelineResult> ExecuteAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute the pipeline with an existing context
    /// </summary>
    /// <param name="context">Pipeline context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline execution result</returns>
    Task<PipelineResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single step in an agent pipeline
/// </summary>
public class PipelineStep
{
    /// <summary>
    /// Name of the agent to execute
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// Optional input transformer
    /// </summary>
    public Func<PipelineContext, string>? InputTransformer { get; set; }

    /// <summary>
    /// Optional output transformer
    /// </summary>
    public Func<PipelineContext, string, string>? OutputTransformer { get; set; }

    /// <summary>
    /// Condition to determine if this step should execute
    /// </summary>
    public Func<PipelineContext, bool>? Condition { get; set; }

    /// <summary>
    /// Whether to continue pipeline on error
    /// </summary>
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Maximum retry attempts for this step
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Delay between retries
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Timeout for this step
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Step metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result from pipeline execution
/// </summary>
public class PipelineResult
{
    /// <summary>
    /// Name of the pipeline
    /// </summary>
    public required string PipelineName { get; set; }

    /// <summary>
    /// Whether the pipeline completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Final output from the pipeline
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Pipeline context with all step results
    /// </summary>
    public required PipelineContext Context { get; set; }

    /// <summary>
    /// Total execution time
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Number of steps executed
    /// </summary>
    public int StepsExecuted { get; set; }

    /// <summary>
    /// Number of steps that failed
    /// </summary>
    public int StepsFailed { get; set; }

    /// <summary>
    /// Error that caused pipeline failure
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// Pipeline execution metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
