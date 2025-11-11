namespace Ironbees.AgentMode;

/// <summary>
/// Base exception for all Agent Mode errors.
/// </summary>
public class AgentModeException : Exception
{
    public AgentModeException(string message) : base(message) { }
    public AgentModeException(string message, Exception inner) : base(message, inner) { }
}
