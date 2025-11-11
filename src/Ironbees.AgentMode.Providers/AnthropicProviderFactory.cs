using Ironbees.AgentMode.Configuration;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Providers;

/// <summary>
/// Factory for creating Anthropic Claude chat clients.
///
/// NOTE: This is a placeholder implementation.
/// Anthropic does not have an official .NET SDK yet.
///
/// Implementation options:
/// 1. Use HttpClient with Anthropic API directly
/// 2. Wait for Microsoft.Extensions.AI.Anthropic package
/// 3. Use community SDK (e.g., Anthropic.SDK NuGet package)
/// </summary>
public class AnthropicProviderFactory : ILLMProviderFactory
{
    public LLMProvider Provider => LLMProvider.Anthropic;

    public IChatClient CreateChatClient(LLMConfiguration config)
    {
        if (config.Provider != LLMProvider.Anthropic)
            throw new ArgumentException($"Invalid provider. Expected {LLMProvider.Anthropic}, got {config.Provider}");

        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new ArgumentException("Anthropic API key is required");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new ArgumentException("Model name is required (e.g., claude-sonnet-4-20250514)");

        // TODO: Implement Anthropic client when SDK becomes available
        // Options:
        // 1. Microsoft.Extensions.AI.Anthropic (when released)
        // 2. Anthropic.SDK community package
        // 3. Custom HttpClient-based implementation

        throw new NotImplementedException(
            "Anthropic provider is not yet implemented. " +
            "Waiting for official Microsoft.Extensions.AI.Anthropic package or will implement HttpClient-based adapter.");
    }
}
