namespace Ironbees.AgentMode.Models;

/// <summary>
/// Response from an agent execution.
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// Updated state fields (partial update, not full state).
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Updates { get; init; }

    /// <summary>
    /// Next node to transition to (e.g., "CODE", "VALIDATE", "END").
    /// If null, orchestrator decides based on workflow graph.
    /// </summary>
    public string? NextNode { get; init; }

    /// <summary>
    /// Optional metadata for logging, telemetry, and debugging.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
