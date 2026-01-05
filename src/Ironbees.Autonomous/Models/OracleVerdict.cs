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
    /// Create an error verdict (allows continuation by default for resilience)
    /// </summary>
    public static OracleVerdict Error(string message, bool allowContinue = true) => new()
    {
        IsComplete = false,
        CanContinue = allowContinue,
        Analysis = message,
        Confidence = 0
    };

    // ============================================================================
    // Helper Methods for Clear Loop Control
    // ============================================================================

    /// <summary>
    /// Goal achieved - stop all execution.
    /// Use when the task is fully complete and no more iterations are needed.
    /// </summary>
    /// <param name="analysis">Explanation of successful completion</param>
    /// <param name="confidence">Confidence level (default: 1.0)</param>
    public static OracleVerdict GoalAchieved(string analysis, double confidence = 1.0) => new()
    {
        IsComplete = true,
        CanContinue = false,
        Analysis = analysis,
        Confidence = confidence
    };

    /// <summary>
    /// Continue to next main iteration (AutoContinue).
    /// Does NOT set NextPromptSuggestion, allowing the main loop's AutoContinue to handle the next iteration.
    /// Use when you want the orchestrator to proceed to the next iteration with a fresh task.
    /// </summary>
    /// <param name="analysis">Current progress analysis</param>
    /// <param name="confidence">Current confidence level</param>
    public static OracleVerdict ContinueToNextIteration(string analysis, double confidence = 0.5) => new()
    {
        IsComplete = false,
        CanContinue = true,
        Analysis = analysis,
        Confidence = confidence,
        NextPromptSuggestion = null  // Important: null triggers AutoContinue, not Oracle retry
    };

    /// <summary>
    /// Retry within the same Oracle loop with a refined prompt.
    /// Use when the current attempt needs refinement before moving to the next main iteration.
    /// WARNING: Setting NextPromptSuggestion triggers Oracle retry loop, not main loop AutoContinue.
    /// </summary>
    /// <param name="refinedPrompt">The improved prompt for retry</param>
    /// <param name="analysis">Explanation of why retry is needed</param>
    /// <param name="confidence">Current confidence level</param>
    public static OracleVerdict RetryWithRefinedPrompt(string refinedPrompt, string analysis, double confidence = 0.3) => new()
    {
        IsComplete = false,
        CanContinue = true,
        Analysis = analysis,
        Confidence = confidence,
        NextPromptSuggestion = refinedPrompt  // Triggers Oracle retry loop
    };

    /// <summary>
    /// Stop execution without achieving goal.
    /// Use when the task cannot continue (fatal error, resource exhaustion, etc.)
    /// </summary>
    /// <param name="reason">Reason for stopping</param>
    public static OracleVerdict Stop(string reason) => new()
    {
        IsComplete = false,
        CanContinue = false,
        Analysis = reason,
        Confidence = 0
    };

    /// <summary>
    /// Create a verdict for partial progress.
    /// Convenience method combining analysis with continuation.
    /// </summary>
    /// <param name="analysis">Progress analysis</param>
    /// <param name="confidence">Confidence in progress</param>
    /// <param name="continueToNext">If true, uses AutoContinue; if false, stops</param>
    public static OracleVerdict Progress(string analysis, double confidence, bool continueToNext = true) => new()
    {
        IsComplete = false,
        CanContinue = continueToNext,
        Analysis = analysis,
        Confidence = confidence,
        NextPromptSuggestion = null
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
