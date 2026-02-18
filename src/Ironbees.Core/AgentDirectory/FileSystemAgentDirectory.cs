namespace Ironbees.Core.AgentDirectory;

/// <summary>
/// File system implementation of <see cref="IAgentDirectory"/>.
/// Manages agent directory structure following the DAI pattern.
/// </summary>
/// <remarks>
/// Thread-safe implementation using file system locks where necessary.
/// Follows the convention-over-configuration principle.
/// </remarks>
public sealed class FileSystemAgentDirectory : IAgentDirectory, IDisposable
{
    private static readonly Dictionary<AgentSubdirectory, string> SubdirectoryNames = new()
    {
        [AgentSubdirectory.Inbox] = "inbox",
        [AgentSubdirectory.Outbox] = "outbox",
        [AgentSubdirectory.Memory] = "memory",
        [AgentSubdirectory.Workspace] = "workspace",
        [AgentSubdirectory.Logs] = "logs"
    };

    private readonly SemaphoreSlim _logLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of <see cref="FileSystemAgentDirectory"/>.
    /// </summary>
    /// <param name="agentName">The name of the agent.</param>
    /// <param name="rootPath">The root path of the agent's directory.</param>
    /// <exception cref="ArgumentException">Thrown when agentName or rootPath is null or empty.</exception>
    public FileSystemAgentDirectory(string agentName, string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        AgentName = agentName;
        RootPath = Path.GetFullPath(rootPath);
    }

    /// <inheritdoc />
    public string AgentName { get; }

    /// <inheritdoc />
    public string RootPath { get; }

    /// <inheritdoc />
    public string GetSubdirectoryPath(AgentSubdirectory subdirectory)
    {
        return Path.Combine(RootPath, SubdirectoryNames[subdirectory]);
    }

    /// <inheritdoc />
    public Task<bool> EnsureDirectoryStructureAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure root directory exists
            if (!System.IO.Directory.Exists(RootPath))
            {
                System.IO.Directory.CreateDirectory(RootPath);
            }

            // Ensure all subdirectories exist
            foreach (var subdirectory in SubdirectoryNames.Values)
            {
                var path = Path.Combine(RootPath, subdirectory);
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }

                // Create .gitkeep file to preserve empty directories in git
                var gitKeepPath = Path.Combine(path, ".gitkeep");
                if (!File.Exists(gitKeepPath))
                {
                    File.WriteAllText(gitKeepPath, string.Empty);
                }
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ValidateFileName(fileName);

        var filePath = GetFilePath(subdirectory, fileName);
        EnsureSubdirectoryExists(subdirectory);

