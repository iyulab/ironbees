using Ironbees.Autonomous.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Ironbees.Autonomous.Context;

/// <summary>
/// DI extensions for autonomous context management.
/// </summary>
public static class AutonomousContextExtensions
{
    /// <summary>
    /// Adds default all-in-one context management (recommended).
    /// Use this for simple scenarios or testing without external memory systems.
    /// </summary>
    public static IServiceCollection AddAutonomousContext(
        this IServiceCollection services,
        Action<AutonomousContextOptions>? configure = null)
    {
        var options = new AutonomousContextOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Use all-in-one DefaultContextManager
        services.AddSingleton<DefaultContextManager>(sp =>
            new DefaultContextManager(sp.GetRequiredService<AutonomousContextOptions>()));

        // Register as all three interfaces
        services.AddSingleton<IAutonomousContextProvider>(sp =>
            sp.GetRequiredService<DefaultContextManager>());
        services.AddSingleton<IAutonomousMemoryStore>(sp =>
            sp.GetRequiredService<DefaultContextManager>());
        services.AddSingleton<IContextSaturationMonitor>(sp =>
            sp.GetRequiredService<DefaultContextManager>());

        return services;
    }

    /// <summary>
    /// Adds separate in-memory context management components.
    /// Use AddAutonomousContext() for simpler setup.
    /// </summary>
    public static IServiceCollection AddAutonomousContextSeparate(
        this IServiceCollection services,
        Action<AutonomousContextOptions>? configure = null)
    {
        var options = new AutonomousContextOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IAutonomousContextProvider>(sp =>
            new InMemoryContextProvider(options.MaxContextItems));
        services.AddSingleton<IAutonomousMemoryStore>(sp =>
            new InMemoryMemoryStore(options.MaxMemories));
        services.AddSingleton<IContextSaturationMonitor>(sp =>
            new InMemorySaturationMonitor(options.Saturation));

        return services;
    }

    /// <summary>
    /// Adds custom context provider implementation.
    /// Use this to integrate with external memory systems like Memory Indexer.
    /// </summary>
    public static IServiceCollection AddAutonomousContextProvider<TProvider>(
        this IServiceCollection services)
        where TProvider : class, IAutonomousContextProvider
    {
        services.AddSingleton<IAutonomousContextProvider, TProvider>();
        return services;
    }

    /// <summary>
    /// Adds custom memory store implementation.
    /// </summary>
    public static IServiceCollection AddAutonomousMemoryStore<TStore>(
        this IServiceCollection services)
        where TStore : class, IAutonomousMemoryStore
    {
        services.AddSingleton<IAutonomousMemoryStore, TStore>();
        return services;
    }

    /// <summary>
    /// Adds custom saturation monitor implementation.
    /// </summary>
    public static IServiceCollection AddSaturationMonitor<TMonitor>(
        this IServiceCollection services)
        where TMonitor : class, IContextSaturationMonitor
    {
        services.AddSingleton<IContextSaturationMonitor, TMonitor>();
        return services;
    }
}
