using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Models;

/// <summary>
/// Checkpoint for execution recovery
/// </summary>
public record ExecutionCheckpoint
{
    /// <summary>Checkpoint ID</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Session ID</summary>
    public required string SessionId { get; init; }

    /// <summary>Iteration number when checkpoint was created</summary>
    public int IterationNumber { get; init; }

    /// <summary>When checkpoint was created</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Queue snapshot (serializable representation)</summary>
    public IReadOnlyList<object> QueueSnapshot { get; init; } = [];

    /// <summary>History snapshot</summary>
    public IReadOnlyList<ExecutionHistoryEntry> HistorySnapshot { get; init; } = [];

    /// <summary>Configuration at checkpoint time</summary>
    public AutonomousConfig? ConfigSnapshot { get; init; }
}
