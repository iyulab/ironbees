// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.AgentMode.Core.Goals;

/// <summary>
/// Represents the type of a goal execution event.
/// </summary>
public enum GoalExecutionEventType
{
    /// <summary>
    /// The goal definition was successfully loaded.
    /// </summary>
    GoalLoaded,

    /// <summary>
    /// The workflow template was resolved with goal parameters.
    /// </summary>
    WorkflowResolved,

    /// <summary>
    /// Progress update during workflow execution.
    /// </summary>
    WorkflowProgress,

    /// <summary>
    /// An iteration of the goal loop completed.
    /// </summary>
    IterationCompleted,

    /// <summary>
    /// A checkpoint was saved.
    /// </summary>
    CheckpointSaved,

    /// <summary>
    /// An agent within the workflow produced output.
    /// </summary>
    AgentMessage,

    /// <summary>
    /// An agent within the workflow completed its task.
    /// </summary>
    AgentCompleted,

    /// <summary>
    /// The goal was completed successfully.
    /// </summary>
    GoalCompleted,

    /// <summary>
    /// The goal execution failed.
    /// </summary>
    GoalFailed,

    /// <summary>
    /// The goal execution was cancelled.
    /// </summary>
    GoalCancelled,

    /// <summary>
    /// The goal execution is resuming from a checkpoint.
    /// </summary>
    GoalResuming
}

/// <summary>
/// Represents an event during goal execution.
/// Provides streaming updates about goal progress, agent outputs, and checkpoints.
/// </summary>
public sealed record GoalExecutionEvent
{
    /// <summary>
    /// Gets the type of this event.
    /// </summary>
    public required GoalExecutionEventType Type { get; init; }

    /// <summary>
    /// Gets the goal ID this event relates to.
    /// </summary>
    public required string GoalId { get; init; }

    /// <summary>
    /// Gets the execution ID for this goal run.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets the timestamp when this event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the event content or message.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets the name of the agent that produced this event (if applicable).
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Gets the current iteration number (for iterative goals).
    /// </summary>
    public int? IterationNumber { get; init; }

    /// <summary>
    /// Gets the checkpoint ID if a checkpoint was created.
    /// </summary>
    public string? CheckpointId { get; init; }

    /// <summary>
    /// Gets the current workflow state ID.
    /// </summary>
    public string? CurrentStateId { get; init; }

    /// <summary>
    /// Gets additional metadata for this event.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets error information if the event represents a failure.
    /// </summary>
    public GoalExecutionError? Error { get; init; }

    /// <summary>
    /// Gets progress percentage (0-100) if available.
    /// </summary>
    public int? ProgressPercentage { get; init; }
}

/// <summary>
/// Represents error information for a failed goal execution.
/// </summary>
public sealed record GoalExecutionError
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the exception type name if an exception occurred.
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// Gets the stack trace if available (typically only in development).
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets whether this error is recoverable (can resume from checkpoint).
    /// </summary>
    public bool IsRecoverable { get; init; }

    /// <summary>
    /// Creates an error from an exception.
    /// </summary>
    public static GoalExecutionError FromException(Exception exception, bool isRecoverable = false)
    {
        return new GoalExecutionError
        {
            Code = exception.GetType().Name,
            Message = exception.Message,
            ExceptionType = exception.GetType().FullName,
            StackTrace = exception.StackTrace,
            IsRecoverable = isRecoverable
        };
    }
}

/// <summary>
/// Represents the final result of a goal execution.
/// </summary>
public sealed record GoalExecutionResult
{
    /// <summary>
    /// Gets the goal ID.
    /// </summary>
    public required string GoalId { get; init; }

    /// <summary>
    /// Gets the execution ID.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets whether the goal completed successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the final status of the goal execution.
    /// </summary>
    public required GoalExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets when the execution started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets when the execution completed.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Gets the total execution duration.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Gets the total number of iterations executed.
    /// </summary>
    public int TotalIterations { get; init; }

    /// <summary>
    /// Gets the final output or result content.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Gets error information if the execution failed.
    /// </summary>
    public GoalExecutionError? Error { get; init; }

    /// <summary>
    /// Gets the latest checkpoint ID for potential resume.
    /// </summary>
    public string? LastCheckpointId { get; init; }

    /// <summary>
    /// Gets additional result metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents the status of a goal execution.
/// </summary>
public enum GoalExecutionStatus
{
    /// <summary>
    /// The goal execution has not started.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The goal is currently loading.
    /// </summary>
    Loading,

    /// <summary>
    /// The workflow template is being resolved.
    /// </summary>
    ResolvingWorkflow,

    /// <summary>
    /// The goal is actively executing.
    /// </summary>
    Running,

    /// <summary>
    /// The goal execution is paused at a checkpoint.
    /// </summary>
    Paused,

    /// <summary>
    /// The goal completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The goal execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The goal execution was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The goal reached maximum iterations without completion.
    /// </summary>
    MaxIterationsReached
}
