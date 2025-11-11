using DotNetEnv;
using Ironbees.AgentMode.Agents.Samples;
using Ironbees.AgentMode.Configuration;
using Ironbees.AgentMode.Providers;

Console.WriteLine("🐝 Ironbees - ConversationalAgent Sample\n");
Console.WriteLine("=========================================\n");

// Load .env file from repository root
var envPath = Path.Combine("..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine("✅ Loaded .env file\n");
}
else
{
    Console.WriteLine("⚠️  .env file not found. Make sure OPENAI_API_KEY is set in environment.\n");
}

// Get configuration from environment
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("❌ Error: OPENAI_API_KEY not found in environment variables.");
    Console.WriteLine("   Please set it in .env file or environment.");
    return;
}

var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

// Create LLM provider using factory pattern
var llmConfig = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    ApiKey = apiKey,
    Model = model
};

var factory = new OpenAIProviderFactory();
var chatClient = factory.CreateChatClient(llmConfig);

Console.WriteLine($"📡 Connected to OpenAI with model: {model}\n");
Console.WriteLine("=========================================\n");

// Example 1: Customer Support Agent
Console.WriteLine("📞 Example 1: Customer Support Agent");
Console.WriteLine("Question: How do I reset my password?\n");

var supportAgent = new CustomerSupportAgent(chatClient);
var supportResponse = await supportAgent.RespondAsync("How do I reset my password?");

Console.WriteLine($"Agent Response:\n{supportResponse}\n");
Console.WriteLine("=========================================\n");

// Example 2: Data Analyst Agent
Console.WriteLine("📊 Example 2: Data Analyst Agent");
Console.WriteLine("Question: How do I calculate correlation between user activity and revenue?\n");

var analystAgent = new DataAnalystAgent(chatClient);
var analystResponse = await analystAgent.RespondAsync(
    "How do I calculate correlation between user activity and revenue?");

Console.WriteLine($"Agent Response:\n{analystResponse}\n");
Console.WriteLine("=========================================\n");

// Example 3: Streaming Response
Console.WriteLine("💬 Example 3: Streaming Response");
Console.WriteLine("Question: Explain multi-provider LLM support in Ironbees Agent Mode\n");
Console.WriteLine("Agent Response (streaming):\n");

await foreach (var chunk in supportAgent.StreamResponseAsync(
    "Explain multi-provider LLM support in Ironbees Agent Mode in 2-3 sentences."))
{
    Console.Write(chunk);
}

Console.WriteLine("\n\n=========================================");
Console.WriteLine("\n✅ ConversationalAgent sample completed!");
