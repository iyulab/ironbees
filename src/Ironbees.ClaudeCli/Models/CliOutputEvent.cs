namespace Ironbees.ClaudeCli.Models;

/// <summary>
/// Real-time output event from CLI execution
/// </summary>
public record CliOutputEvent
{
    /// <summary>
    /// Request ID this output belongs to
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Type of output
    /// </summary>
    public CliOutputType Type { get; init; }

    /// <summary>
    /// Output content
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Timestamp of this output event
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of CLI output
/// </summary>
public enum CliOutputType
{
    /// <summary>
    /// Standard output from CLI
    /// </summary>
    Stdout,

    /// <summary>
    /// Standard error from CLI
    /// </summary>
    Stderr,

    /// <summary>
    /// System message (start, stop, etc.)
    /// </summary>
    System
}
