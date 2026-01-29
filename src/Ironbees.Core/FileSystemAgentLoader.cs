using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.Core;

/// <summary>
/// Loads agent configurations from file system using convention-based structure
/// </summary>
/// <remarks>
/// Expected directory structure:
/// /agents/{agent-name}/
///   agent.yaml           (required)
///   system-prompt.md     (required)
///   tools.md             (optional)
///   mcp-config.json      (optional)
///
/// Features:
/// - Comprehensive validation with detailed error messages
/// - Optional caching for improved performance
/// - Hot reload support with FileSystemWatcher (when enabled)
/// - Duplicate agent name detection
/// </remarks>
public class FileSystemAgentLoader : IAgentLoader, IDisposable
{
    private const string AgentConfigFileName = "agent.yaml";
    private const string SystemPromptFileName = "system-prompt.md";

    private readonly IDeserializer _yamlDeserializer;
    private readonly FileSystemAgentLoaderOptions _options;
    private readonly ILogger<FileSystemAgentLoader>? _logger;
    private readonly ConcurrentDictionary<string, AgentConfig> _configCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _fileLastModified = new();
    private FileSystemWatcher? _fileWatcher;
    private readonly List<AgentConfig> _loadedConfigs = new();
    private readonly object _loadLock = new();

    public FileSystemAgentLoader()
        : this(new FileSystemAgentLoaderOptions(), null)
    {
    }

    public FileSystemAgentLoader(FileSystemAgentLoaderOptions options, ILogger<FileSystemAgentLoader>? logger = null)
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when an agent configuration is reloaded (hot reload)
    /// </summary>
    public event EventHandler<AgentReloadedEventArgs>? AgentReloaded;

    /// <inheritdoc />
    public async Task<AgentConfig> LoadConfigAsync(
        string agentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentPath);

        // Check cache first (if enabled and not modified)
        if (_options.EnableCaching && TryGetCachedConfig(agentPath, out var cachedConfig) && cachedConfig != null)
        {
            return cachedConfig;
        }

        if (!Directory.Exists(agentPath))
        {
            throw new InvalidAgentDirectoryException(agentPath, "Directory does not exist");
        }

        // Validate required files
        var agentYamlPath = Path.Combine(agentPath, AgentConfigFileName);
        var systemPromptPath = Path.Combine(agentPath, SystemPromptFileName);

        if (!File.Exists(agentYamlPath))
        {
            throw new InvalidAgentDirectoryException(
                agentPath,
                $"Missing required file: {AgentConfigFileName}\n" +
                $"Expected at: {agentYamlPath}\n" +
                $"Make sure the agent directory follows the convention: agents/{{agent-name}}/agent.yaml");
        }

        if (!File.Exists(systemPromptPath))
        {
            throw new InvalidAgentDirectoryException(
                agentPath,
                $"Missing required file: {SystemPromptFileName}\n" +
                $"Expected at: {systemPromptPath}\n" +
                $"Make sure the agent directory includes: agents/{{agent-name}}/system-prompt.md");
        }

