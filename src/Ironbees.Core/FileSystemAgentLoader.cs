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
/// </remarks>
public class FileSystemAgentLoader : IAgentLoader
{
    private const string AgentConfigFileName = "agent.yaml";
    private const string SystemPromptFileName = "system-prompt.md";

    private readonly IDeserializer _yamlDeserializer;

    public FileSystemAgentLoader()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc />
    public async Task<AgentConfig> LoadConfigAsync(
        string agentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentPath);

        if (!Directory.Exists(agentPath))
        {
            throw new InvalidAgentDirectoryException(agentPath, "Directory does not exist");
        }

        // Validate required files
        var agentYamlPath = Path.Combine(agentPath, AgentConfigFileName);
        var systemPromptPath = Path.Combine(agentPath, SystemPromptFileName);

        if (!File.Exists(agentYamlPath))
        {
            throw new InvalidAgentDirectoryException(agentPath, $"Missing required file: {AgentConfigFileName}");
        }

        if (!File.Exists(systemPromptPath))
        {
            throw new InvalidAgentDirectoryException(agentPath, $"Missing required file: {SystemPromptFileName}");
        }

        try
        {
            // Load and parse agent.yaml
            var yamlContent = await File.ReadAllTextAsync(agentYamlPath, cancellationToken);
            var config = _yamlDeserializer.Deserialize<AgentConfig>(yamlContent);

            // Load system prompt
            var systemPrompt = await File.ReadAllTextAsync(systemPromptPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new AgentConfigurationException($"System prompt is empty in '{systemPromptPath}'");
            }

            // Return config with system prompt
            return config with { SystemPrompt = systemPrompt.Trim() };
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
        var agentDirectories = Directory.GetDirectories(agentsDirectory);

        foreach (var agentDir in agentDirectories)
        {
            try
            {
                var config = await LoadConfigAsync(agentDir, cancellationToken);
                configs.Add(config);
            }
            catch (Exception)
            {
                // Silently continue loading other agents
                // Callers can check if expected agents are loaded
            }
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
}
