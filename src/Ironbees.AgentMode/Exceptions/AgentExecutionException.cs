namespace Ironbees.AgentMode;

/// <summary>
/// Exception thrown when agent execution fails.
/// </summary>
public class AgentExecutionException : AgentModeException
{
    public string AgentName { get; init; }

    public AgentExecutionException(string agentName, string message, Exception? inner = null)
        : base($"Agent '{agentName}' execution failed: {message}", inner!)
    {
        AgentName = agentName;
    }
}
