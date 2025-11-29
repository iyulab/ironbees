namespace Ironbees.Core.AgentDirectory;

/// <summary>
/// Extension methods for integrating <see cref="IAgentLoader"/> with the DAI directory structure.
/// </summary>
public static class AgentLoaderExtensions
{
    /// <summary>
    /// Loads an agent configuration and creates the extended directory structure.
    /// </summary>
    /// <param name="loader">The agent loader.</param>
    /// <param name="agentPath">The agent directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the agent config and directory.</returns>
    public static async Task<(AgentConfig Config, IAgentDirectory Directory)> LoadWithDirectoryAsync(
        this IAgentLoader loader,
        string agentPath,
        CancellationToken cancellationToken = default)
    {
        var config = await loader.LoadConfigAsync(agentPath, cancellationToken);
        var directory = new FileSystemAgentDirectory(config.Name, agentPath);
        await directory.EnsureDirectoryStructureAsync(cancellationToken);

        return (config, directory);
    }

    /// <summary>
    /// Loads all agent configurations and creates extended directory structures.
    /// </summary>
    /// <param name="loader">The agent loader.</param>
    /// <param name="agentsDirectory">The agents root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of agent configs with their directories.</returns>
    public static async Task<IReadOnlyList<AgentWithDirectory>> LoadAllWithDirectoriesAsync(
        this IAgentLoader loader,
        string? agentsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        agentsDirectory ??= Path.Combine(System.IO.Directory.GetCurrentDirectory(), "agents");

        var configs = await loader.LoadAllConfigsAsync(agentsDirectory, cancellationToken);
        var results = new List<AgentWithDirectory>();

        foreach (var config in configs)
        {
            var agentPath = Path.Combine(agentsDirectory, config.Name);
            var directory = new FileSystemAgentDirectory(config.Name, agentPath);
            await directory.EnsureDirectoryStructureAsync(cancellationToken);

            results.Add(new AgentWithDirectory(config, directory));
        }

        return results;
    }

    /// <summary>
    /// Creates a new agent with the extended directory structure.
    /// </summary>
    /// <param name="loader">The agent loader.</param>
    /// <param name="agentsDirectory">The agents root directory.</param>
    /// <param name="agentName">The name of the new agent.</param>
    /// <param name="config">The agent configuration.</param>
    /// <param name="systemPrompt">The system prompt content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created agent directory.</returns>
    public static async Task<IAgentDirectory> CreateAgentAsync(
        this IAgentLoader loader,
        string agentsDirectory,
        string agentName,
        AgentConfig config,
        string systemPrompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);

        var agentPath = Path.Combine(agentsDirectory, agentName);

        // Create the directory structure
        var directory = await FileSystemAgentDirectory.CreateAsync(agentsDirectory, agentName, cancellationToken);

        // Write the agent.yaml file
        var yamlSerializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();

        var yamlContent = yamlSerializer.Serialize(config);
        await File.WriteAllTextAsync(Path.Combine(agentPath, "agent.yaml"), yamlContent, cancellationToken);

        // Write the system-prompt.md file
        await File.WriteAllTextAsync(Path.Combine(agentPath, "system-prompt.md"), systemPrompt, cancellationToken);

