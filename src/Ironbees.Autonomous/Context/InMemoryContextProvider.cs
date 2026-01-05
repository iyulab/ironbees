using System.Collections.Concurrent;
using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Context;

/// <summary>
/// Simple in-memory implementation of IAutonomousContextProvider.
/// Suitable for testing and simple use cases without external memory systems.
/// </summary>
public class InMemoryContextProvider : IAutonomousContextProvider
{
    private readonly ConcurrentQueue<ContextItem> _contextItems = new();
    private readonly int _maxItems;
    private readonly int _estimatedTokensPerItem;

    /// <summary>
    /// Creates a new in-memory context provider.
    /// </summary>
    /// <param name="maxItems">Maximum context items to retain</param>
    /// <param name="estimatedTokensPerItem">Estimated tokens per item for summary calculation</param>
    public InMemoryContextProvider(int maxItems = 50, int estimatedTokensPerItem = 100)
    {
        _maxItems = maxItems;
        _estimatedTokensPerItem = estimatedTokensPerItem;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContextItem>> GetRelevantContextAsync(
        string query,
        int iterationNumber,
        CancellationToken cancellationToken = default)
    {
        // Simple implementation: return most recent items
        var items = _contextItems
            .OrderByDescending(x => x.Timestamp)
            .Take(Math.Min(7, _contextItems.Count)) // Baddeley's 4-7 working memory chunks
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

        _contextItems.Enqueue(item);

        // Trim to max items
        while (_contextItems.Count > _maxItems && _contextItems.TryDequeue(out _)) { }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> GetExecutionSummaryAsync(
        int maxTokens = 1000,
        CancellationToken cancellationToken = default)
    {
        var items = _contextItems.ToArray();
        if (items.Length == 0)
            return Task.FromResult("No execution history.");

        var totalTokens = 0;
        var summaryParts = new List<string>();

        foreach (var item in items.OrderBy(x => x.Timestamp))
        {
            var tokens = item.EstimatedTokens > 0 ? item.EstimatedTokens : _estimatedTokensPerItem;
            if (totalTokens + tokens > maxTokens)
                break;

            summaryParts.Add($"[{item.Type}] {TruncateContent(item.Content, 200)}");
            totalTokens += tokens;
        }

        return Task.FromResult(string.Join("\n", summaryParts));
    }

    /// <inheritdoc />
    public Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        _contextItems.Clear();
        return Task.CompletedTask;
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimation: ~4 chars per token for English
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;
        return content[..(maxLength - 3)] + "...";
    }
}
