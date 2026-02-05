// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Ironhive.Tools;

/// <summary>
/// YAML representation of a tool definition.
/// Used for loading tool configurations from agents/{name}/tools/ directory.
/// </summary>
public sealed record ToolDefinitionYaml
{
    /// <summary>
    /// Reference to a built-in or MCP tool (e.g., "built-in/file-reader", "mcp/filesystem").
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Custom tool name for inline definitions.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Handler reference for custom tools (e.g., "dotnet://MyProject.Tools.CustomTool").
    /// </summary>
    public string? Handler { get; init; }

    /// <summary>
    /// JSON Schema for tool parameters.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; init; }

    /// <summary>
    /// Whether this tool requires user approval before execution.
    /// </summary>
    public bool RequiresApproval { get; init; }
}

/// <summary>
/// YAML representation of agent tools configuration.
/// </summary>
public sealed record AgentToolsYaml
{
    /// <summary>
    /// List of tool definitions for this agent.
    /// </summary>
    public List<ToolDefinitionYaml> Tools { get; init; } = [];
}
