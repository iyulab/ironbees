// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.Core.Goals;

/// <summary>
/// Loads goal definitions from the filesystem using convention-based structure.
/// </summary>
/// <remarks>
/// Expected directory structure:
/// /goals/{goal-name}/
///   goal.yaml           (required)
///   checkpoints/        (optional, for checkpoint storage)
///
/// Features:
/// - Validation with detailed error messages
/// - Optional caching for improved performance
/// - Hot reload support with FileSystemWatcher (when enabled)
/// </remarks>
public partial class FileSystemGoalLoader : IGoalLoader, IDisposable
{
    private const string GoalConfigFileName = "goal.yaml";
    private const string DefaultGoalsDirectory = "goals";

    private readonly IDeserializer _yamlDeserializer;
    private readonly FileSystemGoalLoaderOptions _options;
    private readonly ILogger<FileSystemGoalLoader>? _logger;
    private readonly ConcurrentDictionary<string, GoalDefinition> _goalCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _fileLastModified = new();
    private FileSystemWatcher? _fileWatcher;

    public FileSystemGoalLoader()
        : this(new FileSystemGoalLoaderOptions(), null)
    {
    }

    public FileSystemGoalLoader(FileSystemGoalLoaderOptions options, ILogger<FileSystemGoalLoader>? logger = null)
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when a goal configuration is reloaded (hot reload).
    /// </summary>
    public event EventHandler<GoalReloadedEventArgs>? GoalReloaded;

    /// <inheritdoc />
    public async Task<GoalDefinition> LoadGoalAsync(
        string goalPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalPath);

        // Check cache first (if enabled and not modified)
        if (_options.EnableCaching && TryGetCachedGoal(goalPath, out var cachedGoal) && cachedGoal != null)
        {
            return cachedGoal;
        }

        if (!Directory.Exists(goalPath))
        {
            throw new GoalLoadException($"Goal directory not found: {goalPath}", goalPath);
        }

        var goalYamlPath = Path.Combine(goalPath, GoalConfigFileName);
        if (!File.Exists(goalYamlPath))
        {
            throw new GoalLoadException(
                $"Missing required file: {GoalConfigFileName}\n" +
                $"Expected at: {goalYamlPath}\n" +
                $"Make sure the goal directory follows the convention: goals/{{goal-name}}/goal.yaml",
                goalPath);
        }

        try
        {
            var yamlContent = await File.ReadAllTextAsync(goalYamlPath, cancellationToken);
            GoalDefinition goal;

            try
            {
                goal = _yamlDeserializer.Deserialize<GoalDefinition>(yamlContent);
            }
            catch (Exception yamlEx)
            {
                throw new GoalLoadException(
                    $"Failed to parse goal.yaml: {yamlEx.Message}",
                    goalPath,
                    yamlEx);
            }

            // Set source path
            goal = goal with { SourcePath = goalPath };

            // Validate configuration (if enabled)
            if (_options.EnableValidation)
            {
                var validationResult = GoalValidator.Validate(goal);

                if (!validationResult.IsValid)
                {
                    if (_options.StrictValidation)
                    {
                        throw new GoalValidationException(
                            $"Goal validation failed: {string.Join("; ", validationResult.Errors)}",
                            goalPath,
                            validationResult.Errors);
                    }
                    else if (_options.LogWarnings && _logger is not null)
                    {
                        foreach (var error in validationResult.Errors)
                        {
                            LogGoalValidationError(_logger, error);
                        }
                    }
                }

                if (validationResult.Warnings.Count > 0 && _options.LogWarnings && _logger is not null)
                {
                    foreach (var warning in validationResult.Warnings)
                    {
                        LogGoalValidationWarning(_logger, warning);
                    }
                }
            }

            // Cache the goal (if enabled)
            if (_options.EnableCaching)
            {
                CacheGoal(goalPath, goal);
            }

            return goal;
        }
        catch (Exception ex) when (ex is not GoalLoadException and not GoalValidationException)
        {
            throw new GoalLoadException($"Failed to load goal from '{goalPath}'", goalPath, ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GoalDefinition>> LoadAllGoalsAsync(
        string? goalsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        goalsDirectory ??= Path.Combine(Directory.GetCurrentDirectory(), DefaultGoalsDirectory);

        if (!Directory.Exists(goalsDirectory))
        {
            return [];
        }

        var goals = new List<GoalDefinition>();
        var errors = new List<(string GoalPath, Exception Error)>();
        var goalDirectories = Directory.GetDirectories(goalsDirectory);

        foreach (var goalDir in goalDirectories)
        {
            try
            {
                var goal = await LoadGoalAsync(goalDir, cancellationToken);
                goals.Add(goal);
            }
            catch (Exception ex)
            {
                errors.Add((goalDir, ex));

                if (_options.StopOnFirstError)
                {
                    throw;
                }
            }
        }

        // Check for duplicate goal IDs
        if (_options.EnableValidation)
        {
            var duplicates = goals
                .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                var duplicateMessage = $"Duplicate goal IDs detected: {string.Join(", ", duplicates)}\n" +
                    $"Each goal must have a unique ID across all goal directories.";

                if (_options.StrictValidation)
                {
                    throw new GoalValidationException(duplicateMessage, duplicates);
                }
                else if (_options.LogWarnings && _logger is not null)
                {
                    LogDuplicateGoalIds(_logger, duplicateMessage);
                }
            }
        }

        // Log errors if enabled
        if (errors.Count > 0 && _options.LogWarnings && _logger is not null)
        {
            LogFailedToLoadGoals(_logger, errors.Count);
            foreach (var (path, error) in errors)
            {
                LogGoalLoadError(_logger, Path.GetFileName(path), error.Message);
            }
        }

        // Start file watcher for hot reload (if enabled)
        if (_options.EnableHotReload && _fileWatcher == null)
        {
            StartFileWatcher(goalsDirectory);
        }

        return goals;
    }

    /// <inheritdoc />
    public Task<bool> ValidateGoalDirectoryAsync(
        string goalPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalPath);

        var result = GoalValidator.ValidateDirectory(goalPath);
        return Task.FromResult(result.IsValid);
    }

