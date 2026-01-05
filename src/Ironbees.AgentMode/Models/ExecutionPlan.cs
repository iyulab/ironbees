using System.Collections.Immutable;

namespace Ironbees.AgentMode.Models;

/// <summary>
/// Structured execution plan with step-by-step actions.
/// </summary>
public record ExecutionPlan
{
    /// <summary>
    /// Human-readable summary of the plan.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// List of action steps to execute sequentially.
    /// </summary>
    public required ImmutableList<PlanStep> Steps { get; init; }

    /// <summary>
    /// List of files that will be modified.
    /// </summary>
    public ImmutableList<string> AffectedFiles { get; init; } = ImmutableList<string>.Empty;

    /// <summary>
    /// Estimated complexity (LOW, MEDIUM, HIGH).
    /// </summary>
    public string Complexity { get; init; } = "MEDIUM";
}
