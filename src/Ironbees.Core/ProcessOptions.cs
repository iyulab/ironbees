using Microsoft.Extensions.AI;

namespace Ironbees.Core;

/// <summary>
/// Options for processing a request with conversation context.
/// </summary>
public record ProcessOptions
{
    /// <summary>
    /// Conversation ID for multi-turn conversation support.
    /// When provided, conversation history is loaded and saved automatically.
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// Specific agent name to use. Overrides automatic selection.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Maximum number of history turns to include in the LLM context.
    /// Each turn consists of a user message and an assistant response.
    /// When null, all available history is included.
    /// </summary>
    public int? MaxHistoryTurns { get; init; }

    /// <summary>
    /// Delta threshold for agent stickiness (default: 0.2).
    /// A different agent must score higher than the current agent by this amount to trigger a switch.
    /// </summary>
    public double StickinessThreshold { get; init; } = 0.2;

    /// <summary>
    /// Overrides the agent's YAML system prompt for this request only.
    /// Use for RAG context injection, per-workspace instructions, or other
    /// runtime-determined system prompts. When null, the agent's configured
    /// system prompt is used unchanged.
    /// </summary>
    public string? SystemPromptOverride { get; init; }

    /// <summary>
    /// Overrides the agent's YAML <c>model.deployment</c> for this request only.
    /// Use when the administrator changes the active model at runtime without redeployment.
    /// When null, the value from <c>agent.yaml</c> is used as-is.
    /// </summary>
    public string? ModelOverride { get; init; }

    /// <summary>
    /// Requests model-generated follow-up suggestions for this request.
    /// Suggestions are returned via the structured surface
    /// (<see cref="IAgentOrchestrator.ProcessStructuredAsync"/> /
    /// <see cref="IAgentOrchestrator.StreamStructuredAsync"/>); the text-only
    /// surface ignores structured payloads. Null disables the feature.
    /// Adapters that do not support suggestions fail loud (<see cref="NotSupportedException"/>).
    /// </summary>
    public SuggestionRequest? Suggestions { get; init; }

    /// <summary>
    /// Reasoning (extended thinking) effort for this request.
    /// Null leaves the adapter/framework default; <see cref="Core.ThinkingEffort.None"/>
    /// explicitly disables reasoning. When enabled, reasoning deltas surface as
    /// <see cref="Streaming.ThinkingChunk"/> on the structured stream
    /// (<see cref="IAgentOrchestrator.StreamStructuredAsync"/>); the text-only
    /// surface never leaks reasoning into text.
    /// </summary>
    public ThinkingEffort? ThinkingEffort { get; init; }

    /// <summary>
    /// Tool set to use for this request only, overriding the agent's configured
    /// <see cref="AgentConfig.Tools"/> for the duration of this call — e.g. a
    /// workspace/session-scoped tool that cannot be expressed as a static name in
    /// <c>agent.yaml</c>. Represented as framework-neutral M.E.AI <see cref="AITool"/>.
    /// Null leaves the agent's configured tools unchanged. Adapters that do not support
    /// per-request tools fail loud (<see cref="NotSupportedException"/>).
    /// </summary>
    public IReadOnlyList<AITool>? Tools { get; init; }
}