        return directory;
    }

    /// <summary>
    /// Migrates an existing agent to the extended directory structure.
    /// </summary>
    /// <param name="loader">The agent loader.</param>
    /// <param name="agentPath">The path to the existing agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migrated agent directory, or null if migration failed.</returns>
    public static async Task<IAgentDirectory?> MigrateAgentAsync(
        this IAgentLoader loader,
        string agentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentPath);

        // Validate the agent directory exists
        var isValid = await loader.ValidateAgentDirectoryAsync(agentPath, cancellationToken);
        if (!isValid)
        {
            return null;
        }

        // Load the config to get the agent name
        var config = await loader.LoadConfigAsync(agentPath, cancellationToken);

        // Create and ensure the directory structure
        var directory = new FileSystemAgentDirectory(config.Name, agentPath);
        await directory.EnsureDirectoryStructureAsync(cancellationToken);

        // Log the migration
        await directory.AppendToLogAsync(
            "migration.log",
            $"Agent migrated to extended directory structure (DAI pattern)",
            cancellationToken);

        return directory;
    }

    /// <summary>
    /// Migrates all agents in a directory to the extended directory structure.
    /// </summary>
    /// <param name="loader">The agent loader.</param>
    /// <param name="agentsDirectory">The agents root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A report of the migration results.</returns>
    public static async Task<MigrationReport> MigrateAllAgentsAsync(
        this IAgentLoader loader,
        string? agentsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        agentsDirectory ??= Path.Combine(System.IO.Directory.GetCurrentDirectory(), "agents");

        var report = new MigrationReport { StartTime = DateTimeOffset.UtcNow };

        if (!System.IO.Directory.Exists(agentsDirectory))
        {
            report.EndTime = DateTimeOffset.UtcNow;
            return report;
        }

        var agentDirs = System.IO.Directory.GetDirectories(agentsDirectory);

        foreach (var agentDir in agentDirs)
        {
            try
            {
                var directory = await loader.MigrateAgentAsync(agentDir, cancellationToken);
                if (directory != null)
                {
                    report.SuccessfulMigrations.Add(directory.AgentName);
                }
                else
                {
                    report.SkippedAgents.Add(Path.GetFileName(agentDir));
                }
            }
            catch (Exception ex)
            {
                report.FailedMigrations.Add(new MigrationFailure(
                    Path.GetFileName(agentDir),
                    ex.Message));
            }
        }

        report.EndTime = DateTimeOffset.UtcNow;
        return report;
    }
}

/// <summary>
/// Represents an agent configuration with its associated directory.
/// </summary>
/// <param name="Config">The agent configuration.</param>
/// <param name="Directory">The agent directory.</param>
public sealed record AgentWithDirectory(AgentConfig Config, IAgentDirectory Directory)
{
    /// <summary>
    /// Gets the agent name.
    /// </summary>
    public string Name => Config.Name;

    /// <summary>
    /// Gets the message queue for this agent.
    /// </summary>
    /// <param name="enableWatcher">Whether to enable file system watcher.</param>
    /// <returns>A message queue for this agent.</returns>
    public IMessageQueue GetMessageQueue(bool enableWatcher = false)
    {
        return new FileSystemMessageQueue(Directory, enableWatcher);
    }
}

/// <summary>
/// Report of a migration operation.
/// </summary>
public sealed class MigrationReport
{
    /// <summary>
    /// Gets or sets the start time of the migration.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the migration.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets the list of successfully migrated agents.
    /// </summary>
    public List<string> SuccessfulMigrations { get; } = new();

    /// <summary>
    /// Gets the list of agents that were skipped (already migrated or invalid).
    /// </summary>
    public List<string> SkippedAgents { get; } = new();

    /// <summary>
    /// Gets the list of failed migrations.
    /// </summary>
    public List<MigrationFailure> FailedMigrations { get; } = new();

    /// <summary>
    /// Gets the total number of agents processed.
    /// </summary>
    public int TotalProcessed =>
        SuccessfulMigrations.Count + SkippedAgents.Count + FailedMigrations.Count;

    /// <summary>
    /// Gets whether all migrations were successful (no failures).
    /// </summary>
    public bool AllSuccessful => FailedMigrations.Count == 0;

    /// <summary>
    /// Gets the duration of the migration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Returns a summary string of the migration.
    /// </summary>
    public override string ToString()
    {
        return $"Migration completed in {Duration.TotalSeconds:F1}s: " +
               $"{SuccessfulMigrations.Count} succeeded, " +
               $"{SkippedAgents.Count} skipped, " +
               $"{FailedMigrations.Count} failed";
    }
}

/// <summary>
/// Represents a failed migration.
/// </summary>
/// <param name="AgentName">The name of the agent that failed to migrate.</param>
/// <param name="Error">The error message.</param>
public sealed record MigrationFailure(string AgentName, string Error);
