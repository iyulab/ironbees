using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ironbees.Core.Middleware;

/// <summary>
/// Middleware that caches LLM responses to reduce API calls and costs.
/// Uses in-memory caching with configurable expiration policies.
/// </summary>
/// <remarks>
/// Caching is particularly useful for:
/// - Repeated queries with identical inputs
/// - Development and testing
/// - Reducing latency for common requests
/// Note: Streaming responses are not cached.
/// </remarks>
public sealed partial class CachingMiddleware : DelegatingChatClient
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingMiddleware> _logger;
    private readonly CachingOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="CachingMiddleware"/>.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="cache">The memory cache to use.</param>
    /// <param name="options">Optional caching configuration.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public CachingMiddleware(
        IChatClient innerClient,
        IMemoryCache cache,
        CachingOptions? options = null,
        ILogger<CachingMiddleware>? logger = null)
        : base(innerClient)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options ?? new CachingOptions();
        _logger = logger ?? NullLogger<CachingMiddleware>.Instance;
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Skip caching if disabled or if options indicate no caching
        if (!_options.Enabled || ShouldSkipCache(options))
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }

        var cacheKey = GenerateCacheKey(messages, options);

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out ChatResponse? cachedResponse) && cachedResponse != null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogCacheHit(_logger, cacheKey[..Math.Min(32, cacheKey.Length)]);
            }
            return cachedResponse;
        }

        // Get fresh response
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // Cache the response if it's successful
        if (ShouldCacheResponse(response))
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.ExpirationSeconds),
                Size = EstimateResponseSize(response)
            };

            if (_options.SlidingExpirationSeconds > 0)
            {
                cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(_options.SlidingExpirationSeconds);
            }

            _cache.Set(cacheKey, response, cacheOptions);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogCachedResponse(_logger, cacheKey[..Math.Min(32, cacheKey.Length)]);
            }
        }

        return response;
    }

    private bool ShouldSkipCache(ChatOptions? options)
    {
        // Check for explicit cache skip in options
        if (options?.AdditionalProperties?.TryGetValue("SkipCache", out var skipValue) == true)
        {
            return skipValue is true or "true" or "True";
        }

        // Check for temperature > 0 (non-deterministic)
        if (_options.OnlyCacheDeterministic && options?.Temperature > 0)
        {
            return true;
        }

        return false;
    }

    private bool ShouldCacheResponse(ChatResponse response)
    {
        // Don't cache if there was an error
        if (response.AdditionalProperties?.TryGetValue("Error", out _) == true)
        {
            return false;
        }

        // Don't cache empty responses
        if (response.Messages.Count == 0 || response.Messages[0].Contents.Count == 0)
        {
            return false;
        }

        // Check minimum content length
        var textContent = response.Messages[0].Text;
        if (!string.IsNullOrEmpty(textContent) && textContent.Length < _options.MinimumResponseLengthToCache)
        {
            return false;
        }

        return true;
    }

    private static string GenerateCacheKey(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var keyBuilder = new StringBuilder();

        // Include model ID in key
        keyBuilder.Append(options?.ModelId ?? "default");
        keyBuilder.Append('|');

        // Include relevant options that affect output
        if (options?.Temperature.HasValue == true)
        {
            keyBuilder.Append(CultureInfo.InvariantCulture, $"temp:{options.Temperature.Value}|");
        }

        if (options?.MaxOutputTokens.HasValue == true)
        {
            keyBuilder.Append(CultureInfo.InvariantCulture, $"max:{options.MaxOutputTokens.Value}|");
        }

        // Include messages
        foreach (var message in messages)
        {
            keyBuilder.Append(message.Role.Value);
            keyBuilder.Append(':');
            keyBuilder.Append(message.Text ?? string.Empty);
            keyBuilder.Append('|');
        }

        // Hash the key to avoid very long cache keys
        var keyBytes = Encoding.UTF8.GetBytes(keyBuilder.ToString());
        var hashBytes = SHA256.HashData(keyBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit for key: {CacheKey}")]
    private static partial void LogCacheHit(ILogger logger, string cacheKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cached response for key: {CacheKey}")]
    private static partial void LogCachedResponse(ILogger logger, string cacheKey);

    private static long EstimateResponseSize(ChatResponse response)
    {
        // Rough estimate of response size for cache size management
        long size = 1024; // Base overhead

        var firstMessage = response.Messages.FirstOrDefault();
        if (firstMessage?.Text != null)
        {
            size += firstMessage.Text.Length * 2; // Unicode chars
        }

        // Add estimate for additional data
        if (response.Usage != null)
        {
            size += 100;
        }

        return size;
    }
}

/// <summary>
/// Configuration options for <see cref="CachingMiddleware"/>.
/// </summary>
public sealed class CachingOptions
{
    /// <summary>
    /// Whether caching is enabled. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Absolute expiration time in seconds. Default is 3600 (1 hour).
    /// </summary>
    public int ExpirationSeconds { get; set; } = 3600;

    /// <summary>
    /// Sliding expiration time in seconds. Set to 0 to disable. Default is 0.
    /// </summary>
    public int SlidingExpirationSeconds { get; set; }

    /// <summary>
    /// Only cache responses from deterministic requests (temperature = 0). Default is true.
    /// </summary>
    public bool OnlyCacheDeterministic { get; set; } = true;

    /// <summary>
    /// Minimum response length to cache. Default is 10.
    /// </summary>
    public int MinimumResponseLengthToCache { get; set; } = 10;

    /// <summary>
    /// Creates options optimized for development/testing.
    /// </summary>
    public static CachingOptions Development => new()
    {
        Enabled = true,
        ExpirationSeconds = 300, // 5 minutes
        OnlyCacheDeterministic = false
    };

    /// <summary>
    /// Creates options optimized for production use.
    /// </summary>
    public static CachingOptions Production => new()
    {
        Enabled = true,
        ExpirationSeconds = 3600, // 1 hour
        SlidingExpirationSeconds = 1800, // 30 minutes
        OnlyCacheDeterministic = true
    };

    /// <summary>
    /// Creates options with caching disabled.
    /// </summary>
    public static CachingOptions Disabled => new()
    {
        Enabled = false
    };
}
