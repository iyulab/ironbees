using System.Text;

namespace Ironbees.Core.AgentDirectory;

/// <summary>
/// Utility class for migrating existing agents to the extended DAI directory structure.
/// </summary>
/// <remarks>
/// Migration creates the following subdirectories if they don't exist:
/// - inbox/   (external requests)
/// - outbox/  (results)
/// - memory/  (long-term storage)
/// - workspace/ (temporary work)
/// - logs/    (execution history)
///
/// Each directory will contain a .gitkeep file to preserve it in version control.
/// </remarks>
public sealed class AgentDirectoryMigrator
{
    private readonly IAgentLoader _loader;
    private readonly MigratorOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="AgentDirectoryMigrator"/>.
    /// </summary>
    /// <param name="loader">The agent loader to use.</param>
    /// <param name="options">Migration options.</param>
    public AgentDirectoryMigrator(IAgentLoader? loader = null, MigratorOptions? options = null)
    {
        _loader = loader ?? new FileSystemAgentLoader();
        _options = options ?? new MigratorOptions();
    }

    /// <summary>
    /// Migrates a single agent to the extended directory structure.
    /// </summary>
    /// <param name="agentPath">Path to the agent directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration result.</returns>
    public async Task<AgentMigrationResult> MigrateAgentAsync(
        string agentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentPath);

        var result = new AgentMigrationResult
        {
            AgentPath = agentPath,
            AgentName = Path.GetFileName(agentPath),
            StartTime = DateTimeOffset.UtcNow
        };

        try
        {
            // Validate the agent directory
            if (!await _loader.ValidateAgentDirectoryAsync(agentPath, cancellationToken))
            {
                result.Status = MigrationStatus.Skipped;
                result.Message = "Invalid agent directory (missing agent.yaml or system-prompt.md)";
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }

            // Load the config to get the agent name
            var config = await _loader.LoadConfigAsync(agentPath, cancellationToken);
            result.AgentName = config.Name;

            // Check if already migrated
            var directory = new FileSystemAgentDirectory(config.Name, agentPath);
            var alreadyMigrated = CheckIfMigrated(agentPath);

            if (alreadyMigrated && !_options.ForceMigration)
            {
                result.Status = MigrationStatus.Skipped;
                result.Message = "Agent already has extended directory structure";
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }

            // Create the extended directory structure
            await directory.EnsureDirectoryStructureAsync(cancellationToken);

            // Create initial memory file with agent metadata
            if (_options.CreateInitialMemory)
            {
                var memoryContent = CreateInitialMemory(config);
                await directory.WriteFileAsync(
                    AgentSubdirectory.Memory,
                    "agent-metadata.json",
                    memoryContent,
                    cancellationToken);
            }

            // Log the migration
            await directory.AppendToLogAsync(
                "migration.log",
                $"Migrated to DAI directory structure (v1.0)",
                cancellationToken);

            result.Status = MigrationStatus.Success;
            result.Message = "Successfully migrated to extended directory structure";
            result.DirectoriesCreated = GetCreatedDirectories(agentPath);
        }
        catch (Exception ex)
        {
            result.Status = MigrationStatus.Failed;
            result.Message = ex.Message;
            result.Error = ex;
        }

