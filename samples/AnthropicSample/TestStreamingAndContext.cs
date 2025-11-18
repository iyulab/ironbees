using DotNetEnv;
using Ironbees.AgentMode.Agents.Samples;
using Ironbees.AgentMode.Configuration;
using Ironbees.AgentMode.Providers;
using Microsoft.Extensions.AI;

namespace AnthropicSample;

public class TestStreamingAndContext
{
    public static async Task RunTestsAsync()
    {
        Console.WriteLine("🐝 Anthropic Streaming & Extended Context Tests\n");
        Console.WriteLine("=================================================\n");

        // Load .env file
        var envPath = Path.Combine("..", "..", ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            Console.WriteLine("✅ Loaded .env file\n");
        }

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-20250514";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("❌ Error: ANTHROPIC_API_KEY not found");
            return;
        }

        Console.WriteLine($"🤖 Model: {model}\n");

        try
        {
            var config = new LLMConfiguration
            {
                Provider = LLMProvider.Anthropic,
                ApiKey = apiKey,
                Model = model,
                Temperature = 0.7f,
                MaxOutputTokens = 2048
            };

            var factory = new AnthropicProviderFactory();
            var chatClient = factory.CreateChatClient(config);

            // Test 4: Streaming Response
            await TestStreamingResponse(chatClient);

            // Small delay between tests
            await Task.Delay(2000);

            // Test 5: Extended Context
            await TestExtendedContext(chatClient);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
        }
    }

    private static async Task TestStreamingResponse(IChatClient chatClient)
    {
        Console.WriteLine("💬 Test 4: Streaming Response");
        Console.WriteLine("=================================================\n");
        Console.WriteLine("Question: Explain the benefits of Claude models\n");
        Console.WriteLine("Streaming response:\n");

        var supportAgent = new CustomerSupportAgent(chatClient);
        var streamedChunks = 0;
        var totalText = "";

        try
        {
            await foreach (var chunk in supportAgent.StreamResponseAsync(
                "Explain the key benefits of Anthropic Claude models in 3-4 sentences."))
            {
                Console.Write(chunk);
                totalText += chunk;
                streamedChunks++;
            }

            Console.WriteLine($"\n\n📊 Streaming Statistics:");
            Console.WriteLine($"   Chunks received: {streamedChunks}");
            Console.WriteLine($"   Total characters: {totalText.Length}");
            Console.WriteLine("\n✅ Streaming test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Streaming test failed: {ex.Message}");
            throw;
        }

        Console.WriteLine("\n=================================================\n");
    }

    private static async Task TestExtendedContext(IChatClient chatClient)
    {
        Console.WriteLine("📚 Test 5: Extended Context Support");
        Console.WriteLine("=================================================\n");
        Console.WriteLine("Testing multi-turn conversation with context...\n");

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are a helpful coding assistant. Provide clear, concise explanations."),
                new(ChatRole.User, "What is dependency injection?"),
                new(ChatRole.Assistant, "Dependency Injection (DI) is a design pattern where objects receive their dependencies from external sources rather than creating them internally. This promotes loose coupling, testability, and maintainability by allowing dependencies to be easily swapped or mocked."),
                new(ChatRole.User, "Give me a simple C# example using Microsoft.Extensions.DependencyInjection")
            };

            Console.WriteLine("Conversation history:");
            Console.WriteLine("1. User: What is dependency injection?");
            Console.WriteLine("2. Assistant: [Previous response about DI]");
            Console.WriteLine("3. User: Give me a simple C# example using Microsoft.Extensions.DependencyInjection\n");
            Console.WriteLine("Assistant response:\n");

            var response = await chatClient.GetResponseAsync(messages);
            Console.WriteLine(response);

            if (response.Usage != null)
            {
                Console.WriteLine($"\n📊 Token Usage (Extended Context):");
                Console.WriteLine($"   Input: {response.Usage.InputTokenCount} tokens");
                Console.WriteLine($"   Output: {response.Usage.OutputTokenCount} tokens");
                Console.WriteLine($"   Total: {response.Usage.TotalTokenCount} tokens");
                Console.WriteLine($"\n💡 Context window utilized: {response.Usage.InputTokenCount} / 200,000 tokens");
            }

            Console.WriteLine("\n✅ Extended context test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Extended context test failed: {ex.Message}");
            throw;
        }

        Console.WriteLine("\n=================================================\n");
    }
}
