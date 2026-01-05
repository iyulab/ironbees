namespace Ironbees.AgentMode.Models;

/// <summary>
/// Compilation warning from MSBuild.
/// </summary>
public record CompilationWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
}
