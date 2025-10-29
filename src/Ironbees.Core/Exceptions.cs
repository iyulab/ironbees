namespace Ironbees.Core;

/// <summary>
/// Exception thrown when an agent is not found
/// </summary>
public class AgentNotFoundException : Exception
{
    public AgentNotFoundException(string agentName)
        : base($"Agent '{agentName}' not found.")
    {
        AgentName = agentName;
    }

    public AgentNotFoundException(string agentName, string message)
        : base(message)
    {
        AgentName = agentName;
    }

    public string AgentName { get; }
}

/// <summary>
/// Exception thrown when agent configuration is invalid
/// </summary>
public class AgentConfigurationException : Exception
{
    public AgentConfigurationException(string message)
        : base(message)
    {
    }

    public AgentConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when agent loading fails
/// </summary>
public class AgentLoadException : Exception
{
    public AgentLoadException(string message)
        : base(message)
    {
    }

    public AgentLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when agent directory structure is invalid
/// </summary>
public class InvalidAgentDirectoryException : Exception
{
    public InvalidAgentDirectoryException(string agentPath, string message)
        : base($"Invalid agent directory '{agentPath}': {message}")
    {
        AgentPath = agentPath;
    }

    public string AgentPath { get; }
}
