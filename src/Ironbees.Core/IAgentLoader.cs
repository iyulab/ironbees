namespace Ironbees.Core;

/// <summary>
/// Loads agent configurations from various sources
/// </summary>
public interface IAgentLoader
{
    /// <summary>
    /// Load a single agent configuration from a directory
    /// </summary>
    /// <param name="agentPath">Path to agent directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent configuration</returns>
    Task<AgentConfig> LoadConfigAsync(
        string agentPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load all agent configurations from a directory
    /// </summary>
    /// <param name="agentsDirectory">Path to agents directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of agent configurations</returns>
    Task<IReadOnlyList<AgentConfig>> LoadAllConfigsAsync(
        string? agentsDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that an agent directory has required files
    /// </summary>
    /// <param name="agentPath">Path to agent directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if valid</returns>
    Task<bool> ValidateAgentDirectoryAsync(
        string agentPath,
        CancellationToken cancellationToken = default);
}
