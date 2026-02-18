namespace Ironbees.Core.Middleware;

/// <summary>
/// Interface for storing and retrieving token usage data.
/// Implementations can use in-memory, file-system, or database storage.
/// </summary>
public interface ITokenUsageStore
{
    /// <summary>
    /// Records a single token usage entry.
    /// </summary>
    /// <param name="usage">The token usage to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when recording is done.</returns>
    Task RecordAsync(TokenUsage usage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records multiple token usage entries.
    /// </summary>
    /// <param name="usages">The token usages to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when recording is done.</returns>
    Task RecordBatchAsync(IEnumerable<TokenUsage> usages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets token usage entries within a time range.
    /// </summary>
    /// <param name="from">Start of time range (inclusive).</param>
    /// <param name="endTime">End of time range (exclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of token usage entries.</returns>
    Task<IReadOnlyList<TokenUsage>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets token usage entries for a specific agent.
    /// </summary>
    /// <param name="agentName">The agent name to filter by.</param>
    /// <param name="from">Optional start of time range.</param>
    /// <param name="endTime">Optional end of time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of token usage entries.</returns>
    Task<IReadOnlyList<TokenUsage>> GetUsageByAgentAsync(
        string agentName,
        DateTimeOffset? from = null,
        DateTimeOffset? endTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets token usage entries for a specific session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of token usage entries.</returns>
    Task<IReadOnlyList<TokenUsage>> GetUsageBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated statistics for a time range.
    /// </summary>
    /// <param name="from">Optional start of time range.</param>
    /// <param name="endTime">Optional end of time range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated statistics.</returns>
    Task<TokenUsageStatistics> GetStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? endTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all recorded usage data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when clearing is done.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears usage data older than the specified date.
    /// </summary>
    /// <param name="olderThan">Clear entries older than this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of entries cleared.</returns>
    Task<int> ClearOlderThanAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
