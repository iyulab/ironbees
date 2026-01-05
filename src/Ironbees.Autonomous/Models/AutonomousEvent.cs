namespace Ironbees.Autonomous.Models;

/// <summary>
/// Event from autonomous execution
/// </summary>
public record AutonomousEvent
{
    /// <summary>Event type</summary>
    public AutonomousEventType Type { get; init; }

    /// <summary>Event timestamp</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Event message</summary>
    public string? Message { get; init; }

    /// <summary>Current task ID if applicable</summary>
    public string? CurrentTaskId { get; init; }

    /// <summary>Oracle verdict if applicable</summary>
    public OracleVerdict? OracleVerdict { get; init; }

    /// <summary>Oracle iteration number</summary>
    public int? OracleIteration { get; init; }

    /// <summary>History entry if applicable</summary>
    public ExecutionHistoryEntry? HistoryEntry { get; init; }

    /// <summary>Current state</summary>
    public AutonomousState? State { get; init; }
}

/// <summary>
/// Type of autonomous event
/// </summary>
public enum AutonomousEventType
{
    // Lifecycle events
    Started = 1,
    Paused = 2,
    Resumed = 3,
    Stopped = 4,
    Completed = 5,

    // Task events
    TaskEnqueued = 10,
    TaskStarted = 11,
    TaskOutput = 12,
    TaskCompleted = 13,
    TaskFailed = 14,

    // Queue events
    QueueEmpty = 20,
    QueueCleared = 21,

    // Iteration events
    IterationStarted = 30,
    IterationCompleted = 31,
    MaxIterationsReached = 32,
    AutoContinuing = 33,

    // Oracle events
    OracleVerifying = 60,
    OracleVerified = 61,
    OracleRetrying = 62,
    OracleComplete = 63,
    OracleError = 64,

    // History events
    HistoryEntryAdded = 70,

    // Checkpoint events
    CheckpointCreated = 80,
    CheckpointRestored = 81,

    // Human-in-the-Loop events
    HumanApprovalRequested = 100,
    HumanApprovalReceived = 101,
    HumanApprovalTimeout = 102,
    HumanFeedbackRequested = 103,
    HumanFeedbackReceived = 104,
    HumanNotification = 105,

    // Context events
    ContextUpdated = 110,
    ReflectionCaptured = 111,
    LearningCaptured = 112,

    // Error events
    Error = 90,

    // Resilience events (lessons from TwentyQuestions)
    RetryAttempt = 120,
    FallbackTriggered = 121,
    FallbackSucceeded = 122,
    FallbackFailed = 123,

    // Final iteration strategy events
    FinalIterationApproaching = 130,
    RequestModified = 131,
    ForcedCompletion = 132
}
