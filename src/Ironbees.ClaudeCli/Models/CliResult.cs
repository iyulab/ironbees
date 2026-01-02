namespace Ironbees.ClaudeCli.Models;

/// <summary>
/// Result from Claude CLI execution
/// </summary>
public record CliResult
{
    /// <summary>
    /// Request ID that produced this result
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Whether execution completed successfully (exit code 0)
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Process exit code
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Complete standard output
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Complete error output
    /// </summary>
    public string ErrorOutput { get; init; } = string.Empty;

    /// <summary>
    /// When execution started
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When execution completed
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Total execution duration
    /// </summary>
    public TimeSpan Duration { get; init; }
}
