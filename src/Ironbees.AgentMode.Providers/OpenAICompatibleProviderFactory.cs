using Ironbees.AgentMode.Configuration;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Ironbees.AgentMode.Providers;

/// <summary>
/// Factory for creating OpenAI-compatible chat clients.
/// Supports custom endpoints like GPUStack, local LLMs, Ollama, etc.
/// </summary>
public class OpenAICompatibleProviderFactory : ILLMProviderFactory
{
    public LLMProvider Provider => LLMProvider.OpenAICompatible;

    public IChatClient CreateChatClient(LLMConfiguration config)
    {
        if (config.Provider != LLMProvider.OpenAICompatible)
            throw new ArgumentException($"Invalid provider. Expected {LLMProvider.OpenAICompatible}, got {config.Provider}");

        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException("Custom endpoint is required for OpenAI-compatible providers (e.g., http://localhost:8080/v1)");

        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new ArgumentException("API key is required");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new ArgumentException("Model name is required");

        // Parse endpoint
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint))
            throw new ArgumentException($"Invalid endpoint URL: {config.Endpoint}");

        // Create OpenAI client with custom endpoint and return as IChatClient
        // Note: Need to use ApiKeyCredential for custom endpoints
        var apiKeyCredential = new System.ClientModel.ApiKeyCredential(config.ApiKey);
        var chatClient = new ChatClient(config.Model, apiKeyCredential, new OpenAIClientOptions
        {
            Endpoint = endpoint
        });

        return chatClient.AsIChatClient();
    }
}
