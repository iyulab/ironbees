namespace Ironbees.Autonomous.Models;

/// <summary>
/// Oracle verification verdict
/// </summary>
public record OracleVerdict
{
    /// <summary>
    /// Whether the goal is considered complete
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Whether execution can/should continue
    /// </summary>
    public bool CanContinue { get; init; }

    /// <summary>
    /// Analysis explanation from oracle
    /// </summary>
    public required string Analysis { get; init; }

    /// <summary>
    /// Suggested next prompt if continuation needed
    /// </summary>
    public string? NextPromptSuggestion { get; init; }

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Token usage for this verification
    /// </summary>
    public TokenUsage? TokenUsage { get; init; }

    /// <summary>
    /// Reflection insights (if reflection mode enabled)
    /// </summary>
    public OracleReflection? Reflection { get; init; }

    /// <summary>
    /// Create an error verdict
    /// </summary>
    public static OracleVerdict Error(string message) => new()
    {
        IsComplete = false,
        CanContinue = false,
        Analysis = message,
        Confidence = 0
    };
}

/// <summary>
/// Reflection insights from oracle (Reflexion pattern)
/// </summary>
public record OracleReflection
{
    /// <summary>Aspects of the approach that were effective</summary>
    public string? WhatWorkedWell { get; init; }

    /// <summary>Specific areas needing improvement</summary>
    public string? WhatCouldImprove { get; init; }

    /// <summary>Key insights to apply in future iterations</summary>
    public string? LessonsLearned { get; init; }

    /// <summary>Recommended approach for next attempt</summary>
    public string? SuggestedStrategy { get; init; }

    /// <summary>Convert to IterationLearning for context</summary>
    public IterationLearning ToLearning(int iterationNumber) => new()
    {
        IterationNumber = iterationNumber,
        Type = LearningType.Pattern,
        Summary = LessonsLearned ?? WhatWorkedWell ?? "No specific learning captured",
        Details = $"Worked: {WhatWorkedWell ?? "N/A"}\nImprove: {WhatCouldImprove ?? "N/A"}\nStrategy: {SuggestedStrategy ?? "N/A"}",
        Confidence = 0.8
    };

    /// <summary>Convert to ReflectionInsight for context</summary>
    public ReflectionInsight ToInsight() => new()
    {
        Type = ReflectionType.Critique,
        Summary = WhatCouldImprove ?? LessonsLearned ?? "Reflection captured",
        Analysis = $"Worked: {WhatWorkedWell ?? "N/A"}\nImprove: {WhatCouldImprove ?? "N/A"}",
        SuggestedAction = SuggestedStrategy,
        Confidence = 0.8
    };
}

/// <summary>
/// Token usage information
/// </summary>
public record TokenUsage
{
    /// <summary>Input tokens</summary>
    public int InputTokens { get; init; }

    /// <summary>Output tokens</summary>
    public int OutputTokens { get; init; }

    /// <summary>Total tokens</summary>
    public int TotalTokens => InputTokens + OutputTokens;
}
