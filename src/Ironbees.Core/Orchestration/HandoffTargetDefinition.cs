// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Orchestration;

/// <summary>
/// Defines a valid handoff target for an agent in handoff orchestration.
/// </summary>
public sealed record HandoffTargetDefinition
{
    /// <summary>
    /// The name of the target agent that can receive handoff.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Human-readable description of when to hand off to this agent.
    /// This helps the LLM decide when a handoff is appropriate.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional conditions or criteria for when this handoff should occur.
    /// </summary>
    public string? Condition { get; init; }
}
