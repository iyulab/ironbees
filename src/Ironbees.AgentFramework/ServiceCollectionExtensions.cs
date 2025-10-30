using Azure;
using Azure.AI.OpenAI;
using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Ironbees.AgentFramework;

/// <summary>
/// Extension methods for setting up Ironbees services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Ironbees services with Azure OpenAI
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddIronbees(
        this IServiceCollection services,
        Action<IronbeesOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new IronbeesOptions();
        configure(options);

        // Validate options
        if (string.IsNullOrWhiteSpace(options.AzureOpenAIEndpoint))
        {
            throw new ArgumentException("AzureOpenAIEndpoint is required", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.AzureOpenAIKey))
        {
            throw new ArgumentException("AzureOpenAIKey is required", nameof(options));
        }

        // Register AzureOpenAIClient
        services.AddSingleton(sp =>
        {
            return new AzureOpenAIClient(
                new Uri(options.AzureOpenAIEndpoint),
                new AzureKeyCredential(options.AzureOpenAIKey));
        });

        // Register core services
        services.AddSingleton<IAgentLoader, FileSystemAgentLoader>();
        services.AddSingleton<IAgentRegistry, AgentRegistry>();

        // Register LLM framework adapter based on configuration
        if (options.UseMicrosoftAgentFramework)
        {
            services.AddSingleton<ILLMFrameworkAdapter, MicrosoftAgentFrameworkAdapter>();
        }
        else
        {
            services.AddSingleton<ILLMFrameworkAdapter, AgentFrameworkAdapter>();
        }

        // Register agent selector
        services.AddSingleton<IAgentSelector>(sp =>
        {
            return new KeywordAgentSelector(
                minimumConfidenceThreshold: options.MinimumConfidenceThreshold ?? 0.3,
                fallbackAgent: null); // Will be set after agents are loaded if needed
        });

        // Register orchestrator
        services.AddSingleton<IAgentOrchestrator>(sp =>
        {
            var loader = sp.GetRequiredService<IAgentLoader>();
            var registry = sp.GetRequiredService<IAgentRegistry>();
            var adapter = sp.GetRequiredService<ILLMFrameworkAdapter>();
            var selector = sp.GetRequiredService<IAgentSelector>();

            return new AgentOrchestrator(
                loader,
                registry,
                adapter,
                selector,
                options.AgentsDirectory);
        });

        return services;
    }
}

/// <summary>
/// Configuration options for Ironbees
/// </summary>
public class IronbeesOptions
{
    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string? AzureOpenAIEndpoint { get; set; }

    /// <summary>
    /// Azure OpenAI API key
    /// </summary>
    public string? AzureOpenAIKey { get; set; }

    /// <summary>
    /// Directory containing agent configurations
    /// </summary>
    public string? AgentsDirectory { get; set; }

    /// <summary>
    /// Minimum confidence threshold for agent selection (0.0 to 1.0, default: 0.3)
    /// </summary>
    public double? MinimumConfidenceThreshold { get; set; }

    /// <summary>
    /// Use Microsoft Agent Framework for agent execution (default: false, uses Azure.AI.OpenAI ChatClient)
    /// </summary>
    public bool UseMicrosoftAgentFramework { get; set; } = false;
}
