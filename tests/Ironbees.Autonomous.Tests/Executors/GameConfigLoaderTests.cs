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

    // --- validation: and prompts: sections (were never mapped; only defaults arrived) ---

    [Fact]
    public void LoadFromString_ValidationSection_ReachesTheDefinition()
    {
        var game = _loader.LoadFromString("""
            validation:
              invalid_patterns:
                english: ["tell me "]
                korean: ["말해"]
              choice_patterns: [" versus "]
              messages:
                open_ended: "custom open"
                choice: "custom choice"
                empty: "custom empty"
                examples: "custom examples"
            """);

        Assert.Equal(["tell me "], game.Validation.InvalidPatterns.English);
        Assert.Equal(["말해"], game.Validation.InvalidPatterns.Korean);
        Assert.Equal([" versus "], game.Validation.ChoicePatterns);
        Assert.Equal("custom open", game.Validation.Messages.OpenEnded);
        Assert.Equal("custom choice", game.Validation.Messages.Choice);
        Assert.Equal("custom empty", game.Validation.Messages.Empty);
        Assert.Equal("custom examples", game.Validation.Messages.Examples);
    }

    [Fact]
    public void LoadFromString_PromptsSection_ReachesTheDefinition()
    {
        var game = _loader.LoadFromString("""
            prompts:
              questions_remaining: "left: {remaining}"
              your_move: "go: "
            """);

        Assert.Equal("left: {remaining}", game.Prompts.QuestionsRemaining);
        Assert.Equal("go: ", game.Prompts.YourMove);
        Assert.Equal(new Ironbees.Autonomous.Executors.PlayerPrompts().HistoryLabel, game.Prompts.HistoryLabel);
    }

    [Fact]
    public void LoadFromString_PartialValidationSection_KeepsTheOtherDefaults()
    {
        var game = _loader.LoadFromString("""
            validation:
              choice_patterns: [" versus "]
            """);

        var defaults = new Ironbees.Autonomous.Executors.ValidationSettings();
        Assert.Equal([" versus "], game.Validation.ChoicePatterns);
        Assert.Equal(defaults.InvalidPatterns.English, game.Validation.InvalidPatterns.English);
        Assert.Equal(defaults.Messages.OpenEnded, game.Validation.Messages.OpenEnded);
    }

    [Fact]
    public async Task LoadGameAsync_TheSampleGameFile_CarriesItsValidationAndPrompts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironbees.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "samples", "TwentyQuestionsSample", "config", "game.yaml");

        var game = await _loader.LoadGameAsync(path, TestContext.Current.CancellationToken);

        Assert.Contains("인가요", game.Validation.ChoicePatterns);
        Assert.Contains("what ", game.Validation.InvalidPatterns.English);
        Assert.Contains("{remaining}", game.Prompts.QuestionsRemaining);
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
            _loader.LoadGameAsync(Path.Combine(_tempDir, "missing.yaml"), TestContext.Current.CancellationToken));
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
        await File.WriteAllTextAsync(filePath, yaml, TestContext.Current.CancellationToken);

        var game = await _loader.LoadGameAsync(filePath, TestContext.Current.CancellationToken);

        Assert.Equal("test-game", game.Id);
        Assert.Equal("Test Game", game.Name);
        Assert.Equal(10, game.Rules.MaxQuestions);
    }
}
