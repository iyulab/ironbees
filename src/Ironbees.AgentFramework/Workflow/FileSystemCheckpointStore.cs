using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ironbees.AgentFramework.Workflow;

/// <summary>
/// File system-based implementation of <see cref="ICheckpointStore"/> that persists
/// workflow checkpoints as JSON files organized by execution ID.
/// </summary>
/// <remarks>
/// Directory structure: {rootPath}/checkpoints/{executionId}/{checkpointId}.json
/// This follows Ironbees' "File System = Single Source of Truth" philosophy,
/// enabling checkpoint data to be inspected with standard file tools.
/// </remarks>
public sealed partial class FileSystemCheckpointStore : ICheckpointStore, IDisposable
{
    private readonly string _rootPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger<FileSystemCheckpointStore>? _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FileSystemCheckpointStore"/>.
    /// </summary>
    /// <param name="rootPath">The root directory for storing checkpoint files.</param>
    /// <param name="jsonOptions">Optional JSON serializer options.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public FileSystemCheckpointStore(
        string rootPath,
        JsonSerializerOptions? jsonOptions = null,
        ILogger<FileSystemCheckpointStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _rootPath = Path.GetFullPath(Path.Combine(rootPath, "checkpoints"));
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        _logger = logger;

        Directory.CreateDirectory(_rootPath);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CheckpointData checkpoint, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(checkpoint);

        var filePath = GetFilePath(checkpoint.ExecutionId, checkpoint.CheckpointId);
        var directory = Path.GetDirectoryName(filePath)!;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
            {
                LogSavedCheckpoint(_logger, checkpoint.CheckpointId, checkpoint.ExecutionId, filePath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<CheckpointData?> GetAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);

        // Search all execution directories for the checkpoint
        if (!Directory.Exists(_rootPath))
            return null;

        foreach (var executionDir in Directory.EnumerateDirectories(_rootPath))
        {
            var filePath = Path.Combine(executionDir, $"{checkpointId}.json");
            if (File.Exists(filePath))
            {
                return await ReadCheckpointAsync(filePath, cancellationToken);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<CheckpointData?> GetLatestForExecutionAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        var checkpoints = await GetAllForExecutionAsync(executionId, cancellationToken);
        return checkpoints.Count > 0 ? checkpoints[^1] : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CheckpointData>> GetAllForExecutionAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        var executionPath = GetExecutionPath(executionId);
        if (!Directory.Exists(executionPath))
            return Array.Empty<CheckpointData>();

        var checkpoints = new List<CheckpointData>();

        foreach (var filePath in Directory.EnumerateFiles(executionPath, "*.json"))
        {
            var checkpoint = await ReadCheckpointAsync(filePath, cancellationToken);
            if (checkpoint != null)
            {
                checkpoints.Add(checkpoint);
            }
        }

        // Sort by creation time
        return checkpoints
            .OrderBy(c => c.CreatedAt)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);

        // First find the checkpoint
        var checkpoint = await GetAsync(checkpointId, cancellationToken);
        if (checkpoint == null)
            return false;

        var filePath = GetFilePath(checkpoint.ExecutionId, checkpointId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);

                if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
                {
                    LogDeletedCheckpoint(_logger, checkpointId, filePath);
                }

                // Clean up empty execution directory
                CleanupEmptyDirectory(Path.GetDirectoryName(filePath)!);

                return true;
            }
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAllForExecutionAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        var executionPath = GetExecutionPath(executionId);
        if (!Directory.Exists(executionPath))
            return 0;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var files = Directory.GetFiles(executionPath, "*.json");
            foreach (var file in files)
            {
                File.Delete(file);
            }

            // Remove the execution directory
            CleanupEmptyDirectory(executionPath);

            if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
            {
                LogDeletedAllCheckpoints(_logger, files.Length, executionId);
            }

            return files.Length;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOlderThanAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Directory.Exists(_rootPath))
            return 0;

        var deletedCount = 0;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var executionDir in Directory.EnumerateDirectories(_rootPath))
            {
                foreach (var filePath in Directory.EnumerateFiles(executionDir, "*.json"))
                {
                    var checkpoint = await ReadCheckpointAsync(filePath, cancellationToken);
                    if (checkpoint != null && checkpoint.CreatedAt < olderThan)
                    {
                        File.Delete(filePath);
                        deletedCount++;
                    }
                }

                // Clean up empty execution directory
                CleanupEmptyDirectory(executionDir);
            }

            if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
            {
                LogCleanedUpCheckpoints(_logger, deletedCount, olderThan);
            }

            return deletedCount;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);

        // Search all execution directories for the checkpoint
        if (!Directory.Exists(_rootPath))
            return Task.FromResult(false);

        foreach (var executionDir in Directory.EnumerateDirectories(_rootPath))
        {
            var filePath = Path.Combine(executionDir, $"{checkpointId}.json");
            if (File.Exists(filePath))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the file path for a checkpoint.
    /// </summary>
    private string GetFilePath(string executionId, string checkpointId)
    {
        // Sanitize IDs to prevent path traversal
        var safeExecutionId = SanitizeId(executionId);
        var safeCheckpointId = SanitizeId(checkpointId);

        return Path.Combine(_rootPath, safeExecutionId, $"{safeCheckpointId}.json");
    }

    /// <summary>
    /// Gets the execution directory path.
    /// </summary>
    private string GetExecutionPath(string executionId)
    {
        var safeExecutionId = SanitizeId(executionId);
        return Path.Combine(_rootPath, safeExecutionId);
    }

    /// <summary>
    /// Sanitizes an ID for safe use in file paths.
    /// </summary>
    private static string SanitizeId(string id)
    {
        // Replace invalid path characters with underscore
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = id;
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }
        return sanitized;
    }

    /// <summary>
    /// Reads a checkpoint from a file.
    /// </summary>
    private async Task<CheckpointData?> ReadCheckpointAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<CheckpointData>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null) { LogFailedToReadCheckpoint(_logger, ex, filePath); }
            return null;
        }
    }

    /// <summary>
    /// Removes an empty directory.
    /// </summary>
    private static void CleanupEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved checkpoint '{CheckpointId}' for execution '{ExecutionId}' to {FilePath}")]
    private static partial void LogSavedCheckpoint(ILogger logger, string checkpointId, string executionId, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted checkpoint '{CheckpointId}' from {FilePath}")]
    private static partial void LogDeletedCheckpoint(ILogger logger, string checkpointId, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted {Count} checkpoints for execution '{ExecutionId}'")]
    private static partial void LogDeletedAllCheckpoints(ILogger logger, int count, string executionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleaned up {Count} checkpoints older than {OlderThan}")]
    private static partial void LogCleanedUpCheckpoints(ILogger logger, int count, DateTimeOffset olderThan);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read checkpoint from {FilePath}")]
    private static partial void LogFailedToReadCheckpoint(ILogger logger, Exception exception, string filePath);

    /// <summary>
    /// Disposes resources used by this store.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _writeLock.Dispose();
        _disposed = true;
    }
}
