using DotNetEnv;
using Ironbees.Core;
using Ironbees.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("🐝 Ironbees + GPU-Stack Sample\n");

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
var endpoint = Environment.GetEnvironmentVariable("GPUSTACK_ENDPOINT") ?? "http://localhost:8080";
var apiKey = Environment.GetEnvironmentVariable("GPUSTACK_API_KEY");
var model = Environment.GetEnvironmentVariable("GPUSTACK_MODEL") ?? "llama3.2";

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("❌ Error: GPUSTACK_API_KEY not set");
    Console.WriteLine("\nPlease set the following environment variables:");
    Console.WriteLine("  - GPUSTACK_API_KEY (required)");
    Console.WriteLine("  - GPUSTACK_ENDPOINT (optional, default: http://localhost:8080)");
    Console.WriteLine("  - GPUSTACK_MODEL (optional, default: llama3.2)");
    return;
}

Console.WriteLine($"🔗 GPU-Stack Endpoint: {endpoint}");
Console.WriteLine($"🔑 Using API Key: {apiKey[..Math.Min(10, apiKey.Length)]}...");
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
    new GpuStackAdapter(endpoint, apiKey, model));
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
    Console.WriteLine("Test 1: Coding Agent");
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

    var streamPrompt = "Write a short paragraph about GPU acceleration for AI workloads.";
    Console.WriteLine($"💬 Prompt: {streamPrompt}\n");
    Console.WriteLine("🤖 Streaming response:");

    await foreach (var chunk in orchestrator.StreamAsync(streamPrompt, "writing-agent"))
    {
        Console.Write(chunk);
    }
    Console.WriteLine("\n");

    // Test 3b: Streaming with Automatic Agent Selection (New Feature v0.1.6)
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 3b: Streaming with Auto-Selection (NEW)");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var autoStreamPrompt = "Write a function to calculate Fibonacci numbers";
    Console.WriteLine($"💬 Prompt: {autoStreamPrompt}\n");
    Console.WriteLine("🤖 Auto-selected agent streaming response:");

    await foreach (var chunk in orchestrator.StreamAsync(autoStreamPrompt))
    {
        Console.Write(chunk);
    }
    Console.WriteLine("\n");

    // Test 4: Analysis Agent
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("Test 4: Analysis Agent");
    Console.WriteLine("═══════════════════════════════════════════════════\n");

    var analysisPrompt = @"Analyze this data and provide insights:
GPU Utilization by Cluster:
- Cluster A: 85% (↑10%)
- Cluster B: 72% (↓3%)
- Cluster C: 91% (↑15%)
- Cluster D: 68% (↑5%)";

    Console.WriteLine($"💬 Prompt: {analysisPrompt}\n");
    Console.WriteLine("🤖 Response from analysis-agent:");

    var analysisResponse = await orchestrator.ProcessAsync(analysisPrompt, "analysis-agent");
    Console.WriteLine(analysisResponse);
    Console.WriteLine();

    Console.WriteLine("✅ All tests completed successfully!");
    Console.WriteLine($"\n🔗 GPU-Stack Endpoint: {endpoint}");
    Console.WriteLine($"🤖 Model used: {model}");
    Console.WriteLine("💡 Local GPU-powered inference with GPU-Stack!");
}
catch (AgentNotFoundException ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine("\nMake sure the 'agents' directory exists with agent configurations.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Unexpected error: {ex.Message}");
    Console.WriteLine($"\nDetails: {ex.GetType().Name}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
}
