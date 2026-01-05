using Ironbees.AgentMode.Configuration;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode;

/// <summary>
/// Factory for creating LLM provider-specific chat clients.
/// Abstracts provider differences behind IChatClient interface.
/// </summary>
public interface ILLMProviderFactory
{
    /// <summary>
    /// Provider type this factory supports.
    /// </summary>
    LLMProvider Provider { get; }

    /// <summary>
    /// Creates a chat client for the configured provider.
    /// </summary>
    /// <param name="config">LLM configuration with provider-specific settings</param>
    /// <returns>Provider-specific IChatClient implementation</returns>
    /// <exception cref="ArgumentException">When configuration is invalid</exception>
    /// <exception cref="InvalidOperationException">When provider initialization fails</exception>
    IChatClient CreateChatClient(LLMConfiguration config);
}
