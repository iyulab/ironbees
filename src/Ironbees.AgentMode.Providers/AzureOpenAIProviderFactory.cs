using Azure;
using Azure.AI.Inference;
using Ironbees.AgentMode.Configuration;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Providers;

/// <summary>
/// Factory for creating Azure OpenAI chat clients.
/// Supports Azure OpenAI Service with deployment-based routing.
/// </summary>
public class AzureOpenAIProviderFactory : ILLMProviderFactory
{
    public LLMProvider Provider => LLMProvider.AzureOpenAI;

    public IChatClient CreateChatClient(LLMConfiguration config)
    {
        if (config.Provider != LLMProvider.AzureOpenAI)
            throw new ArgumentException($"Invalid provider. Expected {LLMProvider.AzureOpenAI}, got {config.Provider}");

        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException("Azure OpenAI endpoint is required (e.g., https://{resource}.openai.azure.com)");

        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new ArgumentException("Azure OpenAI API key is required");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new ArgumentException("Deployment name is required (e.g., gpt-4o)");

        // Parse endpoint
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint))
            throw new ArgumentException($"Invalid endpoint URL: {config.Endpoint}");

        // Create Azure AI Inference client and return as IChatClient
        var credential = new AzureKeyCredential(config.ApiKey);
        var azureClient = new ChatCompletionsClient(endpoint, credential);

        return azureClient.AsIChatClient(config.Model);
    }
}
