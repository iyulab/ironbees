namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Abstract task executor interface for autonomous execution
/// </summary>
/// <typeparam name="TRequest">Task request type</typeparam>
/// <typeparam name="TResult">Task result type</typeparam>
public interface ITaskExecutor<in TRequest, TResult> : IAsyncDisposable
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    /// <summary>
    /// Execute a task asynchronously
    /// </summary>
    /// <param name="request">Task request</param>
    /// <param name="onOutput">Optional callback for real-time output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task result</returns>
    Task<TResult> ExecuteAsync(
        TRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for task requests
/// </summary>
public interface ITaskRequest
{
    /// <summary>
    /// Unique identifier for this request
    /// </summary>
    string RequestId { get; }

    /// <summary>
    /// The prompt or instruction to execute
    /// </summary>
    string Prompt { get; }
}

/// <summary>
/// Marker interface for task results
/// </summary>
public interface ITaskResult
{
    /// <summary>
    /// Request ID that produced this result
    /// </summary>
    string RequestId { get; }

    /// <summary>
    /// Whether execution completed successfully
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Output content from execution
    /// </summary>
    string Output { get; }

    /// <summary>
    /// Error output if any
    /// </summary>
    string? ErrorOutput { get; }
}

/// <summary>
/// Real-time output from task execution
/// </summary>
public record TaskOutput
{
    /// <summary>
    /// Request ID this output belongs to
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Output type
    /// </summary>
    public TaskOutputType Type { get; init; }

    /// <summary>
    /// Output content
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of task output
/// </summary>
public enum TaskOutputType
{
    /// <summary>Standard output</summary>
    Output,
    /// <summary>Error output</summary>
    Error,
    /// <summary>System message</summary>
    System
}
