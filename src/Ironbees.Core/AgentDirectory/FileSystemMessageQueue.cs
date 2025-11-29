using System.Collections.Concurrent;
using System.Text.Json;

namespace Ironbees.Core.AgentDirectory;

/// <summary>
/// File system implementation of <see cref="IMessageQueue"/>.
/// Implements stigmergic collaboration through file-based message passing.
/// </summary>
/// <remarks>
/// Thread-safe implementation supporting concurrent access.
/// Uses file locking for atomic operations where necessary.
/// </remarks>
public sealed class FileSystemMessageQueue : IMessageQueue, IDisposable
{
    private const string ProcessedSubdir = ".processed";
    private const string FailedSubdir = ".failed";

    private readonly IAgentDirectory _directory;
    private readonly SemaphoreSlim _dequeueLock = new(1, 1);
    private readonly FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<Guid, Func<AgentMessage, CancellationToken, Task>> _subscribers = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FileSystemMessageQueue"/>.
    /// </summary>
    /// <param name="directory">The agent directory.</param>
    /// <param name="enableWatcher">Whether to enable file system watcher for subscriptions.</param>
    public FileSystemMessageQueue(IAgentDirectory directory, bool enableWatcher = false)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));

        if (enableWatcher)
        {
            var inboxPath = _directory.GetSubdirectoryPath(AgentSubdirectory.Inbox);
            if (System.IO.Directory.Exists(inboxPath))
            {
                _watcher = new FileSystemWatcher(inboxPath, "*.json")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                _watcher.Created += OnFileCreated;
            }
        }
    }

    /// <inheritdoc />
    public string AgentName => _directory.AgentName;

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var fileName = message.ToFileName();
        var json = message.ToJson();

        await _directory.WriteFileAsync(AgentSubdirectory.Inbox, fileName, json, cancellationToken);

        return message.Id;
    }

    /// <inheritdoc />
    public async Task<AgentMessage?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await _dequeueLock.WaitAsync(cancellationToken);
        try
        {
            var messages = await GetPendingMessagesInternalAsync(cancellationToken);

            if (messages.Count == 0)
            {
                return null;
            }

            var message = messages[0];
            var processingMessage = message.WithStatus(MessageStatus.Processing);

            // Update the message status
            var fileName = message.ToFileName();
            await _directory.WriteFileAsync(
                AgentSubdirectory.Inbox,
                fileName,
                processingMessage.ToJson(),
                cancellationToken);

            return processingMessage;
        }
        finally
        {
            _dequeueLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AgentMessage?> PeekAsync(CancellationToken cancellationToken = default)
    {
        var messages = await GetPendingMessagesInternalAsync(cancellationToken);
        return messages.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        return await GetPendingMessagesInternalAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        var messages = await GetPendingMessagesInternalAsync(cancellationToken);
        return messages.Count;
    }

    /// <inheritdoc />
    public async Task<bool> CompleteAsync(string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var (found, message, fileName) = await FindMessageAsync(messageId, cancellationToken);
        if (!found || message == null || fileName == null)
        {
            return false;
        }

        // Update status
        var completedMessage = message.WithStatus(MessageStatus.Completed);

        // Move to processed directory
        await MoveToSubdirAsync(
            AgentSubdirectory.Inbox,
            fileName,
            ProcessedSubdir,
            completedMessage,
            cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> FailAsync(string messageId, string? error = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var (found, message, fileName) = await FindMessageAsync(messageId, cancellationToken);
        if (!found || message == null || fileName == null)
        {
            return false;
        }

        // Add error to metadata
        var metadata = message.Metadata != null
            ? new Dictionary<string, string>(message.Metadata)
            : new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(error))
        {
            metadata["error"] = error;
            metadata["failedAt"] = DateTimeOffset.UtcNow.ToString("O");
        }

        var failedMessage = message with
        {
            Status = MessageStatus.Failed,
            Metadata = metadata
        };

        // Move to failed directory
        await MoveToSubdirAsync(
            AgentSubdirectory.Inbox,
            fileName,
            FailedSubdir,
            failedMessage,
            cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<string> PublishResultAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var fileName = message.ToFileName();
        var json = message.ToJson();

        await _directory.WriteFileAsync(AgentSubdirectory.Outbox, fileName, json, cancellationToken);

        return message.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentMessage>> GetOutboxMessagesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Outbox, "*.json", cancellationToken);

        var messages = new List<AgentMessage>();
        foreach (var file in files.OrderByDescending(f => f).Take(limit))
        {
            var content = await _directory.ReadFileAsync(AgentSubdirectory.Outbox, file, cancellationToken);
            if (content != null)
            {
                try
                {
                    messages.Add(AgentMessage.FromJson(content));
                }
                catch (JsonException)
                {
                    // Skip invalid messages
                }
            }
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredMessagesAsync(CancellationToken cancellationToken = default)
    {
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Inbox, "*.json", cancellationToken);
        var cleaned = 0;

        foreach (var file in files)
        {
            var content = await _directory.ReadFileAsync(AgentSubdirectory.Inbox, file, cancellationToken);
            if (content == null) continue;

            try
            {
                var message = AgentMessage.FromJson(content);
                if (message.IsExpired)
                {
                    await _directory.DeleteFileAsync(AgentSubdirectory.Inbox, file, cancellationToken);
                    cleaned++;
                }
            }
            catch (JsonException)
            {
                // Skip invalid messages
            }
        }

        return cleaned;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(
        Func<AgentMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = Guid.NewGuid();
        _subscribers.TryAdd(subscriptionId, handler);

        return new Subscription(() => _subscribers.TryRemove(subscriptionId, out _));
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _watcher?.Dispose();
        _dequeueLock.Dispose();
        _disposed = true;
    }

    // Private helpers

    private async Task<List<AgentMessage>> GetPendingMessagesInternalAsync(CancellationToken cancellationToken)
    {
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Inbox, "*.json", cancellationToken);

        var messages = new List<AgentMessage>();
        foreach (var file in files)
        {
            var content = await _directory.ReadFileAsync(AgentSubdirectory.Inbox, file, cancellationToken);
            if (content == null) continue;

            try
            {
                var message = AgentMessage.FromJson(content);
                if (message.Status == MessageStatus.Pending && !message.IsExpired)
                {
                    messages.Add(message);
                }
            }
            catch (JsonException)
            {
                // Skip invalid messages
            }
        }

        // Sort by priority (descending) then timestamp (ascending)
        return messages
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.Timestamp)
            .ToList();
    }

    private async Task<(bool Found, AgentMessage? Message, string? FileName)> FindMessageAsync(
        string messageId,
        CancellationToken cancellationToken)
    {
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Inbox, "*.json", cancellationToken);

        foreach (var file in files)
        {
            var parsedId = AgentMessage.ParseIdFromFileName(file);
            if (parsedId == messageId)
            {
                var content = await _directory.ReadFileAsync(AgentSubdirectory.Inbox, file, cancellationToken);
                if (content != null)
                {
                    return (true, AgentMessage.FromJson(content), file);
                }
            }
        }

        return (false, null, null);
    }

    private async Task MoveToSubdirAsync(
        AgentSubdirectory sourceDir,
        string fileName,
        string targetSubdir,
        AgentMessage message,
        CancellationToken cancellationToken)
    {
        var sourceSubdirPath = _directory.GetSubdirectoryPath(sourceDir);
        var targetPath = Path.Combine(sourceSubdirPath, targetSubdir);

        // Ensure target directory exists
        if (!System.IO.Directory.Exists(targetPath))
        {
            System.IO.Directory.CreateDirectory(targetPath);
        }

        // Write to target
        var targetFile = Path.Combine(targetPath, fileName);
        await File.WriteAllTextAsync(targetFile, message.ToJson(), cancellationToken);

        // Delete from source
        await _directory.DeleteFileAsync(sourceDir, fileName, cancellationToken);
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_disposed || _subscribers.IsEmpty) return;

        try
        {
            // Small delay to ensure file is fully written
            await Task.Delay(50);

            var content = await File.ReadAllTextAsync(e.FullPath);
            var message = AgentMessage.FromJson(content);

            foreach (var handler in _subscribers.Values)
            {
                try
                {
                    await handler(message, CancellationToken.None);
                }
                catch
                {
                    // Ignore handler exceptions
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _disposeAction;
        private bool _disposed;

        public Subscription(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposeAction();
            _disposed = true;
        }
    }
}

/// <summary>
/// Factory implementation for creating file system message queues.
/// </summary>
public sealed class FileSystemMessageQueueFactory : IMessageQueueFactory
{
    private readonly string _agentsDirectory;
    private readonly bool _enableWatchers;
    private readonly ConcurrentDictionary<string, FileSystemMessageQueue> _queues = new();

    /// <summary>
    /// Initializes a new instance of <see cref="FileSystemMessageQueueFactory"/>.
    /// </summary>
    /// <param name="agentsDirectory">The root agents directory.</param>
    /// <param name="enableWatchers">Whether to enable file system watchers.</param>
    public FileSystemMessageQueueFactory(string agentsDirectory, bool enableWatchers = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentsDirectory);
        _agentsDirectory = agentsDirectory;
        _enableWatchers = enableWatchers;
    }

    /// <inheritdoc />
    public IMessageQueue GetQueue(string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        return _queues.GetOrAdd(agentName, name =>
        {
            var agentPath = Path.Combine(_agentsDirectory, name);
            var directory = new FileSystemAgentDirectory(name, agentPath);
            return new FileSystemMessageQueue(directory, _enableWatchers);
        });
    }

    /// <inheritdoc />
    public async Task<string> SendAsync(
        string fromAgent,
        string toAgent,
        string messageType,
        object? payload = null,
        MessagePriority priority = MessagePriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        var message = new AgentMessage
        {
            FromAgent = fromAgent,
            ToAgent = toAgent,
            MessageType = messageType,
            Payload = payload != null
                ? System.Text.Json.JsonSerializer.SerializeToElement(payload)
                : null,
            Priority = priority,
            ReplyTo = fromAgent // Default reply-to is the sender
        };

        var queue = GetQueue(toAgent);
        return await queue.EnqueueAsync(message, cancellationToken);
    }
}
