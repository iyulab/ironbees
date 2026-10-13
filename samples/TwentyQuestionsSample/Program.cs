using System.Globalization;
using DotNetEnv;
using Ironbees.Autonomous;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Configuration;
using Ironbees.Autonomous.Executors;
using Ironbees.Autonomous.Models;
using TwentyQuestionsSample;

// ============================================================================
// Twenty Questions Sample - Configuration-Driven Approach
//
// Demonstrates Ironbees SDK capabilities with minimal C# code:
// - Agent definitions from agents/{name}/ directories
// - Game configuration from config/game.yaml
// - SDK-provided utilities (retry, fallback, prompt building)
// ============================================================================

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Load all configurations
var sampleDir = FindSampleDirectory();
LoadEnvironment(sampleDir);

var settings = await new SettingsLoader().LoadWithEnvironmentAsync(
    Path.Combine(sampleDir, "game-settings.yaml"));

var agents = await new AgentDefinitionLoader().LoadAgentsAsync(
    Path.Combine(sampleDir, "agents"));

var gameConfig = await new GameConfigLoader().LoadGameAsync(
    Path.Combine(sampleDir, "config", "game.yaml"));

// Display startup info
Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  🎮 {gameConfig.Name,-51} ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine($"📁 Agents: {string.Join(", ", agents.Keys)}");
Console.WriteLine($"🤖 Model: {settings.Llm.ResolveModel()}");
Console.WriteLine();

// Select mode and run game
var mode = SelectGameMode(gameConfig);
var state = new GameState();

try
{
    await RunGame(settings, agents, gameConfig, mode, state);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine("\n👋 Thanks for playing!");

// ============================================================================
// Game Execution
// ============================================================================

async Task RunGame(
    AutonomousSettings settings,
    IReadOnlyDictionary<string, AgentDefinition> agents,
    GameDefinition gameConfig,
    GameModeDefinition mode,
    GameState state)
{
    Console.WriteLine($"\n🎯 Mode: {mode.Name}");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    // Setup secret
    if (mode.Agents.Answerer == "human")
    {
        Console.WriteLine(gameConfig.Messages.EnterSecret ?? "\n🔒 Enter your secret: ");
        var secret = Console.ReadLine()?.Trim();
        state.Secret = string.IsNullOrEmpty(secret) ? "elephant" : secret;
    }
    else
    {
        Console.WriteLine("\n🤖 AI is generating a secret...");
        var secretGenerator = new AiAnswerOracle(settings, agents["answerer"], state);
        state.Secret = await secretGenerator.GenerateSecretAsync();
        Console.WriteLine("✅ AI has chosen something!");
    }

    Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // Create executor and oracle based on mode
    ITaskExecutor<GameRequest, GameResult> executor = mode.Agents.Questioner == "ai"
        ? new AiQuestionExecutor(settings, agents["questioner"], gameConfig)
        : new HumanQuestionExecutor(gameConfig);

    IOracleVerifier oracle = mode.Agents.Answerer == "ai"
        ? new AiAnswerOracle(settings, agents["answerer"], state)
        : new HumanAnswerOracle(state, gameConfig);

    // Build and run orchestrator
    // Note: DefaultContextManager is now enabled automatically
    var orchestrator = AutonomousOrchestrator.Create<GameRequest, GameResult>()
        .WithSettings(settings)
        .WithExecutor(executor)
        .WithRequestFactory((_, _) => GameRequest.Create(state.History.Count + 1, state.History, null))
        .WithOracle(oracle)
        .Build();

    // Verify context management is enabled by default
    if (orchestrator.ContextProvider != null)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("📦 Context Management: Enabled (DefaultContextManager)");
        Console.ResetColor();
    }

    orchestrator.OnEvent += evt => HandleEvent(evt, settings, state, orchestrator);
    orchestrator.EnqueuePrompt("Start the game!");

    try { await orchestrator.StartAsync(settings.ToAutonomousConfig()); }
    catch (OperationCanceledException) { }

    ShowResults(gameConfig, state, orchestrator);
}

// ============================================================================
// Helpers
// ============================================================================

string FindSampleDirectory()
{
    foreach (var path in new[] { ".", "../../..", AppDomain.CurrentDomain.BaseDirectory })
    {
        if (File.Exists(Path.Combine(path, "config", "game.yaml")))
            return Path.GetFullPath(path);
    }
    return Directory.GetCurrentDirectory();
}

void LoadEnvironment(string sampleDir)
{
    foreach (var path in new[] { sampleDir, "../..", "../../.." })
    {
        var envPath = Path.Combine(sampleDir, path, ".env");
        if (File.Exists(envPath)) { Env.Load(envPath); return; }
    }
}

