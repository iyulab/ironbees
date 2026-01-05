using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.Autonomous.Executors;

/// <summary>
/// Loads agent definitions from filesystem following Ironbees convention:
/// agents/{name}/agent.yaml + system-prompt.md
/// </summary>
public sealed class AgentDefinitionLoader
{
    private readonly IDeserializer _deserializer;

    public AgentDefinitionLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Load a single agent from its directory
    /// </summary>
    public async Task<AgentDefinition> LoadAgentAsync(
        string agentDirectory,
        CancellationToken cancellationToken = default)
    {
        var agentYamlPath = Path.Combine(agentDirectory, "agent.yaml");
        var systemPromptPath = Path.Combine(agentDirectory, "system-prompt.md");

        if (!File.Exists(agentYamlPath))
        {
            throw new FileNotFoundException($"Agent definition not found: {agentYamlPath}");
        }

        // Load agent.yaml
        var yamlContent = await File.ReadAllTextAsync(agentYamlPath, cancellationToken);
        var yamlModel = _deserializer.Deserialize<YamlAgentDefinition>(yamlContent);

        // Load system-prompt.md if exists
        var systemPrompt = File.Exists(systemPromptPath)
            ? await File.ReadAllTextAsync(systemPromptPath, cancellationToken)
            : yamlModel.SystemPrompt ?? string.Empty;

        return MapToDefinition(yamlModel, systemPrompt, agentDirectory);
    }

    /// <summary>
    /// Load all agents from a parent directory
    /// </summary>
    public async Task<IReadOnlyDictionary<string, AgentDefinition>> LoadAgentsAsync(
        string agentsDirectory,
        CancellationToken cancellationToken = default)
    {
        var agents = new Dictionary<string, AgentDefinition>();

        if (!Directory.Exists(agentsDirectory))
        {
            return agents;
        }

        foreach (var dir in Directory.GetDirectories(agentsDirectory))
        {
            var agentYamlPath = Path.Combine(dir, "agent.yaml");
            if (File.Exists(agentYamlPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var agent = await LoadAgentAsync(dir, cancellationToken);
                agents[agent.Id] = agent;
            }
        }

        return agents;
    }

    /// <summary>
    /// Load fallback items from a YAML file
    /// </summary>
    public async Task<List<string>> LoadFallbackItemsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new List<string>();
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var model = _deserializer.Deserialize<YamlFallbackItems>(content);
        return model.Items ?? new List<string>();
    }

    private static AgentDefinition MapToDefinition(
        YamlAgentDefinition yaml,
        string systemPrompt,
        string directory)
    {
        var agentId = yaml.Id ?? Path.GetFileName(directory);

        return new AgentDefinition
        {
            Id = agentId,
            Name = yaml.Name ?? agentId,
            Description = yaml.Description,
            Role = yaml.Role ?? "agent",
            SystemPrompt = systemPrompt,
            Output = new OutputFormat
            {
                Type = yaml.Output?.Type ?? "json",
                Schema = yaml.Output?.Schema,
                Example = yaml.Output?.Example
            },
            Llm = yaml.Llm != null ? new AgentLlmSettings
            {
                MaxOutputTokens = yaml.Llm.MaxOutputTokens,
                Temperature = yaml.Llm.Temperature,
                TopP = yaml.Llm.TopP
            } : null,
            Fallback = yaml.Fallback != null ? new FallbackConfig
            {
                Enabled = yaml.Fallback.Enabled ?? true,
                Items = yaml.Fallback.Items ?? new List<string>(),
                Strategy = yaml.Fallback.Strategy ?? "sequential"
            } : null,
            Variables = yaml.Variables ?? new Dictionary<string, string>()
        };
    }

    #region YAML Models

    private sealed class YamlAgentDefinition
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? SystemPrompt { get; set; }
        public YamlOutputFormat? Output { get; set; }
        public YamlAgentLlmSettings? Llm { get; set; }
        public YamlFallbackConfig? Fallback { get; set; }
        public Dictionary<string, string>? Variables { get; set; }
    }

    private sealed class YamlOutputFormat
    {
        public string? Type { get; set; }
        public string? Schema { get; set; }
        public string? Example { get; set; }
    }

    private sealed class YamlAgentLlmSettings
    {
        public int? MaxOutputTokens { get; set; }
        public float? Temperature { get; set; }
        public float? TopP { get; set; }
    }

    private sealed class YamlFallbackConfig
    {
        public bool? Enabled { get; set; }
        public List<string>? Items { get; set; }
        public string? Strategy { get; set; }
    }

    private sealed class YamlFallbackItems
    {
        public List<string>? Items { get; set; }
    }

    #endregion
}
