namespace Ironbees.Core;

/// <summary>
/// Configuration for an agent loaded from agent.yaml
/// </summary>
public record AgentConfig
{
    /// <summary>
    /// Unique name of the agent (e.g., "coding-agent")
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of agent capabilities
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Semantic version (e.g., "1.0.0")
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// System prompt loaded from system-prompt.md
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// LLM model configuration
    /// </summary>
    public required ModelConfig Model { get; init; }

    /// <summary>
    /// Agent capabilities (e.g., ["code-generation", "code-review"])
    /// </summary>
    public List<string> Capabilities { get; init; } = new();

    /// <summary>
    /// Tags for categorization (e.g., ["coding", "development"])
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Additional metadata for extensibility
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
