using System.Collections.Concurrent;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Utilities;

namespace Ironbees.Autonomous.Context;

/// <summary>
/// Simple in-memory implementation of IAutonomousMemoryStore.
/// Suitable for testing and simple use cases without external storage.
/// </summary>
public class InMemoryMemoryStore : IAutonomousMemoryStore
{
    private readonly ConcurrentDictionary<string, MemoryUnit> _memories = new();
    private readonly int _maxMemories;

    /// <summary>
    /// Creates a new in-memory memory store.
    /// </summary>
    /// <param name="maxMemories">Maximum memories to retain</param>
    public InMemoryMemoryStore(int maxMemories = 1000)
    {
        _maxMemories = maxMemories;
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(
        MemoryUnit memory,
        CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(memory.Id) ? Guid.NewGuid().ToString() : memory.Id;
        var storedMemory = memory with { Id = id };

        _memories[id] = storedMemory;

        // Evict oldest if over capacity
        if (_memories.Count > _maxMemories)
        {
            var oldest = _memories.Values
                .OrderBy(m => m.LastAccessedAt)
                .ThenBy(m => m.Retention)
                .First();
            _memories.TryRemove(oldest.Id, out _);
        }

        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MemoryUnit>> RetrieveAsync(
        string query,
        int maxResults = 5,
        MemoryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var results = _memories.Values
            .Where(m => ApplyFilter(m, filter))
            .Select(m => (Memory: m, Score: CalculateRelevance(m, queryWords)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Memory.Importance)
            .Take(maxResults)
            .Select(x => x.Memory)
            .ToList();

        return Task.FromResult<IReadOnlyList<MemoryUnit>>(results);
    }

    /// <inheritdoc />
    public Task<MemoryUnit?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        _memories.TryGetValue(id, out var memory);
        return Task.FromResult(memory);
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(
        string id,
        MemoryUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (!_memories.TryGetValue(id, out var existing))
            return Task.FromResult(false);

        var updated = existing with
        {
            Content = update.Content ?? existing.Content,
            Importance = update.Importance ?? existing.Importance,
            Tier = update.Tier ?? existing.Tier,
            LastAccessedAt = update.RecordAccess ? DateTimeOffset.UtcNow : existing.LastAccessedAt,
            AccessCount = update.RecordAccess ? existing.AccessCount + 1 : existing.AccessCount,
            Metadata = update.Metadata != null
                ? MergeMetadata(existing.Metadata, update.Metadata)
                : existing.Metadata
        };

        _memories[id] = updated;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_memories.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<MemoryStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var memories = _memories.Values.ToList();

        var countByTier = memories
            .GroupBy(m => m.Tier)
            .ToDictionary(g => g.Key, g => g.Count());

        var stats = new MemoryStatistics
        {
            TotalCount = memories.Count,
            CountByTier = countByTier,
            EstimatedTokens = memories.Sum(m => EstimateTokens(m.Content)),
            AverageRetention = memories.Count > 0 ? memories.Average(m => m.Retention) : 0,
            LastUpdated = DateTimeOffset.UtcNow
        };

        return Task.FromResult(stats);
    }

    private static bool ApplyFilter(MemoryUnit memory, MemoryFilter? filter)
    {
        if (filter == null)
            return true;

        if (filter.Type.HasValue && memory.Type != filter.Type.Value)
            return false;

        if (filter.Tier.HasValue && memory.Tier != filter.Tier.Value)
            return false;

        if (filter.MinImportance.HasValue && memory.Importance < filter.MinImportance.Value)
            return false;

        if (filter.MinRetention.HasValue && memory.Retention < filter.MinRetention.Value)
            return false;

        if (filter.CreatedAfter.HasValue && memory.CreatedAt < filter.CreatedAfter.Value)
            return false;

        return true;
    }

    private static double CalculateRelevance(MemoryUnit memory, string[] queryWords)
    {
        // Simple keyword-based relevance scoring
        var contentLower = memory.Content.ToLowerInvariant();
        var matches = queryWords.Count(word => contentLower.Contains(word));

        if (matches == 0)
            return 0;

        var wordScore = (double)matches / queryWords.Length;
        var recencyScore = CalculateRecencyScore(memory.LastAccessedAt);
        var retentionScore = memory.Retention;

        // Weighted combination
        return (wordScore * 0.5) + (recencyScore * 0.3) + (retentionScore * 0.2);
    }

    private static double CalculateRecencyScore(DateTimeOffset lastAccessed)
    {
        var age = DateTimeOffset.UtcNow - lastAccessed;
        // Exponential decay: half-life of 1 day
        return Math.Exp(-age.TotalDays * 0.693);
    }

    private static int EstimateTokens(string text) => TokenEstimator.EstimateTokens(text);

    private static IReadOnlyDictionary<string, object>? MergeMetadata(
        IReadOnlyDictionary<string, object>? existing,
        IReadOnlyDictionary<string, object> update)
    {
        if (existing == null)
            return update;

        var merged = new Dictionary<string, object>(existing);
        foreach (var kvp in update)
        {
            merged[kvp.Key] = kvp.Value;
        }
        return merged;
    }
}