        try
        {
            // Load and parse agent.yaml
            var yamlContent = await File.ReadAllTextAsync(agentYamlPath, cancellationToken);
            AgentConfig config;

            try
            {
                config = _yamlDeserializer.Deserialize<AgentConfig>(yamlContent);
            }
            catch (Exception yamlEx)
            {
                throw new YamlParsingException(agentPath, AgentConfigFileName, yamlContent, yamlEx);
            }

            // Load system prompt
            var systemPrompt = await File.ReadAllTextAsync(systemPromptPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new AgentConfigurationException(
                    $"System prompt is empty in '{systemPromptPath}'\n" +
                    $"The system prompt must contain at least some instructions for the agent.");
            }

            // Create final config with system prompt
            var finalConfig = config with { SystemPrompt = systemPrompt.Trim() };

            // Validate configuration (if enabled)
            if (_options.EnableValidation)
            {
                var validationResult = AgentConfigValidator.Validate(finalConfig, agentPath);

                if (!validationResult.IsValid)
                {
                    if (_options.StrictValidation)
                    {
                        throw new AgentConfigurationException(agentPath, validationResult);
                    }
                    else if (validationResult.Warnings.Count > 0 && _options.LogWarnings)
                    {
                        _logger?.LogWarning("Agent validation warnings: {Warnings}", validationResult.GetFormattedErrors());
                    }
                }
            }

            // Cache the config (if enabled)
            if (_options.EnableCaching)
            {
                CacheConfig(agentPath, finalConfig);
            }

            return finalConfig;
        }
        catch (Exception ex) when (ex is not AgentConfigurationException and not InvalidAgentDirectoryException)
        {
            throw new AgentLoadException($"Failed to load agent from '{agentPath}'", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentConfig>> LoadAllConfigsAsync(
        string? agentsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        agentsDirectory ??= Path.Combine(Directory.GetCurrentDirectory(), "agents");

        if (!Directory.Exists(agentsDirectory))
        {
            return Array.Empty<AgentConfig>();
        }

        var configs = new List<AgentConfig>();
        var errors = new List<(string AgentPath, Exception Error)>();
        var agentDirectories = Directory.GetDirectories(agentsDirectory);

        foreach (var agentDir in agentDirectories)
        {
            try
            {
                var config = await LoadConfigAsync(agentDir, cancellationToken);
                configs.Add(config);
            }
            catch (Exception ex)
            {
                errors.Add((agentDir, ex));

                // Continue loading other agents unless StopOnFirstError is set
                if (_options.StopOnFirstError)
                {
                    throw;
                }
            }
        }

        // Check for duplicate agent names
        if (_options.EnableValidation)
        {
            var duplicates = configs
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                var duplicateMessage = $"Duplicate agent names detected: {string.Join(", ", duplicates)}\n" +
                    $"Each agent must have a unique name across all agent directories.";

                if (_options.StrictValidation)
                {
                    throw new AgentConfigurationException(duplicateMessage);
                }
                else if (_options.LogWarnings)
                {
                    _logger?.LogWarning("Duplicate agent names: {Message}", duplicateMessage);
                }
            }
        }

        // Log errors if enabled and errors occurred
        if (errors.Count > 0 && _options.LogWarnings)
        {
            _logger?.LogWarning("Failed to load {ErrorCount} agent(s)", errors.Count);
            foreach (var (path, error) in errors)
            {
                _logger?.LogWarning("  - {AgentName}: {ErrorMessage}", Path.GetFileName(path), error.Message);
            }
        }

        // Store loaded configs for hot reload
        lock (_loadLock)
        {
            _loadedConfigs.Clear();
            _loadedConfigs.AddRange(configs);
        }

        // Start file watcher for hot reload (if enabled and not already started)
        if (_options.EnableHotReload && _fileWatcher == null)
        {
            StartFileWatcher(agentsDirectory);
        }

        return configs;
    }

    /// <inheritdoc />
    public Task<bool> ValidateAgentDirectoryAsync(
        string agentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentPath);

        if (!Directory.Exists(agentPath))
        {
            return Task.FromResult(false);
        }

        var agentYamlPath = Path.Combine(agentPath, AgentConfigFileName);
        var systemPromptPath = Path.Combine(agentPath, SystemPromptFileName);

        var isValid = File.Exists(agentYamlPath) && File.Exists(systemPromptPath);
        return Task.FromResult(isValid);
    }

    /// <summary>
    /// Clears the configuration cache
    /// </summary>
    public void ClearCache()
    {
        _configCache.Clear();
        _fileLastModified.Clear();
    }

    // Private helper methods

