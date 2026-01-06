using Ironbees.Autonomous.Context;

namespace Ironbees.Autonomous.Models;

/// <summary>
/// Rich context for context-aware oracle verification
/// </summary>
/// <remarks>
/// Provides oracle with full workflow state including iteration history,
/// previous verdicts, and workflow metadata for improved decision making.
/// </remarks>
public record OracleContext
{
    /// <summary>
    /// Unique workflow execution identifier
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Current iteration number (0-based)
    /// </summary>
    public required int IterationNumber { get; init; }

    /// <summary>
    /// Original task prompt/goal
    /// </summary>
    public required string OriginalPrompt { get; init; }

    /// <summary>
    /// Current task prompt (may differ from original if refined)
    /// </summary>
    public required string CurrentPrompt { get; init; }

    /// <summary>
    /// Execution history from previous iterations
    /// </summary>
    public IReadOnlyList<ExecutionHistoryEntry> History { get; init; } = Array.Empty<ExecutionHistoryEntry>();

    /// <summary>
    /// Previous oracle verdicts (chronological order)
    /// </summary>
    public IReadOnlyList<OracleVerdict> PreviousVerdicts { get; init; } = Array.Empty<OracleVerdict>();

    /// <summary>
    /// Workflow-specific metadata (e.g., goal ID, user ID, session ID)
    /// </summary>
    public IReadOnlyDictionary<string, object> WorkflowMetadata { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Relevant context from DefaultContextManager (if enabled)
    /// </summary>
    /// <remarks>
    /// v0.4.0: Automatically populated by DefaultContextManager.
    /// Contains execution history, learnings, and important outputs.
    /// </remarks>
    public IReadOnlyList<ContextEntry> RelevantContext { get; init; } = Array.Empty<ContextEntry>();

    /// <summary>
    /// Maximum iterations allowed for this workflow
    /// </summary>
    public int MaxIterations { get; init; }

    /// <summary>
    /// Whether this is the final iteration
    /// </summary>
    public bool IsFinalIteration => IterationNumber >= MaxIterations - 1;

    /// <summary>
    /// Create context for initial iteration
    /// </summary>
    public static OracleContext Initial(
        string workflowId,
        string prompt,
        int maxIterations,
        IDictionary<string, object>? metadata = null) => new()
    {
        WorkflowId = workflowId,
        IterationNumber = 0,
        OriginalPrompt = prompt,
        CurrentPrompt = prompt,
        MaxIterations = maxIterations,
        WorkflowMetadata = metadata as IReadOnlyDictionary<string, object> ??
            new Dictionary<string, object>(metadata ?? new Dictionary<string, object>())
    };

    /// <summary>
    /// Create context for next iteration
    /// </summary>
    public OracleContext NextIteration(
        string? nextPrompt = null,
        OracleVerdict? previousVerdict = null) => this with
    {
        IterationNumber = IterationNumber + 1,
        CurrentPrompt = nextPrompt ?? CurrentPrompt,
        PreviousVerdicts = previousVerdict != null
            ? PreviousVerdicts.Append(previousVerdict).ToArray()
            : PreviousVerdicts
    };
}
