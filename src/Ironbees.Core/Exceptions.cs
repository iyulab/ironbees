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

    public AgentConfigurationException(string agentPath, ValidationResult validationResult)
        : base(validationResult.GetFormattedErrors())
    {
        AgentPath = agentPath;
        ValidationResult = validationResult;
    }

    public string? AgentPath { get; }
    public ValidationResult? ValidationResult { get; }
}

/// <summary>
/// Exception thrown when YAML parsing fails with detailed diagnostic information
/// </summary>
public class YamlParsingException : AgentConfigurationException
{
    public YamlParsingException(string agentPath, string fileName, string yamlContent, Exception innerException)
        : base(BuildDetailedMessage(agentPath, fileName, innerException), innerException)
    {
        AgentPath = agentPath;
        FileName = fileName;
        YamlContent = yamlContent;
    }

    public new string AgentPath { get; }
    public string FileName { get; }
    public string YamlContent { get; }

    private static string BuildDetailedMessage(string agentPath, string fileName, Exception innerException)
    {
        var message = $"Failed to parse YAML file '{fileName}' in agent directory '{agentPath}':\n";
        message += $"  Error: {innerException.Message}\n";

        // Try to extract line/column information from YamlDotNet exceptions
        var exceptionMessage = innerException.Message;
        if (exceptionMessage.Contains("Line:") || exceptionMessage.Contains("line"))
        {
            message += $"  {exceptionMessage}\n";
        }

        message += "\nCommon YAML issues:\n";
        message += "  - Check indentation (use spaces, not tabs)\n";
        message += "  - Ensure proper key: value format\n";
        message += "  - Check for missing or extra quotes\n";
        message += "  - Validate list syntax (- item)";

        return message;
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

    public AgentLoadException(string message, IReadOnlyList<AgentLoadError> failedAgents)
        : base(BuildDetailedMessage(message, failedAgents))
    {
        FailedAgents = failedAgents;
    }

    /// <summary>
    /// Gets the list of agents that failed to load with their errors.
    /// </summary>
    public IReadOnlyList<AgentLoadError>? FailedAgents { get; }

    private static string BuildDetailedMessage(string message, IReadOnlyList<AgentLoadError> failedAgents)
    {
        if (failedAgents.Count == 0)
        {
            return message;
        }

        var details = string.Join(Environment.NewLine, failedAgents.Select(e =>
            $"  - {e.AgentName}: {e.Error.Message}"));
        return $"{message}{Environment.NewLine}Failed agents:{Environment.NewLine}{details}";
    }
}

/// <summary>
/// Represents an error that occurred while loading a specific agent.
/// </summary>
public sealed record AgentLoadError(string AgentName, string AgentPath, Exception Error);

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
