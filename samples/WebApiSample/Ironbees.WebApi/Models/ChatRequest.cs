namespace Ironbees.WebApi.Models;

/// <summary>
/// Request model for chat endpoint
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User message to process
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Optional agent name. If not provided, automatic selection will be used.
    /// </summary>
    public string? AgentName { get; set; }
}
