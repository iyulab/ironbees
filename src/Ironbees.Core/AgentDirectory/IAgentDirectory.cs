namespace Ironbees.Core.AgentDirectory;

/// <summary>
/// Represents standard subdirectories within an agent's directory structure.
/// Following the DAI (Directory-based Agent Interface) pattern from research-04.
/// </summary>
public enum AgentSubdirectory
{
    /// <summary>
    /// External request reception directory (Public Write).
    /// Other agents and external systems can write requests here.
    /// </summary>
    Inbox,

    /// <summary>
    /// Results storage directory (Owner Write).
    /// Agent writes its outputs and results here.
    /// </summary>
    Outbox,

    /// <summary>
    /// Long-term memory storage directory (Owner R/W).
    /// Persists agent's knowledge and context across sessions.
    /// </summary>
    Memory,

    /// <summary>
    /// Temporary workspace directory (Owner Full).
    /// Scratch space for intermediate work products.
    /// </summary>
    Workspace,

    /// <summary>
    /// Execution history logs directory (Append Only).
    /// Records all agent actions and decisions for observability.
    /// </summary>
    Logs
}

/// <summary>
/// Interface for managing agent directory structure.
/// Implements the DAI (Directory-based Agent Interface) pattern for stigmergic agent collaboration.
/// </summary>
/// <remarks>
/// Directory structure:
/// agents/{agent-name}/
/// ├── agent.yaml           (config - Read-Only at Runtime)
/// ├── system-prompt.md     (system prompt - Read-Only at Runtime)
/// ├── inbox/               (external requests - Public Write)
/// ├── outbox/              (results - Owner Write)
/// ├── memory/              (long-term storage - Owner R/W)
/// ├── workspace/           (temp work - Owner Full)
/// └── logs/                (history - Append Only)
/// </remarks>
public interface IAgentDirectory
{
    /// <summary>
    /// Gets the name of the agent.
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Gets the root path of the agent's directory.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Gets the path to a specific subdirectory.
    /// </summary>
    /// <param name="subdirectory">The subdirectory to get the path for.</param>
    /// <returns>The full path to the subdirectory.</returns>
    string GetSubdirectoryPath(AgentSubdirectory subdirectory);

    /// <summary>
    /// Ensures all required subdirectories exist.
    /// Creates missing directories if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all directories exist or were created successfully.</returns>
    Task<bool> EnsureDirectoryStructureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a file to the specified subdirectory.
    /// </summary>
    /// <param name="subdirectory">The target subdirectory.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes binary content to the specified subdirectory.
    /// </summary>
    /// <param name="subdirectory">The target subdirectory.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="content">The binary content to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a file from the specified subdirectory.
    /// </summary>
    /// <param name="subdirectory">The source subdirectory.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content, or null if the file doesn't exist.</returns>
    Task<string?> ReadFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads binary content from the specified subdirectory.
    /// </summary>
    /// <param name="subdirectory">The source subdirectory.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The binary content, or null if the file doesn't exist.</returns>
    Task<byte[]?> ReadFileBytesAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all files in the specified subdirectory.
    /// </summary>
    /// <param name="subdirectory">The subdirectory to list.</param>
    /// <param name="searchPattern">Optional search pattern (default: "*").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file names in the subdirectory.</returns>
    Task<IReadOnlyList<string>> ListFilesAsync(
        AgentSubdirectory subdirectory,
        string searchPattern = "*",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from the specified subdirectory.
    /// </summary>
    /// <param name="subdirectory">The target subdirectory.</param>
    /// <param name="fileName">The name of the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists in the specified subdirectory.
    /// </summary>
    /// <param name="subdirectory">The target subdirectory.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists.</returns>
    Task<bool> FileExistsAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends content to a log file. Creates the file if it doesn't exist.
    /// </summary>
    /// <param name="logFileName">The name of the log file.</param>
    /// <param name="content">The content to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendToLogAsync(
        string logFileName,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up the workspace directory, removing all temporary files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of files deleted.</returns>
    Task<int> CleanWorkspaceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the agent directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Directory information including file counts and sizes.</returns>
    Task<AgentDirectoryInfo> GetDirectoryInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about an agent's directory structure.
/// </summary>
public sealed record AgentDirectoryInfo
{
    /// <summary>
    /// Gets the agent name.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Gets the root path.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Gets the total size of all files in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// Gets file counts per subdirectory.
    /// </summary>
    public required IReadOnlyDictionary<AgentSubdirectory, int> FileCountBySubdirectory { get; init; }

    /// <summary>
    /// Gets size in bytes per subdirectory.
    /// </summary>
    public required IReadOnlyDictionary<AgentSubdirectory, long> SizeBySubdirectory { get; init; }

    /// <summary>
    /// Gets the timestamp when this info was collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
