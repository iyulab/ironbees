using Ironbees.Core.Conversation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TokenMeter;

namespace Ironbees.Core;

/// <summary>
/// Extension methods for registering Ironbees Core services
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Add Ironbees Core services (agent loading, registry, selector, orchestrator).
    /// This does NOT register an ILLMFrameworkAdapter — use an adapter package
    /// (e.g. Ironbees.AgentFramework or Ironbees.Ironhive) for that.
    /// </summary>
    public static IServiceCollection AddIronbeesCore(
        this IServiceCollection services,
        Action<IronbeesCoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new IronbeesCoreOptions();
        configure?.Invoke(options);

        // Register TokenMeter services (skip if already registered)
        services.TryAddSingleton<ITokenCounter>(_ => TokenCounter.Default());
        services.TryAddSingleton<ICostCalculator>(_ => CostCalculator.Default());

        // Register core services (skip if already registered)
        services.TryAddSingleton<IAgentLoader, FileSystemAgentLoader>();
        services.TryAddSingleton<IAgentRegistry, AgentRegistry>();

        services.TryAddSingleton<IAgentSelector>(sp =>
        {
            return new KeywordAgentSelector(
                minimumConfidenceThreshold: options.MinimumConfidenceThreshold,
                fallbackAgent: null);
        });

        // Register conversation store if directory is configured
        if (options.ConversationsDirectory is not null)
        {
            services.TryAddSingleton<IConversationStore>(sp =>
                new FileSystemConversationStore(options.ConversationsDirectory));
        }

        services.TryAddSingleton<IAgentOrchestrator>(sp =>
        {
            var loader = sp.GetRequiredService<IAgentLoader>();
            var registry = sp.GetRequiredService<IAgentRegistry>();
            var adapter = sp.GetRequiredService<ILLMFrameworkAdapter>();
            var selector = sp.GetRequiredService<IAgentSelector>();
            var conversationStore = sp.GetService<IConversationStore>();

            return new AgentOrchestrator(
                loader,
                registry,
                adapter,
                selector,
                options.AgentsDirectory,
                conversationStore);
        });

        return services;
    }
}

/// <summary>
/// Configuration options for Ironbees Core services
/// </summary>
public class IronbeesCoreOptions
{
    /// <summary>
    /// Directory containing agent configurations
    /// </summary>
    public string? AgentsDirectory { get; set; }

    /// <summary>
    /// Minimum confidence threshold for agent selection (0.0 to 1.0, default: 0.3)
    /// </summary>
    public double MinimumConfidenceThreshold { get; set; } = 0.3;

    /// <summary>
    /// Directory for storing conversation state files.
    /// When set, enables multi-turn conversation support via FileSystemConversationStore.
    /// </summary>
    public string? ConversationsDirectory { get; set; }
}
