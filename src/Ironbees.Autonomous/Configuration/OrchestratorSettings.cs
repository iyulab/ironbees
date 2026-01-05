using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;

namespace Ironbees.Autonomous.Configuration;

/// <summary>
/// Complete orchestrator settings that can be loaded from YAML.
/// Combines LLM settings with orchestration configuration.
/// </summary>
public record OrchestratorSettings
{
    /// <summary>
    /// LLM connection and generation settings
    /// </summary>
    public LlmSettings Llm { get; init; } = new();

    /// <summary>
    /// Orchestration behavior settings
    /// </summary>
    public OrchestrationSettings Orchestration { get; init; } = new();

    /// <summary>
    /// Debug and logging settings
    /// </summary>
    public DebugSettings Debug { get; init; } = new();

    /// <summary>
    /// Converts to AutonomousConfig for runtime use
    /// </summary>
    public AutonomousConfig ToAutonomousConfig()
    {
        return new AutonomousConfig
        {
            MaxIterations = Orchestration.MaxIterations,
            EnableOracle = Orchestration.Oracle.Enabled,
            MaxOracleIterations = Orchestration.Oracle.MaxIterations,
            CompletionMode = Orchestration.CompletionMode,
            EnableCheckpointing = Orchestration.EnableCheckpointing,
            ContinueOnFailure = Orchestration.ContinueOnFailure,
            MinConfidenceThreshold = Orchestration.Confidence.MinThreshold,
            HumanReviewConfidenceThreshold = Orchestration.Confidence.HumanReviewThreshold,
            EnableHumanInTheLoop = Orchestration.HumanInTheLoop.Enabled,
            AutoApproveOnTimeout = Orchestration.HumanInTheLoop.AutoApproveOnTimeout,
            RequestFeedbackOnComplete = Orchestration.HumanInTheLoop.RequestFeedbackOnComplete,
            RequiredApprovalPoints = Orchestration.HumanInTheLoop.RequiredApprovalPoints,
            EnableContextTracking = Orchestration.Context.EnableTracking,
            EnableReflection = Orchestration.Context.EnableReflection,
            MaxContextLearnings = Orchestration.Context.MaxLearnings,
            MaxContextOutputs = Orchestration.Context.MaxOutputs,
            AutoContinueOnOracle = Orchestration.AutoContinue.Enabled,
            AutoContinuePromptTemplate = Orchestration.AutoContinue.PromptTemplate,
            RetryOnFailureCount = Orchestration.Retry.Count,
            RetryDelayMs = Orchestration.Retry.DelayMs,
            EnableFallbackStrategy = Orchestration.Retry.EnableFallback
        };
    }
}

/// <summary>
/// Orchestration behavior settings
/// </summary>
public record OrchestrationSettings
{
    /// <summary>
    /// Maximum iterations per task
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// Completion mode: UntilQueueEmpty, SingleGoal, UntilGoalAchieved
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
    /// Oracle verification settings
    /// </summary>
    public OracleSettings Oracle { get; init; } = new();

    /// <summary>
    /// Confidence threshold settings
    /// </summary>
    public ConfidenceThresholdSettings Confidence { get; init; } = new();

    /// <summary>
    /// Human-in-the-loop settings
    /// </summary>
    public HitlSettings HumanInTheLoop { get; init; } = new();

    /// <summary>
    /// Context tracking settings
    /// </summary>
    public ContextSettings Context { get; init; } = new();

    /// <summary>
    /// Auto-continue settings
    /// </summary>
    public AutoContinueSettings AutoContinue { get; init; } = new();

    /// <summary>
    /// Retry and resilience settings
    /// </summary>
    public RetrySettings Retry { get; init; } = new();
}

/// <summary>
/// Oracle verification settings
/// </summary>
public record OracleSettings
{
    /// <summary>
    /// Enable oracle verification
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum oracle iterations per task
    /// </summary>
    public int MaxIterations { get; init; } = 5;
}

/// <summary>
/// Confidence threshold settings
/// </summary>
public record ConfidenceThresholdSettings
{
    /// <summary>
    /// Minimum confidence to consider goal complete
    /// </summary>
    public double MinThreshold { get; init; } = 0.7;

    /// <summary>
    /// Threshold below which human review is requested
    /// </summary>
    public double HumanReviewThreshold { get; init; } = 0.5;
}

/// <summary>
/// Human-in-the-loop settings
/// </summary>
public record HitlSettings
{
    /// <summary>
    /// Enable human-in-the-loop oversight
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Auto-approve if human response timeout
    /// </summary>
    public bool AutoApproveOnTimeout { get; init; } = true;

    /// <summary>
    /// Request feedback after task completion
    /// </summary>
    public bool RequestFeedbackOnComplete { get; init; } = false;

    /// <summary>
    /// Required approval points (BeforeExecution, AfterExecution, OnLowConfidence, BeforeRetry, BeforeFallback)
    /// </summary>
    public IReadOnlyList<HumanInterventionPoint> RequiredApprovalPoints { get; init; } = [];
}

/// <summary>
/// Context tracking settings
/// </summary>
public record ContextSettings
{
    /// <summary>
    /// Enable execution context tracking
    /// </summary>
    public bool EnableTracking { get; init; } = true;

    /// <summary>
    /// Enable reflection mode in oracle
    /// </summary>
    public bool EnableReflection { get; init; } = true;

    /// <summary>
    /// Maximum learnings to keep in context
    /// </summary>
    public int MaxLearnings { get; init; } = 10;

    /// <summary>
    /// Maximum previous outputs to keep
    /// </summary>
    public int MaxOutputs { get; init; } = 5;
}

/// <summary>
/// Auto-continue settings
/// </summary>
public record AutoContinueSettings
{
    /// <summary>
    /// Enable auto-continue when oracle returns CanContinue=true
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Template for next iteration prompt.
    /// Placeholders: {iteration}, {previous_output}, {oracle_analysis}
    /// </summary>
    public string PromptTemplate { get; init; } = "Continue with iteration {iteration}";
}

/// <summary>
/// Retry and resilience settings
/// </summary>
public record RetrySettings
{
    /// <summary>
    /// Number of retry attempts on failure
    /// </summary>
    public int Count { get; init; } = 0;

    /// <summary>
    /// Delay between retries in milliseconds
    /// </summary>
    public int DelayMs { get; init; } = 1000;

    /// <summary>
    /// Enable fallback strategy when all retries fail
    /// </summary>
    public bool EnableFallback { get; init; } = false;
}

/// <summary>
/// Debug and logging settings
/// </summary>
public record DebugSettings
{
    /// <summary>
    /// Enable verbose debug output
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Show full LLM responses
    /// </summary>
    public bool ShowLlmResponses { get; init; } = false;

    /// <summary>
    /// Show token usage
    /// </summary>
    public bool ShowTokenUsage { get; init; } = false;

    /// <summary>
    /// Show reasoning/thinking content if available
    /// </summary>
    public bool ShowReasoning { get; init; } = false;
}
