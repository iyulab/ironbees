namespace Ironbees.Core;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public class ConversationMessage
{
    /// <summary>
    /// Role of the message sender (user, assistant, system)
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Content of the message
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Agent name if the message is from an assistant
    /// </summary>
    public string? AgentName { get; set; }
}

/// <summary>
/// Represents a conversation session
/// </summary>
public class ConversationSession
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// Messages in this conversation
    /// </summary>
    public List<ConversationMessage> Messages { get; set; } = new();

    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional metadata for the session
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Manages conversation history and context
/// </summary>
public interface IConversationManager
{
    /// <summary>
    /// Create a new conversation session
    /// </summary>
    /// <param name="sessionId">Optional session ID (generated if not provided)</param>
    /// <returns>The created session</returns>
    Task<ConversationSession> CreateSessionAsync(string? sessionId = null);

    /// <summary>
    /// Get an existing session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>The session if found, null otherwise</returns>
    Task<ConversationSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Add a message to a session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="message">Message to add</param>
    Task AddMessageAsync(string sessionId, ConversationMessage message);

    /// <summary>
    /// Get messages from a session with optional filtering
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="maxMessages">Maximum number of recent messages to retrieve</param>
    /// <returns>List of messages</returns>
    Task<List<ConversationMessage>> GetMessagesAsync(string sessionId, int? maxMessages = null);

    /// <summary>
    /// Clear all messages in a session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    Task ClearSessionAsync(string sessionId);

    /// <summary>
    /// Delete a session completely
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    Task DeleteSessionAsync(string sessionId);

    /// <summary>
    /// Get all active sessions
    /// </summary>
    /// <returns>List of session IDs</returns>
    Task<List<string>> GetActiveSessionsAsync();

    /// <summary>
    /// Clean up expired sessions
    /// </summary>
    /// <param name="expirationTime">Sessions older than this will be deleted</param>
    /// <returns>Number of sessions deleted</returns>
    Task<int> CleanupExpiredSessionsAsync(TimeSpan expirationTime);
}
