using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.Autonomous.Executors;

/// <summary>
/// Loads game/application configuration from YAML files.
/// </summary>
public sealed class GameConfigLoader
{
    private readonly IDeserializer _deserializer;

    public GameConfigLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Load game configuration from a YAML file
    /// </summary>
    public async Task<GameDefinition> LoadGameAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Game configuration not found: {filePath}");
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return LoadFromString(content);
    }

    /// <summary>
    /// Load game configuration from a YAML string
    /// </summary>
    public GameDefinition LoadFromString(string yamlContent)
    {
        var yamlModel = _deserializer.Deserialize<YamlGameDefinition>(yamlContent);
        return MapToDefinition(yamlModel);
    }

    private static GameDefinition MapToDefinition(YamlGameDefinition? yaml)
    {
        if (yaml == null)
        {
            return new GameDefinition();
        }

        return new GameDefinition
        {
            Id = yaml.Id ?? "game",
            Name = yaml.Name ?? "Game",
            Description = yaml.Description,
            Modes = yaml.Modes?.ToDictionary(
                kvp => kvp.Key,
                kvp => MapModeDefinition(kvp.Value)) ?? new(),
            Rules = MapRules(yaml.Rules),
            Messages = MapMessages(yaml.Messages)
        };
    }

    private static GameModeDefinition MapModeDefinition(YamlGameModeDefinition? yaml)
    {
        if (yaml == null)
        {
            return new GameModeDefinition();
        }

        return new GameModeDefinition
        {
            Name = yaml.Name ?? string.Empty,
            Description = yaml.Description,
            Agents = MapAgentAssignments(yaml.Agents)
        };
    }

    private static AgentAssignments MapAgentAssignments(YamlAgentAssignments? yaml)
    {
        if (yaml == null)
        {
            return new AgentAssignments();
        }

        return new AgentAssignments
        {
            Questioner = yaml.Questioner ?? "ai",
            Answerer = yaml.Answerer ?? "human",
            SecretGenerator = yaml.SecretGenerator
        };
    }

    private static GameRules MapRules(YamlGameRules? yaml)
    {
        if (yaml == null)
        {
            return new GameRules();
        }

        return new GameRules
        {
            MaxQuestions = yaml.MaxQuestions ?? 20,
            MaxGuessAttempts = yaml.MaxGuessAttempts ?? 3,
            GuessConfidenceThreshold = yaml.GuessConfidenceThreshold ?? 0.8,
            ValidAnswers = yaml.ValidAnswers ?? new List<string> { "yes", "no", "maybe" }
        };
    }

    private static GameMessages MapMessages(YamlGameMessages? yaml)
    {
        if (yaml == null)
        {
            return new GameMessages();
        }

        return new GameMessages
        {
            Welcome = yaml.Welcome,
            AiWins = yaml.AiWins,
            HumanWins = yaml.HumanWins,
            Timeout = yaml.Timeout,
            EnterSecret = yaml.EnterSecret
        };
    }

    #region YAML Models

    private sealed class YamlGameDefinition
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, YamlGameModeDefinition>? Modes { get; set; }
        public YamlGameRules? Rules { get; set; }
        public YamlGameMessages? Messages { get; set; }
    }

    private sealed class YamlGameModeDefinition
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public YamlAgentAssignments? Agents { get; set; }
    }

    private sealed class YamlAgentAssignments
    {
        public string? Questioner { get; set; }
        public string? Answerer { get; set; }
        public string? SecretGenerator { get; set; }
    }

    private sealed class YamlGameRules
    {
        public int? MaxQuestions { get; set; }
        public int? MaxGuessAttempts { get; set; }
        public double? GuessConfidenceThreshold { get; set; }
        public List<string>? ValidAnswers { get; set; }
    }

    private sealed class YamlGameMessages
    {
        public string? Welcome { get; set; }
        public string? AiWins { get; set; }
        public string? HumanWins { get; set; }
        public string? Timeout { get; set; }
        public string? EnterSecret { get; set; }
    }

    #endregion
}
