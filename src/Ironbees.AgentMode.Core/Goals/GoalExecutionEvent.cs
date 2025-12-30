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
    GoalResuming,

    // ========================================
    // Agentic Pattern Events
    // ========================================

    /// <summary>
    /// Human-in-the-Loop intervention is requested.
    /// </summary>
    HitlRequested,

    /// <summary>
    /// Human-in-the-Loop response was received.
    /// </summary>
    HitlResponseReceived,

    /// <summary>
    /// Confidence level was updated during iterative processing.
    /// </summary>
    ConfidenceUpdated,

    /// <summary>
    /// Sampling progress update for progressive data processing.
    /// </summary>
    SamplingProgress,

    /// <summary>
    /// Pattern was discovered during analysis.
    /// </summary>
    PatternDiscovered,

    /// <summary>
    /// Rules stability was achieved (no new patterns for N iterations).
    /// </summary>
    RulesStabilized
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

    // ========================================
    // Agentic Pattern Properties
    // ========================================

    /// <summary>
    /// Gets HITL request details when Type is HitlRequested.
    /// </summary>
    public HitlRequestDetails? HitlRequest { get; init; }

    /// <summary>
    /// Gets confidence information when Type is ConfidenceUpdated.
    /// </summary>
    public ConfidenceInfo? Confidence { get; init; }

    /// <summary>
    /// Gets sampling progress when Type is SamplingProgress.
    /// </summary>
    public SamplingProgressInfo? Sampling { get; init; }
}

/// <summary>
/// Details for a Human-in-the-Loop request.
/// </summary>
public sealed record HitlRequestDetails
{
    /// <summary>
    /// Gets the unique request ID for this HITL interaction.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the type of HITL request.
    /// </summary>
    public required HitlRequestType RequestType { get; init; }

    /// <summary>
    /// Gets the reason for requesting human intervention.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the checkpoint name that triggered this HITL.
    /// </summary>
    public string? CheckpointName { get; init; }

    /// <summary>
    /// Gets contextual information for the human to review.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Gets the available options for the human to choose from.
    /// </summary>
    public IReadOnlyList<HitlOption>? Options { get; init; }

    /// <summary>
    /// Gets when the request was made.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets when the request will timeout.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Type of HITL request.
/// </summary>
public enum HitlRequestType
{
    /// <summary>
    /// Request for approval to proceed.
    /// </summary>
    Approval,

    /// <summary>
    /// Request for a decision between options.
    /// </summary>
    Decision,

    /// <summary>
    /// Request for text input or configuration.
    /// </summary>
    Input,

    /// <summary>
    /// Request for review and feedback.
    /// </summary>
    Review,

    /// <summary>
    /// Request due to uncertainty or low confidence.
    /// </summary>
    Uncertainty,

    /// <summary>
    /// Request due to an exception or error.
    /// </summary>
    Exception
}

/// <summary>
/// An option presented to the human in a HITL request.
/// </summary>
public sealed record HitlOption
{
    /// <summary>
    /// Gets the unique identifier for this option.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the display label for this option.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the description of what this option does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether this is the recommended/default option.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Gets additional data associated with this option.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Information about confidence level during iterative processing.
/// </summary>
public sealed record ConfidenceInfo
{
    /// <summary>
    /// Gets the current confidence level (0.0 - 1.0).
    /// </summary>
    public required double CurrentConfidence { get; init; }

    /// <summary>
    /// Gets the target confidence threshold.
    /// </summary>
    public double TargetThreshold { get; init; }

    /// <summary>
    /// Gets the number of samples processed.
    /// </summary>
    public int SamplesProcessed { get; init; }

    /// <summary>
    /// Gets the number of consecutive stable iterations.
    /// </summary>
    public int StableIterations { get; init; }

    /// <summary>
    /// Gets whether the rules are considered stable.
    /// </summary>
    public bool IsStable { get; init; }

    /// <summary>
    /// Gets the change in confidence from the previous iteration.
    /// </summary>
    public double? ConfidenceDelta { get; init; }

    /// <summary>
    /// Gets the number of patterns discovered so far.
    /// </summary>
    public int PatternsDiscovered { get; init; }
}

/// <summary>
/// Information about sampling progress during progressive processing.
/// </summary>
public sealed record SamplingProgressInfo
{
    /// <summary>
    /// Gets the current batch number.
    /// </summary>
    public required int CurrentBatch { get; init; }

    /// <summary>
    /// Gets the number of samples in the current batch.
    /// </summary>
    public required int SamplesInBatch { get; init; }

    /// <summary>
    /// Gets the total samples processed across all batches.
    /// </summary>
    public required int TotalProcessed { get; init; }

    /// <summary>
    /// Gets the total dataset size if known.
    /// </summary>
    public int? TotalDatasetSize { get; init; }

    /// <summary>
    /// Gets the processing percentage if total size is known.
    /// </summary>
    public double? ProcessingPercentage => TotalDatasetSize > 0
        ? (double)TotalProcessed / TotalDatasetSize * 100
        : null;

    /// <summary>
    /// Gets the patterns discovered in this batch.
    /// </summary>
    public IReadOnlyList<string>? DiscoveredPatterns { get; init; }

    /// <summary>
    /// Gets the exceptions found in this batch.
    /// </summary>
    public int ExceptionsInBatch { get; init; }

    /// <summary>
    /// Gets the current error rate.
    /// </summary>
    public double? ErrorRate { get; init; }
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
