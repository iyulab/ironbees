using Ironbees.AgentMode.Models;

namespace Ironbees.AgentMode.Agents;

/// <summary>
/// Base interface for coding agents that execute specific tasks in the workflow.
/// Agents are stateless and receive CodingState as input.
/// </summary>
public interface ICodingAgent
{
    /// <summary>
    /// Unique name of the agent (e.g., "planner", "coder", "validator").
    /// Used for logging, telemetry, and agent selection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Agent description for documentation and debugging.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// List of tools (MCP servers) this agent requires.
    /// Used by orchestrator to validate tool availability.
    /// </summary>
    IReadOnlyList<string> RequiredTools { get; }

    /// <summary>
    /// Executes the agent's task based on current CodingState.
    /// </summary>
    /// <param name="state">Current workflow state with all context</param>
    /// <param name="cancellationToken">Cancellation token for interruption</param>
    /// <returns>Agent's response with updated state fields</returns>
    /// <exception cref="AgentExecutionException">When agent execution fails</exception>
    Task<AgentResponse> ExecuteAsync(
        CodingState state,
        CancellationToken cancellationToken = default);
}
