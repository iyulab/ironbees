namespace Ironbees.AgentMode;

/// <summary>
/// Exception thrown when tool is not found.
/// </summary>
public class ToolNotFoundException : AgentModeException
{
    public string ToolName { get; init; }

    public ToolNotFoundException(string toolName)
        : base($"Tool '{toolName}' not found in any registered MCP server")
    {
        ToolName = toolName;
    }
}
