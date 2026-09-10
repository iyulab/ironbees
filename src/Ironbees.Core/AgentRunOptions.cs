using Microsoft.Extensions.AI;

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

    /// <summary>
    /// Reasoning (extended thinking) effort for this invocation.
    /// Null leaves the adapter/framework default; <see cref="ThinkingEffort.None"/>
    /// explicitly disables reasoning. When enabled, adapters surface reasoning
    /// deltas as <see cref="Streaming.ThinkingChunk"/> on the structured stream.
    /// </summary>
    public ThinkingEffort? ThinkingEffort { get; init; }

    /// <summary>
    /// Maximum number of output tokens for this invocation, overriding the agent's configured
    /// value. Null leaves that value in place.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Tool set to use for this invocation only, overriding the agent's configured
    /// <see cref="AgentConfig.Tools"/> for the duration of this call. Represented as
    /// framework-neutral M.E.AI <see cref="AITool"/> so <c>Ironbees.Core</c> stays free
    /// of a framework-specific tool-collection type; adapters convert to their own
    /// invocable tool type (e.g. the IronHive adapter wraps each via its AITool→ITool
    /// adapter). Use this for request/session-scoped tools (e.g. a workspace-bound tool)
    /// that cannot be expressed as a static name in <see cref="AgentConfig.Tools"/> because
    /// <see cref="IAgent"/> instances are cached and shared by name (<see cref="IAgentRegistry"/>)
    /// — mutating a shared agent's tool set directly would race across concurrent requests.
    /// Null leaves the agent's configured tools unchanged.
    /// </summary>
    public IReadOnlyList<AITool>? Tools { get; init; }
}