    private bool TryGetCachedConfig(string agentPath, out AgentConfig? config)
    {
        config = null;

        if (!_configCache.TryGetValue(agentPath, out var cachedConfig))
        {
            return false;
        }

        config = cachedConfig;

        // Check if files have been modified
        var agentYamlPath = Path.Combine(agentPath, AgentConfigFileName);
        var systemPromptPath = Path.Combine(agentPath, SystemPromptFileName);

        var yamlModified = File.GetLastWriteTimeUtc(agentYamlPath);
        var promptModified = File.GetLastWriteTimeUtc(systemPromptPath);
        var latestModified = yamlModified > promptModified ? yamlModified : promptModified;

        if (_fileLastModified.TryGetValue(agentPath, out var cachedModified) &&
            latestModified <= cachedModified)
        {
            return true;
        }

        // Files modified, invalidate cache
        _configCache.TryRemove(agentPath, out _);
        _fileLastModified.TryRemove(agentPath, out _);
        return false;
    }

    private void CacheConfig(string agentPath, AgentConfig config)
    {
        var agentYamlPath = Path.Combine(agentPath, AgentConfigFileName);
        var systemPromptPath = Path.Combine(agentPath, SystemPromptFileName);

        var yamlModified = File.GetLastWriteTimeUtc(agentYamlPath);
        var promptModified = File.GetLastWriteTimeUtc(systemPromptPath);
        var latestModified = yamlModified > promptModified ? yamlModified : promptModified;

        _configCache[agentPath] = config;
        _fileLastModified[agentPath] = latestModified;
    }

    private void StartFileWatcher(string agentsDirectory)
    {
        _fileWatcher = new FileSystemWatcher(agentsDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.*"
        };

        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileChanged;
        _fileWatcher.Renamed += OnFileRenamed;

        _fileWatcher.EnableRaisingEvents = true;
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Only react to agent.yaml and system-prompt.md changes
        var fileName = Path.GetFileName(e.FullPath);
        if (fileName != AgentConfigFileName && fileName != SystemPromptFileName)
        {
            return;
        }

        var agentPath = Path.GetDirectoryName(e.FullPath);
        if (string.IsNullOrEmpty(agentPath))
        {
            return;
        }

        // Invalidate cache
        _configCache.TryRemove(agentPath, out _);
        _fileLastModified.TryRemove(agentPath, out _);

        // Reload config
        try
        {
            // Small delay to ensure file is fully written
            await Task.Delay(100);

            var newConfig = await LoadConfigAsync(agentPath);

            // Raise reload event
            AgentReloaded?.Invoke(this, new AgentReloadedEventArgs(agentPath, newConfig));
        }
        catch (Exception)
        {
            // Silently ignore reload errors (file might be in inconsistent state during editing)
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileChanged(sender, e);
    }

    public void Dispose()
    {
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
/// Options for configuring FileSystemAgentLoader behavior
/// </summary>
public class FileSystemAgentLoaderOptions
{
    /// <summary>
    /// Enable caching of loaded configurations (default: true)
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Enable validation of agent configurations (default: true)
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Stop loading on first error rather than continuing (default: false)
    /// </summary>
    public bool StopOnFirstError { get; set; } = false;

    /// <summary>
    /// Enable strict validation that throws on warnings (default: false)
    /// </summary>
    public bool StrictValidation { get; set; } = false;

    /// <summary>
    /// Log warnings to console (default: true)
    /// </summary>
    public bool LogWarnings { get; set; } = true;

    /// <summary>
    /// Enable hot reload with FileSystemWatcher (default: false)
    /// </summary>
    public bool EnableHotReload { get; set; } = false;
}

/// <summary>
/// Event args for agent reload event
/// </summary>
public class AgentReloadedEventArgs : EventArgs
{
    public AgentReloadedEventArgs(string agentPath, AgentConfig config)
    {
        AgentPath = agentPath;
        Config = config;
    }

    public string AgentPath { get; }
    public AgentConfig Config { get; }
}
