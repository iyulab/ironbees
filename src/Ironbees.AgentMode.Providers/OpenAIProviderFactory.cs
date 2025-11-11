using Ironbees.AgentMode.Configuration;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Ironbees.AgentMode.Providers;

/// <summary>
/// Factory for creating OpenAI chat clients.
/// Supports official OpenAI API (https://api.openai.com/v1).
/// </summary>
public class OpenAIProviderFactory : ILLMProviderFactory
{
    public LLMProvider Provider => LLMProvider.OpenAI;

    public IChatClient CreateChatClient(LLMConfiguration config)
    {
        if (config.Provider != LLMProvider.OpenAI)
            throw new ArgumentException($"Invalid provider. Expected {LLMProvider.OpenAI}, got {config.Provider}");

        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new ArgumentException("OpenAI API key is required");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new ArgumentException("Model name is required (e.g., gpt-4o-mini, gpt-4o)");

        // Create OpenAI chat client with API key and return as IChatClient
        var chatClient = new ChatClient(config.Model, config.ApiKey);
        return chatClient.AsIChatClient();
    }
}
