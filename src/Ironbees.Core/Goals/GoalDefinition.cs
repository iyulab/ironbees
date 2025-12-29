// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Goals;

/// <summary>
/// Represents a Goal definition loaded from the filesystem.
/// Goals are high-level objectives that orchestrate agent workflows.
/// </summary>
public record GoalDefinition
{
    /// <summary>
    /// Unique identifier for the goal.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name of the goal.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what this goal accomplishes.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Version of the goal definition.
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// The workflow template to use for execution.
    /// </summary>
    public required string WorkflowTemplate { get; init; }

    /// <summary>
    /// Success criteria that must be met for the goal to be considered complete.
    /// </summary>
    public List<SuccessCriterion> SuccessCriteria { get; init; } = [];

    /// <summary>
    /// Constraints that limit goal execution.
    /// </summary>
    public GoalConstraints Constraints { get; init; } = new();

    /// <summary>
    /// Checkpoint settings for goal execution persistence.
    /// </summary>
    public CheckpointSettings Checkpoint { get; init; } = new();

    /// <summary>
    /// Parameters to pass to the workflow template.
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = [];

    /// <summary>
    /// Tags for categorization and filtering.
    /// </summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>
    /// The directory path where this goal was loaded from.
    /// </summary>
    public string? SourcePath { get; init; }
}

/// <summary>
/// Represents a criterion for determining goal success.
/// </summary>
public record SuccessCriterion
{
    /// <summary>
    /// Unique identifier for the criterion.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Description of what this criterion measures.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The type of evaluation to perform.
    /// </summary>
    public SuccessCriterionType Type { get; init; } = SuccessCriterionType.Manual;

    /// <summary>
    /// Condition expression for automatic evaluation.
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// Weight of this criterion (0.0 - 1.0) for weighted evaluation.
    /// </summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// Whether this criterion is required for goal success.
    /// </summary>
    public bool Required { get; init; } = true;
}

/// <summary>
/// Types of success criterion evaluation.
/// </summary>
public enum SuccessCriterionType
{
    /// <summary>
    /// Requires manual evaluation.
    /// </summary>
    Manual,

    /// <summary>
    /// Evaluated by an LLM.
    /// </summary>
    LlmEvaluation,

    /// <summary>
    /// Evaluated by a condition expression.
    /// </summary>
    Condition,

    /// <summary>
    /// Evaluated by an external tool.
    /// </summary>
    Tool
}

/// <summary>
/// Constraints that limit goal execution.
/// </summary>
public record GoalConstraints
{
    /// <summary>
    /// Maximum number of iterations allowed.
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// Maximum total tokens allowed across all iterations.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Maximum execution time.
    /// </summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>
    /// List of agents allowed to participate in this goal.
    /// Empty list means all agents are allowed.
    /// </summary>
    public List<string> AllowedAgents { get; init; } = [];

    /// <summary>
    /// List of tools allowed during goal execution.
    /// Empty list means all tools are allowed.
    /// </summary>
    public List<string> AllowedTools { get; init; } = [];
}

/// <summary>
/// Settings for goal execution checkpointing.
/// </summary>
public record CheckpointSettings
{
    /// <summary>
    /// Whether checkpointing is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Interval between automatic checkpoints.
    /// </summary>
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Whether to checkpoint after each iteration.
    /// </summary>
    public bool AfterEachIteration { get; init; } = true;

    /// <summary>
    /// Directory for storing checkpoints (relative to goal directory).
    /// </summary>
    public string CheckpointDirectory { get; init; } = "checkpoints";
}
