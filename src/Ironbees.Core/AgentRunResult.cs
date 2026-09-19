using Microsoft.Extensions.AI;

namespace Ironbees.Core;

/// <summary>
/// Structured result of a non-streaming agent run.
/// The text-only <c>RunAsync</c> surface is a projection of this result.
/// </summary>
public record AgentRunResult
{
    /// <summary>
    /// The response text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Model-generated follow-up suggestions. Present only when requested via
    /// <see cref="AgentRunOptions.Suggestions"/> and supported by the adapter.
    /// </summary>
    public IReadOnlyList<AgentSuggestion>? Suggestions { get; init; }

    /// <summary>
    /// Token usage summed over every model call the run made, when the adapter can observe it.
    /// Null means unknown, not zero.
    /// </summary>
    public UsageDetails? Usage { get; init; }

    /// <summary>
    /// Number of model round-trips the run made, when the adapter runs its own tool loop.
    /// Null means the adapter does not know.
    /// </summary>
    public int? TurnsUsed { get; init; }

    /// <summary>
    /// True when the run stopped because it reached its tool-turn limit (<see cref="AgentRunOptions.MaxToolTurns"/>
    /// or the adapter default) while the model still wanted to call tools — <see cref="Text"/> is then the last
    /// partial answer, not a finished one. Null means the adapter does not know.
    /// </summary>
    public bool? TurnLimitReached { get; init; }
}
