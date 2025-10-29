namespace Ironbees.WebApi.Models;

/// <summary>
/// Response for conversation-based chat
/// </summary>
public class ConversationChatResponse
{
    /// <summary>
    /// Session ID for this conversation
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// Assistant response message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Agent that processed the message
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// Confidence score (if auto-selected)
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Total messages in the conversation
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Whether this is a new session
    /// </summary>
    public bool IsNewSession { get; set; }
}
