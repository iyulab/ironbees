namespace Ironbees.Core;

/// <summary>
/// Orchestrates agent loading, selection, and execution
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Load all agents from configured directory
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LoadAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Process input with a specific agent
    /// </summary>
    /// <param name="input">User input</param>
    /// <param name="agentName">Agent name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response</returns>
    Task<string> ProcessAsync(
        string input,
        string agentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process input with automatic agent selection
    /// </summary>
    /// <param name="input">User input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response</returns>
    Task<string> ProcessAsync(
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream response from a specific agent
    /// </summary>
    /// <param name="input">User input</param>
    /// <param name="agentName">Agent name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of response chunks</returns>
    IAsyncEnumerable<string> StreamAsync(
        string input,
        string agentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all available agent names
    /// </summary>
    /// <returns>Collection of agent names</returns>
    IReadOnlyCollection<string> ListAgents();

    /// <summary>
    /// Get a specific agent by name
    /// </summary>
    /// <param name="name">Agent name</param>
    /// <returns>Agent instance or null</returns>
    IAgent? GetAgent(string name);
}
