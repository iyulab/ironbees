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
}
