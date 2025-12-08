using Microsoft.Extensions.AI;

namespace Ironbees.Core.Conversation;

/// <summary>
/// Represents a serializable conversation state for persistence.
/// Designed for file-based storage following Ironbees' observability philosophy.
/// </summary>
public sealed class ConversationState
{
    /// <summary>
    /// Unique conversation identifier.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// Agent name associated with this conversation.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// List of messages in the conversation.
    /// </summary>
    public IReadOnlyList<ConversationMessage> Messages { get; init; } = [];

    /// <summary>
    /// When the conversation was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the conversation was last updated.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Custom metadata for the conversation.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Represents a single message in a conversation.
/// Simplified from ChatMessage for serialization.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>
    /// Role of the message sender (system, user, assistant, tool).
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Content of the message.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional tool call ID for tool messages.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Converts to Microsoft.Extensions.AI ChatMessage.
    /// </summary>
    public ChatMessage ToChatMessage()
    {
        var role = Role.ToLowerInvariant() switch
        {
            "system" => ChatRole.System,
            "user" => ChatRole.User,
            "assistant" => ChatRole.Assistant,
            "tool" => ChatRole.Tool,
            _ => ChatRole.User
        };

        return new ChatMessage(role, Content);
    }

    /// <summary>
    /// Creates from Microsoft.Extensions.AI ChatMessage.
    /// </summary>
    public static ConversationMessage FromChatMessage(ChatMessage message)
    {
        return new ConversationMessage
        {
            Role = message.Role.Value,
            Content = message.Text ?? string.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
