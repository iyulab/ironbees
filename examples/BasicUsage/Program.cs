using Ironbees.AgentFramework;
using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ironbees.Examples.BasicUsage;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🐝 Ironbees Agent Framework - Basic Usage Example\n");

        // Get configuration from environment variables
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("❌ Error: Azure OpenAI credentials not configured");
            Console.WriteLine("\nPlease set the following environment variables:");
            Console.WriteLine("  - AZURE_OPENAI_ENDPOINT");
            Console.WriteLine("  - AZURE_OPENAI_KEY");
            return;
        }

        // Set up dependency injection
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add Ironbees services
        services.AddIronbees(options =>
        {
            options.AzureOpenAIEndpoint = endpoint;
            options.AzureOpenAIKey = apiKey;
            options.AgentsDirectory = Path.Combine("..", "..", "agents");
        });

        // Build service provider
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

        try
        {
            // Load agents from filesystem
            Console.WriteLine("📂 Loading agents from filesystem...");
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

            // Example 1: Using specific agent (coding-agent)
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("Example 1: Direct agent execution");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            var codingPrompt = "Write a C# method to calculate the nth Fibonacci number using recursion with memoization.";
            Console.WriteLine($"💬 Prompt: {codingPrompt}\n");

            Console.WriteLine("🤖 Response from coding-agent:");
            var response = await orchestrator.ProcessAsync(codingPrompt, "coding-agent");
            Console.WriteLine(response);
            Console.WriteLine();

            // Example 2: Streaming response
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("Example 2: Streaming execution");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            var streamingPrompt = "Explain the SOLID principles in software engineering briefly.";
            Console.WriteLine($"💬 Prompt: {streamingPrompt}\n");

            Console.WriteLine("🤖 Streaming response from coding-agent:");
            await foreach (var chunk in orchestrator.StreamAsync(streamingPrompt, "coding-agent"))
            {
                Console.Write(chunk);
            }
            Console.WriteLine("\n");

            // Example 3: Agent selection with confidence scoring
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("Example 3: Intelligent agent selection");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            var selectionPrompt = "Help me write code to connect to a database";
            Console.WriteLine($"💬 Prompt: {selectionPrompt}\n");

            Console.WriteLine("🔍 Agent selection process:");
            var selectionResult = await orchestrator.SelectAgentAsync(selectionPrompt);

            if (selectionResult.SelectedAgent != null)
            {
                Console.WriteLine($"  ✓ Selected: {selectionResult.SelectedAgent.Name}");
                Console.WriteLine($"  ✓ Confidence: {selectionResult.ConfidenceScore:P0}");
                Console.WriteLine($"  ✓ Reason: {selectionResult.SelectionReason}\n");

                Console.WriteLine("🤖 Response from selected agent:");
                var selectedResponse = await orchestrator.ProcessAsync(selectionPrompt);
                Console.WriteLine(selectedResponse);
            }
            else
            {
                Console.WriteLine($"  ✗ No suitable agent found");
                Console.WriteLine($"  ✗ Reason: {selectionResult.SelectionReason}");
            }
            Console.WriteLine();

            // Example 4: Comparing agent scores
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("Example 4: Agent score comparison");
            Console.WriteLine("═══════════════════════════════════════════════════\n");

            var comparePrompt = "Explain how dependency injection works";
            Console.WriteLine($"💬 Prompt: {comparePrompt}\n");

            var scoreResult = await orchestrator.SelectAgentAsync(comparePrompt);
            Console.WriteLine("📊 All agent scores:");
            foreach (var score in scoreResult.AllScores)
            {
                Console.WriteLine($"  • {score.Agent.Name}: {score.Score:P0}");
                if (score.Reasons.Any())
                {
                    Console.WriteLine($"    Reasons: {string.Join(", ", score.Reasons)}");
                }
            }
            Console.WriteLine();

            Console.WriteLine("✅ All examples completed successfully!");
        }
        catch (AgentNotFoundException ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("\nMake sure the 'agents' directory exists with at least one agent configuration.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex.Message}");
            Console.WriteLine($"\n{ex.StackTrace}");
        }
    }
}
