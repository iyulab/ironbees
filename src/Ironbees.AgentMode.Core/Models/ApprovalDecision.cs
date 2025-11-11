namespace Ironbees.AgentMode.Models;

/// <summary>
/// User's approval decision for HITL gates.
/// </summary>
public record ApprovalDecision
{
    /// <summary>
    /// Whether user approved the action.
    /// </summary>
    public required bool Approved { get; init; }

    /// <summary>
    /// Optional feedback for rejection (used to refine plan).
    /// Example: "Please use async/await instead of Task.Run"
    /// </summary>
    public string? Feedback { get; init; }
}
