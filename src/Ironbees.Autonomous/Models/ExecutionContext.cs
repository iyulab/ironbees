namespace Ironbees.Autonomous.Models;

/// <summary>
/// Execution context for passing state between iterations.
/// Enables reflection, self-correction, and accumulated knowledge.
/// Named AutonomousExecutionContext to avoid conflict with System.Threading.ExecutionContext.
/// </summary>
public record AutonomousExecutionContext
{
    /// <summary>Session ID for this execution</summary>
    public required string SessionId { get; init; }

    /// <summary>Original goal/prompt</summary>
    public required string OriginalGoal { get; init; }

    /// <summary>Current iteration number</summary>
    public int CurrentIteration { get; init; }

    /// <summary>Current oracle iteration within the task</summary>
    public int CurrentOracleIteration { get; init; }

    /// <summary>Accumulated learnings from previous iterations</summary>
    public IReadOnlyList<IterationLearning> Learnings { get; init; } = [];

    /// <summary>Accumulated errors and their resolutions</summary>
    public IReadOnlyList<ErrorResolution> ErrorResolutions { get; init; } = [];

    /// <summary>Key-value metadata for custom state</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>Previous execution outputs (for context)</summary>
    public IReadOnlyList<string> PreviousOutputs { get; init; } = [];

    /// <summary>Human feedback received</summary>
    public IReadOnlyList<string> HumanFeedbackHistory { get; init; } = [];

    /// <summary>Reflection insights from oracle</summary>
    public IReadOnlyList<ReflectionInsight> Reflections { get; init; } = [];

    /// <summary>Create initial context</summary>
    public static AutonomousExecutionContext Initial(string sessionId, string goal) => new()
    {
        SessionId = sessionId,
        OriginalGoal = goal,
        CurrentIteration = 0,
        CurrentOracleIteration = 0
    };

    /// <summary>Create context for next iteration</summary>
    public AutonomousExecutionContext WithNextIteration(int iteration, int oracleIteration) => this with
    {
        CurrentIteration = iteration,
        CurrentOracleIteration = oracleIteration
    };

    /// <summary>Add a learning from this iteration</summary>
    public AutonomousExecutionContext WithLearning(IterationLearning learning) => this with
    {
        Learnings = [.. Learnings, learning]
    };

    /// <summary>Add an error resolution</summary>
    public AutonomousExecutionContext WithErrorResolution(ErrorResolution resolution) => this with
    {
        ErrorResolutions = [.. ErrorResolutions, resolution]
    };

    /// <summary>Add metadata</summary>
    public AutonomousExecutionContext WithMetadata(string key, object value)
    {
        var dict = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = dict };
    }

    /// <summary>Add previous output</summary>
    public AutonomousExecutionContext WithPreviousOutput(string output)
    {
        // Keep last 5 outputs for context
        var outputs = PreviousOutputs.TakeLast(4).Append(output).ToList();
        return this with { PreviousOutputs = outputs };
    }

    /// <summary>Add human feedback</summary>
    public AutonomousExecutionContext WithHumanFeedback(string feedback) => this with
    {
        HumanFeedbackHistory = [.. HumanFeedbackHistory, feedback]
    };

    /// <summary>Add reflection insight</summary>
    public AutonomousExecutionContext WithReflection(ReflectionInsight reflection) => this with
    {
        Reflections = [.. Reflections, reflection]
    };

    /// <summary>Build context summary for prompts</summary>
    public string BuildContextSummary()
    {
        var parts = new List<string>();

        if (Learnings.Count > 0)
        {
            parts.Add($"Previous Learnings ({Learnings.Count}):");
            foreach (var learning in Learnings.TakeLast(3))
            {
                parts.Add($"  - [{learning.Type}] {learning.Summary}");
            }
        }

        if (ErrorResolutions.Count > 0)
        {
            parts.Add($"Error Resolutions ({ErrorResolutions.Count}):");
            foreach (var resolution in ErrorResolutions.TakeLast(2))
            {
                parts.Add($"  - Error: {resolution.ErrorSummary} → Resolution: {resolution.ResolutionApplied}");
            }
        }

        if (HumanFeedbackHistory.Count > 0)
        {
            parts.Add($"Human Feedback ({HumanFeedbackHistory.Count}):");
            foreach (var feedback in HumanFeedbackHistory.TakeLast(2))
            {
                parts.Add($"  - {feedback}");
            }
        }

        if (Reflections.Count > 0)
        {
            parts.Add($"Reflections ({Reflections.Count}):");
            foreach (var reflection in Reflections.TakeLast(2))
            {
                parts.Add($"  - {reflection.Summary}");
            }
        }

        return parts.Count > 0 ? string.Join("\n", parts) : "No prior context.";
    }
}

