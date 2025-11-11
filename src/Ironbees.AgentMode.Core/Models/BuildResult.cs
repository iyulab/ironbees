using System.Collections.Immutable;

namespace Ironbees.AgentMode.Models;

/// <summary>
/// Result of MSBuild compilation.
/// </summary>
public record BuildResult
{
    /// <summary>
    /// Whether build succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// List of compilation errors.
    /// </summary>
    public ImmutableList<CompilationError> Errors { get; init; }
        = ImmutableList<CompilationError>.Empty;

    /// <summary>
    /// List of compilation warnings.
    /// </summary>
    public ImmutableList<CompilationWarning> Warnings { get; init; }
        = ImmutableList<CompilationWarning>.Empty;

    /// <summary>
    /// Build duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
}
