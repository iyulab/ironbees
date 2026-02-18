namespace Ironbees.Core;

/// <summary>
/// Selects agents based on keyword matching with capabilities and tags, enhanced with TF-IDF weighting
/// </summary>
public class KeywordAgentSelector : IAgentSelector
{
    private static readonly char[] WordSeparators = [' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '-', '_'];

    private readonly double _minimumConfidenceThreshold;
    private readonly IAgent? _fallbackAgent;
    private readonly KeywordNormalizer _normalizer;
    private readonly HashSet<string> _stopwords;

    private TfidfWeightCalculator? _tfidfCalculator;
    private readonly Dictionary<string, HashSet<string>> _keywordCache;
    private readonly object _cacheLock = new();

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
        _normalizer = new KeywordNormalizer();
        _stopwords = StopwordsProvider.GetDefaultStopwords();
        _keywordCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
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

        // Initialize TF-IDF calculator on first use (lazy initialization)
        if (_tfidfCalculator == null && availableAgents.Count > 0)
        {
            _tfidfCalculator = new TfidfWeightCalculator(availableAgents);
        }

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

        // Score based on capabilities (weight: 0.5 - highest priority)
        maxScore += 0.5;
        var capabilityScore = ScoreKeywords(
            inputWords,
            config.Capabilities,
            "capability",
            reasons);
        totalScore += capabilityScore * 0.5;

        // Score based on tags (weight: 0.35 - second priority)
        maxScore += 0.35;
        var tagScore = ScoreKeywords(
            inputWords,
            config.Tags,
            "tag",
            reasons);
        totalScore += tagScore * 0.35;

        // Score based on description (weight: 0.1 - lower priority)
        maxScore += 0.1;
        var descriptionScore = ScoreText(
            inputWords,
            config.Description.ToLowerInvariant(),
            "description",
            reasons);
        totalScore += descriptionScore * 0.1;

        // Score based on agent name (weight: 0.05 - lowest priority)
        maxScore += 0.05;
        var nameScore = ScoreText(
            inputWords,
            config.Name.ToLowerInvariant(),
            "name",
            reasons);
        totalScore += nameScore * 0.05;

        // Normalize base score
        var baseScore = maxScore > 0 ? totalScore / maxScore : 0;

        // Apply TF-IDF weighting boost (up to 30% improvement for better discrimination)
        var finalScore = baseScore;
        if (_tfidfCalculator != null && inputWords.Count > 0)
        {
            var agentDocumentText = GetAgentDocumentText(agent);
            var tfidfScore = _tfidfCalculator.CalculateTfidfScore(inputWords, agentDocumentText);

            // Boost score based on TF-IDF (0-30% improvement)
            var tfidfBoost = tfidfScore * 0.3;
            finalScore = baseScore * (1.0 + tfidfBoost);

            if (tfidfBoost > 0.05)
            {
                reasons.Add($"TF-IDF relevance boost: {tfidfBoost:P0}");
            }
        }

        return new AgentScore
        {
            Agent = agent,
            Score = Math.Clamp(finalScore, 0, 1),
            Reasons = reasons
        };
    }

    private static string GetAgentDocumentText(IAgent agent)
    {
        var config = agent.Config;
        var parts = new List<string>
        {
            config.Name,
            config.Description
        };

        parts.AddRange(config.Capabilities);
        parts.AddRange(config.Tags);

        return string.Join(" ", parts);
    }

    private static double ScoreKeywords(
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
                inputWords.Any(w =>
                    (w.Contains(keywordLower) && keywordLower.Length >= 3) ||
                    (keywordLower.Contains(w) && w.Length >= 3)))
            {
                matches++;
                matchedKeywords.Add(keyword);
            }
        }

        if (matches > 0)
        {
            reasons.Add($"{category} matches: {string.Join(", ", matchedKeywords)}");
            // Geometric mean of agent-side and query-side coverage prevents
            // agents with fewer keywords from getting disproportionately high scores
            var agentCoverage = (double)matches / keywords.Count;
            var queryCoverage = (double)matches / Math.Max(inputWords.Count, 1);
            return Math.Sqrt(agentCoverage * queryCoverage);
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
        // Check cache first
        lock (_cacheLock)
        {
            if (_keywordCache.TryGetValue(text, out var cachedKeywords))
            {
                return cachedKeywords;
            }
        }

        // Extract and normalize keywords
        var words = text
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 2 && !_stopwords.Contains(w))
            .Select(w => _normalizer.Normalize(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Cache the result (limit cache size to prevent memory issues)
        lock (_cacheLock)
        {
            if (_keywordCache.Count < 1000)
            {
                _keywordCache[text] = words;
            }
        }

        return words;
    }

    /// <summary>
    /// Clears the keyword extraction cache
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _keywordCache.Clear();
            _tfidfCalculator = null;
        }
    }
}
