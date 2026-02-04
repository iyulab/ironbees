using Ironbees.Core;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive;

/// <summary>
/// Wraps an IronHive IAgent to satisfy the Ironbees IAgent interface
/// </summary>
internal class IronhiveAgentWrapper : IAgent
{
    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public AgentConfig Config { get; }

    /// <summary>
    /// The underlying IronHive agent instance
    /// </summary>
    public IronHiveAgent IronhiveAgent { get; }

    public IronhiveAgentWrapper(IronHiveAgent ironhiveAgent, AgentConfig config)
    {
        IronhiveAgent = ironhiveAgent ?? throw new ArgumentNullException(nameof(ironhiveAgent));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Name = config.Name;
        Description = config.Description;
    }
}
