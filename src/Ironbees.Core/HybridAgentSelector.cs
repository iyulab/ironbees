namespace Ironbees.Core;

/// <summary>
/// Combines keyword-based and embedding-based agent selection for robust matching.
/// Uses weighted averaging to balance lexical and semantic similarity.
/// </summary>
public class HybridAgentSelector : IAgentSelector
{
    private readonly KeywordAgentSelector _keywordSelector;
    private readonly EmbeddingAgentSelector _embeddingSelector;
    private readonly double _keywordWeight;
    private readonly double _embeddingWeight;

    /// <summary>
    /// Creates a new instance of the hybrid agent selector.
    /// </summary>
    /// <param name="keywordSelector">The keyword-based selector.</param>
    /// <param name="embeddingSelector">The embedding-based selector.</param>
    /// <param name="keywordWeight">
    /// Weight for keyword-based scoring (default: 0.4).
    /// Higher values favor lexical matching.
    /// </param>
    /// <param name="embeddingWeight">
    /// Weight for embedding-based scoring (default: 0.6).
    /// Higher values favor semantic matching.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when weights are negative or sum to zero.
    /// </exception>
    public HybridAgentSelector(
        KeywordAgentSelector keywordSelector,
        EmbeddingAgentSelector embeddingSelector,
        double keywordWeight = 0.4,
        double embeddingWeight = 0.6)
    {
        _keywordSelector = keywordSelector ?? throw new ArgumentNullException(nameof(keywordSelector));
        _embeddingSelector = embeddingSelector ?? throw new ArgumentNullException(nameof(embeddingSelector));

        if (keywordWeight < 0 || embeddingWeight < 0)
        {
            throw new ArgumentException("Weights must be non-negative");
        }

        var totalWeight = keywordWeight + embeddingWeight;
        if (totalWeight == 0)
        {
            throw new ArgumentException("At least one weight must be greater than zero");
        }

        // Normalize weights to sum to 1.0
        _keywordWeight = keywordWeight / totalWeight;
        _embeddingWeight = embeddingWeight / totalWeight;
    }

    /// <summary>
    /// Creates a new instance with custom weight configuration.
    /// </summary>
    /// <param name="keywordSelector">The keyword-based selector.</param>
    /// <param name="embeddingSelector">The embedding-based selector.</param>
    /// <param name="config">Hybrid selector configuration.</param>
    public HybridAgentSelector(
        KeywordAgentSelector keywordSelector,
        EmbeddingAgentSelector embeddingSelector,
        HybridSelectorConfig config)
        : this(keywordSelector, embeddingSelector, config.KeywordWeight, config.EmbeddingWeight)
    {
    }

    /// <inheritdoc />
    public async Task<AgentSelectionResult> SelectAgentAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(availableAgents);

        if (availableAgents.Count == 0)
        {
            return new AgentSelectionResult
            {
                SelectedAgent = null,
                ConfidenceScore = 0.0,
                SelectionReason = "No agents available",
                AllScores = Array.Empty<AgentScore>()
            };
        }

        if (availableAgents.Count == 1)
        {
            var singleAgent = availableAgents.First();
            return new AgentSelectionResult
            {
                SelectedAgent = singleAgent,
                ConfidenceScore = 1.0,
                SelectionReason = $"Only agent available: {singleAgent.Config.Name}",
                AllScores = new[] { new AgentScore { Agent = singleAgent, Score = 1.0 } }
            };
        }

        // Get results from both selectors in parallel
        var keywordTask = _keywordSelector.SelectAgentAsync(input, availableAgents, cancellationToken);
        var embeddingTask = _embeddingSelector.SelectAgentAsync(input, availableAgents, cancellationToken);

        await Task.WhenAll(keywordTask, embeddingTask);

        var keywordResult = await keywordTask;
        var embeddingResult = await embeddingTask;

        // Calculate per-agent hybrid scores using AllScores from both selectors
        var hybridScores = new Dictionary<string, (IAgent Agent, double Score)>();

