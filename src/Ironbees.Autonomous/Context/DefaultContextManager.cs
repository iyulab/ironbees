using System.Collections.Concurrent;
using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Context;

/// <summary>
/// Default all-in-one context manager for autonomous execution.
/// Provides simple, lightweight context management without external dependencies.
/// For production with advanced memory features, use Memory Indexer integration.
/// </summary>
public sealed class DefaultContextManager : IAutonomousContextProvider, IAutonomousMemoryStore, IContextSaturationMonitor, IDisposable
{
    private readonly ConcurrentQueue<ContextItem> _contextQueue = new();
    private readonly ConcurrentDictionary<string, MemoryUnit> _memories = new();
    private readonly ConcurrentDictionary<string, int> _tokenUsage = new();
    private readonly AutonomousContextOptions _options;
    private readonly object _lock = new();

    private SaturationLevel _previousLevel = SaturationLevel.Normal;

    /// <summary>
    /// Creates a new default context manager with specified options.
    /// </summary>
    public DefaultContextManager(AutonomousContextOptions? options = null)
    {
        _options = options ?? new AutonomousContextOptions();
    }

    /// <summary>
    /// Creates a new default context manager with default options.
    /// </summary>
    public static DefaultContextManager Create() => new();

    /// <summary>
    /// Creates a new default context manager with custom configuration.
    /// </summary>
    public static DefaultContextManager Create(Action<AutonomousContextOptions> configure)
    {
        var options = new AutonomousContextOptions();
        configure(options);
        return new DefaultContextManager(options);
    }

    #region IAutonomousContextProvider

    /// <inheritdoc />
    public Task<IReadOnlyList<ContextItem>> GetRelevantContextAsync(
        string query,
        int iterationNumber,
        CancellationToken cancellationToken = default)
    {
        // Return most recent items (working memory model: 4-7 chunks)
        var items = _contextQueue
            .OrderByDescending(x => x.Timestamp)
            .Take(7)
            .ToList();

        return Task.FromResult<IReadOnlyList<ContextItem>>(items);
    }

    /// <inheritdoc />
    public Task RecordOutputAsync(
        string output,
        ContextMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var item = new ContextItem
        {
            Id = Guid.NewGuid().ToString(),
            Content = output,
            Type = metadata?.OutputType ?? "output",
            Relevance = metadata?.Importance ?? 0.5,
            EstimatedTokens = EstimateTokens(output),
            Timestamp = DateTimeOffset.UtcNow
        };

        _contextQueue.Enqueue(item);
        RecordUsage(item.EstimatedTokens, "context");

        // Trim to max items
        while (_contextQueue.Count > _options.MaxContextItems && _contextQueue.TryDequeue(out _)) { }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> GetExecutionSummaryAsync(
        int maxTokens = 1000,
        CancellationToken cancellationToken = default)
    {
        var items = _contextQueue.ToArray();
        if (items.Length == 0)
            return Task.FromResult(string.Empty);

        var totalTokens = 0;
        var parts = new List<string>();

        foreach (var item in items.OrderBy(x => x.Timestamp))
        {
            if (totalTokens + item.EstimatedTokens > maxTokens)
                break;

            parts.Add($"[{item.Type}] {Truncate(item.Content, 150)}");
            totalTokens += item.EstimatedTokens;
        }

        return Task.FromResult(string.Join("\n", parts));
    }

    /// <inheritdoc />
    public Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        _contextQueue.Clear();
        _tokenUsage.Clear();
        _previousLevel = SaturationLevel.Normal;
        return Task.CompletedTask;
    }

    #endregion

    #region IAutonomousMemoryStore

