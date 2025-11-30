using Ironbees.AgentMode.Core.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Ironbees.AgentFramework.Workflow;

/// <summary>
/// Defines the contract for executing MAF workflows converted from Ironbees YAML workflow definitions.
/// This interface bridges Ironbees' file-based workflow definitions with MAF's workflow execution engine.
/// </summary>
public interface IMafWorkflowExecutor
{
    /// <summary>
    /// Executes a workflow definition by converting it to MAF format and running it.
    /// </summary>
    /// <param name="definition">The workflow definition to execute.</param>
    /// <param name="input">The initial input for the workflow.</param>
    /// <param name="agentResolver">A function that resolves agent names to AIAgent instances.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An async enumerable of workflow execution events.</returns>
    IAsyncEnumerable<WorkflowExecutionEvent> ExecuteAsync(
        WorkflowDefinition definition,
        string input,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a pre-converted MAF workflow.
    /// </summary>
    /// <param name="workflow">The MAF workflow to execute.</param>
    /// <param name="input">The initial input for the workflow.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An async enumerable of workflow execution events.</returns>
    IAsyncEnumerable<WorkflowExecutionEvent> ExecuteWorkflowAsync(
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a workflow definition with checkpointing enabled.
    /// Checkpoints are automatically saved via the configured ICheckpointStore.
    /// </summary>
    /// <param name="definition">The workflow definition to execute.</param>
    /// <param name="input">The initial input for the workflow.</param>
    /// <param name="executionId">Unique identifier for this execution (used for checkpoint grouping).</param>
    /// <param name="agentResolver">A function that resolves agent names to AIAgent instances.</param>
    /// <param name="checkpointStore">The store for persisting checkpoints.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An async enumerable of workflow execution events with checkpoint data.</returns>
    IAsyncEnumerable<WorkflowExecutionEvent> ExecuteWithCheckpointingAsync(
        WorkflowDefinition definition,
        string input,
        string executionId,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        ICheckpointStore checkpointStore,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes workflow execution from a saved checkpoint.
    /// </summary>
    /// <param name="workflow">The MAF workflow to resume.</param>
    /// <param name="checkpoint">The checkpoint data to resume from.</param>
    /// <param name="checkpointStore">The store for persisting new checkpoints.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An async enumerable of workflow execution events.</returns>
    IAsyncEnumerable<WorkflowExecutionEvent> ResumeFromCheckpointAsync(
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        CheckpointData checkpoint,
        ICheckpointStore checkpointStore,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an event during workflow execution.
/// </summary>
public record WorkflowExecutionEvent
{
    /// <summary>
    /// The type of the event.
    /// </summary>
    public required WorkflowExecutionEventType Type { get; init; }

    /// <summary>
    /// The name of the agent that produced this event, if applicable.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// The content or message associated with this event.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// The checkpoint information for state persistence, if available.
    /// </summary>
    public object? Checkpoint { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional metadata associated with this event.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Types of workflow execution events.
/// </summary>
public enum WorkflowExecutionEventType
{
    /// <summary>
    /// Workflow execution has started.
    /// </summary>
    WorkflowStarted,

    /// <summary>
    /// An agent has started processing.
    /// </summary>
    AgentStarted,

    /// <summary>
    /// An agent has produced a message or output.
    /// </summary>
    AgentMessage,

    /// <summary>
    /// An agent has completed processing.
    /// </summary>
    AgentCompleted,

    /// <summary>
    /// A workflow super-step has completed (checkpoint available).
    /// </summary>
    SuperStepCompleted,

    /// <summary>
    /// Workflow execution has completed successfully.
    /// </summary>
    WorkflowCompleted,

    /// <summary>
    /// An error occurred during workflow execution.
    /// </summary>
    Error
}
