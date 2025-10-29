using DotNetEnv;
using Ironbees.Core;
using Ironbees.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;

// Load .env file
var envPath = Path.Combine("..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine("✅ Loaded .env file\n");
}

// Get configuration
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("❌ OPENAI_API_KEY environment variable is required");
    return;
}

Console.WriteLine($"🤖 Using model: {model}");
Console.WriteLine("=====================================\n");

// Setup DI
var services = new ServiceCollection();
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
services.AddSingleton<IConversationManager, ConversationManager>();

var serviceProvider = services.BuildServiceProvider();
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();
var conversationManager = serviceProvider.GetRequiredService<IConversationManager>();

// Load agents
await orchestrator.LoadAgentsAsync();

var agents = orchestrator.ListAgents();
Console.WriteLine($"✅ Loaded {agents.Count} agents:");
foreach (var name in agents)
{
    var agent = orchestrator.GetAgent(name);
    Console.WriteLine($"   - {name}: {agent?.Description ?? "N/A"}");
}
Console.WriteLine();

// Test 1: RAG Agent
Console.WriteLine("=====================================");
Console.WriteLine("TEST 1: RAG Agent (Document-based QA)");
Console.WriteLine("=====================================\n");

var ragTest = @"
Context: The Ironbees framework is a multi-agent orchestration system built on .NET 9.0.
It supports multiple LLM providers including Azure OpenAI and standard OpenAI.
The framework uses YAML configuration files to define agents with capabilities, tags, and system prompts.

Question: What version of .NET does Ironbees use?
";

try
{
    var ragResponse = await orchestrator.ProcessAsync(ragTest, "rag-agent");
    Console.WriteLine($"Response:\n{ragResponse}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}\n");
}

// Test 2: Function Calling Agent
Console.WriteLine("=====================================");
Console.WriteLine("TEST 2: Function Calling Agent");
Console.WriteLine("=====================================\n");

var functionTest = "Explain how you would get weather data for Seoul and convert the temperature to Fahrenheit if given these functions: get_weather(location), convert_temp(value, from_unit, to_unit)";

try
{
    var functionResponse = await orchestrator.ProcessAsync(functionTest, "function-calling-agent");
    Console.WriteLine($"Response:\n{functionResponse}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}\n");
}

// Test 3: Router Agent
Console.WriteLine("=====================================");
Console.WriteLine("TEST 3: Router Agent (Intent Classification)");
Console.WriteLine("=====================================\n");

var routerTest = "User request: 'Write a Python function to calculate fibonacci numbers' - Classify this request and recommend the best agent.";

try
{
    var routerResponse = await orchestrator.ProcessAsync(routerTest, "router-agent");
    Console.WriteLine($"Response:\n{routerResponse}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}\n");
}

// Test 4: Memory Agent (with Conversation Manager)
Console.WriteLine("=====================================");
Console.WriteLine("TEST 4: Memory Agent (Conversation Context)");
Console.WriteLine("=====================================\n");

try
{
    var session = await conversationManager.CreateSessionAsync();
    Console.WriteLine($"📝 Created session: {session.SessionId}\n");

    // First message
    await conversationManager.AddMessageAsync(session.SessionId, new ConversationMessage
    {
        Role = "user",
        Content = "I prefer Python programming language"
    });

    var memoryResponse1 = await orchestrator.ProcessAsync(
        "I prefer Python programming language",
        "memory-agent"
    );

    await conversationManager.AddMessageAsync(session.SessionId, new ConversationMessage
    {
        Role = "assistant",
        Content = memoryResponse1,
        AgentName = "memory-agent"
    });

    Console.WriteLine($"User: I prefer Python programming language");
    Console.WriteLine($"Agent: {memoryResponse1}\n");

    // Second message (should recall preference)
    var previousMessages = await conversationManager.GetMessagesAsync(session.SessionId);
    var contextBuilder = new System.Text.StringBuilder();
    contextBuilder.AppendLine("Previous conversation:");
    foreach (var msg in previousMessages)
    {
        contextBuilder.AppendLine($"{msg.Role}: {msg.Content}");
    }

    var memoryTest2 = $"{contextBuilder}\n\nCurrent message: What programming language should I use for web scraping?";

    var memoryResponse2 = await orchestrator.ProcessAsync(memoryTest2, "memory-agent");

    Console.WriteLine($"User: What programming language should I use for web scraping?");
    Console.WriteLine($"Agent: {memoryResponse2}\n");

    var messageCount = (await conversationManager.GetMessagesAsync(session.SessionId)).Count;
    Console.WriteLine($"✅ Session has {messageCount + 2} messages\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}\n");
}

// Test 5: Summarization Agent
Console.WriteLine("=====================================");
Console.WriteLine("TEST 5: Summarization Agent");
Console.WriteLine("=====================================\n");

var summaryTest = @"
Long text to summarize:

The Ironbees framework represents a significant advancement in multi-agent orchestration for .NET applications.
Built on .NET 9.0, it provides a flexible and extensible architecture for creating, managing, and coordinating
multiple AI agents. The framework supports various LLM providers including Azure OpenAI and standard OpenAI APIs.

Key features include:
1. YAML-based agent configuration for easy management
2. Flexible agent selection mechanisms including keyword-based and embedding-based selectors
3. Comprehensive conversation history management
4. Support for streaming responses
5. Extensible architecture allowing custom implementations

The framework includes several built-in agents for common use cases:
- RAG Agent for document-based question answering
- Function Calling Agent for external tool integration
- Router Agent for intelligent request routing
- Memory Agent for context persistence
- Summarization Agent for text compression

Each agent can be customized through YAML configuration files, allowing developers to adjust
system prompts, model parameters, capabilities, and tags without changing code.

Task: Provide a brief summary (2-3 sentences) of the main points.
";

try
{
    var summaryResponse = await orchestrator.ProcessAsync(summaryTest, "summarization-agent");
    Console.WriteLine($"Response:\n{summaryResponse}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}\n");
}

// Test 6: Automatic Agent Selection
Console.WriteLine("=====================================");
Console.WriteLine("TEST 6: Automatic Agent Selection");
Console.WriteLine("=====================================\n");

var autoTests = new[]
{
    "Summarize this article about quantum computing...",
    "Search for weather in Tokyo and show me the result",
    "Which agent should handle data analysis tasks?",
    "I want to remember my project preferences"
};

foreach (var test in autoTests)
{
    Console.WriteLine($"Query: {test}");

    try
    {
        var selection = await orchestrator.SelectAgentAsync(test);

        if (selection.SelectedAgent != null)
        {
            Console.WriteLine($"✅ Selected: {selection.SelectedAgent.Name}");
            Console.WriteLine($"   Confidence: {selection.ConfidenceScore:P0}");
            Console.WriteLine($"   Reason: {selection.SelectionReason}");
        }
        else
        {
            Console.WriteLine($"❌ No suitable agent found");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error: {ex.Message}");
    }

    Console.WriteLine();
}

Console.WriteLine("=====================================");
Console.WriteLine("🎉 ALL TESTS COMPLETED!");
Console.WriteLine("=====================================");
