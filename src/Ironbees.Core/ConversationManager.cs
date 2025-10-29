using System.Collections.Concurrent;

namespace Ironbees.Core;

/// <summary>
/// In-memory implementation of conversation manager
/// </summary>
public class ConversationManager : IConversationManager
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();

    /// <inheritdoc/>
    public Task<ConversationSession> CreateSessionAsync(string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString();

        var session = new ConversationSession
        {
            SessionId = sessionId
        };

        if (!_sessions.TryAdd(sessionId, session))
        {
            throw new InvalidOperationException($"Session {sessionId} already exists");
        }

        return Task.FromResult(session);
    }

    /// <inheritdoc/>
    public Task<ConversationSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc/>
    public Task AddMessageAsync(string sessionId, ConversationMessage message)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        session.Messages.Add(message);
        session.LastActivityAt = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<ConversationMessage>> GetMessagesAsync(string sessionId, int? maxMessages = null)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        var messages = maxMessages.HasValue
            ? session.Messages.TakeLast(maxMessages.Value).ToList()
            : session.Messages.ToList();

        return Task.FromResult(messages);
    }

    /// <inheritdoc/>
    public Task ClearSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        session.Messages.Clear();
        session.LastActivityAt = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteSessionAsync(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<string>> GetActiveSessionsAsync()
    {
        var sessionIds = _sessions.Keys.ToList();
        return Task.FromResult(sessionIds);
    }

    /// <inheritdoc/>
    public Task<int> CleanupExpiredSessionsAsync(TimeSpan expirationTime)
    {
        var cutoffTime = DateTime.UtcNow - expirationTime;
        var expiredSessions = _sessions
            .Where(kvp => kvp.Value.LastActivityAt < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        return Task.FromResult(expiredSessions.Count);
    }
}