    /// <inheritdoc />
    public async Task<GoalDefinition?> GetGoalByIdAsync(
        string goalId,
        string? goalsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalId);

        var allGoals = await LoadAllGoalsAsync(goalsDirectory, cancellationToken);
        return allGoals.FirstOrDefault(g =>
            string.Equals(g.Id, goalId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears the goal cache.
    /// </summary>
    public void ClearCache()
    {
        _goalCache.Clear();
        _fileLastModified.Clear();
    }

    private bool TryGetCachedGoal(string goalPath, out GoalDefinition? goal)
    {
        goal = null;

        if (!_goalCache.TryGetValue(goalPath, out var cachedGoal))
        {
            return false;
        }

        goal = cachedGoal;

        var goalYamlPath = Path.Combine(goalPath, GoalConfigFileName);
        var fileModified = File.GetLastWriteTimeUtc(goalYamlPath);

        if (_fileLastModified.TryGetValue(goalPath, out var cachedModified) &&
            fileModified <= cachedModified)
        {
            return true;
        }

        // File modified, invalidate cache
        _goalCache.TryRemove(goalPath, out _);
        _fileLastModified.TryRemove(goalPath, out _);
        return false;
    }

    private void CacheGoal(string goalPath, GoalDefinition goal)
    {
        var goalYamlPath = Path.Combine(goalPath, GoalConfigFileName);
        var fileModified = File.GetLastWriteTimeUtc(goalYamlPath);

        _goalCache[goalPath] = goal;
        _fileLastModified[goalPath] = fileModified;
    }

    private void StartFileWatcher(string goalsDirectory)
    {
        _fileWatcher = new FileSystemWatcher(goalsDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = GoalConfigFileName
        };

        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileChanged;
        _fileWatcher.Renamed += OnFileRenamed;

        _fileWatcher.EnableRaisingEvents = true;
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var goalPath = Path.GetDirectoryName(e.FullPath);
        if (string.IsNullOrEmpty(goalPath))
        {
            return;
        }

        // Invalidate cache
        _goalCache.TryRemove(goalPath, out _);
        _fileLastModified.TryRemove(goalPath, out _);

        // Reload goal
        try
        {
            await Task.Delay(100); // Small delay to ensure file is fully written
            var newGoal = await LoadGoalAsync(goalPath);
            GoalReloaded?.Invoke(this, new GoalReloadedEventArgs(goalPath, newGoal));
        }
        catch (Exception)
        {
            // Silently ignore reload errors
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileChanged(sender, e);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Goal validation error: {Error}")]
    private static partial void LogGoalValidationError(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Goal validation warning: {Warning}")]
    private static partial void LogGoalValidationWarning(ILogger logger, string warning);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Duplicate goal IDs: {Message}")]
    private static partial void LogDuplicateGoalIds(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load {ErrorCount} goal(s)")]
    private static partial void LogFailedToLoadGoals(ILogger logger, int errorCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "  - {GoalName}: {ErrorMessage}")]
    private static partial void LogGoalLoadError(ILogger logger, string goalName, string errorMessage);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnFileChanged;
            _fileWatcher.Created -= OnFileChanged;
            _fileWatcher.Deleted -= OnFileChanged;
            _fileWatcher.Renamed -= OnFileRenamed;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }
}

/// <summary>
/// Options for configuring FileSystemGoalLoader behavior.
/// </summary>
public class FileSystemGoalLoaderOptions
{
    /// <summary>
    /// Enable caching of loaded goals (default: true).
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Enable validation of goal definitions (default: true).
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Stop loading on first error rather than continuing (default: false).
    /// </summary>
    public bool StopOnFirstError { get; set; }

    /// <summary>
    /// Enable strict validation that throws on errors (default: false).
    /// </summary>
    public bool StrictValidation { get; set; }

    /// <summary>
    /// Log warnings to console (default: true).
    /// </summary>
    public bool LogWarnings { get; set; } = true;

    /// <summary>
    /// Enable hot reload with FileSystemWatcher (default: false).
    /// </summary>
    public bool EnableHotReload { get; set; }
}

/// <summary>
/// Event args for goal reload event.
/// </summary>
public class GoalReloadedEventArgs : EventArgs
{
    public GoalReloadedEventArgs(string goalPath, GoalDefinition goal)
    {
        GoalPath = goalPath;
        Goal = goal;
    }

    public string GoalPath { get; }
    public GoalDefinition Goal { get; }
}
