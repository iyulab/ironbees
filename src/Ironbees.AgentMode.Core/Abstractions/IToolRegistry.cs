namespace Ironbees.AgentMode.MCP;

/// <summary>
/// Registry for managing MCP servers and providing tools to agents.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers an MCP server with the registry.
    /// </summary>
    /// <param name="server">MCP server instance</param>
    void RegisterServer(IMcpServer server);

    /// <summary>
    /// Gets all tools available for a specific agent.
    /// </summary>
    /// <param name="agentName">Name of the agent</param>
    /// <returns>List of tools the agent can use</returns>
    IReadOnlyList<ToolDefinition> GetToolsForAgent(string agentName);

    /// <summary>
    /// Executes a tool from any registered server.
    /// </summary>
    /// <param name="toolName">Name of the tool</param>
    /// <param name="arguments">Tool arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    Task<ToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default);
}
