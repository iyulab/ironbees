namespace Ironbees.AgentMode.Models;

/// <summary>
/// Represents a file edit (diff) to be applied.
/// </summary>
public record FileEdit
{
    /// <summary>
    /// File path relative to solution root.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Edit type (CREATE, MODIFY, DELETE).
    /// </summary>
    public required EditType Type { get; init; }

    /// <summary>
    /// Original file content (for MODIFY) or null (for CREATE).
    /// </summary>
    public string? OriginalContent { get; init; }

    /// <summary>
    /// New file content after edit.
    /// </summary>
    public required string NewContent { get; init; }

    /// <summary>
    /// Unified diff format for display to user.
    /// </summary>
    public string? DiffText { get; init; }
}
