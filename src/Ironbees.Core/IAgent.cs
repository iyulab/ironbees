namespace Ironbees.Core;

/// <summary>
/// Represents an LLM agent with specific capabilities and configuration
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Unique name of the agent
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Agent configuration
    /// </summary>
    AgentConfig Config { get; }
}
