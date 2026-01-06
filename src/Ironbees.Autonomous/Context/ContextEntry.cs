namespace Ironbees.Autonomous.Context;

/// <summary>
/// Represents a single entry in the execution context
/// </summary>
/// <remarks>
/// Context entries track execution history, learnings, and important outputs
/// for context-aware oracle verification and decision making.
/// </remarks>
public record ContextEntry
{
    /// <summary>
    /// Type of context entry
    /// </summary>
    public required ContextEntryType Type { get; init; }

    /// <summary>
    /// Content of the context entry
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Timestamp when this entry was created
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Iteration number when this entry was created
    /// </summary>
    public int IterationNumber { get; init; }

    /// <summary>
    /// Relevance score (0.0 to 1.0)
    /// </summary>
    public double Relevance { get; init; } = 1.0;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();
}

/// <summary>
/// Type of context entry
/// </summary>
public enum ContextEntryType
{
    /// <summary>
    /// Execution output from a task
    /// </summary>
    ExecutionOutput,

    /// <summary>
    /// Learning from successful execution
    /// </summary>
    Learning,

    /// <summary>
    /// Error or failure information
    /// </summary>
    Error,

    /// <summary>
    /// Important output or result
    /// </summary>
    ImportantOutput,

    /// <summary>
    /// User feedback or input
    /// </summary>
    UserFeedback,

    /// <summary>
    /// Oracle verdict or analysis
    /// </summary>
    OracleVerdict,

    /// <summary>
    /// System event or state change
    /// </summary>
    SystemEvent
}
