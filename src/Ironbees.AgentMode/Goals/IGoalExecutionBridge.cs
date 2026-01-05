// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Goals;

namespace Ironbees.AgentMode.Goals;

/// <summary>
/// Bridge interface that coordinates goal loading, template resolution, and MAF execution.
/// Implements the "Thin Wrapper" philosophy: declaration in Ironbees, execution delegated to MAF.
/// </summary>
/// <remarks>
/// <para>
/// The GoalExecutionBridge is the high-level coordinator for goal-based workflows:
/// </para>
/// <list type="number">
/// <item><description>Loads Goal definition using IGoalLoader</description></item>
/// <item><description>Resolves workflow template using IWorkflowTemplateResolver</description></item>
/// <item><description>Delegates execution to IMafWorkflowExecutor</description></item>
/// <item><description>Streams GoalExecutionEvents for progress tracking</description></item>
/// </list>
/// </remarks>
public interface IGoalExecutionBridge
{
    /// <summary>
    /// Executes a goal by its ID, streaming execution events.
    /// </summary>
    /// <param name="goalId">The ID of the goal to execute.</param>
    /// <param name="input">The input to start the goal with.</param>
    /// <param name="options">Optional execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of execution events.</returns>
    IAsyncEnumerable<GoalExecutionEvent> ExecuteGoalAsync(
        string goalId,
        string input,
        GoalExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a goal definition directly, streaming execution events.
    /// </summary>
    /// <param name="goal">The goal definition to execute.</param>
    /// <param name="input">The input to start the goal with.</param>
    /// <param name="options">Optional execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of execution events.</returns>
    IAsyncEnumerable<GoalExecutionEvent> ExecuteGoalAsync(
        GoalDefinition goal,
        string input,
        GoalExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a goal execution from a checkpoint.
    /// </summary>
    /// <param name="executionId">The execution ID to resume.</param>
    /// <param name="checkpointId">Optional specific checkpoint ID. If null, uses the latest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of execution events from the resume point.</returns>
    IAsyncEnumerable<GoalExecutionEvent> ResumeGoalAsync(
        string executionId,
        string? checkpointId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a goal execution.
    /// </summary>
    /// <param name="executionId">The execution ID to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution status, or null if not found.</returns>
    Task<GoalExecutionStatus?> GetExecutionStatusAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running goal execution.
    /// </summary>
    /// <param name="executionId">The execution ID to cancel.</param>
    /// <param name="saveCheckpoint">Whether to save a checkpoint before cancelling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the execution was cancelled, false if not found or already completed.</returns>
    Task<bool> CancelExecutionAsync(
        string executionId,
        bool saveCheckpoint = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the result of a completed goal execution.
    /// </summary>
    /// <param name="executionId">The execution ID to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result, or null if not found or not completed.</returns>
    Task<GoalExecutionResult?> GetExecutionResultAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all checkpoints for an execution.
    /// </summary>
    /// <param name="executionId">The execution ID to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of checkpoint information.</returns>
    Task<IReadOnlyList<GoalCheckpointInfo>> GetCheckpointsAsync(
        string executionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for goal execution.
/// </summary>
public sealed record GoalExecutionOptions
{
    /// <summary>
    /// Gets the execution ID. If null, a new ID will be generated.
    /// </summary>
    public string? ExecutionId { get; init; }

    /// <summary>
    /// Gets whether to enable checkpointing during execution.
    /// Default is determined by the goal's CheckpointSettings.
    /// </summary>
    public bool? EnableCheckpointing { get; init; }

    /// <summary>
    /// Gets the checkpoint directory override.
    /// </summary>
    public string? CheckpointDirectory { get; init; }

    /// <summary>
    /// Gets whether to save a checkpoint after each iteration.
    /// Default is determined by the goal's CheckpointSettings.
    /// </summary>
    public bool? CheckpointAfterEachIteration { get; init; }

    /// <summary>
    /// Gets the maximum iterations override.
    /// </summary>
    public int? MaxIterations { get; init; }

    /// <summary>
    /// Gets the maximum tokens override.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Gets the execution timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets additional parameters to pass to the workflow template.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Parameters { get; init; }

    /// <summary>
    /// Gets whether to include detailed progress events.
    /// </summary>
    public bool IncludeDetailedProgress { get; init; } = true;

    /// <summary>
    /// Creates default options.
    /// </summary>
    public static GoalExecutionOptions Default { get; } = new();
}

/// <summary>
/// Information about a goal checkpoint.
/// </summary>
public sealed record GoalCheckpointInfo
{
    /// <summary>
    /// Gets the checkpoint ID.
    /// </summary>
    public required string CheckpointId { get; init; }

    /// <summary>
    /// Gets the execution ID this checkpoint belongs to.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Gets the goal ID.
    /// </summary>
    public required string GoalId { get; init; }

    /// <summary>
    /// Gets when the checkpoint was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the iteration number when the checkpoint was created.
    /// </summary>
    public int? IterationNumber { get; init; }

    /// <summary>
    /// Gets the workflow state ID when the checkpoint was created.
    /// </summary>
    public string? StateId { get; init; }

    /// <summary>
    /// Gets the checkpoint size in bytes.
    /// </summary>
    public long? SizeBytes { get; init; }
}
