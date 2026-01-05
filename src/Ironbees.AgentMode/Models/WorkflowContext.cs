using System.Collections.Immutable;

namespace Ironbees.AgentMode.Models;

/// <summary>
/// Optional context for workflow execution.
/// </summary>
public record WorkflowContext
{
    /// <summary>
    /// Path to .NET solution file.
    /// </summary>
    public string? SolutionPath { get; init; }

    /// <summary>
    /// Target project name (if solution has multiple projects).
    /// </summary>
    public string? TargetProject { get; init; }

    /// <summary>
    /// Additional context files (e.g., related code, documentation).
    /// </summary>
    public ImmutableList<string> ContextFiles { get; init; }
        = ImmutableList<string>.Empty;

    /// <summary>
    /// User preferences for code generation.
    /// </summary>
    public ImmutableDictionary<string, string> Preferences { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}
