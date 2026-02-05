// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Orchestration;

/// <summary>
/// Configuration settings for DAG-based graph orchestration.
/// Allows declarative definition of directed acyclic graph workflows.
/// </summary>
public sealed record GraphSettings
{
    /// <summary>
    /// The nodes in the graph, each representing an agent execution point.
    /// </summary>
    public required IReadOnlyList<GraphNodeDefinition> Nodes { get; init; }

    /// <summary>
    /// The edges connecting nodes, defining the flow of execution.
    /// </summary>
    public required IReadOnlyList<GraphEdgeDefinition> Edges { get; init; }

    /// <summary>
    /// The ID of the node where execution begins.
    /// </summary>
    public required string StartNode { get; init; }

    /// <summary>
    /// The ID of the node whose output becomes the final result.
    /// </summary>
    public required string OutputNode { get; init; }
}

/// <summary>
/// Defines a node in the orchestration graph.
/// </summary>
public sealed record GraphNodeDefinition
{
    /// <summary>
    /// Unique identifier for this node within the graph.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The name of the agent to execute at this node.
    /// Must match an agent name in the orchestrator's agent list.
    /// </summary>
    public required string Agent { get; init; }
}

/// <summary>
/// Defines an edge connecting two nodes in the orchestration graph.
/// </summary>
public sealed record GraphEdgeDefinition
{
    /// <summary>
    /// The source node ID where execution flows from.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// The target node ID where execution flows to.
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Optional condition that determines whether this edge is taken.
    /// If specified, the edge is only followed when the condition is satisfied.
    /// Supports simple keyword matching against the previous agent's output.
    /// </summary>
    /// <remarks>
    /// For example, setting Condition to "approved" means this edge is only
    /// followed if the source node's output contains the word "approved".
    /// </remarks>
    public string? Condition { get; init; }
}
