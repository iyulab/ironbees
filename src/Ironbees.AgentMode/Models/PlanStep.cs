namespace Ironbees.AgentMode.Models;

/// <summary>
/// Single step in the execution plan.
/// </summary>
public record PlanStep
{
    /// <summary>
    /// Step number (1-based).
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Description of the action to perform.
    /// Example: "Add JwtBearer authentication middleware to Startup.cs"
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Type of action (CREATE, MODIFY, DELETE, REFACTOR).
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// File path affected by this step.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Rationale for this step (for HITL transparency).
    /// </summary>
    public string? Rationale { get; init; }
}
