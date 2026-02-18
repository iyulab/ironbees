using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ironbees.Core.Conversation;

/// <summary>
/// File system-based conversation store.
/// Stores conversations as JSON files for observability (ls, grep, cat compatible).
///
/// Directory structure:
///   {baseDirectory}/
///     {conversationId}.json           (if no agent name)
///     {agentName}/{conversationId}.json  (if agent name specified)
/// </summary>
public sealed partial class FileSystemConversationStore : IConversationStore, IDisposable
{
    private readonly string _baseDirectory;
    private readonly ILogger<FileSystemConversationStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const string FileExtension = ".json";

    /// <summary>
    /// Initializes a new instance of <see cref="FileSystemConversationStore"/>.
    /// </summary>
    /// <param name="baseDirectory">Base directory for storing conversations.</param>
    /// <param name="logger">Optional logger.</param>
    public FileSystemConversationStore(
        string baseDirectory,
        ILogger<FileSystemConversationStore>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be null or empty.", nameof(baseDirectory));

        _baseDirectory = baseDirectory;
        _logger = logger ?? NullLogger<FileSystemConversationStore>.Instance;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Ensure base directory exists
        Directory.CreateDirectory(_baseDirectory);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(state.ConversationId))
            throw new ArgumentException("Conversation ID cannot be null or empty.", nameof(state));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetFilePath(state.ConversationId, state.AgentName);
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogSavedConversation(_logger, state.ConversationId, filePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<ConversationState?> LoadAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Conversation ID cannot be null or empty.", nameof(conversationId));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Try to find the file (it could be in root or in an agent subdirectory)
            var filePath = FindConversationFile(conversationId);

            if (filePath == null || !File.Exists(filePath))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    LogConversationNotFound(_logger, conversationId);
                }
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var state = JsonSerializer.Deserialize<ConversationState>(json, _jsonOptions);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogLoadedConversation(_logger, conversationId, filePath);
            }
            return state;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Conversation ID cannot be null or empty.", nameof(conversationId));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = FindConversationFile(conversationId);

            if (filePath == null || !File.Exists(filePath))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    LogConversationNotFoundForDeletion(_logger, conversationId);
                }
                return false;
            }

            File.Delete(filePath);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogDeletedConversation(_logger, conversationId);
            }
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListAsync(string? agentName = null, CancellationToken cancellationToken = default)
    {
        var searchDirectory = string.IsNullOrEmpty(agentName)
            ? _baseDirectory
            : Path.Combine(_baseDirectory, agentName);

        if (!Directory.Exists(searchDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var pattern = $"*{FileExtension}";
        var searchOption = string.IsNullOrEmpty(agentName)
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory.GetFiles(searchDirectory, pattern, searchOption);
        var conversationIds = files
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogListedConversations(_logger, conversationIds.Count, agentName ?? "all");
        }
        return Task.FromResult<IReadOnlyList<string>>(conversationIds);
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Conversation ID cannot be null or empty.", nameof(conversationId));

        var filePath = FindConversationFile(conversationId);
        return Task.FromResult(filePath != null && File.Exists(filePath));
    }

    /// <inheritdoc/>
    public async Task AppendMessageAsync(string conversationId, ConversationMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Conversation ID cannot be null or empty.", nameof(conversationId));

        ArgumentNullException.ThrowIfNull(message);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var existing = await LoadInternalAsync(conversationId, cancellationToken);

            if (existing == null)
            {
                // Create new conversation
                existing = new ConversationState
                {
                    ConversationId = conversationId,
                    Messages = [message],
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };
            }
            else
            {
                // Append to existing
                var messages = existing.Messages.ToList();
                messages.Add(message);

                existing = new ConversationState
                {
                    ConversationId = existing.ConversationId,
                    AgentName = existing.AgentName,
                    Messages = messages,
                    CreatedAt = existing.CreatedAt,
                    LastUpdatedAt = DateTimeOffset.UtcNow,
                    Metadata = existing.Metadata
                };
            }

            await SaveInternalAsync(existing, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogAppendedMessage(_logger, conversationId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetMessageCountAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(conversationId, cancellationToken);
        return state?.Messages.Count ?? 0;
    }

    /// <summary>
    /// Disposes the lock semaphore.
    /// </summary>
    public void Dispose()
    {
        _lock.Dispose();
    }

    private string GetFilePath(string conversationId, string? agentName)
    {
        var fileName = $"{conversationId}{FileExtension}";

        return string.IsNullOrEmpty(agentName)
            ? Path.Combine(_baseDirectory, fileName)
            : Path.Combine(_baseDirectory, agentName, fileName);
    }

    private string? FindConversationFile(string conversationId)
    {
        var fileName = $"{conversationId}{FileExtension}";

        // First check root directory
        var rootPath = Path.Combine(_baseDirectory, fileName);
        if (File.Exists(rootPath))
        {
            return rootPath;
        }

        // Then search subdirectories
        if (Directory.Exists(_baseDirectory))
        {
            var files = Directory.GetFiles(_baseDirectory, fileName, SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return files[0];
            }
        }

        return null;
    }

    private async Task<ConversationState?> LoadInternalAsync(string conversationId, CancellationToken cancellationToken)
    {
        var filePath = FindConversationFile(conversationId);

        if (filePath == null || !File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<ConversationState>(json, _jsonOptions);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved conversation {ConversationId} to {FilePath}")]
    private static partial void LogSavedConversation(ILogger logger, string conversationId, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Conversation {ConversationId} not found")]
    private static partial void LogConversationNotFound(ILogger logger, string conversationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded conversation {ConversationId} from {FilePath}")]
    private static partial void LogLoadedConversation(ILogger logger, string conversationId, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Conversation {ConversationId} not found for deletion")]
    private static partial void LogConversationNotFoundForDeletion(ILogger logger, string conversationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted conversation {ConversationId}")]
    private static partial void LogDeletedConversation(ILogger logger, string conversationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listed {Count} conversations for agent {AgentName}")]
    private static partial void LogListedConversations(ILogger logger, int count, string agentName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Appended message to conversation {ConversationId}")]
    private static partial void LogAppendedMessage(ILogger logger, string conversationId);

    private async Task SaveInternalAsync(ConversationState state, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(state.ConversationId, state.AgentName);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
