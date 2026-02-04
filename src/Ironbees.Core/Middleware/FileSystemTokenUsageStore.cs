using System.Collections.Concurrent;
using System.Text.Json;

namespace Ironbees.Core.Middleware;

/// <summary>
/// File system-based implementation of <see cref="ITokenUsageStore"/> that persists
/// token usage records as JSON files organized by date.
/// </summary>
/// <remarks>
/// Directory structure: {rootPath}/{yyyy}/{MM}/{dd}/{id}.json
/// This enables efficient time-based queries and cleanup operations.
/// </remarks>
public sealed class FileSystemTokenUsageStore : ITokenUsageStore, IDisposable
{
    private readonly string _rootPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _indexedFiles = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FileSystemTokenUsageStore"/>.
    /// </summary>
    /// <param name="rootPath">The root directory for storing usage files.</param>
    /// <param name="jsonOptions">Optional JSON serializer options.</param>
    public FileSystemTokenUsageStore(string rootPath, JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _rootPath = Path.GetFullPath(rootPath);
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        Directory.CreateDirectory(_rootPath);
    }

    /// <inheritdoc/>
    public async Task RecordAsync(TokenUsage usage, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(usage);

        var filePath = GetFilePath(usage);
        var directory = Path.GetDirectoryName(filePath)!;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(usage, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _indexedFiles.TryAdd(filePath, 0);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RecordBatchAsync(
        IEnumerable<TokenUsage> usages,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(usages);

        var usageList = usages.ToList();
        if (usageList.Count == 0)
            return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var usage in usageList)
            {
                var filePath = GetFilePath(usage);
                var directory = Path.GetDirectoryName(filePath)!;

                Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(usage, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
                _indexedFiles.TryAdd(filePath, 0);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TokenUsage>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await GetAllUsageInternalAsync(from, to, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TokenUsage>> GetUsageByAgentAsync(
        string agentName,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var allUsage = await GetAllUsageInternalAsync(from, to, cancellationToken);
        return allUsage
            .Where(u => string.Equals(u.AgentName, agentName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TokenUsage>> GetUsageBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var allUsage = await GetAllUsageInternalAsync(null, null, cancellationToken);
        return allUsage
            .Where(u => string.Equals(u.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<TokenUsageStatistics> GetStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var usages = await GetAllUsageInternalAsync(from, to, cancellationToken);

        var byModel = usages
            .GroupBy(u => u.ModelId)
            .ToDictionary(
                g => g.Key,
                g => new ModelUsageStatistics
                {
                    Requests = g.Count(),
                    InputTokens = g.Sum(u => u.InputTokens),
                    OutputTokens = g.Sum(u => u.OutputTokens),
                    EstimatedCost = g.Sum(u => u.EstimatedCost ?? 0)
                });

        var byAgent = usages
            .Where(u => !string.IsNullOrEmpty(u.AgentName))
            .GroupBy(u => u.AgentName!)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(u => u.TotalTokens));

        return new TokenUsageStatistics
        {
            TotalRequests = usages.Count,
            TotalInputTokens = usages.Sum(u => u.InputTokens),
            TotalOutputTokens = usages.Sum(u => u.OutputTokens),
            TotalEstimatedCost = usages.Sum(u => u.EstimatedCost ?? 0),
            ByModel = byModel,
            ByAgent = byAgent,
            From = from,
            To = to
        };
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(_rootPath))
            {
                foreach (var dir in Directory.GetDirectories(_rootPath))
                {
                    Directory.Delete(dir, recursive: true);
                }

                foreach (var file in Directory.GetFiles(_rootPath, "*.json"))
                {
                    File.Delete(file);
                }
            }

            _indexedFiles.Clear();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<int> ClearOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var deleted = 0;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var files = await GetUsageFilesAsync(null, cutoff, cancellationToken);

            foreach (var file in files)
            {
                var usage = await ReadUsageFileAsync(file, cancellationToken);
                if (usage != null && usage.Timestamp < cutoff)
                {
                    File.Delete(file);
                    _indexedFiles.TryRemove(file, out _);
                    deleted++;
                }
            }

            // Clean up empty directories
            CleanupEmptyDirectories(_rootPath);
        }
        finally
        {
            _writeLock.Release();
        }

        return deleted;
    }

    /// <summary>
    /// Disposes the store and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _writeLock.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Internal method for getting usage with nullable date parameters.
    /// </summary>
    private async Task<IReadOnlyList<TokenUsage>> GetAllUsageInternalAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var results = new List<TokenUsage>();
        var files = await GetUsageFilesAsync(from, to, cancellationToken);

        foreach (var file in files)
        {
            var usage = await ReadUsageFileAsync(file, cancellationToken);
            if (usage != null && IsInTimeRange(usage, from, to))
            {
                results.Add(usage);
            }
        }

        return results.OrderBy(u => u.Timestamp).ToList();
    }

    private string GetFilePath(TokenUsage usage)
    {
        var date = usage.Timestamp.UtcDateTime;
        return Path.Combine(
            _rootPath,
            date.Year.ToString("D4"),
            date.Month.ToString("D2"),
            date.Day.ToString("D2"),
            $"{usage.Id}.json");
    }

    private async Task<IReadOnlyList<string>> GetUsageFilesAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();

        if (!Directory.Exists(_rootPath))
            return files;

        // Determine date range for directory scanning
        var startDate = from?.UtcDateTime.Date ?? DateTime.MinValue;
        var endDate = to?.UtcDateTime.Date ?? DateTime.UtcNow.Date;

        foreach (var yearDir in Directory.GetDirectories(_rootPath))
        {
            if (!int.TryParse(Path.GetFileName(yearDir), out var year))
                continue;

            if (year < startDate.Year || year > endDate.Year)
                continue;

            foreach (var monthDir in Directory.GetDirectories(yearDir))
            {
                if (!int.TryParse(Path.GetFileName(monthDir), out var month))
                    continue;

                var monthStart = new DateTime(year, month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                if (monthEnd < startDate || monthStart > endDate)
                    continue;

                foreach (var dayDir in Directory.GetDirectories(monthDir))
                {
                    if (!int.TryParse(Path.GetFileName(dayDir), out var day))
                        continue;

                    try
                    {
                        var dirDate = new DateTime(year, month, day);
                        if (dirDate < startDate || dirDate > endDate)
                            continue;

                        files.AddRange(Directory.GetFiles(dayDir, "*.json"));
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Invalid date, skip
                    }
                }
            }
        }

        return files;
    }

    private async Task<TokenUsage?> ReadUsageFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<TokenUsage>(json, _jsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Log and skip corrupted/inaccessible files
            return null;
        }
    }

    private static bool IsInTimeRange(TokenUsage usage, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && usage.Timestamp < from.Value)
            return false;

        if (to.HasValue && usage.Timestamp > to.Value)
            return false;

        return true;
    }

    private static void CleanupEmptyDirectories(string rootPath)
    {
        foreach (var dir in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try
                {
                    Directory.Delete(dir);
                }
                catch (IOException)
                {
                    // Directory in use, skip
                }
            }
        }
    }
}
