using Ironbees.AgentMode.Configuration;

namespace Ironbees.AgentMode.Providers;

/// <summary>
/// Registry for LLM provider factories.
/// Manages factory selection based on provider type.
/// </summary>
public class LLMProviderFactoryRegistry
{
    private readonly Dictionary<LLMProvider, ILLMProviderFactory> _factories = new();

    /// <summary>
    /// Creates a registry with all built-in provider factories registered.
    /// </summary>
    public static LLMProviderFactoryRegistry CreateDefault()
    {
        var registry = new LLMProviderFactoryRegistry();
        registry.RegisterFactory(new AzureOpenAIProviderFactory());
        registry.RegisterFactory(new OpenAIProviderFactory());
        registry.RegisterFactory(new OpenAICompatibleProviderFactory());
        registry.RegisterFactory(new AnthropicProviderFactory());
        return registry;
    }

    /// <summary>
    /// Registers a provider factory.
    /// </summary>
    public void RegisterFactory(ILLMProviderFactory factory)
    {
        _factories[factory.Provider] = factory;
    }

    /// <summary>
    /// Gets a factory for the specified provider.
    /// </summary>
    public ILLMProviderFactory GetFactory(LLMProvider provider)
    {
        if (!_factories.TryGetValue(provider, out var factory))
            throw new NotSupportedException($"Provider '{provider}' is not registered. Available providers: {string.Join(", ", _factories.Keys)}");

        return factory;
    }

    /// <summary>
    /// Checks if a provider is supported.
    /// </summary>
    public bool IsProviderSupported(LLMProvider provider) => _factories.ContainsKey(provider);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public IEnumerable<LLMProvider> GetSupportedProviders() => _factories.Keys;
}