GameModeDefinition SelectGameMode(GameDefinition gameConfig)
{
    if (gameConfig.Modes.Count == 0)
        return new GameModeDefinition { Name = "Default", Agents = new AgentAssignments { Questioner = "ai", Answerer = "human" } };

    Console.WriteLine("🎮 Select game mode:");
    var modes = gameConfig.Modes.ToList();
    for (int i = 0; i < modes.Count; i++)
        Console.WriteLine($"   [{i + 1}] {modes[i].Value.Name} - {modes[i].Value.Description}");

    Console.Write($"\n👉 Choice (1-{modes.Count}): ");
    var input = Console.ReadLine()?.Trim();

    return int.TryParse(input, out var idx) && idx >= 1 && idx <= modes.Count
        ? modes[idx - 1].Value
        : modes[0].Value;
}

void HandleEvent(AutonomousEvent evt, AutonomousSettings settings, GameState state, AutonomousOrchestrator<GameRequest, GameResult> orchestrator)
{
    switch (evt.Type)
    {
        case AutonomousEventType.IterationStarted:
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"─── Round {evt.Message?.Split(' ').LastOrDefault()} ───");
            // Show saturation status if available
            if (orchestrator.SaturationMonitor != null)
            {
                var saturation = orchestrator.SaturationMonitor.CurrentState;
                Console.WriteLine($"    📊 Context: {saturation.CurrentTokens} tokens ({saturation.Percentage:F0}%)");
            }
            Console.ResetColor();
            break;

        case AutonomousEventType.OracleVerified when evt.OracleVerdict != null:
            Console.ForegroundColor = evt.OracleVerdict.IsComplete ? ConsoleColor.Green : ConsoleColor.White;
            Console.WriteLine($"📋 {evt.OracleVerdict.Analysis}");
            Console.ResetColor();
            break;

        case AutonomousEventType.AutoContinuing:
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔄 {evt.Message}");
            Console.ResetColor();
            break;

        case AutonomousEventType.TaskOutput when !string.IsNullOrEmpty(evt.Message) && evt.Message != "Generating question...":
            var output = ExtractQuestion(evt.Message);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"❓ Q{state.History.Count + 1}: {output}");
            Console.ResetColor();
            break;

        case AutonomousEventType.Completed:
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n🏁 {evt.Message}");
            Console.ResetColor();
            break;

        case AutonomousEventType.MaxIterationsReached:
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n⏰ {evt.Message}");
            Console.ResetColor();
            break;

        case AutonomousEventType.Error:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ {evt.Message}");
            Console.ResetColor();
            break;
    }
}

string ExtractQuestion(string output)
{
    // Try JSON extraction
    if (output.Contains("\"question\""))
    {
        var start = output.IndexOf("\"question\"", StringComparison.Ordinal);
        var colonIdx = output.IndexOf(':', start);
        var quoteStart = output.IndexOf('"', colonIdx + 1) + 1;
        var quoteEnd = output.IndexOf('"', quoteStart);
        if (quoteStart > 0 && quoteEnd > quoteStart)
            return output[quoteStart..quoteEnd];
    }
    return output;
}

void ShowResults(GameDefinition gameConfig, GameState state, AutonomousOrchestrator<GameRequest, GameResult> orchestrator)
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                      📊 GAME RESULTS                       ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

    Console.WriteLine($"\n🔑 Secret: {state.Secret}");
    Console.WriteLine($"❓ Questions: {state.History.Count}");
    Console.WriteLine($"🎯 Guesses: {state.GuessAttempts}");

    // Show context/memory statistics
    if (orchestrator.SaturationMonitor != null)
    {
        var saturation = orchestrator.SaturationMonitor.CurrentState;
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n📦 Context Statistics:");
        Console.WriteLine($"   • Total Tokens Used: {saturation.CurrentTokens}");
        Console.WriteLine($"   • Saturation Level: {saturation.Level} ({saturation.Percentage:F1}%)");
        Console.ResetColor();
    }

    Console.ForegroundColor = state.IsWin ? ConsoleColor.Green : ConsoleColor.Yellow;
    var msg = state.IsWin
        ? (gameConfig.Messages.AiWins ?? "🎉 Correct!").Replace("{questions}", state.History.Count.ToString(CultureInfo.InvariantCulture))
        : (gameConfig.Messages.HumanWins ?? gameConfig.Messages.Timeout ?? "Game Over!");
    Console.WriteLine($"\n{msg}");
    Console.ResetColor();

    if (state.History.Count > 0)
    {
        Console.WriteLine("\n📜 History:");
        foreach (var qa in state.History)
        {
            var cl = string.IsNullOrEmpty(qa.Clarification) ? "" : $" ({qa.Clarification})";
            Console.WriteLine($"   Q{qa.Number}: {qa.Question} → {qa.Answer}{cl}");
        }
    }
}
