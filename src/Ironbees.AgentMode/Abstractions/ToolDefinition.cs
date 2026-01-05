namespace Ironbees.AgentMode.MCP;

/// <summary>
/// Tool definition following MCP specification.
/// </summary>
public record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonSchema InputSchema { get; init; }
}
