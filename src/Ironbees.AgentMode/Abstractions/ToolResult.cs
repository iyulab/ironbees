namespace Ironbees.AgentMode.MCP;

/// <summary>
/// Tool execution result.
/// </summary>
public record ToolResult
{
    public bool Success { get; init; }
    public object? Content { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
