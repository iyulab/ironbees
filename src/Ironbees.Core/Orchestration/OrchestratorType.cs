// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Orchestration;

/// <summary>
/// Defines the type of orchestrator pattern for multi-agent coordination.
/// </summary>
public enum OrchestratorType
{
    /// <summary>
    /// Agents execute in sequence, one after another.
    /// </summary>
    Sequential,

    /// <summary>
    /// Agents execute in parallel, results are aggregated.
    /// </summary>
    Parallel,

    /// <summary>
    /// Hub agent coordinates spoke agents in a star topology.
    /// </summary>
    HubSpoke,

    /// <summary>
    /// Agents can hand off control to other agents dynamically.
    /// </summary>
    Handoff,

    /// <summary>
    /// Multiple agents participate in a group chat conversation.
    /// </summary>
    GroupChat,

    /// <summary>
    /// Custom graph-based execution flow with arbitrary transitions.
    /// </summary>
    Graph
}
