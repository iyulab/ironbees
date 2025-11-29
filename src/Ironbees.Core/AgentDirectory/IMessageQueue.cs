namespace Ironbees.Core.AgentDirectory;

/// <summary>
/// Interface for file-based message queuing between agents.
/// Implements the stigmergic collaboration pattern from research-04.
/// </summary>
/// <remarks>
/// Messages are stored as JSON files in the inbox/outbox directories.
/// File naming convention: {timestamp}_{id}.json
/// This enables:
/// - Observable message state via file system tools (ls, cat, grep)
/// - Natural ordering by file name (timestamp-based)
/// - Easy debugging and monitoring
/// - Process-independent message persistence
/// </remarks>
public interface IMessageQueue
{
    /// <summary>
    /// Gets the agent name this queue belongs to.
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Enqueues a message to the target agent's inbox.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message ID.</returns>
    Task<string> EnqueueAsync(AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next pending message from this agent's inbox.
    /// Messages are ordered by priority (descending) then timestamp (ascending).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next message, or null if the inbox is empty.</returns>
    Task<AgentMessage?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks at the next pending message without removing it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next message, or null if the inbox is empty.</returns>
    Task<AgentMessage?> PeekAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending messages in the inbox.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending messages ordered by priority and timestamp.</returns>
    Task<IReadOnlyList<AgentMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of pending messages in the inbox.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of pending messages.</returns>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as completed and moves it to the processed directory.
    /// </summary>
    /// <param name="messageId">The message ID to complete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was found and marked complete.</returns>
    Task<bool> CompleteAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as failed.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="error">Optional error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was found and marked failed.</returns>
    Task<bool> FailAsync(string messageId, string? error = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a result message to this agent's outbox.
    /// </summary>
    /// <param name="message">The result message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message ID.</returns>
    Task<string> PublishResultAsync(AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages from this agent's outbox.
    /// </summary>
    /// <param name="limit">Maximum number of messages to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of outbox messages.</returns>
    Task<IReadOnlyList<AgentMessage>> GetOutboxMessagesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired messages from the inbox.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of messages cleaned up.</returns>
    Task<int> CleanupExpiredMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to new messages in the inbox.
    /// </summary>
    /// <param name="handler">The handler to call when a new message arrives.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A disposable subscription.</returns>
    IDisposable Subscribe(Func<AgentMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating message queues.
/// </summary>
public interface IMessageQueueFactory
{
    /// <summary>
    /// Creates or gets a message queue for the specified agent.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <returns>The message queue for the agent.</returns>
    IMessageQueue GetQueue(string agentName);

    /// <summary>
    /// Sends a message from one agent to another.
    /// </summary>
    /// <param name="fromAgent">The sender agent name.</param>
    /// <param name="toAgent">The target agent name.</param>
    /// <param name="messageType">The message type.</param>
    /// <param name="payload">Optional payload.</param>
    /// <param name="priority">Message priority.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message ID.</returns>
    Task<string> SendAsync(
        string fromAgent,
        string toAgent,
        string messageType,
        object? payload = null,
        MessagePriority priority = MessagePriority.Normal,
        CancellationToken cancellationToken = default);
}