        await File.WriteAllTextAsync(filePath, content, cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ValidateFileName(fileName);

        var filePath = GetFilePath(subdirectory, fileName);
        EnsureSubdirectoryExists(subdirectory);

        await File.WriteAllBytesAsync(filePath, content, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> ReadFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var filePath = GetFilePath(subdirectory, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> ReadFileBytesAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var filePath = GetFilePath(subdirectory, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListFilesAsync(
        AgentSubdirectory subdirectory,
        string searchPattern = "*",
        CancellationToken cancellationToken = default)
    {
        var directoryPath = GetSubdirectoryPath(subdirectory);

        if (!System.IO.Directory.Exists(directoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var files = System.IO.Directory.GetFiles(directoryPath, searchPattern)
            .Select(Path.GetFileName)
            .Where(f => f != null && f != ".gitkeep")
            .Cast<string>()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    /// <inheritdoc />
    public Task<bool> DeleteFileAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var filePath = GetFilePath(subdirectory, fileName);

        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        File.Delete(filePath);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(
        AgentSubdirectory subdirectory,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var filePath = GetFilePath(subdirectory, fileName);
        return Task.FromResult(File.Exists(filePath));
    }

    /// <inheritdoc />
    public async Task AppendToLogAsync(
        string logFileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFileName);
        ValidateFileName(logFileName);

        var filePath = GetFilePath(AgentSubdirectory.Logs, logFileName);
        EnsureSubdirectoryExists(AgentSubdirectory.Logs);

        // Use lock for thread-safe append operations
        await _logLock.WaitAsync(cancellationToken);
        try
        {
            var timestampedContent = $"[{DateTimeOffset.UtcNow:O}] {content}{Environment.NewLine}";
            await File.AppendAllTextAsync(filePath, timestampedContent, cancellationToken);
        }
        finally
        {
            _logLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<int> CleanWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var workspacePath = GetSubdirectoryPath(AgentSubdirectory.Workspace);

        if (!System.IO.Directory.Exists(workspacePath))
        {
            return Task.FromResult(0);
        }

        var files = System.IO.Directory.GetFiles(workspacePath)
            .Where(f => !f.EndsWith(".gitkeep", StringComparison.Ordinal))
            .ToList();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(file);
        }

        // Also clean subdirectories in workspace
        var subdirs = System.IO.Directory.GetDirectories(workspacePath);
        foreach (var subdir in subdirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            System.IO.Directory.Delete(subdir, recursive: true);
        }

        return Task.FromResult(files.Count + subdirs.Length);
    }

    /// <inheritdoc />
    public Task<AgentDirectoryInfo> GetDirectoryInfoAsync(CancellationToken cancellationToken = default)
    {
        var fileCounts = new Dictionary<AgentSubdirectory, int>();
        var sizes = new Dictionary<AgentSubdirectory, long>();
        long totalSize = 0;

        foreach (var subdirectory in Enum.GetValues<AgentSubdirectory>())
        {
            var path = GetSubdirectoryPath(subdirectory);

            if (!System.IO.Directory.Exists(path))
            {
                fileCounts[subdirectory] = 0;
                sizes[subdirectory] = 0;
                continue;
            }

            var files = System.IO.Directory.GetFiles(path)
                .Where(f => !f.EndsWith(".gitkeep", StringComparison.Ordinal))
                .ToList();

            fileCounts[subdirectory] = files.Count;

            long subdirSize = 0;
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                subdirSize += fileInfo.Length;
            }

            sizes[subdirectory] = subdirSize;
            totalSize += subdirSize;
        }

        return Task.FromResult(new AgentDirectoryInfo
        {
            AgentName = AgentName,
            RootPath = RootPath,
            TotalSizeBytes = totalSize,
            FileCountBySubdirectory = fileCounts,
            SizeBySubdirectory = sizes
        });
    }

    /// <summary>
    /// Creates a new agent directory with all required subdirectories.
    /// </summary>
    /// <param name="agentsDirectory">The root agents directory.</param>
    /// <param name="agentName">The name of the agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new FileSystemAgentDirectory instance.</returns>
    public static async Task<FileSystemAgentDirectory> CreateAsync(
        string agentsDirectory,
        string agentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var rootPath = Path.Combine(agentsDirectory, agentName);
        var directory = new FileSystemAgentDirectory(agentName, rootPath);
        await directory.EnsureDirectoryStructureAsync(cancellationToken);

        return directory;
    }

    /// <summary>
    /// Opens an existing agent directory.
    /// </summary>
    /// <param name="agentPath">The full path to the agent directory.</param>
    /// <returns>A FileSystemAgentDirectory instance if the directory exists, null otherwise.</returns>
    public static FileSystemAgentDirectory? Open(string agentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentPath);

        if (!System.IO.Directory.Exists(agentPath))
        {
            return null;
        }

        var agentName = Path.GetFileName(agentPath);
        return new FileSystemAgentDirectory(agentName, agentPath);
    }

    /// <summary>
    /// Disposes the log lock semaphore.
    /// </summary>
    public void Dispose()
    {
        _logLock.Dispose();
    }

    // Private helpers

    private string GetFilePath(AgentSubdirectory subdirectory, string fileName)
    {
        return Path.Combine(GetSubdirectoryPath(subdirectory), fileName);
    }

    private void EnsureSubdirectoryExists(AgentSubdirectory subdirectory)
    {
        var path = GetSubdirectoryPath(subdirectory);
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }
    }

    private static void ValidateFileName(string fileName)
    {
        // Security check: prevent path traversal attacks
        if (fileName.Contains("..") ||
            fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException(
                "File name contains invalid characters or path traversal sequences.",
                nameof(fileName));
        }

        // Check for invalid file name characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.Any(c => invalidChars.Contains(c)))
        {
            throw new ArgumentException(
                "File name contains invalid characters.",
                nameof(fileName));
        }
    }
}
