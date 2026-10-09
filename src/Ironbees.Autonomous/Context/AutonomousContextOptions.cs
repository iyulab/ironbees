using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Context;

/// <summary>
/// Options for configuring autonomous context management.
/// </summary>
public class AutonomousContextOptions
{
    /// <summary>
    /// Maximum context items to retain in memory.
    /// </summary>
    public int MaxContextItems { get; set; } = 50;

    /// <summary>
    /// Maximum memories to retain in memory store.
    /// </summary>
    public int MaxMemories { get; set; } = 1000;

    /// <summary>
    /// Saturation configuration.
    /// </summary>
    public SaturationConfig Saturation { get; set; } = new();

    /// <summary>
    /// Maximum tokens for execution summary — the limit <c>GetExecutionSummaryAsync()</c> uses when the caller passes none.
    /// </summary>
    public int MaxSummaryTokens { get; set; } = 1000;
}
