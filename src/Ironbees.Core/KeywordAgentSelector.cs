namespace Ironbees.Core;

/// <summary>
/// Selects agents based on keyword matching with capabilities and tags
/// </summary>
public class KeywordAgentSelector : IAgentSelector
{
    private readonly double _minimumConfidenceThreshold;
    private readonly IAgent? _fallbackAgent;

    /// <summary>
    /// Creates a new keyword-based agent selector
    /// </summary>
    /// <param name="minimumConfidenceThreshold">Minimum confidence score to consider (default: 0.3)</param>
    /// <param name="fallbackAgent">Agent to use when no match found (optional)</param>
    public KeywordAgentSelector(
        double minimumConfidenceThreshold = 0.3,
        IAgent? fallbackAgent = null)
    {
        if (minimumConfidenceThreshold < 0 || minimumConfidenceThreshold > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumConfidenceThreshold),
                "Threshold must be between 0 and 1");
        }

        _minimumConfidenceThreshold = minimumConfidenceThreshold;
        _fallbackAgent = fallbackAgent;
    }

    /// <inheritdoc />
    public Task<AgentSelectionResult> SelectAgentAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(availableAgents);

        if (availableAgents.Count == 0)
        {
            return Task.FromResult(new AgentSelectionResult
            {
                SelectedAgent = null,
                ConfidenceScore = 0,
                SelectionReason = "No agents available",
                AllScores = Array.Empty<AgentScore>()
            });
        }

        // Score all agents
        var scores = ScoreAgentsInternal(input, availableAgents);

        // Get best match
        var bestScore = scores.FirstOrDefault();

        if (bestScore == null || bestScore.Score < _minimumConfidenceThreshold)
        {
            // Use fallback if available
            if (_fallbackAgent != null)
            {
                return Task.FromResult(new AgentSelectionResult
                {
                    SelectedAgent = _fallbackAgent,
                    ConfidenceScore = 0.5,
                    SelectionReason = $"No confident match found (best: {bestScore?.Score:F2}), using fallback agent",
                    AllScores = scores
                });
            }

            // No fallback, return best effort or null
            return Task.FromResult(new AgentSelectionResult
            {
                SelectedAgent = bestScore?.Agent,
                ConfidenceScore = bestScore?.Score ?? 0,
                SelectionReason = bestScore != null
                    ? $"Low confidence match: {string.Join(", ", bestScore.Reasons)}"
                    : "No matching agents found",
                AllScores = scores
            });
        }

        return Task.FromResult(new AgentSelectionResult
        {
            SelectedAgent = bestScore.Agent,
            ConfidenceScore = bestScore.Score,
            SelectionReason = $"Matched on: {string.Join(", ", bestScore.Reasons)}",
            AllScores = scores
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentScore>> ScoreAgentsAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(availableAgents);

        var scores = ScoreAgentsInternal(input, availableAgents);
        return Task.FromResult<IReadOnlyList<AgentScore>>(scores);
    }

    private List<AgentScore> ScoreAgentsInternal(
        string input,
        IReadOnlyCollection<IAgent> availableAgents)
    {
        var inputLower = input.ToLowerInvariant();
        var inputWords = ExtractKeywords(inputLower);

        var scores = new List<AgentScore>();

        foreach (var agent in availableAgents)
        {
            var score = CalculateScore(agent, inputWords);
            scores.Add(score);
        }

        // Sort by score descending
        return scores.OrderByDescending(s => s.Score).ToList();
    }

    private AgentScore CalculateScore(IAgent agent, HashSet<string> inputWords)
    {
        var reasons = new List<string>();
        var totalScore = 0.0;
        var maxScore = 0.0;

        var config = agent.Config;

        // Score based on capabilities (weight: 0.4)
        maxScore += 0.4;
        var capabilityScore = ScoreKeywords(
            inputWords,
            config.Capabilities,
            "capability",
            reasons);
        totalScore += capabilityScore * 0.4;

        // Score based on tags (weight: 0.3)
        maxScore += 0.3;
        var tagScore = ScoreKeywords(
            inputWords,
            config.Tags,
            "tag",
            reasons);
        totalScore += tagScore * 0.3;

        // Score based on description (weight: 0.2)
        maxScore += 0.2;
        var descriptionScore = ScoreText(
            inputWords,
            config.Description.ToLowerInvariant(),
            "description",
            reasons);
        totalScore += descriptionScore * 0.2;

        // Score based on agent name (weight: 0.1)
        maxScore += 0.1;
        var nameScore = ScoreText(
            inputWords,
            config.Name.ToLowerInvariant(),
            "name",
            reasons);
        totalScore += nameScore * 0.1;

        // Normalize score
        var normalizedScore = maxScore > 0 ? totalScore / maxScore : 0;

        return new AgentScore
        {
            Agent = agent,
            Score = Math.Clamp(normalizedScore, 0, 1),
            Reasons = reasons
        };
    }

    private double ScoreKeywords(
        HashSet<string> inputWords,
        List<string> keywords,
        string category,
        List<string> reasons)
    {
        if (keywords.Count == 0)
        {
            return 0;
        }

        var matches = 0;
        var matchedKeywords = new List<string>();

        foreach (var keyword in keywords)
        {
            var keywordLower = keyword.ToLowerInvariant();
            if (inputWords.Contains(keywordLower) ||
                inputWords.Any(w => w.Contains(keywordLower) || keywordLower.Contains(w)))
            {
                matches++;
                matchedKeywords.Add(keyword);
            }
        }

        if (matches > 0)
        {
            reasons.Add($"{category} matches: {string.Join(", ", matchedKeywords)}");
            return (double)matches / keywords.Count;
        }

        return 0;
    }

    private double ScoreText(
        HashSet<string> inputWords,
        string text,
        string category,
        List<string> reasons)
    {
        var textWords = ExtractKeywords(text);
        var matches = inputWords.Intersect(textWords).ToList();

        if (matches.Count > 0)
        {
            reasons.Add($"{category} keyword matches: {matches.Count}");
            return (double)matches.Count / Math.Max(inputWords.Count, 1);
        }

        return 0;
    }

    private HashSet<string> ExtractKeywords(string text)
    {
        // Common stop words to ignore
        var stopWords = new HashSet<string>
        {
            "a", "an", "the", "and", "or", "but", "is", "are", "was", "were",
            "be", "been", "being", "have", "has", "had", "do", "does", "did",
            "will", "would", "should", "could", "can", "may", "might", "must",
            "i", "you", "he", "she", "it", "we", "they", "me", "him", "her",
            "us", "them", "my", "your", "his", "its", "our", "their",
            "in", "on", "at", "to", "for", "of", "with", "from", "about",
            "as", "by", "this", "that", "these", "those"
        };

        var words = text
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet();

        return words;
    }
}