        foreach (var agent in availableAgents)
        {
            var keywordScore = keywordResult.AllScores
                .FirstOrDefault(s => s.Agent.Config.Name == agent.Config.Name)?.Score ?? 0.0;

            var embeddingScore = embeddingResult.AllScores
                .FirstOrDefault(s => s.Agent.Config.Name == agent.Config.Name)?.Score ?? 0.0;

            var hybridScore = (_keywordWeight * keywordScore) + (_embeddingWeight * embeddingScore);
            hybridScores[agent.Config.Name] = (agent, hybridScore);
        }

        // Find the agent with highest hybrid score
        var best = hybridScores.OrderByDescending(kvp => kvp.Value.Score).First();

        var reason = BuildSelectionReason(
            best.Key,
            best.Value.Score,
            keywordResult,
            embeddingResult,
            hybridScores);

        var allScores = hybridScores
            .OrderByDescending(kvp => kvp.Value.Score)
            .Select(kvp => new AgentScore
            {
                Agent = kvp.Value.Agent,
                Score = kvp.Value.Score,
                Reasons = new List<string> { $"Hybrid: {kvp.Value.Score:P1}" }
            })
            .ToList();

        return new AgentSelectionResult
        {
            SelectedAgent = best.Value.Agent,
            ConfidenceScore = best.Value.Score,
            SelectionReason = reason,
            AllScores = allScores
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentScore>> ScoreAgentsAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default)
    {
        var result = await SelectAgentAsync(input, availableAgents, cancellationToken);
        return result.AllScores;
    }

    /// <summary>
    /// Builds a human-readable explanation of the hybrid selection.
    /// </summary>
    private string BuildSelectionReason(
        string selectedAgent,
        double confidence,
        AgentSelectionResult keywordResult,
        AgentSelectionResult embeddingResult,
        Dictionary<string, (IAgent Agent, double Score)> hybridScores)
    {
        var keywordAgentName = keywordResult.SelectedAgent?.Config.Name ?? "none";
        var embeddingAgentName = embeddingResult.SelectedAgent?.Config.Name ?? "none";

        var reason = $"Hybrid selection: '{selectedAgent}' with {confidence:P1} confidence.\n";
        reason += $"  Keyword ({_keywordWeight:P0}): '{keywordAgentName}' ({keywordResult.ConfidenceScore:P1})\n";
        reason += $"  Embedding ({_embeddingWeight:P0}): '{embeddingAgentName}' ({embeddingResult.ConfidenceScore:P1})";

        // Show if both selectors agreed
        if (keywordAgentName == embeddingAgentName && keywordAgentName == selectedAgent)
        {
            reason += "\n  ✓ Both selectors agreed on this agent.";
        }
        else
        {
            reason += "\n  ℹ Selectors disagreed, hybrid score determined final choice.";
        }

        // Show runner-up
        var runnerUp = hybridScores
            .Where(kvp => kvp.Key != selectedAgent)
            .OrderByDescending(kvp => kvp.Value.Score)
            .FirstOrDefault();

        if (runnerUp.Key != null)
        {
            reason += $"\n  Runner-up: '{runnerUp.Key}' ({runnerUp.Value.Score:P1})";
        }

        return reason;
    }
}

/// <summary>
/// Configuration options for the hybrid agent selector.
/// </summary>
public class HybridSelectorConfig
{
    /// <summary>
    /// Weight for keyword-based scoring (default: 0.4).
    /// </summary>
    public double KeywordWeight { get; set; } = 0.4;

    /// <summary>
    /// Weight for embedding-based scoring (default: 0.6).
    /// </summary>
    public double EmbeddingWeight { get; set; } = 0.6;

    /// <summary>
    /// Creates a balanced configuration (50/50 split).
    /// </summary>
    public static HybridSelectorConfig Balanced => new()
    {
        KeywordWeight = 0.5,
        EmbeddingWeight = 0.5
    };

    /// <summary>
    /// Creates a keyword-focused configuration (70/30 split).
    /// </summary>
    public static HybridSelectorConfig KeywordFocused => new()
    {
        KeywordWeight = 0.7,
        EmbeddingWeight = 0.3
    };

    /// <summary>
    /// Creates an embedding-focused configuration (30/70 split).
    /// </summary>
    public static HybridSelectorConfig EmbeddingFocused => new()
    {
        KeywordWeight = 0.3,
        EmbeddingWeight = 0.7
    };
}
