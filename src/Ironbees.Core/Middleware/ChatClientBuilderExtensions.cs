using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;

namespace Ironbees.Core.Middleware;

/// <summary>
/// Extension methods for building chat client pipelines with Ironbees middleware.
/// </summary>
public static class ChatClientBuilderExtensions
{
    /// <summary>
    /// Adds token tracking middleware to the chat client pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="store">The token usage store.</param>
    /// <param name="options">Optional token tracking configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseTokenTracking(
        this ChatClientBuilder builder,
        ITokenUsageStore store,
        TokenTrackingOptions? options = null,
        ILogger<TokenTrackingMiddleware>? logger = null)
    {
        return builder.Use(inner => new TokenTrackingMiddleware(inner, store, options, logger));
    }

    /// <summary>
    /// Adds token tracking middleware with an in-memory store.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="store">Output parameter to receive the created store.</param>
    /// <param name="options">Optional token tracking configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseTokenTracking(
        this ChatClientBuilder builder,
        out InMemoryTokenUsageStore store,
        TokenTrackingOptions? options = null,
        ILogger<TokenTrackingMiddleware>? logger = null)
    {
        store = new InMemoryTokenUsageStore();
        return builder.UseTokenTracking(store, options, logger);
    }

    /// <summary>
    /// Adds resilience middleware to the chat client pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="options">Optional resilience configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseResilience(
        this ChatClientBuilder builder,
        ResilienceOptions? options = null,
        ILogger<ResilienceMiddleware>? logger = null)
    {
        return builder.Use(inner => new ResilienceMiddleware(inner, options, logger));
    }

    /// <summary>
    /// Adds resilience middleware with a custom Polly pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="pipeline">The pre-configured Polly resilience pipeline.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseResilience(
        this ChatClientBuilder builder,
        ResiliencePipeline<ChatResponse> pipeline,
        ILogger<ResilienceMiddleware>? logger = null)
    {
        return builder.Use(inner => new ResilienceMiddleware(inner, pipeline, logger));
    }

    /// <summary>
    /// Adds caching middleware to the chat client pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="cache">The memory cache to use.</param>
    /// <param name="options">Optional caching configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseCaching(
        this ChatClientBuilder builder,
        IMemoryCache cache,
        CachingOptions? options = null,
        ILogger<CachingMiddleware>? logger = null)
    {
        return builder.Use(inner => new CachingMiddleware(inner, cache, options, logger));
    }

    /// <summary>
    /// Adds caching middleware with a new memory cache instance.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="options">Optional caching configuration.</param>
    /// <param name="memoryCacheOptions">Optional memory cache configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseCaching(
        this ChatClientBuilder builder,
        CachingOptions? options = null,
        MemoryCacheOptions? memoryCacheOptions = null,
        ILogger<CachingMiddleware>? logger = null)
    {
        var cache = new MemoryCache(memoryCacheOptions ?? new MemoryCacheOptions());
        return builder.UseCaching(cache, options, logger);
    }

    /// <summary>
    /// Adds logging middleware to the chat client pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="options">Optional logging configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseLogging(
        this ChatClientBuilder builder,
        LoggingOptions? options = null,
        ILogger<LoggingMiddleware>? logger = null)
    {
        return builder.Use(inner => new LoggingMiddleware(inner, options, logger));
    }

    /// <summary>
    /// Adds a complete middleware stack for production use.
    /// Order: Logging → TokenTracking → Resilience → Caching → BaseClient
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="tokenStore">The token usage store.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="loggerFactory">Optional logger factory for all middleware.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseProductionStack(
        this ChatClientBuilder builder,
        ITokenUsageStore tokenStore,
        IMemoryCache cache,
        ILoggerFactory? loggerFactory = null)
    {
        return builder
            .UseLogging(
                LoggingOptions.Production,
                loggerFactory?.CreateLogger<LoggingMiddleware>())
            .UseTokenTracking(
                tokenStore,
                new TokenTrackingOptions { LogUsage = false },
                loggerFactory?.CreateLogger<TokenTrackingMiddleware>())
            .UseResilience(
                ResilienceOptions.Production,
                loggerFactory?.CreateLogger<ResilienceMiddleware>())
            .UseCaching(
                cache,
                CachingOptions.Production,
                loggerFactory?.CreateLogger<CachingMiddleware>());
    }

    /// <summary>
    /// Adds a complete middleware stack for development use.
    /// Order: Logging → TokenTracking → Caching → BaseClient
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="tokenStore">Output parameter to receive the created token store.</param>
    /// <param name="loggerFactory">Optional logger factory for all middleware.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseDevelopmentStack(
        this ChatClientBuilder builder,
        out InMemoryTokenUsageStore tokenStore,
        ILoggerFactory? loggerFactory = null)
    {
        tokenStore = new InMemoryTokenUsageStore();

        return builder
            .UseLogging(
                LoggingOptions.Development,
                loggerFactory?.CreateLogger<LoggingMiddleware>())
            .UseTokenTracking(
                tokenStore,
                new TokenTrackingOptions { LogUsage = true },
                loggerFactory?.CreateLogger<TokenTrackingMiddleware>())
            .UseCaching(
                CachingOptions.Development,
                logger: loggerFactory?.CreateLogger<CachingMiddleware>());
    }
}
