// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Orchestration;

/// <summary>
/// Abstraction for storing orchestration checkpoints.
/// Enables pause/resume functionality for long-running orchestrations.
/// </summary>
public interface ICheckpointStore
{
    /// <summary>
    /// Saves a checkpoint for an orchestration.
    /// </summary>
    /// <param name="orchestrationId">The orchestration identifier.</param>
    /// <param name="checkpoint">The checkpoint data to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveCheckpointAsync(
        string orchestrationId,
        OrchestrationCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a checkpoint for an orchestration.
    /// </summary>
    /// <param name="orchestrationId">The orchestration identifier.</param>
    /// <param name="checkpointId">Optional specific checkpoint ID. If null, loads the latest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint, or null if not found.</returns>
    Task<OrchestrationCheckpoint?> LoadCheckpointAsync(
        string orchestrationId,
        string? checkpointId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all checkpoints for an orchestration.
    /// </summary>
    /// <param name="orchestrationId">The orchestration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of checkpoints ordered by creation time (newest first).</returns>
    Task<IReadOnlyList<OrchestrationCheckpoint>> ListCheckpointsAsync(
        string orchestrationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific checkpoint.
    /// </summary>
    /// <param name="orchestrationId">The orchestration identifier.</param>
    /// <param name="checkpointId">The checkpoint identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteCheckpointAsync(
        string orchestrationId,
        string checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all checkpoints for an orchestration.
    /// </summary>
    /// <param name="orchestrationId">The orchestration identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAllCheckpointsAsync(
        string orchestrationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a checkpoint in an orchestration.
/// </summary>
public sealed record OrchestrationCheckpoint
{
    /// <summary>
    /// Unique identifier for this checkpoint.
    /// </summary>
    public required string CheckpointId { get; init; }

    /// <summary>
    /// The orchestration this checkpoint belongs to.
    /// </summary>
    public required string OrchestrationId { get; init; }

    /// <summary>
    /// When the checkpoint was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The current state/step in the orchestration.
    /// </summary>
    public string? CurrentState { get; init; }

    /// <summary>
    /// The current agent being executed.
    /// </summary>
    public string? CurrentAgent { get; init; }

    /// <summary>
    /// Serialized orchestration context/state.
    /// </summary>
    public string? SerializedState { get; init; }

    /// <summary>
    /// Accumulated results from completed agents.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AgentResults { get; init; }

    /// <summary>
    /// Conversation history up to this point.
    /// </summary>
    public IReadOnlyList<CheckpointMessage>? Messages { get; init; }

    /// <summary>
    /// Token usage up to this checkpoint.
    /// </summary>
    public TokenUsageInfo? TokenUsage { get; init; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// A message stored in a checkpoint.
/// </summary>
public sealed record CheckpointMessage
{
    /// <summary>
    /// The role (user, assistant, system).
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The agent name if applicable.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// The message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
