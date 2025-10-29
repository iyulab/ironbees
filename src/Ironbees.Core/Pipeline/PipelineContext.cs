namespace Ironbees.Core.Pipeline;

/// <summary>
/// Context object that flows through the pipeline, carrying data between agents
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// Original input to the pipeline
    /// </summary>
    public string OriginalInput { get; }

    /// <summary>
    /// Current input for the next agent in the pipeline
    /// </summary>
    public string CurrentInput { get; set; }

    /// <summary>
    /// Collection of results from each agent in the pipeline
    /// </summary>
    public List<PipelineStepResult> StepResults { get; } = new();

    /// <summary>
    /// Shared state that can be accessed by all agents
    /// </summary>
    public Dictionary<string, object> SharedState { get; } = new();

    /// <summary>
    /// Indicates whether the pipeline should continue execution
    /// </summary>
    public bool ShouldContinue { get; set; } = true;

    /// <summary>
    /// Error that occurred during pipeline execution
    /// </summary>
    public Exception? Error { get; set; }

    public PipelineContext(string input)
    {
        OriginalInput = input;
        CurrentInput = input;
    }

    /// <summary>
    /// Add a step result to the pipeline context
    /// </summary>
    public void AddStepResult(PipelineStepResult result)
    {
        StepResults.Add(result);
        CurrentInput = result.Output; // Next agent receives previous agent's output
    }

    /// <summary>
    /// Get result from a specific step by agent name
    /// </summary>
    public PipelineStepResult? GetStepResult(string agentName)
    {
        return StepResults.FirstOrDefault(r => r.AgentName == agentName);
    }

    /// <summary>
    /// Get the last successful step result
    /// </summary>
    public PipelineStepResult? GetLastStepResult()
    {
        return StepResults.LastOrDefault(r => r.Success);
    }

    /// <summary>
    /// Stop pipeline execution
    /// </summary>
    public void StopPipeline(string reason)
    {
        ShouldContinue = false;
        SharedState["StopReason"] = reason;
    }
}

/// <summary>
/// Result from a single pipeline step
/// </summary>
public class PipelineStepResult
{
    /// <summary>
    /// Name of the agent that executed this step
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// Input provided to the agent
    /// </summary>
    public required string Input { get; set; }

    /// <summary>
    /// Output from the agent
    /// </summary>
    public required string Output { get; set; }

    /// <summary>
    /// Whether the step executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error that occurred during step execution
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// Time taken to execute this step
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Timestamp when the step was executed
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata for this step
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
