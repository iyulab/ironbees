namespace Ironbees.Core;

/// <summary>
/// Selects the most appropriate agent based on user input
/// </summary>
public interface IAgentSelector
{
    /// <summary>
    /// Selects the best agent for the given input
    /// </summary>
    /// <param name="input">User input to analyze</param>
    /// <param name="availableAgents">Collection of available agents</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selection result with chosen agent and confidence score</returns>
    Task<AgentSelectionResult> SelectAgentAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes input and returns confidence scores for all available agents
    /// </summary>
    /// <param name="input">User input to analyze</param>
    /// <param name="availableAgents">Collection of available agents</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ranked list of agents with confidence scores</returns>
    Task<IReadOnlyList<AgentScore>> ScoreAgentsAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of agent selection
/// </summary>
public record AgentSelectionResult
{
    /// <summary>
    /// Selected agent (null if no suitable agent found)
    /// </summary>
    public IAgent? SelectedAgent { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    /// Reason for selection
    /// </summary>
    public string SelectionReason { get; init; } = string.Empty;

    /// <summary>
    /// All scored agents, sorted by confidence (highest first)
    /// </summary>
    public IReadOnlyList<AgentScore> AllScores { get; init; } = Array.Empty<AgentScore>();
}

/// <summary>
/// Agent with confidence score
/// </summary>
public record AgentScore
{
    /// <summary>
    /// The agent being scored
    /// </summary>
    public required IAgent Agent { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Reasons for this score
    /// </summary>
    public List<string> Reasons { get; init; } = new();
}
