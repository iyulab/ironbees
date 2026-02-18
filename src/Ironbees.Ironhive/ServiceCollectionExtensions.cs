using Ironbees.Core;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Checkpoint;
using Ironbees.Ironhive.Orchestration;
using Ironbees.Ironhive.Tools;
using IronHive.Abstractions;
using IronHive.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
                nameof(configure));
        }

        // Register orchestration services
        services.AddSingleton<IronhiveMiddlewareFactory>();
        services.AddSingleton<IIronhiveOrchestratorFactory, IronhiveOrchestratorFactory>();
        services.AddSingleton<OrchestrationEventMapper>();

        // Register tool registry
        services.AddSingleton<IronhiveToolRegistry>();

        // Register checkpoint store (Ironbees ICheckpointStore)
        if (options.CheckpointStore is not null)
        {
            services.AddSingleton(options.CheckpointStore);
        }
        else
        {
            services.AddSingleton<ICheckpointStore>(sp =>
                new FileSystemIronhiveCheckpointStore(
                    options.CheckpointDirectory,
                    sp.GetRequiredService<ILogger<FileSystemIronhiveCheckpointStore>>()));
        }

        // Register IronHive checkpoint adapter (bridges Ironbees store to IronHive interface)
        services.AddSingleton<IronHive.Abstractions.Agent.Orchestration.ICheckpointStore>(sp =>
            new IronhiveCheckpointStoreAdapter(
                sp.GetRequiredService<ICheckpointStore>(),
                sp.GetService<ILogger<IronhiveCheckpointStoreAdapter>>()));

        // Register IronHive adapter
        services.AddSingleton<ILLMFrameworkAdapter, IronhiveAdapter>();

        // Register options for injection
        services.AddSingleton(options);

        // Register OpenTelemetry if enabled
        if (options.EnableOpenTelemetry)
        {
            ConfigureOpenTelemetry(services);
        }

        return services;
    }

    private static void ConfigureOpenTelemetry(IServiceCollection services)
    {
        // OpenTelemetry configuration is typically done at the application level.
        // We just ensure the IronHive tracing source is available.
        // Users should configure OpenTelemetry in their application with:
        // services.AddOpenTelemetry()
        //     .WithTracing(tracing => tracing.AddSource("IronHive.Orchestration"));
    }
}
