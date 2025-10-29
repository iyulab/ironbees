using DotNetEnv;
using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAISample;

Console.WriteLine("🐝 Ironbees + OpenAI API Sample\n");

// Load .env file from project root
var envPath = Path.Combine("..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine("✅ Loaded .env file");
}
else
{
    Console.WriteLine("⚠️  .env file not found, using environment variables");
}

// Get configuration from environment
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("❌ Error: OPENAI_API_KEY not set");
    Console.WriteLine("\nPlease set the following environment variable:");
    Console.WriteLine("  - OPENAI_API_KEY");
    return;
}

Console.WriteLine($"🔑 Using API Key: {apiKey[..10]}...");
Console.WriteLine($"🤖 Using Model: {model}\n");

// Set up dependency injection
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Register Ironbees components manually
var agentsPath = Path.Combine("..", "..", "agents");
services.AddSingleton<IAgentLoader>(sp => new FileSystemAgentLoader());
services.AddSingleton<IAgentRegistry, AgentRegistry>();
services.AddSingleton<IAgentSelector>(sp =>
    new KeywordAgentSelector(minimumConfidenceThreshold: 0.3));
services.AddSingleton<ILLMFrameworkAdapter>(sp =>
    new OpenAIAdapter(apiKey, model));
services.AddSingleton<IAgentOrchestrator>(sp =>
    new AgentOrchestrator(
        sp.GetRequiredService<IAgentLoader>(),
        sp.GetRequiredService<IAgentRegistry>(),
        sp.GetRequiredService<ILLMFrameworkAdapter>(),
        sp.GetRequiredService<IAgentSelector>(),
        agentsPath));

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

try
{
    // Load agents from filesystem
    Console.WriteLine("📂 Loading agents...");
    await orchestrator.LoadAgentsAsync();
    Console.WriteLine("✅ Agents loaded successfully\n");

    // List available agents
    var agents = orchestrator.ListAgents();
    Console.WriteLine($"📋 Available agents ({agents.Count}):");
    foreach (var agentName in agents)
    {
        var agent = orchestrator.GetAgent(agentName);
        Console.WriteLine($"  • {agentName}: {agent?.Description}");
    }
    Console.WriteLine();

    // Test 1: Coding Agent
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 1: Coding Agent (gpt-5-nano)");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var codingPrompt = "Write a simple C# function to reverse a string.";
    Console.WriteLine($"💬 Prompt: {codingPrompt}\n");
    Console.WriteLine("🤖 Response from coding-agent:");

    var codingResponse = await orchestrator.ProcessAsync(codingPrompt, "coding-agent");
    Console.WriteLine(codingResponse);
    Console.WriteLine();

    // Test 2: Automatic Agent Selection
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 2: Automatic Agent Selection");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var autoPrompts = new[]
    {
        "Write a blog post about AI",
        "Analyze this sales data: Q1=100, Q2=150, Q3=120, Q4=180",
        "Review the quality of this code: function add(a,b) { return a+b; }",
        "Help me debug this Python code"
    };

    foreach (var prompt in autoPrompts)
    {
        Console.WriteLine($"💬 Prompt: {prompt}");

        var selection = await orchestrator.SelectAgentAsync(prompt);
        Console.WriteLine($"🔍 Selected: {selection.SelectedAgent?.Name ?? "None"}");
        Console.WriteLine($"   Confidence: {selection.ConfidenceScore:P0}");
        Console.WriteLine($"   Reason: {selection.SelectionReason}\n");
    }

    // Test 3: Streaming Response
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 3: Streaming Response (Writing Agent)");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var streamPrompt = "Write a short paragraph about the benefits of multi-agent systems.";
    Console.WriteLine($"💬 Prompt: {streamPrompt}\n");
    Console.WriteLine("🤖 Streaming response:");

    await foreach (var chunk in orchestrator.StreamAsync(streamPrompt, "writing-agent"))
    {
        Console.Write(chunk);
    }
    Console.WriteLine("\n");

    // Test 4: Analysis Agent
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 4: Analysis Agent");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var analysisPrompt = @"Analyze this data and provide insights:
Sales by Region:
- North: $120k (↑15%)
- South: $95k (↓5%)
- East: $150k (↑25%)
- West: $110k (↑10%)";

    Console.WriteLine($"💬 Prompt: {analysisPrompt}\n");
    Console.WriteLine("🤖 Response from analysis-agent:");

    var analysisResponse = await orchestrator.ProcessAsync(analysisPrompt, "analysis-agent");
    Console.WriteLine(analysisResponse);
    Console.WriteLine();

    // Test 5: Review Agent
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 5: Review Agent");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var reviewPrompt = @"Review this function for quality:

public int Calculate(int x, int y)
{
    var result = x + y;
    return result;
}";

    Console.WriteLine($"💬 Prompt: {reviewPrompt}\n");
    Console.WriteLine("🤖 Response from review-agent:");

    var reviewResponse = await orchestrator.ProcessAsync(reviewPrompt, "review-agent");
    Console.WriteLine(reviewResponse);
    Console.WriteLine();

    // Test 6: All Agent Scores Comparison
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 6: Agent Score Comparison");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var comparePrompt = "Help me with software testing";
    Console.WriteLine($"💬 Prompt: {comparePrompt}\n");

    var scoreResult = await orchestrator.SelectAgentAsync(comparePrompt);
    Console.WriteLine("📊 All agent scores:");
    foreach (var score in scoreResult.AllScores.OrderByDescending(s => s.Score))
    {
        Console.WriteLine($"  • {score.Agent.Name,-20} {score.Score:P0}");
        if (score.Reasons.Any())
        {
            Console.WriteLine($"    Reasons: {string.Join(", ", score.Reasons)}");
        }
    }
    Console.WriteLine();

    Console.WriteLine("✅ All tests completed successfully!");
    Console.WriteLine($"\n💡 Model used: {model}");
    Console.WriteLine("💰 Cost-efficient gpt-5-nano model performed well!");
}
catch (AgentNotFoundException ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine("\nMake sure the 'agents' directory exists with agent configurations.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Unexpected error: {ex.Message}");
    Console.WriteLine($"\n{ex.StackTrace}");
}
