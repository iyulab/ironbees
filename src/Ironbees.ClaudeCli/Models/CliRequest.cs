namespace Ironbees.ClaudeCli.Models;

/// <summary>
/// Request for Claude CLI execution
/// </summary>
public record CliRequest
{
    /// <summary>
    /// Unique identifier for this request
    /// </summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Working directory for CLI execution
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Prompt to send to Claude
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Additional CLI arguments
    /// </summary>
    public string[] AdditionalArgs { get; init; } = [];

    /// <summary>
    /// Optional timeout for execution (default: no timeout)
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
