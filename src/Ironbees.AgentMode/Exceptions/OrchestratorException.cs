namespace Ironbees.AgentMode;

/// <summary>
/// Exception thrown when orchestrator execution fails.
/// </summary>
public class OrchestratorException : AgentModeException
{
    public string? StateId { get; init; }
    public string? CurrentNode { get; init; }

    public OrchestratorException(string message, string? stateId = null, string? currentNode = null)
        : base(message)
    {
        StateId = stateId;
        CurrentNode = currentNode;
    }
}
