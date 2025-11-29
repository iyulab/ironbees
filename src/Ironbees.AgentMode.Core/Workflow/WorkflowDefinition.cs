using System.Collections.Immutable;

namespace Ironbees.AgentMode.Core.Workflow;

/// <summary>
/// Represents a complete workflow definition loaded from YAML.
/// This is the root model for declarative workflow configuration.
/// </summary>
public sealed record WorkflowDefinition
{
    /// <summary>
    /// Unique name identifier for the workflow.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Version string for workflow versioning (semver recommended).
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Human-readable description of the workflow purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// List of agent references used in this workflow.
    /// References point to agents in the filesystem convention (e.g., "agents/planner").
    /// </summary>
    public ImmutableList<AgentReference> Agents { get; init; } = [];

    /// <summary>
    /// Ordered list of workflow states defining the execution graph.
    /// </summary>
    public ImmutableList<WorkflowStateDefinition> States { get; init; } = [];

    /// <summary>
    /// Global workflow settings and defaults.
    /// </summary>
    public WorkflowSettings Settings { get; init; } = new();
}

/// <summary>
/// Reference to an agent defined in the filesystem.
/// </summary>
public sealed record AgentReference
{
    /// <summary>
    /// Relative path to agent directory (e.g., "agents/planner").
    /// </summary>
    public required string Ref { get; init; }

    /// <summary>
    /// Optional alias for referencing in states.
    /// If not provided, the directory name is used.
    /// </summary>
    public string? Alias { get; init; }
}

/// <summary>
/// Defines a single state/node in the workflow graph.
/// </summary>
public sealed record WorkflowStateDefinition
{
    /// <summary>
    /// Unique identifier for this state within the workflow.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Type of state node.
    /// </summary>
    public WorkflowStateType Type { get; init; } = WorkflowStateType.Agent;

    /// <summary>
    /// Agent executor reference (required for Agent type states).
    /// Must match an agent alias or directory name from Agents list.
    /// </summary>
    public string? Executor { get; init; }

    /// <summary>
    /// List of agent executors for parallel execution (for Parallel type).
    /// </summary>
    public ImmutableList<string> Executors { get; init; } = [];

    /// <summary>
    /// Optional trigger condition for state activation.
    /// </summary>
    public TriggerDefinition? Trigger { get; init; }

    /// <summary>
    /// Simple next state for unconditional transitions.
    /// </summary>
    public string? Next { get; init; }

    /// <summary>
    /// Conditional transitions based on state evaluation.
    /// </summary>
    public ImmutableList<ConditionalTransition> Conditions { get; init; } = [];

    /// <summary>
    /// Human-in-the-loop settings (for HumanGate type).
    /// </summary>
    public HumanGateSettings? HumanGate { get; init; }

    /// <summary>
    /// Maximum iterations for retry loops.
    /// </summary>
    public int? MaxIterations { get; init; }

    /// <summary>
    /// Timeout duration for this state.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// Types of workflow state nodes.
/// </summary>
public enum WorkflowStateType
{
    /// <summary>Workflow entry point.</summary>
    Start,

    /// <summary>Agent execution node.</summary>
    Agent,

    /// <summary>Parallel agent execution.</summary>
    Parallel,

    /// <summary>Human approval/input gate.</summary>
    HumanGate,

    /// <summary>Error handling/escalation node.</summary>
    Escalation,

    /// <summary>Workflow terminal node.</summary>
    Terminal
}

/// <summary>
/// Defines a trigger condition for state activation.
/// </summary>
public sealed record TriggerDefinition
{
    /// <summary>
    /// Type of trigger condition.
    /// </summary>
    public required TriggerType Type { get; init; }

    /// <summary>
    /// Path for file-based triggers.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Custom expression for complex triggers.
    /// </summary>
    public string? Expression { get; init; }
}

/// <summary>
/// Types of trigger conditions.
/// </summary>
public enum TriggerType
{
    /// <summary>Activates when a file exists at the specified path.</summary>
    FileExists,

    /// <summary>Activates when a directory is not empty.</summary>
    DirectoryNotEmpty,

    /// <summary>Activates immediately (no condition).</summary>
    Immediate,

    /// <summary>Custom expression-based trigger.</summary>
    Expression
}

/// <summary>
/// Conditional transition based on state evaluation.
/// </summary>
public sealed record ConditionalTransition
{
    /// <summary>
    /// Condition expression to evaluate.
    /// Supports: "success", "failure", "build.success", "test.success", etc.
    /// </summary>
    public string? If { get; init; }

    /// <summary>
    /// Target state if condition is true.
    /// </summary>
    public required string Then { get; init; }

    /// <summary>
    /// If true, this is the default/else branch.
    /// </summary>
    public bool IsDefault { get; init; }
}

/// <summary>
/// Settings for human-in-the-loop approval gates.
/// </summary>
public sealed record HumanGateSettings
{
    /// <summary>
    /// Approval mode: "always_require", "on_sensitive", "never".
    /// </summary>
    public string ApprovalMode { get; init; } = "always_require";

    /// <summary>
    /// Timeout for approval response.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Target state on approval.
    /// </summary>
    public string? OnApprove { get; init; }

    /// <summary>
    /// Target state on rejection.
    /// </summary>
    public string? OnReject { get; init; }

    /// <summary>
    /// Notification settings for approval requests.
    /// </summary>
    public string? NotifyEmail { get; init; }
}

/// <summary>
/// Global workflow settings.
/// </summary>
public sealed record WorkflowSettings
{
    /// <summary>
    /// Default timeout for all states.
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Default maximum iterations for loops.
    /// </summary>
    public int DefaultMaxIterations { get; init; } = 5;

    /// <summary>
    /// Enable checkpointing for state persistence.
    /// </summary>
    public bool EnableCheckpointing { get; init; } = true;

    /// <summary>
    /// Directory for checkpoint storage.
    /// </summary>
    public string CheckpointDirectory { get; init; } = ".ironbees/checkpoints";
}
