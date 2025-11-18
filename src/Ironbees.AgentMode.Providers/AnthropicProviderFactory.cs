using Anthropic.SDK;
using Ironbees.AgentMode.Configuration;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Providers;

/// <summary>
/// Factory for creating Anthropic Claude chat clients.
/// Uses the community Anthropic.SDK package (https://www.nuget.org/packages/Anthropic.SDK).
///
/// Supported models:
/// - claude-sonnet-4-20250514 (Claude Sonnet 4.5)
/// - claude-3-5-sonnet-20241022 (Claude 3.5 Sonnet)
/// - claude-3-opus-20240229 (Claude 3 Opus)
/// - claude-3-haiku-20240307 (Claude 3 Haiku)
///
/// Note: This uses the community SDK. When Microsoft releases an official
/// Microsoft.Extensions.AI.Anthropic package, we will migrate to that.
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

        // Create Anthropic SDK client
        var anthropicClient = new AnthropicClient(config.ApiKey);

        // Wrap in adapter that implements IChatClient
        return new AnthropicChatClientAdapter(anthropicClient, config);
    }
}
