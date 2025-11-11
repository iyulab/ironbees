using Ironbees.AgentMode.Models;

namespace Ironbees.AgentMode;

/// <summary>
/// Stateful graph orchestrator for autonomous coding workflows.
/// Manages state transitions, agent coordination, and human-in-the-loop approvals.
/// </summary>
public interface IStatefulOrchestrator
{
    /// <summary>
    /// Executes a coding workflow from user request to completion.
    /// Streams state updates as workflow progresses through nodes.
    /// </summary>
    /// <param name="request">User's natural language request (e.g., "Add authentication to UserController")</param>
    /// <param name="context">Optional context (solution path, project references, etc.)</param>
    /// <param name="cancellationToken">Cancellation token for workflow interruption</param>
    /// <returns>Async stream of CodingState updates for real-time monitoring</returns>
    /// <exception cref="ArgumentNullException">When request is null or empty</exception>
    /// <exception cref="OrchestratorException">When workflow execution fails</exception>
    IAsyncEnumerable<CodingState> ExecuteAsync(
        string request,
        WorkflowContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves or rejects a workflow waiting for human decision.
    /// Used for HITL (Human-in-the-Loop) approval gates.
    /// </summary>
    /// <param name="stateId">Unique identifier of the workflow state</param>
    /// <param name="decision">Approval decision with optional feedback</param>
    /// <returns>Task that completes when approval is processed</returns>
    /// <exception cref="StateNotFoundException">When stateId doesn't exist</exception>
    /// <exception cref="InvalidStateException">When state is not awaiting approval</exception>
    Task ApproveAsync(string stateId, ApprovalDecision decision);

    /// <summary>
    /// Cancels an active workflow execution.
    /// </summary>
    /// <param name="stateId">Unique identifier of the workflow state</param>
    /// <returns>Task that completes when cancellation is processed</returns>
    /// <exception cref="StateNotFoundException">When stateId doesn't exist</exception>
    Task CancelAsync(string stateId);

    /// <summary>
    /// Retrieves the current state of a workflow.
    /// </summary>
    /// <param name="stateId">Unique identifier of the workflow state</param>
    /// <returns>Current CodingState snapshot</returns>
    /// <exception cref="StateNotFoundException">When stateId doesn't exist</exception>
    Task<CodingState> GetStateAsync(string stateId);
}
