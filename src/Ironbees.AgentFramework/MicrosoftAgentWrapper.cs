using Ironbees.Core;
using Microsoft.Agents.AI;

namespace Ironbees.AgentFramework;

/// <summary>
/// Wrapper for Microsoft Agent Framework AIAgent
/// </summary>
internal class MicrosoftAgentWrapper : IAgent
{
    public MicrosoftAgentWrapper(AIAgent aiAgent, AgentConfig config)
    {
        AIAgent = aiAgent;
        Config = config;
        Name = config.Name;
        Description = config.Description;
    }

    public AIAgent AIAgent { get; }
    public string Name { get; }
    public string Description { get; }
    public AgentConfig Config { get; }
}
