namespace Ironbees.Autonomous.Models;

/// <summary>
/// State of autonomous execution
/// </summary>
public enum AutonomousState
{
    /// <summary>Idle, not running</summary>
    Idle = 0,

    /// <summary>Running</summary>
    Running = 1,

    /// <summary>Paused by user</summary>
    Paused = 2,

    /// <summary>Completed normally (queue empty)</summary>
    Completed = 3,

    /// <summary>Stopped by maximum iterations</summary>
    StoppedByMaxIterations = 4,

    /// <summary>Stopped by user request</summary>
    StoppedByUser = 5,

    /// <summary>Stopped by condition</summary>
    StoppedByCondition = 6,

    /// <summary>Stopped due to error</summary>
    StoppedByError = 7,

    /// <summary>Stopped because goal was achieved</summary>
    StoppedByGoalAchieved = 8
}

/// <summary>
/// Status of autonomous execution
/// </summary>
public record AutonomousStatus
{
    /// <summary>Current state</summary>
    public AutonomousState State { get; init; }

    /// <summary>Session ID</summary>
    public string? SessionId { get; init; }

    /// <summary>Number of tasks in queue</summary>
    public int QueuedTaskCount { get; init; }

    /// <summary>Current iteration number</summary>
    public int CurrentIteration { get; init; }

    /// <summary>Maximum iterations</summary>
    public int MaxIterations { get; init; }

    /// <summary>Whether oracle is enabled</summary>
    public bool OracleEnabled { get; init; }

    /// <summary>Current oracle iteration</summary>
    public int CurrentOracleIteration { get; init; }

    /// <summary>Maximum oracle iterations</summary>
    public int MaxOracleIterations { get; init; }

    /// <summary>Completion mode</summary>
    public CompletionMode CompletionMode { get; init; }

    /// <summary>Number of checkpoints</summary>
    public int CheckpointCount { get; init; }

    /// <summary>Whether checkpointing is enabled</summary>
    public bool CheckpointingEnabled { get; init; }

    /// <summary>Number of history entries</summary>
    public int HistoryEntryCount { get; init; }

    /// <summary>Current task ID being executed</summary>
    public string? CurrentTaskId { get; init; }

    /// <summary>Last error message if any</summary>
    public string? LastError { get; init; }
}