        result.EndTime = DateTimeOffset.UtcNow;
        return result;
    }

    /// <summary>
    /// Migrates all agents in a directory.
    /// </summary>
    /// <param name="agentsDirectory">The agents root directory.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch migration result.</returns>
    public async Task<BatchMigrationResult> MigrateAllAsync(
        string? agentsDirectory = null,
        IProgress<AgentMigrationResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        agentsDirectory ??= Path.Combine(System.IO.Directory.GetCurrentDirectory(), "agents");

        var batch = new BatchMigrationResult
        {
            AgentsDirectory = agentsDirectory,
            StartTime = DateTimeOffset.UtcNow
        };

        if (!System.IO.Directory.Exists(agentsDirectory))
        {
            batch.EndTime = DateTimeOffset.UtcNow;
            return batch;
        }

        var agentDirs = System.IO.Directory.GetDirectories(agentsDirectory);
        batch.TotalAgents = agentDirs.Length;

        foreach (var agentDir in agentDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await MigrateAgentAsync(agentDir, cancellationToken);
            batch.Results.Add(result);
            progress?.Report(result);

            // Optional delay between migrations
            if (_options.DelayBetweenMigrations > TimeSpan.Zero)
            {
                await Task.Delay(_options.DelayBetweenMigrations, cancellationToken);
            }
        }

        batch.EndTime = DateTimeOffset.UtcNow;
        return batch;
    }

    /// <summary>
    /// Creates a migration report as a string.
    /// </summary>
    /// <param name="batch">The batch migration result.</param>
    /// <returns>A formatted report string.</returns>
    public static string CreateReport(BatchMigrationResult batch)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                   AGENT MIGRATION REPORT                       ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Directory: {batch.AgentsDirectory}");
        sb.AppendLine($"Start Time: {batch.StartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"End Time: {batch.EndTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration: {batch.Duration.TotalSeconds:F2} seconds");
        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine("                         SUMMARY                                ");
        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine($"  Total Agents: {batch.TotalAgents}");
        sb.AppendLine($"  ✓ Successful: {batch.SuccessCount}");
        sb.AppendLine($"  ○ Skipped:    {batch.SkippedCount}");
        sb.AppendLine($"  ✗ Failed:     {batch.FailedCount}");
        sb.AppendLine();

        if (batch.Results.Any(r => r.Status == MigrationStatus.Success))
        {
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("                  SUCCESSFUL MIGRATIONS                         ");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var result in batch.Results.Where(r => r.Status == MigrationStatus.Success))
            {
                sb.AppendLine($"  ✓ {result.AgentName}");
                if (result.DirectoriesCreated.Count > 0)
                {
                    sb.AppendLine($"    Directories created: {string.Join(", ", result.DirectoriesCreated)}");
                }
            }
            sb.AppendLine();
        }

        if (batch.Results.Any(r => r.Status == MigrationStatus.Skipped))
        {
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("                    SKIPPED AGENTS                              ");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var result in batch.Results.Where(r => r.Status == MigrationStatus.Skipped))
            {
                sb.AppendLine($"  ○ {result.AgentName}: {result.Message}");
            }
            sb.AppendLine();
        }

        if (batch.Results.Any(r => r.Status == MigrationStatus.Failed))
        {
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("                    FAILED MIGRATIONS                           ");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var result in batch.Results.Where(r => r.Status == MigrationStatus.Failed))
            {
                sb.AppendLine($"  ✗ {result.AgentName}: {result.Message}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    // Private helpers

    private static bool CheckIfMigrated(string agentPath)
    {
        // Check if all DAI subdirectories exist
        var subdirs = new[] { "inbox", "outbox", "memory", "workspace", "logs" };
        return subdirs.All(subdir =>
            System.IO.Directory.Exists(Path.Combine(agentPath, subdir)));
    }

    private static List<string> GetCreatedDirectories(string agentPath)
    {
        var created = new List<string>();
        var subdirs = new[] { "inbox", "outbox", "memory", "workspace", "logs" };

        foreach (var subdir in subdirs)
        {
            var path = Path.Combine(agentPath, subdir);
            if (System.IO.Directory.Exists(path))
            {
                created.Add(subdir);
            }
        }

        return created;
    }

    private static string CreateInitialMemory(AgentConfig config)
    {
        var metadata = new
        {
            agentName = config.Name,
            description = config.Description,
            capabilities = config.Capabilities,
            tags = config.Tags,
            migratedAt = DateTimeOffset.UtcNow,
            version = "1.0"
        };

        return System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

/// <summary>
/// Options for the agent directory migrator.
/// </summary>
public sealed class MigratorOptions
{
    /// <summary>
    /// Force migration even if already migrated. Default: false.
    /// </summary>
    public bool ForceMigration { get; set; } = false;

    /// <summary>
    /// Create initial memory file with agent metadata. Default: true.
    /// </summary>
    public bool CreateInitialMemory { get; set; } = true;

    /// <summary>
    /// Delay between migrations. Default: no delay.
    /// </summary>
    public TimeSpan DelayBetweenMigrations { get; set; } = TimeSpan.Zero;
}

/// <summary>
/// Status of a migration operation.
/// </summary>
public enum MigrationStatus
{
    /// <summary>Migration was successful.</summary>
    Success,
    /// <summary>Migration was skipped.</summary>
    Skipped,
    /// <summary>Migration failed.</summary>
    Failed
}

/// <summary>
/// Result of a single agent migration.
/// </summary>
public sealed class AgentMigrationResult
{
    /// <summary>Path to the agent directory.</summary>
    public required string AgentPath { get; set; }

    /// <summary>Name of the agent.</summary>
    public required string AgentName { get; set; }

    /// <summary>Migration status.</summary>
    public MigrationStatus Status { get; set; }

    /// <summary>Status message.</summary>
    public string? Message { get; set; }

    /// <summary>Exception if failed.</summary>
    public Exception? Error { get; set; }

    /// <summary>List of directories created.</summary>
    public List<string> DirectoriesCreated { get; set; } = new();

    /// <summary>Start time of migration.</summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>End time of migration.</summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>Duration of migration.</summary>
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Result of a batch migration operation.
/// </summary>
public sealed class BatchMigrationResult
{
    /// <summary>The agents directory.</summary>
    public required string AgentsDirectory { get; set; }

    /// <summary>Total number of agents processed.</summary>
    public int TotalAgents { get; set; }

    /// <summary>Individual results.</summary>
    public List<AgentMigrationResult> Results { get; } = new();

    /// <summary>Start time.</summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>End time.</summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>Duration.</summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>Count of successful migrations.</summary>
    public int SuccessCount => Results.Count(r => r.Status == MigrationStatus.Success);

    /// <summary>Count of skipped agents.</summary>
    public int SkippedCount => Results.Count(r => r.Status == MigrationStatus.Skipped);

    /// <summary>Count of failed migrations.</summary>
    public int FailedCount => Results.Count(r => r.Status == MigrationStatus.Failed);

    /// <summary>Whether all migrations succeeded.</summary>
    public bool AllSuccessful => FailedCount == 0;
}