/// <summary>
/// Learning from an iteration
/// </summary>
public record IterationLearning
{
    /// <summary>Iteration number</summary>
    public int IterationNumber { get; init; }

    /// <summary>Type of learning</summary>
    public LearningType Type { get; init; }

    /// <summary>Summary of what was learned</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed description</summary>
    public string? Details { get; init; }

    /// <summary>Confidence in this learning (0-1)</summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>When this learning was captured</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of learning
/// </summary>
public enum LearningType
{
    /// <summary>Successful approach discovered</summary>
    SuccessfulApproach = 1,

    /// <summary>Failed approach to avoid</summary>
    FailedApproach = 2,

    /// <summary>Optimization opportunity</summary>
    Optimization = 3,

    /// <summary>Constraint discovered</summary>
    Constraint = 4,

    /// <summary>Dependency identified</summary>
    Dependency = 5,

    /// <summary>Pattern recognized</summary>
    Pattern = 6
}

/// <summary>
/// Error and its resolution
/// </summary>
public record ErrorResolution
{
    /// <summary>Iteration when error occurred</summary>
    public int IterationNumber { get; init; }

    /// <summary>Error summary</summary>
    public required string ErrorSummary { get; init; }

    /// <summary>Error category</summary>
    public ErrorCategory Category { get; init; }

    /// <summary>Resolution applied</summary>
    public required string ResolutionApplied { get; init; }

    /// <summary>Whether resolution was successful</summary>
    public bool WasSuccessful { get; init; }

    /// <summary>When the error occurred</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Error category
/// </summary>
public enum ErrorCategory
{
    /// <summary>Syntax or validation error</summary>
    Syntax = 1,

    /// <summary>Runtime execution error</summary>
    Runtime = 2,

    /// <summary>Timeout</summary>
    Timeout = 3,

    /// <summary>Resource unavailable</summary>
    Resource = 4,

    /// <summary>Permission denied</summary>
    Permission = 5,

    /// <summary>Logic error</summary>
    Logic = 6,

    /// <summary>External service error</summary>
    External = 7,

    /// <summary>Unknown</summary>
    Unknown = 99
}

/// <summary>
/// Reflection insight from oracle or self-analysis
/// </summary>
public record ReflectionInsight
{
    /// <summary>Type of reflection</summary>
    public ReflectionType Type { get; init; }

    /// <summary>Summary of insight</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed analysis</summary>
    public string? Analysis { get; init; }

    /// <summary>Suggested action based on reflection</summary>
    public string? SuggestedAction { get; init; }

    /// <summary>Confidence in this insight (0-1)</summary>
    public double Confidence { get; init; } = 0.8;

    /// <summary>When reflection was generated</summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of reflection
/// </summary>
public enum ReflectionType
{
    /// <summary>Self-assessment of output quality</summary>
    QualityAssessment = 1,

    /// <summary>Gap analysis against goal</summary>
    GapAnalysis = 2,

    /// <summary>Strategy adjustment recommendation</summary>
    StrategyAdjustment = 3,

    /// <summary>Critique of approach</summary>
    Critique = 4,

    /// <summary>Improvement suggestion</summary>
    Improvement = 5
}
