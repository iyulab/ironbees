namespace Ironbees.Autonomous.Models;

/// <summary>
/// Enhanced oracle verdict with context-aware insights
/// </summary>
/// <remarks>
/// Extends OracleVerdict with additional metadata for context-aware decision making.
/// Provides goal tracking, confidence evolution, and richer feedback.
/// </remarks>
public record EnhancedOracleVerdict : OracleVerdict
{
    /// <summary>
    /// List of completed sub-goals (if applicable)
    /// </summary>
    public IReadOnlyList<string> CompletedGoals { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of remaining sub-goals (if applicable)
    /// </summary>
    public IReadOnlyList<string> RemainingGoals { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Confidence evolution across iterations
    /// </summary>
    /// <remarks>
    /// Maps iteration number to confidence score.
    /// Useful for detecting confidence trends and stability.
    /// </remarks>
    public IReadOnlyDictionary<int, double> ConfidenceHistory { get; init; } =
        new Dictionary<int, double>();

    /// <summary>
    /// Insights from previous iterations that influenced this verdict
    /// </summary>
    public IReadOnlyList<string> ContextInsights { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Additional metadata from context-aware analysis
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Create an enhanced verdict from base verdict
    /// </summary>
    public static EnhancedOracleVerdict FromBase(
        OracleVerdict baseVerdict,
        IEnumerable<string>? completedGoals = null,
        IEnumerable<string>? remainingGoals = null,
        IDictionary<int, double>? confidenceHistory = null,
        IEnumerable<string>? contextInsights = null,
        IDictionary<string, object>? metadata = null) => new()
    {
        IsComplete = baseVerdict.IsComplete,
        CanContinue = baseVerdict.CanContinue,
        Analysis = baseVerdict.Analysis,
        NextPromptSuggestion = baseVerdict.NextPromptSuggestion,
        Confidence = baseVerdict.Confidence,
        TokenUsage = baseVerdict.TokenUsage,
        Reflection = baseVerdict.Reflection,
        CompletedGoals = completedGoals?.ToArray() ?? Array.Empty<string>(),
        RemainingGoals = remainingGoals?.ToArray() ?? Array.Empty<string>(),
        ConfidenceHistory = confidenceHistory as IReadOnlyDictionary<int, double> ??
            new Dictionary<int, double>(confidenceHistory ?? new Dictionary<int, double>()),
        ContextInsights = contextInsights?.ToArray() ?? Array.Empty<string>(),
        Metadata = metadata as IReadOnlyDictionary<string, object> ??
            new Dictionary<string, object>(metadata ?? new Dictionary<string, object>())
    };

    /// <summary>
    /// Goal achieved with context
    /// </summary>
    public static EnhancedOracleVerdict GoalAchieved(
        string analysis,
        double confidence = 1.0,
        IEnumerable<string>? completedGoals = null) => new()
    {
        IsComplete = true,
        CanContinue = false,
        Analysis = analysis,
        Confidence = confidence,
        CompletedGoals = completedGoals?.ToArray() ?? Array.Empty<string>()
    };

    /// <summary>
    /// Continue with goal tracking
    /// </summary>
    public static EnhancedOracleVerdict ContinueWithProgress(
        string analysis,
        double confidence,
        IEnumerable<string>? completedGoals = null,
        IEnumerable<string>? remainingGoals = null) => new()
    {
        IsComplete = false,
        CanContinue = true,
        Analysis = analysis,
        Confidence = confidence,
        CompletedGoals = completedGoals?.ToArray() ?? Array.Empty<string>(),
        RemainingGoals = remainingGoals?.ToArray() ?? Array.Empty<string>()
    };
}
