namespace Ironbees.AgentMode.MCP;

/// <summary>
/// JSON Schema definition for tool input validation.
/// Follows JSON Schema specification (draft-07).
/// </summary>
public record JsonSchema
{
    public string Type { get; init; } = "object";
    public Dictionary<string, JsonSchema>? Properties { get; init; }
    public string[]? Required { get; init; }
    public string? Description { get; init; }
}
