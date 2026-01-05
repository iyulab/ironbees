namespace Ironbees.AgentMode.Models;

/// <summary>
/// Details of a single test failure.
/// </summary>
public record TestFailure
{
    public required string TestName { get; init; }
    public required string Message { get; init; }
    public string? StackTrace { get; init; }
}
