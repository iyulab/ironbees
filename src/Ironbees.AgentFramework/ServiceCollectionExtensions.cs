using Azure;
using Azure.AI.OpenAI;
using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace Ironbees.AgentFramework;

/// <summary>
/// Extension methods for setting up Ironbees services with OpenAI or Azure OpenAI
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Ironbees services with OpenAI or Azure OpenAI.
    /// Provide either <see cref="IronbeesOptions.OpenAIApiKey"/> for plain OpenAI
    /// or <see cref="IronbeesOptions.AzureOpenAIEndpoint"/> + <see cref="IronbeesOptions.AzureOpenAIKey"/> for Azure OpenAI.
    /// </summary>
    public static IServiceCollection AddIronbees(
        this IServiceCollection services,
        Action<IronbeesOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new IronbeesOptions();
        configure(options);

        var hasOpenAI = !string.IsNullOrWhiteSpace(options.OpenAIApiKey);
        var hasAzure = !string.IsNullOrWhiteSpace(options.AzureOpenAIEndpoint)
                    && !string.IsNullOrWhiteSpace(options.AzureOpenAIKey);

        if (!hasOpenAI && !hasAzure)
        {
            throw new ArgumentException(
                "Either OpenAIApiKey or (AzureOpenAIEndpoint + AzureOpenAIKey) must be provided.",
                nameof(options));
        }

        // Register core services via AddIronbeesCore
        services.AddIronbeesCore(core =>
        {
            core.AgentsDirectory = options.AgentsDirectory;
            core.MinimumConfidenceThreshold = options.MinimumConfidenceThreshold ?? 0.3;
        });

        // Register OpenAIClient (base class — works for both plain OpenAI and Azure)
        if (hasAzure)
        {
            services.AddSingleton<OpenAIClient>(sp =>
            {
                return new AzureOpenAIClient(
                    new Uri(options.AzureOpenAIEndpoint!),
                    new AzureKeyCredential(options.AzureOpenAIKey!));
            });
        }
        else
        {
            services.AddSingleton<OpenAIClient>(sp =>
            {
                return new OpenAIClient(options.OpenAIApiKey!);
            });
        }

        // Register LLM framework adapter
        services.AddSingleton<ILLMFrameworkAdapter, AgentFrameworkAdapter>();

        return services;
    }
}

/// <summary>
/// Configuration options for Ironbees
/// </summary>
public class IronbeesOptions
{
    /// <summary>
    /// Plain OpenAI API key. Alternative to Azure credentials.
    /// </summary>
    public string? OpenAIApiKey { get; set; }

    /// <summary>
    /// Azure OpenAI endpoint URL. Use with <see cref="AzureOpenAIKey"/>.
    /// </summary>
    public string? AzureOpenAIEndpoint { get; set; }

    /// <summary>
    /// Azure OpenAI API key. Use with <see cref="AzureOpenAIEndpoint"/>.
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

}
