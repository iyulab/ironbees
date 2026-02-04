using System.Collections.Concurrent;

namespace Ironbees.Core.Middleware;

/// <summary>
/// In-memory implementation of <see cref="ITokenUsageStore"/>.
/// Suitable for development, testing, and short-running applications.
/// Data is not persisted across application restarts.
/// </summary>
public sealed class InMemoryTokenUsageStore : ITokenUsageStore
{
    private readonly ConcurrentBag<TokenUsage> _usages = [];

    /// <inheritdoc/>
    public Task RecordAsync(TokenUsage usage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _usages.Add(usage);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RecordBatchAsync(IEnumerable<TokenUsage> usages, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var usage in usages)
        {
            _usages.Add(usage);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TokenUsage>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _usages
            .Where(u => u.Timestamp >= from && u.Timestamp < to)
            .OrderByDescending(u => u.Timestamp)
            .ToList();
        return Task.FromResult<IReadOnlyList<TokenUsage>>(result);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TokenUsage>> GetUsageByAgentAsync(
        string agentName,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = _usages.Where(u =>
            string.Equals(u.AgentName, agentName, StringComparison.OrdinalIgnoreCase));

        if (from.HasValue)
        {
            query = query.Where(u => u.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(u => u.Timestamp < to.Value);
        }

        var result = query.OrderByDescending(u => u.Timestamp).ToList();
        return Task.FromResult<IReadOnlyList<TokenUsage>>(result);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TokenUsage>> GetUsageBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _usages
            .Where(u => string.Equals(u.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(u => u.Timestamp)
            .ToList();
        return Task.FromResult<IReadOnlyList<TokenUsage>>(result);
    }

    /// <inheritdoc/>
    public Task<TokenUsageStatistics> GetStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _usages.AsEnumerable();

        if (from.HasValue)
        {
            query = query.Where(u => u.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(u => u.Timestamp < to.Value);
        }

        var usages = query.ToList();

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
                g => (long)g.Sum(u => u.TotalTokens));

        var stats = new TokenUsageStatistics
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

        return Task.FromResult(stats);
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _usages.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> ClearOlderThanAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ConcurrentBag doesn't support removal, so we need to rebuild
        var toKeep = _usages.Where(u => u.Timestamp >= olderThan).ToList();
        var removed = _usages.Count - toKeep.Count;

        _usages.Clear();
        foreach (var usage in toKeep)
        {
            _usages.Add(usage);
        }

        return Task.FromResult(removed);
    }

    /// <summary>
    /// Gets the current count of recorded usages.
    /// </summary>
    public int Count => _usages.Count;
}
