namespace Ironbees.AgentMode;

/// <summary>
/// Exception thrown when tool execution fails.
/// </summary>
public class ToolExecutionException : AgentModeException
{
    public string ToolName { get; init; }
    public string ServerName { get; init; }

    public ToolExecutionException(string serverName, string toolName, string message, Exception? inner = null)
        : base($"Tool '{toolName}' from server '{serverName}' execution failed: {message}", inner!)
    {
        ServerName = serverName;
        ToolName = toolName;
    }
}
