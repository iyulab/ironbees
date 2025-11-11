namespace Ironbees.AgentMode;

/// <summary>
/// Exception thrown when state is in invalid state for operation.
/// </summary>
public class InvalidStateException : AgentModeException
{
    public string StateId { get; init; }
    public string CurrentNode { get; init; }

    public InvalidStateException(string stateId, string currentNode, string message)
        : base(message)
    {
        StateId = stateId;
        CurrentNode = currentNode;
    }
}
