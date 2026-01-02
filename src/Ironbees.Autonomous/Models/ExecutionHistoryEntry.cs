namespace Ironbees.Autonomous.Models;

/// <summary>
/// Entry in execution history
/// </summary>
public record ExecutionHistoryEntry
{
    /// <summary>Unique entry ID</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Session ID this entry belongs to</summary>
    public required string SessionId { get; init; }

    /// <summary>Iteration number within session</summary>
    public int IterationNumber { get; init; }

    /// <summary>The prompt that was executed</summary>
    public required string ExecutionPrompt { get; init; }

    /// <summary>Output from execution</summary>
    public string? ExecutionOutput { get; init; }

    /// <summary>The prompt sent to oracle (if oracle enabled)</summary>
    public string? OraclePrompt { get; init; }

    /// <summary>Oracle verification result</summary>
    public OracleVerdict? OracleVerdict { get; init; }

    /// <summary>When execution started</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When execution completed</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Whether execution was successful</summary>
    public bool Success { get; init; }

    /// <summary>Error message if failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Duration of execution</summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}
