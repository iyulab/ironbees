namespace Ironbees.Core.Streaming;

/// <summary>
/// Base class for streaming response chunks.
/// Provides typed events for rich streaming experiences.
/// </summary>
public abstract record StreamChunk
{
    /// <summary>
    /// Timestamp when this chunk was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Text content chunk from the LLM response.
/// </summary>
/// <param name="Content">The text content.</param>
/// <param name="IsComplete">Whether this is the final text chunk.</param>
public sealed record TextChunk(string Content, bool IsComplete = false) : StreamChunk;

/// <summary>
/// Indicates the start of a tool call.
/// </summary>
/// <param name="ToolName">Name of the tool being called.</param>
/// <param name="ToolCallId">Unique identifier for this tool call.</param>
/// <param name="Arguments">Arguments being passed to the tool.</param>
public sealed record ToolCallStartChunk(
    string ToolName,
    string? ToolCallId = null,
    IReadOnlyDictionary<string, object>? Arguments = null) : StreamChunk;

/// <summary>
/// Indicates completion of a tool call with results.
/// </summary>
/// <param name="ToolName">Name of the tool that was called.</param>
/// <param name="ToolCallId">Unique identifier for this tool call.</param>
/// <param name="Success">Whether the tool call succeeded.</param>
/// <param name="Result">Result data from the tool call.</param>
/// <param name="Error">Error message if the tool call failed.</param>
public sealed record ToolCallCompleteChunk(
    string ToolName,
    string? ToolCallId = null,
    bool Success = true,
    object? Result = null,
    string? Error = null) : StreamChunk;

/// <summary>
/// Token usage information chunk.
/// </summary>
public sealed record UsageChunk : StreamChunk
{
    /// <summary>
    /// Number of input tokens used.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens generated.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Total tokens (calculated if not provided).
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Creates a new UsageChunk with automatic total calculation.
    /// </summary>
    public UsageChunk(int InputTokens, int OutputTokens, int? TotalTokens = null)
    {
        this.InputTokens = InputTokens;
        this.OutputTokens = OutputTokens;
        this.TotalTokens = TotalTokens ?? (InputTokens + OutputTokens);
    }
}

/// <summary>
/// Error information chunk.
/// </summary>
/// <param name="Error">Error message.</param>
/// <param name="IsFatal">Whether this error is fatal and streaming should stop.</param>
/// <param name="ErrorCode">Optional error code.</param>
public sealed record ErrorChunk(
    string Error,
    bool IsFatal = false,
    string? ErrorCode = null) : StreamChunk;

/// <summary>
/// Progress update chunk for long-running operations.
/// </summary>
/// <param name="Percentage">Progress percentage (0-100).</param>
/// <param name="Message">Optional progress message.</param>
/// <param name="CurrentStep">Current step name.</param>
/// <param name="TotalSteps">Total number of steps.</param>
public sealed record ProgressChunk(
    int Percentage,
    string? Message = null,
    string? CurrentStep = null,
    int? TotalSteps = null) : StreamChunk;

/// <summary>
/// Thinking/reasoning chunk (for models that support extended thinking).
/// </summary>
/// <param name="Content">The thinking/reasoning content.</param>
/// <param name="IsComplete">Whether thinking is complete.</param>
public sealed record ThinkingChunk(string Content, bool IsComplete = false) : StreamChunk;

/// <summary>
/// Metadata chunk for additional information.
/// </summary>
/// <param name="Key">Metadata key.</param>
/// <param name="Value">Metadata value.</param>
public sealed record MetadataChunk(string Key, object Value) : StreamChunk;

/// <summary>
/// Indicates the stream has completed.
/// </summary>
/// <param name="Success">Whether the stream completed successfully.</param>
/// <param name="FinishReason">Reason for completion (stop, length, tool_calls, etc.).</param>
public sealed record CompletionChunk(
    bool Success = true,
    string? FinishReason = null) : StreamChunk;
