using Ironbees.Core;
using Ironbees.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleChatSample;

/// <summary>
/// Interactive console chat application demonstrating Ironbees agent orchestration.
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           🐝 Ironbees Console Chat Sample               ║");
        Console.WriteLine("║       Multi-Agent Orchestration for .NET 9.0             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // 1. Configure services
        var services = new ServiceCollection();

        try
        {
            services.AddIronbees(options =>
            {
                // Azure OpenAI configuration from environment variables
                options.AzureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                    ?? throw new InvalidOperationException(
                        "AZURE_OPENAI_ENDPOINT environment variable not set.\n" +
                        "Set it with: export AZURE_OPENAI_ENDPOINT=\"https://your-resource.openai.azure.com\"");

                options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")
                    ?? throw new InvalidOperationException(
                        "AZURE_OPENAI_KEY environment variable not set.\n" +
                        "Set it with: export AZURE_OPENAI_KEY=\"your-api-key\"");

                // Agent configuration
                options.AgentsDirectory = GetAgentsDirectory();
                options.ConfidenceThreshold = 0.6;
                options.FallbackAgentName = "general-assistant";

                // Use Microsoft Agent Framework (default)
                options.UseMicrosoftAgentFramework = false;
            });

            var serviceProvider = services.BuildServiceProvider();

            // 2. Get orchestrator
            var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();

            // 3. Load agents
            Console.WriteLine("📂 Loading agents...");
            await orchestrator.LoadAgentsAsync();

            var agents = orchestrator.GetAllAgents();
            Console.WriteLine($"✅ Loaded {agents.Count} agent(s):");
            foreach (var agent in agents)
            {
                Console.WriteLine($"   • {agent.Name} - {agent.Description}");
            }
            Console.WriteLine();

            // 4. Interactive chat loop
            await RunChatLoopAsync(orchestrator);
        }
        catch (InvalidOperationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Configuration Error: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Setup Instructions:");
            Console.WriteLine("1. Set AZURE_OPENAI_ENDPOINT environment variable");
            Console.WriteLine("2. Set AZURE_OPENAI_KEY environment variable");
            Console.WriteLine("3. Create agents directory with at least one agent");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Fatal Error: {ex.Message}");
            Console.WriteLine($"   {ex.GetType().Name}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    private static async Task RunChatLoopAsync(IAgentOrchestrator orchestrator)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 Chat Session Started                      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /exit, /quit    - Exit the application");
        Console.WriteLine("  /agents         - List all available agents");
        Console.WriteLine("  /clear          - Clear the console");
        Console.WriteLine("  /agent <name>   - Switch to specific agent");
        Console.WriteLine("  /auto           - Enable auto agent selection (default)");
        Console.WriteLine("  /help           - Show this help message");
        Console.WriteLine();

        string? selectedAgent = null; // null = auto-select

        while (true)
        {
            // Prompt
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (selectedAgent != null)
            {
                Console.Write($"You (@{selectedAgent}): ");
            }
            else
            {
                Console.Write("You (auto): ");
            }
            Console.ResetColor();

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            // Handle commands
            if (input.StartsWith("/"))
            {
                var handled = await HandleCommandAsync(input.Trim(), orchestrator, ref selectedAgent);
                if (!handled)
                {
                    return; // Exit command
                }
                Console.WriteLine();
                continue;
            }

            // Process agent request
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"Agent: ");
            Console.ResetColor();

            try
            {
                // Streaming response
                await foreach (var chunk in orchestrator.StreamAsync(input, selectedAgent))
                {
                    Console.Write(chunk);
                }

                Console.WriteLine();
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }
    }

    private static async Task<bool> HandleCommandAsync(
        string command,
        IAgentOrchestrator orchestrator,
        ref string? selectedAgent)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "/exit":
            case "/quit":
                Console.WriteLine("👋 Goodbye!");
                return false; // Exit

            case "/agents":
                var agents = orchestrator.GetAllAgents();
                Console.WriteLine($"\n📋 Available Agents ({agents.Count}):");
                foreach (var agent in agents)
                {
                    var marker = agent.Name == selectedAgent ? "✓" : " ";
                    Console.WriteLine($"  [{marker}] {agent.Name}");
                    Console.WriteLine($"      {agent.Description}");
                    Console.WriteLine($"      Capabilities: {string.Join(", ", agent.Capabilities)}");
                }
                Console.WriteLine();
                break;

            case "/agent":
                if (parts.Length < 2)
                {
                    Console.WriteLine("❌ Usage: /agent <name>");
                    break;
                }

                var agentName = parts[1];
                var allAgents = orchestrator.GetAllAgents();

                if (allAgents.Any(a => a.Name.Equals(agentName, StringComparison.OrdinalIgnoreCase)))
                {
                    selectedAgent = allAgents.First(a => a.Name.Equals(agentName, StringComparison.OrdinalIgnoreCase)).Name;
                    Console.WriteLine($"✅ Switched to agent: {selectedAgent}");
                }
                else
                {
                    Console.WriteLine($"❌ Agent '{agentName}' not found");
                    Console.WriteLine($"   Available: {string.Join(", ", allAgents.Select(a => a.Name))}");
                }
                break;

            case "/auto":
                selectedAgent = null;
                Console.WriteLine("✅ Auto agent selection enabled");
                break;

            case "/clear":
                Console.Clear();
                Console.WriteLine("🐝 Ironbees Console Chat Sample\n");
                break;

            case "/help":
                Console.WriteLine("\n📖 Available Commands:");
                Console.WriteLine("  /exit, /quit    - Exit the application");
                Console.WriteLine("  /agents         - List all available agents");
                Console.WriteLine("  /clear          - Clear the console");
                Console.WriteLine("  /agent <name>   - Switch to specific agent");
                Console.WriteLine("  /auto           - Enable auto agent selection (default)");
                Console.WriteLine("  /help           - Show this help message");
                Console.WriteLine();
                break;

            default:
                Console.WriteLine($"❌ Unknown command: {cmd}");
                Console.WriteLine("   Type /help for available commands");
                break;
        }

        return true; // Continue
    }

    private static string GetAgentsDirectory()
    {
        // Try multiple possible locations
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "agents"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "agents"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "agents")
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Default to ./agents (will create if needed)
        return Path.Combine(Directory.GetCurrentDirectory(), "agents");
    }
}
