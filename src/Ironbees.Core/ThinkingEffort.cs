namespace Ironbees.Core;

/// <summary>
/// Framework-neutral reasoning (extended thinking) effort for a single run.
/// Passed through <see cref="ProcessOptions.ThinkingEffort"/> / <see cref="AgentRunOptions.ThinkingEffort"/>
/// to the framework adapter, which translates it to the framework's own thinking control.
/// A null option (not set) leaves the adapter/framework default behavior unchanged;
/// <see cref="None"/> is an explicit "disable reasoning" request.
/// </summary>
public enum ThinkingEffort
{
    /// <summary>Explicitly disable reasoning for this run.</summary>
    None,

    /// <summary>Very minimal reasoning — fastest responses.</summary>
    Minimal,

    /// <summary>Light reasoning.</summary>
    Low,

    /// <summary>Standard reasoning — a good default for most tasks.</summary>
    Medium,

    /// <summary>Deep reasoning for complex problems.</summary>
    High,

    /// <summary>Deepest reasoning for very complex or creative work.</summary>
    XHigh,
}
