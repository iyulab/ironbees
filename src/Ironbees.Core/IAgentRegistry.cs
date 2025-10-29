namespace Ironbees.Core;

/// <summary>
/// Registry for managing loaded agents
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Register an agent
    /// </summary>
    /// <param name="name">Agent name</param>
    /// <param name="agent">Agent instance</param>
    void Register(string name, IAgent agent);

    /// <summary>
    /// Get an agent by name
    /// </summary>
    /// <param name="name">Agent name</param>
    /// <returns>Agent instance or null if not found</returns>
    IAgent? Get(string name);

    /// <summary>
    /// Try to get an agent by name
    /// </summary>
    /// <param name="name">Agent name</param>
    /// <param name="agent">Agent instance if found</param>
    /// <returns>True if agent found</returns>
    bool TryGet(string name, out IAgent? agent);

    /// <summary>
    /// List all registered agent names
    /// </summary>
    /// <returns>Collection of agent names</returns>
    IReadOnlyCollection<string> ListAgents();

    /// <summary>
    /// Check if agent is registered
    /// </summary>
    /// <param name="name">Agent name</param>
    /// <returns>True if registered</returns>
    bool Contains(string name);

    /// <summary>
    /// Unregister an agent
    /// </summary>
    /// <param name="name">Agent name</param>
    void Unregister(string name);

    /// <summary>
    /// Clear all registered agents
    /// </summary>
    void Clear();
}
