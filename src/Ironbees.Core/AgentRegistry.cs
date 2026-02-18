using System.Collections.Concurrent;

namespace Ironbees.Core;

/// <summary>
/// Thread-safe registry for managing loaded agents
/// </summary>
public class AgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, IAgent> _agents = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(string name, IAgent agent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(agent);

        if (!_agents.TryAdd(name, agent))
        {
            throw new InvalidOperationException($"Agent '{name}' is already registered.");
        }
    }

    /// <inheritdoc />
    public IAgent? GetAgent(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _agents.TryGetValue(name, out var agent) ? agent : null;
    }

    /// <inheritdoc />
    public bool TryGet(string name, out IAgent? agent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _agents.TryGetValue(name, out agent);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ListAgents()
    {
        return _agents.Keys.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public bool Contains(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _agents.ContainsKey(name);
    }

    /// <inheritdoc />
    public void Unregister(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _agents.TryRemove(name, out _);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _agents.Clear();
    }
}
