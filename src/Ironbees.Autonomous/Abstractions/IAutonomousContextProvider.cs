namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Provides context for autonomous execution iterations.
/// Thin interface that external systems (e.g., Memory Indexer) can implement.
/// </summary>
/// <remarks>
/// Design principle: Declaration only, no implementation logic.
/// Memory Indexer or other context systems implement this interface
/// to provide rich context to autonomous orchestration.
/// </remarks>
public interface IAutonomousContextProvider
{
    /// <summary>
    /// Gets relevant context for the current execution iteration.
    /// </summary>
    /// <param name="query">Current query or task description</param>
    /// <param name="iterationNumber">Current iteration number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Context items relevant to the current iteration</returns>
    Task<IReadOnlyList<ContextItem>> GetRelevantContextAsync(
        string query,
        int iterationNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records execution output as context for future iterations.
    /// </summary>
    /// <param name="output">Output to record</param>
    /// <param name="metadata">Optional metadata about the output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordOutputAsync(
        string output,
        ContextMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets summary of execution history for prompt building.
    /// </summary>
    /// <param name="maxTokens">Maximum tokens to include in summary</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summarized execution history</returns>
    Task<string> GetExecutionSummaryAsync(
        int maxTokens = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears context for a new execution session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearSessionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A single context item for autonomous execution.
/// </summary>
public record ContextItem
{
    /// <summary>
    /// Unique identifier for this context item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The content of this context item.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Relevance score (0.0 to 1.0) to the current query.
    /// </summary>
    public double Relevance { get; init; } = 1.0;

    /// <summary>
    /// Type of context (e.g., "history", "memory", "knowledge").
    /// </summary>
    public string Type { get; init; } = "general";

    /// <summary>
    /// Estimated token count for this item.
    /// </summary>
    public int EstimatedTokens { get; init; }

    /// <summary>
    /// Source of this context (e.g., "L1:working", "L2:session", "L3:user").
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// When this context was created or last accessed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Metadata for context recording.
/// </summary>
public record ContextMetadata
{
    /// <summary>
    /// Type of the output (e.g., "question", "answer", "reasoning").
    /// </summary>
    public string? OutputType { get; init; }

    /// <summary>
    /// Importance score (0.0 to 1.0) for retention prioritization.
    /// </summary>
    public double Importance { get; init; } = 0.5;

    /// <summary>
    /// Whether this should be promoted to long-term memory.
    /// </summary>
    public bool ShouldPersist { get; init; }

    /// <summary>
    /// Related iteration number.
    /// </summary>
    public int? IterationNumber { get; init; }

    /// <summary>
    /// Custom tags for categorization.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
