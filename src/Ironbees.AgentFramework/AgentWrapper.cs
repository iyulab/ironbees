using Ironbees.Core;
using OpenAI.Chat;

namespace Ironbees.AgentFramework;

/// <summary>
/// Wrapper for ChatClient-based agent
/// </summary>
internal class AgentWrapper : IAgent
{
    public AgentWrapper(ChatClient chatClient, AgentConfig config)
    {
        ChatClient = chatClient;
        Config = config;
        Name = config.Name;
        Description = config.Description;
    }

    public ChatClient ChatClient { get; }
    public string Name { get; }
    public string Description { get; }
    public AgentConfig Config { get; }
}
