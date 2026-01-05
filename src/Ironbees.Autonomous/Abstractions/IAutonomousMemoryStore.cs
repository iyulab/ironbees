namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Memory storage interface for autonomous execution.
/// Provides simple read/write operations that external memory systems can implement.
/// </summary>
/// <remarks>
/// Design principle: Minimal interface for autonomous execution needs.
/// Complex memory operations (tiering, forgetting curves, consolidation)
/// are handled by external implementations like Memory Indexer.
/// </remarks>
public interface IAutonomousMemoryStore
{
    /// <summary>
    /// Stores a memory unit.
    /// </summary>
    /// <param name="memory">Memory unit to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stored memory ID</returns>
    Task<string> StoreAsync(
        MemoryUnit memory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves memories by semantic similarity.
    /// </summary>
    /// <param name="query">Query text for semantic search</param>
    /// <param name="maxResults">Maximum results to return</param>
    /// <param name="filter">Optional filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching memory units ordered by relevance</returns>
    Task<IReadOnlyList<MemoryUnit>> RetrieveAsync(
        string query,
        int maxResults = 5,
        MemoryFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific memory by ID.
    /// </summary>
    /// <param name="id">Memory ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The memory unit, or null if not found</returns>
    Task<MemoryUnit?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing memory.
    /// </summary>
    /// <param name="id">Memory ID</param>
    /// <param name="update">Update to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if updated, false if not found</returns>
    Task<bool> UpdateAsync(
        string id,
        MemoryUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a memory.
    /// </summary>
    /// <param name="id">Memory ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets memory statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Memory store statistics</returns>
    Task<MemoryStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A unit of memory for autonomous execution.
/// </summary>
public record MemoryUnit
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The content of this memory.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Memory type for categorization.
    /// </summary>
    public MemoryType Type { get; init; } = MemoryType.Episodic;

    /// <summary>
    /// Memory tier (L1=working, L2=session, L3=long-term).
    /// </summary>
    public MemoryTier Tier { get; init; } = MemoryTier.Session;

    /// <summary>
    /// Importance score (0.0 to 1.0).
    /// </summary>
    public double Importance { get; init; } = 0.5;

    /// <summary>
    /// Retention score based on access patterns (0.0 to 1.0).
    /// </summary>
    public double Retention { get; init; } = 1.0;

    /// <summary>
    /// When this memory was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this memory was last accessed.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of times this memory has been accessed.
    /// </summary>
    public int AccessCount { get; init; } = 1;

    /// <summary>
    /// Optional embedding vector (if pre-computed).
    /// </summary>
    public float[]? Embedding { get; init; }

    /// <summary>
    /// Custom metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Memory type classification.
/// </summary>
public enum MemoryType
{
    /// <summary>
    /// Event-based memory (what happened).
    /// </summary>
    Episodic,

    /// <summary>
    /// Fact-based memory (what is known).
    /// </summary>
    Semantic,

    /// <summary>
    /// Procedure-based memory (how to do).
    /// </summary>
    Procedural,

    /// <summary>
    /// System instruction memory (locked).
    /// </summary>
    System
}

/// <summary>
/// Memory tier for hierarchical storage.
/// </summary>
public enum MemoryTier
{
    /// <summary>
    /// L1: Working memory (in-context, high priority).
    /// </summary>
    Working = 1,

    /// <summary>
    /// L2: Session memory (current session).
    /// </summary>
    Session = 2,

    /// <summary>
    /// L3: Long-term memory (persistent across sessions).
    /// </summary>
    LongTerm = 3
}

/// <summary>
/// Filter criteria for memory retrieval.
/// </summary>
public record MemoryFilter
{
    /// <summary>
    /// Filter by memory type.
    /// </summary>
    public MemoryType? Type { get; init; }

    /// <summary>
    /// Filter by tier.
    /// </summary>
    public MemoryTier? Tier { get; init; }

    /// <summary>
    /// Minimum importance score.
    /// </summary>
    public double? MinImportance { get; init; }

    /// <summary>
    /// Minimum retention score.
    /// </summary>
    public double? MinRetention { get; init; }

    /// <summary>
    /// Only include memories created after this time.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; init; }

    /// <summary>
    /// Required tags (all must match).
    /// </summary>
    public IReadOnlyList<string>? RequiredTags { get; init; }
}

/// <summary>
/// Update to apply to an existing memory.
/// </summary>
public record MemoryUpdate
{
    /// <summary>
    /// New content (null to keep existing).
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// New importance score.
    /// </summary>
    public double? Importance { get; init; }

    /// <summary>
    /// New tier.
    /// </summary>
    public MemoryTier? Tier { get; init; }

    /// <summary>
    /// Record access (updates LastAccessedAt and AccessCount).
    /// </summary>
    public bool RecordAccess { get; init; }

    /// <summary>
    /// Additional metadata to merge.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Memory store statistics.
/// </summary>
public record MemoryStatistics
{
    /// <summary>
    /// Total memory count.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Count by tier.
    /// </summary>
    public IReadOnlyDictionary<MemoryTier, int> CountByTier { get; init; } = new Dictionary<MemoryTier, int>();

    /// <summary>
    /// Estimated total tokens.
    /// </summary>
    public int EstimatedTokens { get; init; }

    /// <summary>
    /// Average retention score.
    /// </summary>
    public double AverageRetention { get; init; }

    /// <summary>
    /// Last update time.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
}
