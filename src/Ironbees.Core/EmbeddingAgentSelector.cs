using System.Collections.Concurrent;

namespace Ironbees.Core;

/// <summary>
/// Selects agents based on semantic similarity using embedding vectors.
/// Uses cosine similarity to find the most relevant agent for a given query.
/// </summary>
public class EmbeddingAgentSelector : IAgentSelector
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ConcurrentDictionary<string, AgentEmbedding> _agentEmbeddings = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>
    /// Creates a new instance of the embedding-based agent selector.
    /// </summary>
    /// <param name="embeddingProvider">The embedding provider to use for generating embeddings.</param>
    public EmbeddingAgentSelector(IEmbeddingProvider embeddingProvider)
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
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

        // Ensure all agents have embeddings cached
        await EnsureAgentEmbeddingsAsync(availableAgents, cancellationToken);

        // Generate query embedding
        var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(input, cancellationToken);

        // Calculate similarity scores for each agent
        var scores = new List<AgentScore>();

        foreach (var agent in availableAgents)
        {
            if (!_agentEmbeddings.TryGetValue(agent.Config.Name, out var agentEmbedding))
            {
                continue; // Should not happen after EnsureAgentEmbeddingsAsync
            }

            // Compute cosine similarity with query
            var similarity = VectorSimilarity.CosineSimilarity(queryEmbedding, agentEmbedding.CombinedEmbedding);
            var normalizedScore = NormalizeScore(similarity);

            scores.Add(new AgentScore
            {
                Agent = agent,
                Score = normalizedScore,
                Reasons = new List<string> { $"Semantic similarity: {normalizedScore:P1}" }
            });
        }

        // Sort by score descending
        scores = scores.OrderByDescending(s => s.Score).ToList();

        var best = scores.First();
        var reason = BuildSelectionReason(best.Agent.Config.Name, best.Score, scores);

        return new AgentSelectionResult
        {
            SelectedAgent = best.Agent,
            ConfidenceScore = best.Score,
            SelectionReason = reason,
            AllScores = scores
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
    /// Ensures all agents have cached embeddings.
    /// </summary>
    private async Task EnsureAgentEmbeddingsAsync(
        IReadOnlyCollection<IAgent> agents,
        CancellationToken cancellationToken)
    {
        var agentsNeedingEmbeddings = agents
            .Where(a => !_agentEmbeddings.ContainsKey(a.Config.Name))
            .ToList();

        if (agentsNeedingEmbeddings.Count == 0)
        {
            return; // All agents already have embeddings
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            agentsNeedingEmbeddings = agents
                .Where(a => !_agentEmbeddings.ContainsKey(a.Config.Name))
                .ToList();

            if (agentsNeedingEmbeddings.Count == 0)
            {
                return;
            }

            // Generate embeddings for all agents in batch
            var texts = agentsNeedingEmbeddings
                .Select(a => BuildAgentText(a.Config))
                .ToList();

            var embeddings = await _embeddingProvider.GenerateEmbeddingsAsync(texts, cancellationToken);

            // Cache the embeddings
            for (int i = 0; i < agentsNeedingEmbeddings.Count; i++)
            {
                var agent = agentsNeedingEmbeddings[i];
                _agentEmbeddings[agent.Config.Name] = new AgentEmbedding
                {
                    AgentName = agent.Config.Name,
                    CombinedEmbedding = embeddings[i],
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Builds a text representation of an agent for embedding generation.
    /// Combines description, capabilities, and tags into a single text.
    /// </summary>
    private static string BuildAgentText(AgentConfig agent)
    {
        var parts = new List<string>
        {
            agent.Description
        };

        if (agent.Capabilities.Count > 0)
        {
            parts.Add($"Capabilities: {string.Join(", ", agent.Capabilities)}");
        }

        if (agent.Tags.Count > 0)
        {
            parts.Add($"Tags: {string.Join(", ", agent.Tags)}");
        }

        return string.Join(". ", parts);
    }

    /// <summary>
    /// Normalizes similarity score to confidence range [0.0, 1.0].
    /// Cosine similarity is already in [-1.0, 1.0], we map to [0.0, 1.0].
    /// </summary>
    private static double NormalizeScore(double similarity)
    {
        // Cosine similarity is in [-1.0, 1.0], but negative values are rare for text
        // Map [0.0, 1.0] to [0.0, 1.0] (no-op for positive values)
        // Map [-1.0, 0.0] to [0.0, 0.0] (treat as no match)
        return Math.Max(0.0, similarity);
    }

    /// <summary>
    /// Builds a human-readable explanation of the selection.
    /// </summary>
    private static string BuildSelectionReason(
        string selectedAgent,
        double confidence,
        List<AgentScore> allScores)
    {
        var reason = $"Selected '{selectedAgent}' with {confidence:P1} confidence based on semantic similarity.";

        if (allScores.Count > 1)
        {
            var runnerUp = allScores[1];
            reason += $" Runner-up: '{runnerUp.Agent.Config.Name}' ({runnerUp.Score:P1}).";
        }

        return reason;
    }

    /// <summary>
    /// Clears the embedding cache. Useful for testing or when agents are updated.
    /// </summary>
    public void ClearCache()
    {
        _agentEmbeddings.Clear();
    }

    /// <summary>
    /// Pre-generates and caches embeddings for all agents.
    /// Useful for warming up the cache at application startup.
    /// </summary>
    /// <param name="agents">The agents to pre-generate embeddings for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WarmupCacheAsync(
        IReadOnlyCollection<IAgent> agents,
        CancellationToken cancellationToken = default)
    {
        await EnsureAgentEmbeddingsAsync(agents, cancellationToken);
    }

    private class AgentEmbedding
    {
        public required string AgentName { get; init; }
        public required float[] CombinedEmbedding { get; init; }
        public required DateTime GeneratedAt { get; init; }
    }
}
