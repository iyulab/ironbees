namespace Ironbees.WebApi.Models;

/// <summary>
/// Request for conversation-based chat
/// </summary>
public class ConversationChatRequest
{
    /// <summary>
    /// Session ID (optional - will be created if not provided)
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// User message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Agent name (optional - auto-select if not provided)
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Maximum number of previous messages to include as context
    /// </summary>
    public int MaxContextMessages { get; set; } = 10;
}
