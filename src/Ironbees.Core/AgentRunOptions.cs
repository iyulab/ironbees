namespace Ironbees.Core;

/// <summary>
/// Framework-neutral per-invoke options passed to an <see cref="ILLMFrameworkAdapter"/>.
/// Null fields leave the adapter's default behavior unchanged. Fields are added
/// demand-driven; adapters that cannot honor a requested option must fail loud
/// (<see cref="NotSupportedException"/>), never silently drop it.
/// </summary>
public record AgentRunOptions
{
    /// <summary>
    /// Requests model-generated follow-up suggestions for this invocation.
    /// Null disables the feature.
    /// </summary>
    public SuggestionRequest? Suggestions { get; init; }
}
