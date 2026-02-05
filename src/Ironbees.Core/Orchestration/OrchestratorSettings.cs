// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Orchestration;

/// <summary>
/// Configuration settings for multi-agent orchestration.
/// </summary>
public sealed record OrchestratorSettings
{
    /// <summary>
    /// The type of orchestration pattern to use.
    /// </summary>
    public OrchestratorType Type { get; init; } = OrchestratorType.Sequential;

    /// <summary>
    /// Initial agent for handoff orchestration. Required when <see cref="Type"/> is <see cref="OrchestratorType.Handoff"/>.
    /// </summary>
    public string? InitialAgent { get; init; }

    /// <summary>
    /// Maximum number of agent-to-agent transitions allowed for handoff orchestration.
    /// </summary>
    public int MaxTransitions { get; init; } = 20;

    /// <summary>
    /// Maximum number of conversation rounds for group chat orchestration.
    /// </summary>
    public int MaxRounds { get; init; } = 10;

    /// <summary>
    /// Overall timeout for the orchestration execution.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Timeout for individual agent execution.
    /// </summary>
    public TimeSpan AgentTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Whether to stop orchestration when an agent fails.
    /// </summary>
    public bool StopOnAgentFailure { get; init; } = true;

    /// <summary>
    /// Speaker selection strategy for group chat orchestration.
    /// Supported values depend on the underlying framework.
    /// Common values: "round_robin", "random", "llm_based", "manual".
    /// </summary>
    public string? SpeakerSelectionStrategy { get; init; }

    /// <summary>
    /// Termination condition expression for group chat orchestration.
    /// </summary>
    public string? TerminationCondition { get; init; }

    /// <summary>
    /// Hub agent name for hub-spoke orchestration. Required when <see cref="Type"/> is <see cref="OrchestratorType.HubSpoke"/>.
    /// </summary>
    public string? HubAgent { get; init; }

    /// <summary>
    /// Whether to enable checkpointing during orchestration.
    /// </summary>
    public bool EnableCheckpointing { get; init; } = true;

    /// <summary>
    /// Whether to require approval before executing certain agents.
    /// </summary>
    public bool RequireApproval { get; init; }

    /// <summary>
    /// Graph settings for DAG-based orchestration.
    /// Required when <see cref="Type"/> is <see cref="OrchestratorType.Graph"/>.
    /// </summary>
    public GraphSettings? Graph { get; init; }

    /// <summary>
    /// Middleware settings for agent execution.
    /// Enables declarative configuration of retry, circuit breaker, rate limiting, etc.
    /// </summary>
    public MiddlewareSettings? Middleware { get; init; }
}
