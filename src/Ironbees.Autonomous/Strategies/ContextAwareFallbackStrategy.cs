using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Executors;

namespace Ironbees.Autonomous.Strategies;

/// <summary>
/// Fallback strategy that selects questions based on conversation context.
/// Uses AgentDefinition's FallbackConfig with context-aware pools.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public class ContextAwareFallbackStrategy<TRequest, TResult> : IFallbackStrategy<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : class, ITaskResult
{
    private readonly FallbackConfig _config;
    private readonly List<GuessRule>? _guessRules;
    private readonly Func<TRequest, string, bool, TResult> _resultFactory;
    private readonly HashSet<string> _usedFallbacks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Create a context-aware fallback strategy
    /// </summary>
    public ContextAwareFallbackStrategy(
        FallbackConfig config,
        List<GuessRule>? guessRules,
        Func<TRequest, string, bool, TResult> resultFactory)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _guessRules = guessRules;
        _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
    }

    /// <inheritdoc />
    public bool CanProvideFallback(FallbackContext<TRequest> context)
    {
        return _config.Enabled && GetNextFallback(context) != null;
    }

    /// <inheritdoc />
    public Task<TResult?> GetFallbackAsync(
        FallbackContext<TRequest> context,
        CancellationToken cancellationToken = default)
    {
        var fallbackInfo = GetNextFallback(context);

        if (fallbackInfo == null)
            return Task.FromResult<TResult?>(default);

        _usedFallbacks.Add(fallbackInfo.Value.Content);
        var result = _resultFactory(context.FailedRequest, fallbackInfo.Value.Content, fallbackInfo.Value.IsGuess);

        return Task.FromResult<TResult?>(result);
    }

    /// <summary>
    /// Find the next appropriate fallback based on context
    /// </summary>
    private (string Content, bool IsGuess)? GetNextFallback(FallbackContext<TRequest> context)
    {
        // Get asked questions from metadata (more reliable than previousOutputs)
        var askedQuestions = GetAskedQuestions(context);

        // Check if we should make a guess
        if (context.Metadata.TryGetValue("must_guess", out var mustGuessObj) && mustGuessObj is bool mustGuess && mustGuess)
        {
            var guess = DeduceBestGuess(context);
            if (guess != null)
                return (guess, true);
        }

        // Extract context keywords from yes answers
        var contextKeywords = ExtractContextKeywords(context);

        // Try context-aware pools first (high priority first)
        if (_config.Pools != null)
        {
            var orderedPools = _config.Pools
                .OrderByDescending(p => p.Priority == "high")
                .ThenByDescending(p => p.Context.Count); // More specific contexts first

            foreach (var pool in orderedPools)
            {
                if (PoolMatchesContext(pool, contextKeywords))
                {
                    var question = GetUnusedFromPool(pool.Questions, askedQuestions);
                    if (question != null)
                        return (question, false);
                }
            }
        }

        // Fall back to default pool
        var defaultQuestion = GetUnusedFromPool(_config.Default, askedQuestions);
        if (defaultQuestion != null)
            return (defaultQuestion, false);

        // Last resort: Items list (backwards compatibility)
        var itemQuestion = GetUnusedFromPool(_config.Items, askedQuestions);
        if (itemQuestion != null)
            return (itemQuestion, false);

        return null;
    }

    /// <summary>
    /// Get all asked questions from context
    /// </summary>
    private HashSet<string> GetAskedQuestions(FallbackContext<TRequest> context)
    {
        var asked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // From metadata (preferred)
        if (context.Metadata.TryGetValue("asked_questions", out var askedObj) && askedObj is IEnumerable<string> askedList)
        {
            foreach (var q in askedList)
                asked.Add(NormalizeQuestion(q));
        }

        // From previousOutputs (fallback)
        foreach (var output in context.PreviousOutputs)
            asked.Add(NormalizeQuestion(output));

        // From used fallbacks
        foreach (var used in _usedFallbacks)
            asked.Add(NormalizeQuestion(used));

        return asked;
    }

    /// <summary>
    /// Extract context keywords from yes answers
    /// </summary>
    private static HashSet<string> ExtractContextKeywords(FallbackContext<TRequest> context)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Get yes answers from metadata
        IEnumerable<string> yesAnswers;
        if (context.Metadata.TryGetValue("yes_answers", out var yesObj) && yesObj is IEnumerable<string> yesList)
        {
            yesAnswers = yesList;
        }
        else
        {
            // Fallback: try to extract from previousOutputs (legacy format "question: answer")
            yesAnswers = context.PreviousOutputs
                .Where(p => p.Contains(": yes", StringComparison.OrdinalIgnoreCase) ||
                           p.Contains(":yes", StringComparison.OrdinalIgnoreCase));
        }

        // Common keywords to extract
        var commonKeywords = new[]
        {
            "living", "animal", "mammal", "large", "wild", "domesticated",
            "predator", "herbivore", "carnivore", "africa", "asia", "water",
            "trunk", "tusks", "stripes", "spots", "mane", "man-made", "electronic", "metal",
            "bigger", "smaller", "pet", "fly", "swim"
        };

        foreach (var answer in yesAnswers)
        {
            var lower = answer.ToLowerInvariant();
            foreach (var keyword in commonKeywords)
            {
                if (lower.Contains(keyword))
                    keywords.Add(keyword);
            }
        }

        return keywords;
    }

    /// <summary>
    /// Check if a pool's context requirements are met
    /// </summary>
    private static bool PoolMatchesContext(FallbackPool pool, HashSet<string> contextKeywords)
    {
        if (pool.Context.Count == 0)
            return true;

        return pool.Context.All(c =>
            contextKeywords.Any(k => k.Contains(c, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Get an unused question from a pool
    /// </summary>
    private string? GetUnusedFromPool(IEnumerable<string> pool, HashSet<string> askedQuestions)
    {
        foreach (var question in pool)
        {
            var normalized = NormalizeQuestion(question);

            // Skip if already used as fallback
            if (_usedFallbacks.Any(u => NormalizeQuestion(u) == normalized))
                continue;

            // Skip if already asked (exact or similar)
            if (askedQuestions.Contains(normalized))
                continue;

            // Skip if any asked question is very similar
            if (askedQuestions.Any(a => IsSimilarQuestion(a, normalized)))
                continue;

            return question;
        }

        return null;
    }

    /// <summary>
    /// Normalize question for comparison
    /// </summary>
    private static string NormalizeQuestion(string question)
    {
        return question.ToLowerInvariant()
            .Replace("?", "")
            .Replace("is it ", "")
            .Replace("does it ", "")
            .Replace("is the secret ", "")
            .Replace("the secret", "")
            .Trim();
    }

    /// <summary>
    /// Check if two questions are similar
    /// </summary>
    private static bool IsSimilarQuestion(string q1, string q2)
    {
        // Exact match
        if (q1 == q2)
            return true;

        // One contains the other
        if (q1.Contains(q2) || q2.Contains(q1))
            return true;

        // Check for key concept overlap (e.g., both ask about "living")
        var words1 = q1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = q2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        // If questions share more than 60% of significant words, they're similar
        var significantWords = new[] { "living", "animal", "mammal", "plant", "large", "small", "wild", "domestic", "trunk", "africa" };
        var shared = words1.Intersect(words2).Count(w => significantWords.Contains(w));

        return shared >= 2;
    }

    /// <summary>
    /// Deduce the best guess based on context and rules
    /// </summary>
    private string? DeduceBestGuess(FallbackContext<TRequest> context)
    {
        if (_guessRules == null || _guessRules.Count == 0)
            return null;

        var contextKeywords = ExtractContextKeywords(context);

        // Find matching rule (first match wins, except default)
        GuessRule? defaultRule = null;
        foreach (var rule in _guessRules)
        {
            if (rule.Conditions.Count == 0 && rule.Default != null)
            {
                defaultRule = rule;
                continue;
            }

            if (rule.Matches(contextKeywords))
                return rule.GetGuess();
        }

        return defaultRule?.GetGuess();
    }

    /// <summary>
    /// Reset the used fallbacks list
    /// </summary>
    public void Reset()
    {
        _usedFallbacks.Clear();
    }
}
