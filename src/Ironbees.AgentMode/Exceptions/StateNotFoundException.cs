namespace Ironbees.AgentMode;

/// <summary>
/// Exception thrown when state is not found.
/// </summary>
public class StateNotFoundException : AgentModeException
{
    public string StateId { get; init; }

    public StateNotFoundException(string stateId)
        : base($"State '{stateId}' not found")
    {
        StateId = stateId;
    }
}
