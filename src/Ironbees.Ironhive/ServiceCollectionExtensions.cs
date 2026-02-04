using Ironbees.Core;
using IronHive.Abstractions;
using IronHive.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ironbees.Ironhive;

/// <summary>
/// Extension methods for setting up Ironbees services with IronHive
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Ironbees services with IronHive as the LLM execution backend
    /// </summary>
    public static IServiceCollection AddIronbeesIronhive(
        this IServiceCollection services,
        Action<IronhiveOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new IronhiveOptions();
        configure(options);

        // Register core services via AddIronbeesCore
        services.AddIronbeesCore(core =>
        {
            core.AgentsDirectory = options.AgentsDirectory;
            core.MinimumConfidenceThreshold = options.MinimumConfidenceThreshold;
        });

        // Register IHiveService
        if (options.HiveService is not null)
        {
            // Use pre-configured instance
            services.TryAddSingleton(options.HiveService);
        }
        else if (options.ConfigureHive is not null)
        {
            // Build via IHiveServiceBuilder
            var builder = services.AddHiveServiceCore();
            options.ConfigureHive(builder);
        }
        else
        {
            throw new ArgumentException(
                "Either ConfigureHive or HiveService must be provided.",
                nameof(options));
        }

        // Register IronHive adapter
        services.AddSingleton<ILLMFrameworkAdapter, IronhiveAdapter>();

        return services;
    }
}
