namespace Ironbees.Core.Conversation;

/// <summary>
/// Interface for conversation state persistence.
/// Implementations should follow Ironbees' file-based observability philosophy.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Saves a conversation state.
    /// </summary>
    /// <param name="state">Conversation state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(ConversationState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a conversation state by ID.
    /// </summary>
    /// <param name="conversationId">Unique conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conversation state if found, null otherwise.</returns>
    Task<ConversationState?> LoadAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a conversation by ID.
    /// </summary>
    /// <param name="conversationId">Unique conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all conversation IDs.
    /// </summary>
    /// <param name="agentName">Optional filter by agent name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of conversation IDs.</returns>
    Task<IReadOnlyList<string>> ListAsync(string? agentName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a conversation exists.
    /// </summary>
    /// <param name="conversationId">Unique conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if exists, false otherwise.</returns>
    Task<bool> ExistsAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a message to an existing conversation.
    /// Creates the conversation if it doesn't exist.
    /// </summary>
    /// <param name="conversationId">Unique conversation identifier.</param>
    /// <param name="message">Message to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendMessageAsync(string conversationId, ConversationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the message count for a conversation.
    /// </summary>
    /// <param name="conversationId">Unique conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Message count, or 0 if conversation doesn't exist.</returns>
    Task<int> GetMessageCountAsync(string conversationId, CancellationToken cancellationToken = default);
}
