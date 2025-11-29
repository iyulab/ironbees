using Ironbees.AgentMode.Models;

namespace Ironbees.AgentMode.Core.Workflow;

/// <summary>
/// Generic interface for workflow orchestration.
/// Extends beyond CodingState to support any workflow execution context.
/// </summary>
/// <typeparam name="TState">Type of workflow state.</typeparam>
public interface IWorkflowOrchestrator<TState> where TState : class
{
    /// <summary>
    /// Executes a workflow from a definition.
    /// Streams state updates as workflow progresses through nodes.
    /// </summary>
    /// <param name="workflow">Workflow definition to execute.</param>
    /// <param name="input">Initial input for the workflow.</param>
    /// <param name="context">Optional execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of state updates.</returns>
    IAsyncEnumerable<TState> ExecuteAsync(
        WorkflowDefinition workflow,
        string input,
        WorkflowExecutionContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a workflow from a checkpoint.
    /// </summary>
    /// <param name="checkpointId">Checkpoint identifier to resume from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of state updates from checkpoint.</returns>
    IAsyncEnumerable<TState> ResumeFromCheckpointAsync(
        string checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles approval for a workflow waiting at a human gate.
    /// </summary>
    /// <param name="executionId">Workflow execution identifier.</param>
    /// <param name="decision">Approval decision.</param>
    /// <returns>Task that completes when approval is processed.</returns>
    Task ApproveAsync(string executionId, ApprovalDecision decision);

    /// <summary>
    /// Cancels an active workflow execution.
    /// </summary>
    /// <param name="executionId">Workflow execution identifier.</param>
    /// <returns>Task that completes when cancellation is processed.</returns>
    Task CancelAsync(string executionId);

    /// <summary>
    /// Gets the current state of a workflow execution.
    /// </summary>
    /// <param name="executionId">Workflow execution identifier.</param>
    /// <returns>Current state snapshot.</returns>
    Task<TState> GetStateAsync(string executionId);

    /// <summary>
    /// Lists active workflow executions.
    /// </summary>
    /// <returns>Collection of active execution summaries.</returns>
    Task<IReadOnlyList<WorkflowExecutionSummary>> ListActiveExecutionsAsync();
}

/// <summary>
/// Context for workflow execution.
/// </summary>
public sealed record WorkflowExecutionContext
{
    /// <summary>
    /// Base directory for agent resolution.
    /// </summary>
    public string? AgentsDirectory { get; init; }

    /// <summary>
    /// Working directory for the workflow.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Additional metadata for the execution.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Parent execution ID for nested workflows.
    /// </summary>
    public string? ParentExecutionId { get; init; }
}

/// <summary>
/// Summary of a workflow execution.
/// </summary>
public sealed record WorkflowExecutionSummary
{
    /// <summary>
    /// Unique execution identifier.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Workflow name being executed.
    /// </summary>
    public required string WorkflowName { get; init; }

    /// <summary>
    /// Current state/node in the workflow.
    /// </summary>
    public required string CurrentState { get; init; }

    /// <summary>
    /// Execution status.
    /// </summary>
    public required WorkflowExecutionStatus Status { get; init; }

    /// <summary>
    /// When execution started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public required DateTimeOffset LastUpdatedAt { get; init; }
}

/// <summary>
/// Status of workflow execution.
/// </summary>
public enum WorkflowExecutionStatus
{
    /// <summary>Execution is running.</summary>
    Running,

    /// <summary>Waiting for human approval.</summary>
    WaitingForApproval,

    /// <summary>Waiting for trigger condition.</summary>
    WaitingForTrigger,

    /// <summary>Execution completed successfully.</summary>
    Completed,

    /// <summary>Execution failed with error.</summary>
    Failed,

    /// <summary>Execution was cancelled.</summary>
    Cancelled
}