    /// <inheritdoc />
    public Task<string> StoreAsync(MemoryUnit memory, CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(memory.Id) ? Guid.NewGuid().ToString() : memory.Id;
        _memories[id] = memory with { Id = id };

        // Evict oldest if over capacity
        while (_memories.Count > _options.MaxMemories)
        {
            var oldest = _memories.Values
                .OrderBy(m => m.LastAccessedAt)
                .ThenBy(m => m.Retention)
                .FirstOrDefault();
            if (oldest != null) _memories.TryRemove(oldest.Id, out _);
            else break;
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
        var queryWords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var results = _memories.Values
            .Where(m => MatchesFilter(m, filter))
            .Select(m => (Memory: m, Score: ScoreRelevance(m, queryWords)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Memory)
            .ToList();

        return Task.FromResult<IReadOnlyList<MemoryUnit>>(results);
    }

    /// <inheritdoc />
    public Task<MemoryUnit?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _memories.TryGetValue(id, out var memory);
        return Task.FromResult(memory);
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(string id, MemoryUpdate update, CancellationToken cancellationToken = default)
    {
        if (!_memories.TryGetValue(id, out var existing))
            return Task.FromResult(false);

        _memories[id] = existing with
        {
            Content = update.Content ?? existing.Content,
            Importance = update.Importance ?? existing.Importance,
            Tier = update.Tier ?? existing.Tier,
            LastAccessedAt = update.RecordAccess ? DateTimeOffset.UtcNow : existing.LastAccessedAt,
            AccessCount = update.RecordAccess ? existing.AccessCount + 1 : existing.AccessCount
        };

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_memories.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<MemoryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var memories = _memories.Values.ToList();
        return Task.FromResult(new MemoryStatistics
        {
            TotalCount = memories.Count,
            CountByTier = memories.GroupBy(m => m.Tier).ToDictionary(g => g.Key, g => g.Count()),
            EstimatedTokens = memories.Sum(m => EstimateTokens(m.Content)),
            AverageRetention = memories.Count > 0 ? memories.Average(m => m.Retention) : 0,
            LastUpdated = DateTimeOffset.UtcNow
        });
    }

    #endregion

    #region IContextSaturationMonitor

    /// <inheritdoc />
    public SaturationState CurrentState { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler<SaturationChangedEventArgs>? SaturationChanged;

    /// <inheritdoc />
    public event EventHandler<SaturationActionRequiredEventArgs>? ActionRequired;

    /// <inheritdoc />
    public void RecordUsage(int tokens, string source = "unknown")
    {
        // Ensure atomicity of token update and state recalculation
        lock (_lock)
        {
            _tokenUsage.AddOrUpdate(source, tokens, (_, existing) => existing + tokens);
            UpdateSaturationStateCore();
        }
    }

    /// <inheritdoc />
    public Task<SaturationState> UpdateStateAsync(CancellationToken cancellationToken = default)
    {
        UpdateSaturationState();
        return Task.FromResult(CurrentState);
    }

    /// <inheritdoc />
    public void ResetIteration()
    {
        _tokenUsage.Clear();
        _previousLevel = SaturationLevel.Normal;
        CurrentState = new SaturationState { MaxTokens = _options.Saturation.MaxTokens };
    }

    /// <inheritdoc />
    public void Configure(SaturationConfig config)
    {
        // Apply new config (simplified - just update max tokens)
    }

    private void UpdateSaturationState()
    {
        lock (_lock)
        {
            UpdateSaturationStateCore();
        }
    }

    /// <summary>
    /// Core saturation state update logic. Must be called within _lock.
    /// </summary>
    private void UpdateSaturationStateCore()
    {
        var totalTokens = _tokenUsage.Values.Sum();
        var maxTokens = _options.Saturation.MaxTokens;
        var percentage = maxTokens > 0 ? (float)totalTokens / maxTokens * 100 : 0;
        var level = GetLevel(percentage);
        var action = GetAction(level);

        CurrentState = new SaturationState
        {
            Level = level,
            Percentage = percentage,
            CurrentTokens = totalTokens,
            MaxTokens = maxTokens,
            UsageBySource = new Dictionary<string, int>(_tokenUsage),
            RecommendedAction = action,
            LastUpdated = DateTimeOffset.UtcNow
        };

        if (level != _previousLevel)
        {
            SaturationChanged?.Invoke(this, new SaturationChangedEventArgs
            {
                PreviousLevel = _previousLevel,
                NewLevel = level,
                CurrentState = CurrentState
            });
            _previousLevel = level;
        }

        if (_options.Saturation.AutoTriggerActions && action >= SaturationAction.ShouldPageOut)
        {
            ActionRequired?.Invoke(this, new SaturationActionRequiredEventArgs
            {
                Action = action,
                CurrentState = CurrentState,
                SuggestedTokensToFree = (int)(totalTokens - maxTokens * _options.Saturation.TargetAfterEviction / 100),
                Reason = $"Saturation at {percentage:F1}%"
            });
        }
    }

    private SaturationLevel GetLevel(float percentage) => percentage switch
    {
        >= 95 => SaturationLevel.Overflow,
        >= 85 => SaturationLevel.Critical,
        >= 75 => SaturationLevel.High,
        >= 60 => SaturationLevel.Elevated,
        _ => SaturationLevel.Normal
    };

    private static SaturationAction GetAction(SaturationLevel level) => level switch
    {
        SaturationLevel.Overflow => SaturationAction.Emergency,
        SaturationLevel.Critical => SaturationAction.MustEvict,
        SaturationLevel.High => SaturationAction.ShouldPageOut,
        SaturationLevel.Elevated => SaturationAction.ConsiderSummarization,
        _ => SaturationAction.None
    };

    #endregion

    #region Helpers

    /// <summary>
    /// Estimates token count using content-aware heuristics.
    /// Different content types have different character-per-token ratios:
    /// - Korean/CJK: ~1.5 chars/token (due to Unicode decomposition)
    /// - Code: ~3 chars/token (more special characters)
    /// - English: ~4 chars/token (standard)
    /// </summary>
    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Count different character types
        var koreanCount = 0;
        var codeCharCount = 0;
        var totalCount = text.Length;

        foreach (var c in text)
        {
            // Korean characters (Hangul Syllables block: AC00-D7A3)
            if (c >= '\uAC00' && c <= '\uD7A3')
                koreanCount++;
            // Code-related characters
            else if ("{}[]();,<>=+-*/%&|!~^".Contains(c))
                codeCharCount++;
        }

        // Calculate weighted chars-per-token ratio
        var koreanRatio = totalCount > 0 ? (double)koreanCount / totalCount : 0;
        var codeRatio = totalCount > 0 ? (double)codeCharCount / totalCount : 0;

        // Determine effective chars-per-token based on content type
        double charsPerToken;
        if (koreanRatio > 0.3)
            charsPerToken = 1.5; // Korean-heavy content
        else if (codeRatio > 0.1)
            charsPerToken = 3.0; // Code-heavy content
        else
            charsPerToken = 4.0; // Standard English/mixed content

        return (int)Math.Ceiling(totalCount / charsPerToken);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    private static bool MatchesFilter(MemoryUnit m, MemoryFilter? f) =>
        f == null ||
        ((!f.Type.HasValue || m.Type == f.Type) &&
         (!f.Tier.HasValue || m.Tier == f.Tier) &&
         (!f.MinImportance.HasValue || m.Importance >= f.MinImportance) &&
         (!f.MinRetention.HasValue || m.Retention >= f.MinRetention) &&
         (!f.CreatedAfter.HasValue || m.CreatedAt >= f.CreatedAfter));

    private static double ScoreRelevance(MemoryUnit m, string[] queryWords)
    {
        var contentLower = m.Content.ToLowerInvariant();
        var matches = queryWords.Count(w => contentLower.Contains(w));
        if (matches == 0) return 0;

        var wordScore = (double)matches / queryWords.Length;
        var recencyScore = Math.Exp(-(DateTimeOffset.UtcNow - m.LastAccessedAt).TotalDays * 0.693);
        return wordScore * 0.6 + recencyScore * 0.2 + m.Retention * 0.2;
    }

    public void Dispose()
    {
        _contextQueue.Clear();
        _memories.Clear();
        _tokenUsage.Clear();
    }

    #endregion
}
