using Ironbees.Autonomous.Executors;
using Xunit;

namespace Ironbees.Autonomous.Tests.Executors;

public class GameConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GameConfigLoader _loader = new();

    public GameConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"game-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // --- LoadFromString ---

    [Fact]
    public void LoadFromString_EmptyYaml_ShouldReturnDefaults()
    {
        var game = _loader.LoadFromString("{}");

        Assert.Equal("game", game.Id);
        Assert.Equal("Game", game.Name);
        Assert.Null(game.Description);
        Assert.Empty(game.Modes);
    }

    [Fact]
    public void LoadFromString_NullYaml_ShouldReturnDefaults()
    {
        // Empty string YAML deserializes as null, MapToDefinition(null) returns new GameDefinition()
        var game = _loader.LoadFromString("");

        Assert.NotNull(game);
        Assert.Equal(string.Empty, game.Id);
    }

    [Fact]
    public void LoadFromString_BasicFields_ShouldMapCorrectly()
    {
        var yaml = """
            id: "twenty-questions"
            name: "Twenty Questions"
            description: "A classic guessing game"
            """;

        var game = _loader.LoadFromString(yaml);

        Assert.Equal("twenty-questions", game.Id);
        Assert.Equal("Twenty Questions", game.Name);
        Assert.Equal("A classic guessing game", game.Description);
    }

    [Fact]
    public void LoadFromString_GameModes_ShouldMapCorrectly()
    {
        var yaml = """
            modes:
              classic:
                name: "Classic Mode"
                description: "AI asks, human answers"
                agents:
                  questioner: "ai"
                  answerer: "human"
                  secret_generator: "human"
              reverse:
                name: "Reverse Mode"
                description: "Human asks, AI answers"
                agents:
                  questioner: "human"
                  answerer: "ai"
            """;

        var game = _loader.LoadFromString(yaml);

        Assert.Equal(2, game.Modes.Count);
        Assert.True(game.Modes.ContainsKey("classic"));
        Assert.True(game.Modes.ContainsKey("reverse"));

        var classic = game.Modes["classic"];
        Assert.Equal("Classic Mode", classic.Name);
        Assert.Equal("ai", classic.Agents.Questioner);
        Assert.Equal("human", classic.Agents.Answerer);
        Assert.Equal("human", classic.Agents.SecretGenerator);

        var reverse = game.Modes["reverse"];
        Assert.Equal("human", reverse.Agents.Questioner);
        Assert.Equal("ai", reverse.Agents.Answerer);
    }

    [Fact]
    public void LoadFromString_GameRules_ShouldMapCorrectly()
    {
        var yaml = """
            rules:
              max_questions: 30
              max_guess_attempts: 5
              guess_confidence_threshold: 0.9
              valid_answers:
                - "yes"
                - "no"
                - "maybe"
                - "unknown"
            """;

        var game = _loader.LoadFromString(yaml);

        Assert.Equal(30, game.Rules.MaxQuestions);
        Assert.Equal(5, game.Rules.MaxGuessAttempts);
        Assert.Equal(0.9, game.Rules.GuessConfidenceThreshold);
        Assert.Equal(4, game.Rules.ValidAnswers.Count);
        Assert.Contains("unknown", game.Rules.ValidAnswers);
    }

    [Fact]
    public void LoadFromString_RulesDefaults_ShouldBeCorrect()
    {
        var game = _loader.LoadFromString("{}");

        Assert.Equal(20, game.Rules.MaxQuestions);
        Assert.Equal(3, game.Rules.MaxGuessAttempts);
        Assert.Equal(0.8, game.Rules.GuessConfidenceThreshold);
        Assert.Equal(3, game.Rules.ValidAnswers.Count);
    }

    [Fact]
    public void LoadFromString_GameMessages_ShouldMapCorrectly()
    {
        var yaml = """
            messages:
              welcome: "Welcome to the game!"
              ai_wins: "AI wins!"
              human_wins: "You win!"
              timeout: "Time's up!"
              enter_secret: "Enter your secret:"
            """;

        var game = _loader.LoadFromString(yaml);

        Assert.Equal("Welcome to the game!", game.Messages.Welcome);
        Assert.Equal("AI wins!", game.Messages.AiWins);
        Assert.Equal("You win!", game.Messages.HumanWins);
        Assert.Equal("Time's up!", game.Messages.Timeout);
        Assert.Equal("Enter your secret:", game.Messages.EnterSecret);
    }

    [Fact]
    public void LoadFromString_AgentAssignmentsDefaults_ShouldBeCorrect()
    {
        var yaml = """
            modes:
              test:
                name: "Test"
            """;

        var game = _loader.LoadFromString(yaml);
        var mode = game.Modes["test"];

        Assert.Equal("ai", mode.Agents.Questioner);
        Assert.Equal("human", mode.Agents.Answerer);
        Assert.Null(mode.Agents.SecretGenerator);
    }

    // --- LoadGameAsync ---

    [Fact]
    public async Task LoadGameAsync_FileNotFound_ShouldThrow()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _loader.LoadGameAsync(Path.Combine(_tempDir, "missing.yaml")));
    }

    [Fact]
    public async Task LoadGameAsync_ValidFile_ShouldParse()
    {
        var yaml = """
            id: "test-game"
            name: "Test Game"
            rules:
              max_questions: 10
            """;
        var filePath = Path.Combine(_tempDir, "game.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var game = await _loader.LoadGameAsync(filePath);

        Assert.Equal("test-game", game.Id);
        Assert.Equal("Test Game", game.Name);
        Assert.Equal(10, game.Rules.MaxQuestions);
    }
}
