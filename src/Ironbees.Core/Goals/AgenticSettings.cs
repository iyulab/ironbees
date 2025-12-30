// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Goals;

/// <summary>
/// Settings for agentic patterns in goal-directed workflows.
/// Defines HITL (Human-in-the-Loop), sampling, and confidence-based behaviors.
/// </summary>
/// <remarks>
/// <para>
/// AgenticSettings enables declarative definition of iterative agent patterns:
/// </para>
/// <list type="bullet">
/// <item><description>Progressive sampling strategies for large datasets</description></item>
/// <item><description>Confidence-based early termination</description></item>
/// <item><description>Human-in-the-Loop checkpoints for critical decisions</description></item>
/// </list>
/// <para>
/// Note: Ironbees only defines the schema. Actual execution is delegated to MAF.
/// </para>
/// </remarks>
public record AgenticSettings
{
    /// <summary>
    /// Sampling strategy configuration for progressive data processing.
    /// </summary>
    public SamplingSettings? Sampling { get; init; }

    /// <summary>
    /// Confidence-based termination settings.
    /// </summary>
    public ConfidenceSettings? Confidence { get; init; }

    /// <summary>
    /// Human-in-the-Loop policy and checkpoint configuration.
    /// </summary>
    public HitlSettings? Hitl { get; init; }
}

/// <summary>
/// Configuration for progressive sampling strategies.
/// </summary>
public record SamplingSettings
{
    /// <summary>
    /// The sampling strategy to use.
    /// </summary>
    public SamplingStrategy Strategy { get; init; } = SamplingStrategy.Progressive;

    /// <summary>
    /// Initial batch size for the first sampling iteration.
    /// </summary>
    public int InitialBatchSize { get; init; } = 100;

    /// <summary>
    /// Growth factor for progressive sampling (batch size multiplier per iteration).
    /// </summary>
    public double GrowthFactor { get; init; } = 5.0;

    /// <summary>
    /// Maximum number of samples to process before requiring user confirmation.
    /// </summary>
    public int? MaxSamples { get; init; }

    /// <summary>
    /// Minimum samples required before confidence evaluation begins.
    /// </summary>
    public int MinSamplesForConfidence { get; init; } = 50;
}

/// <summary>
/// Available sampling strategies for progressive data processing.
/// </summary>
public enum SamplingStrategy
{
    /// <summary>
    /// Progressive sampling with increasing batch sizes.
    /// </summary>
    Progressive,

    /// <summary>
    /// Random sampling from the dataset.
    /// </summary>
    Random,

    /// <summary>
    /// Stratified sampling to ensure representative distribution.
    /// </summary>
    Stratified,

    /// <summary>
    /// Sequential sampling in order.
    /// </summary>
    Sequential
}

/// <summary>
/// Configuration for confidence-based workflow termination.
/// </summary>
public record ConfidenceSettings
{
    /// <summary>
    /// Confidence threshold required for workflow completion (0.0 - 1.0).
    /// </summary>
    public double Threshold { get; init; } = 0.95;

    /// <summary>
    /// Number of consecutive iterations without new patterns required
    /// to consider the rules stable.
    /// </summary>
    public int StabilityWindow { get; init; } = 3;

    /// <summary>
    /// Minimum confidence level before HITL is triggered.
    /// </summary>
    public double? MinConfidenceForHitl { get; init; }

    /// <summary>
    /// Whether to track confidence history across iterations.
    /// </summary>
    public bool TrackHistory { get; init; } = true;
}

/// <summary>
/// Configuration for Human-in-the-Loop (HITL) checkpoints.
/// </summary>
public record HitlSettings
{
    /// <summary>
    /// The policy determining when HITL is triggered.
    /// </summary>
    public HitlPolicy Policy { get; init; } = HitlPolicy.OnUncertainty;

    /// <summary>
    /// Uncertainty threshold that triggers HITL when policy is OnUncertainty.
    /// </summary>
    public double UncertaintyThreshold { get; init; } = 0.7;

    /// <summary>
    /// Named checkpoints where HITL should be triggered.
    /// </summary>
    public List<string> Checkpoints { get; init; } = [];

    /// <summary>
    /// Timeout for waiting for human response.
    /// </summary>
    public TimeSpan? ResponseTimeout { get; init; }

    /// <summary>
    /// Action to take when HITL times out.
    /// </summary>
    public HitlTimeoutAction TimeoutAction { get; init; } = HitlTimeoutAction.Pause;
}

/// <summary>
/// Policy determining when Human-in-the-Loop intervention is triggered.
/// </summary>
public enum HitlPolicy
{
    /// <summary>
    /// Always request human approval at checkpoints.
    /// </summary>
    Always,

    /// <summary>
    /// Request human input only when uncertainty exceeds threshold.
    /// </summary>
    OnUncertainty,

    /// <summary>
    /// Request human input when specific thresholds are reached.
    /// </summary>
    OnThreshold,

    /// <summary>
    /// Request human input only on exceptions or errors.
    /// </summary>
    OnException,

    /// <summary>
    /// Never request human input (fully autonomous).
    /// </summary>
    Never
}

/// <summary>
/// Action to take when HITL response times out.
/// </summary>
public enum HitlTimeoutAction
{
    /// <summary>
    /// Pause execution and wait for response.
    /// </summary>
    Pause,

    /// <summary>
    /// Continue with default action.
    /// </summary>
    ContinueWithDefault,

    /// <summary>
    /// Cancel the workflow.
    /// </summary>
    Cancel,

    /// <summary>
    /// Skip this HITL checkpoint and continue.
    /// </summary>
    Skip
}
