// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using IronHive.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.Ironhive.Tools;

/// <summary>
/// Registry for managing IronHive tools loaded from filesystem configuration.
/// Supports loading tools from agents/{name}/tools/ directories.
/// </summary>
public partial class IronhiveToolRegistry
{
    private readonly Dictionary<string, ToolItem> _builtInTools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ToolItem>> _agentTools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<IronhiveToolRegistry> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public IronhiveToolRegistry(ILogger<IronhiveToolRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Registers a built-in tool that can be referenced by agents.
    /// </summary>
    /// <param name="name">The tool name (e.g., "file-reader").</param>
    /// <param name="tool">The tool implementation.</param>
    public void RegisterBuiltInTool(string name, ToolItem tool)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(tool);

        _builtInTools[$"built-in/{name}"] = tool;
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogRegisteredBuiltInTool(_logger, name);
        }
    }

    /// <summary>
    /// Loads tools for an agent from the agents/{agentName}/tools/ directory.
    /// </summary>
    /// <param name="agentsDirectory">The base agents directory.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of loaded tools.</returns>
    public async Task<IReadOnlyList<ToolItem>> LoadAgentToolsAsync(
        string agentsDirectory,
        string agentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentsDirectory);
        ArgumentNullException.ThrowIfNull(agentName);

        // Check if already loaded
        if (_agentTools.TryGetValue(agentName, out var cachedTools))
        {
            return cachedTools;
        }

        var toolsDirectory = Path.Combine(agentsDirectory, agentName, "tools");
        var tools = new List<ToolItem>();

        // Try loading from tools.yaml in agent directory
        var toolsYamlPath = Path.Combine(agentsDirectory, agentName, "tools.yaml");
        if (File.Exists(toolsYamlPath))
        {
            var loadedTools = await LoadToolsFromYamlAsync(toolsYamlPath, cancellationToken);
            tools.AddRange(loadedTools);
        }

        // Also load individual tool files from tools/ directory
        if (Directory.Exists(toolsDirectory))
        {
            var toolFiles = Directory.GetFiles(toolsDirectory, "*.yaml");
            foreach (var toolFile in toolFiles)
            {
                var loadedTools = await LoadToolsFromYamlAsync(toolFile, cancellationToken);
                tools.AddRange(loadedTools);
            }
        }

        _agentTools[agentName] = tools;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogLoadedTools(_logger, tools.Count, agentName);
        }

        return tools;
    }

    /// <summary>
    /// Gets tools for an agent, including resolved references.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <returns>List of tools for the agent.</returns>
    public IReadOnlyList<ToolItem> GetAgentTools(string agentName)
    {
        ArgumentNullException.ThrowIfNull(agentName);

        return _agentTools.TryGetValue(agentName, out var tools)
            ? tools
            : Array.Empty<ToolItem>();
    }

    /// <summary>
    /// Resolves a tool reference to a ToolItem.
    /// </summary>
    /// <param name="reference">The tool reference (e.g., "built-in/file-reader").</param>
    /// <returns>The resolved tool, or null if not found.</returns>
    public ToolItem? ResolveToolReference(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (_builtInTools.TryGetValue(reference, out var tool))
        {
            return tool;
        }

        LogToolReferenceNotFound(_logger, reference);
        return null;
    }

    private async Task<List<ToolItem>> LoadToolsFromYamlAsync(
        string yamlPath,
        CancellationToken cancellationToken)
    {
        var tools = new List<ToolItem>();

        try
        {
            var yamlContent = await File.ReadAllTextAsync(yamlPath, cancellationToken);
            var toolsConfig = _yamlDeserializer.Deserialize<AgentToolsYaml>(yamlContent);

            if (toolsConfig?.Tools is null)
            {
                return tools;
            }

            foreach (var toolDef in toolsConfig.Tools)
            {
                var tool = CreateToolFromDefinition(toolDef);
                if (tool is not null)
                {
                    tools.Add(tool);
                }
            }
        }
        catch (Exception ex)
        {
            LogFailedToLoadTools(_logger, ex, yamlPath);
        }

        return tools;
    }

    private ToolItem? CreateToolFromDefinition(ToolDefinitionYaml definition)
    {
        // Handle reference to built-in or MCP tool
        if (!string.IsNullOrEmpty(definition.Ref))
        {
            var resolved = ResolveToolReference(definition.Ref);
            if (resolved is not null)
            {
                return resolved;
            }

            // For MCP tools, create a placeholder that will be resolved at runtime
            if (definition.Ref.StartsWith("mcp/", StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    LogMcpToolReferenceWillResolve(_logger, definition.Ref);
                }
                return CreateMcpToolPlaceholder(definition.Ref);
            }

            return null;
        }

        // Handle inline tool definition
        if (!string.IsNullOrEmpty(definition.Name))
        {
            return CreateInlineTool(definition);
        }

        LogToolDefinitionInvalid(_logger);
        return null;
    }

    private static ToolItem CreateMcpToolPlaceholder(string reference)
    {
        var mcpName = reference["mcp/".Length..];
        return new ToolItem
        {
            Name = mcpName,
            Description = $"MCP Tool: {mcpName} (resolved at runtime)",
            IsMcpTool = true,
            McpServerName = mcpName
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registered built-in tool: {ToolName}")]
    private static partial void LogRegisteredBuiltInTool(ILogger logger, string toolName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {ToolCount} tools for agent {AgentName}")]
    private static partial void LogLoadedTools(ILogger logger, int toolCount, string agentName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool reference not found: {Reference}")]
    private static partial void LogToolReferenceNotFound(ILogger logger, string reference);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load tools from {YamlPath}")]
    private static partial void LogFailedToLoadTools(ILogger logger, Exception exception, string yamlPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MCP tool reference will be resolved at runtime: {Ref}")]
    private static partial void LogMcpToolReferenceWillResolve(ILogger logger, string @ref);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool definition has neither ref nor name")]
    private static partial void LogToolDefinitionInvalid(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating inline tool: {ToolName}")]
    private static partial void LogCreatingInlineTool(ILogger logger, string? toolName);

    private ToolItem CreateInlineTool(ToolDefinitionYaml definition)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogCreatingInlineTool(_logger, definition.Name);
        }

        return new ToolItem
        {
            Name = definition.Name!,
            Description = definition.Description ?? "",
            RequiresApproval = definition.RequiresApproval,
            HandlerReference = definition.Handler
        };
    }
}

/// <summary>
/// Represents a tool that can be used by IronHive agents.
/// </summary>
public sealed record ToolItem
{
    /// <summary>
    /// The tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of the tool.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Whether this tool requires user approval before execution.
    /// </summary>
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// Whether this is an MCP tool.
    /// </summary>
    public bool IsMcpTool { get; init; }

    /// <summary>
    /// MCP server name for MCP tools.
    /// </summary>
    public string? McpServerName { get; init; }

    /// <summary>
    /// Handler reference for custom tools (e.g., "dotnet://...").
    /// </summary>
    public string? HandlerReference { get; init; }
}
