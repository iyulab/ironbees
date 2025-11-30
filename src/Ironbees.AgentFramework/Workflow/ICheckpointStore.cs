namespace Ironbees.AgentFramework.Workflow;

/// <summary>
/// Represents checkpoint data for workflow execution state persistence.
/// Used to enable resume from checkpoint functionality with MAF workflows.
/// </summary>
public sealed record CheckpointData
{
    /// <summary>
    /// Unique identifier for this checkpoint.
    /// </summary>
    public required string CheckpointId { get; init; }

    /// <summary>
    /// The execution identifier this checkpoint belongs to.
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// The workflow name being executed.
    /// </summary>
    public required string WorkflowName { get; init; }

    /// <summary>
    /// The current state ID when checkpoint was created.
    /// </summary>
    public string? CurrentStateId { get; init; }

    /// <summary>
    /// The MAF checkpoint object serialized to JSON.
    /// This is the native MAF checkpoint data that can be used to resume execution.
    /// </summary>
    public string? MafCheckpointJson { get; init; }

    /// <summary>
    /// The original input that started the workflow.
    /// </summary>
    public string? Input { get; init; }

    /// <summary>
    /// Additional context data as serialized JSON.
    /// </summary>
    public string? ContextJson { get; init; }

    /// <summary>
    /// When the checkpoint was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the workflow execution started.
    /// </summary>
    public DateTimeOffset? ExecutionStartedAt { get; init; }

    /// <summary>
    /// Optional metadata key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Defines the contract for storing and retrieving workflow checkpoints.
/// Implementations should provide durable storage for checkpoint data to enable
/// resume from checkpoint functionality.
/// </summary>
public interface ICheckpointStore
{
    /// <summary>
    /// Saves a checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint data to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when saving is done.</returns>
    Task SaveAsync(CheckpointData checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a checkpoint by its ID.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint data, or null if not found.</returns>
    Task<CheckpointData?> GetAsync(string checkpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest checkpoint for an execution.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent checkpoint for the execution, or null if none found.</returns>
    Task<CheckpointData?> GetLatestForExecutionAsync(string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all checkpoints for an execution.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of checkpoints ordered by creation time.</returns>
    Task<IReadOnlyList<CheckpointData>> GetAllForExecutionAsync(string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the checkpoint was deleted, false if it was not found.</returns>
    Task<bool> DeleteAsync(string checkpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all checkpoints for an execution.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of checkpoints deleted.</returns>
    Task<int> DeleteAllForExecutionAsync(string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes checkpoints older than the specified date.
    /// </summary>
    /// <param name="olderThan">Delete checkpoints created before this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of checkpoints deleted.</returns>
    Task<int> CleanupOlderThanAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a checkpoint exists.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the checkpoint exists.</returns>
    Task<bool> ExistsAsync(string checkpointId, CancellationToken cancellationToken = default);
}
