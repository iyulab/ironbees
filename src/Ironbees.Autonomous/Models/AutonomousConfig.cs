using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Models;

/// <summary>
/// Configuration for autonomous execution
/// </summary>
public record AutonomousConfig
{
    /// <summary>
    /// Maximum iterations per task (prevents infinite loops)
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// Enable oracle verification
    /// </summary>
    public bool EnableOracle { get; init; } = true;

    /// <summary>
    /// Maximum oracle iterations per task
    /// </summary>
    public int MaxOracleIterations { get; init; } = 5;

    /// <summary>
    /// Oracle configuration
    /// </summary>
    public OracleConfig OracleConfig { get; init; } = new();

    /// <summary>
    /// Completion mode
    /// </summary>
    public CompletionMode CompletionMode { get; init; } = CompletionMode.UntilQueueEmpty;

    /// <summary>
    /// Enable checkpointing for recovery
    /// </summary>
    public bool EnableCheckpointing { get; init; } = true;

    /// <summary>
    /// Continue processing queue on task failure
    /// </summary>
    public bool ContinueOnFailure { get; init; } = false;

    /// <summary>
    /// Minimum confidence threshold to consider goal complete
    /// </summary>
    public double MinConfidenceThreshold { get; init; } = 0.7;

    // ========================================
    // Human-in-the-Loop Configuration
    // ========================================

    /// <summary>
    /// Enable human-in-the-loop oversight
    /// </summary>
    public bool EnableHumanInTheLoop { get; init; } = false;

    /// <summary>
    /// Intervention points requiring human approval
    /// </summary>
    public IReadOnlyList<HumanInterventionPoint> RequiredApprovalPoints { get; init; } = [];

    /// <summary>
    /// Auto-approve if human response timeout
    /// </summary>
    public bool AutoApproveOnTimeout { get; init; } = true;

    /// <summary>
    /// Request feedback after task completion
    /// </summary>
    public bool RequestFeedbackOnComplete { get; init; } = false;

    /// <summary>
    /// Confidence threshold below which human review is requested
    /// </summary>
    public double HumanReviewConfidenceThreshold { get; init; } = 0.5;

    // ========================================
    // Context and Reflection Configuration
    // ========================================

    /// <summary>
    /// Enable execution context tracking (state passing between iterations)
    /// </summary>
    public bool EnableContextTracking { get; init; } = true;

    /// <summary>
    /// Enable reflection mode in oracle (Reflexion pattern)
    /// </summary>
    public bool EnableReflection { get; init; } = true;

    /// <summary>
    /// Maximum number of learnings to keep in context
    /// </summary>
    public int MaxContextLearnings { get; init; } = 10;

    /// <summary>
    /// Maximum number of previous outputs to keep in context
    /// </summary>
    public int MaxContextOutputs { get; init; } = 5;

    // ========================================
    // Auto-Continue Configuration (Lesson from TwentyQuestions)
    // ========================================

    /// <summary>
    /// Automatically enqueue next iteration when oracle returns CanContinue=true.
    /// Eliminates need for manual event handling to continue iterative workflows.
    /// </summary>
    public bool AutoContinueOnOracle { get; init; } = false;

    /// <summary>
    /// Template for generating the next iteration prompt.
    /// Supports placeholders: {iteration}, {previous_output}, {oracle_analysis}
    /// </summary>
    public string AutoContinuePromptTemplate { get; init; } = "Continue with iteration {iteration}";

    // ========================================
    // Retry and Resilience Configuration
    // ========================================

    /// <summary>
    /// Number of retry attempts when executor returns empty or failed result
    /// </summary>
    public int RetryOnFailureCount { get; init; } = 0;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>
    /// Enable fallback strategy when all retries fail
    /// </summary>
    public bool EnableFallbackStrategy { get; init; } = false;

    // ========================================
    // Final Iteration Strategy Configuration
    // ========================================

    /// <summary>
    /// Enable final iteration strategy for enforcing completion on last iteration.
    /// Use this to ensure the task produces a complete result when max iterations is reached.
    /// Example: In 20 Questions, force a guess on question 20.
    /// </summary>
    public bool EnableFinalIterationStrategy { get; init; } = false;
}

/// <summary>
/// Completion mode for autonomous execution
/// </summary>
public enum CompletionMode
{
    /// <summary>
    /// Process until queue is empty
    /// </summary>
    UntilQueueEmpty = 0,

    /// <summary>
    /// Process single goal then stop
    /// </summary>
    SingleGoal = 1,

    /// <summary>
    /// Process until goal is achieved (oracle verified)
    /// </summary>
    UntilGoalAchieved = 2
}
