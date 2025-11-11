namespace Ironbees.AgentMode.MCP;

/// <summary>
/// Interface for MCP (Model Context Protocol) servers that provide tools to agents.
/// Implements the MCP protocol specification (JSON-RPC 2.0).
/// </summary>
public interface IMcpServer : IAsyncDisposable
{
    /// <summary>
    /// Unique name of the MCP server (e.g., "roslyn", "msbuild", "git").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Server version following semantic versioning (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// List of tools provided by this server.
    /// Each tool has a name, description, and input schema.
    /// </summary>
    IReadOnlyList<ToolDefinition> Tools { get; }

    /// <summary>
    /// Initializes the MCP server with configuration.
    /// Called once during application startup.
    /// </summary>
    /// <param name="configuration">Server-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when server is ready</returns>
    Task InitializeAsync(
        IReadOnlyDictionary<string, object> configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a tool with provided arguments.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute</param>
    /// <param name="arguments">Tool arguments as key-value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    /// <exception cref="ToolNotFoundException">When toolName doesn't exist</exception>
    /// <exception cref="ToolExecutionException">When tool execution fails</exception>
    Task<ToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default);
}
