using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TokenMeter;

namespace Ironbees.Core.Middleware;

/// <summary>
/// Middleware that enforces rate limiting on LLM API calls.
/// Supports requests per minute and tokens per minute limits.
/// </summary>
public sealed partial class RateLimitingMiddleware : DelegatingChatClient
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly ITokenCounter? _tokenCounter;

    // Sliding window tracking
    private readonly ConcurrentQueue<DateTimeOffset> _requestTimestamps = new();
    private readonly ConcurrentQueue<(DateTimeOffset Timestamp, int Tokens)> _tokenUsage = new();

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitingMiddleware"/>.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="options">Rate limiting configuration.</param>
    /// <param name="tokenCounter">Optional token counter from TokenMeter for accurate estimation.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public RateLimitingMiddleware(
        IChatClient innerClient,
        RateLimitOptions? options = null,
        ITokenCounter? tokenCounter = null,
        ILogger<RateLimitingMiddleware>? logger = null)
        : base(innerClient)
    {
        _options = options ?? new RateLimitOptions();
        _tokenCounter = tokenCounter;
        _logger = logger ?? NullLogger<RateLimitingMiddleware>.Instance;
        _semaphore = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnforceRateLimitAsync(cancellationToken);

        var acquired = false;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            acquired = true;

            var response = await base.GetResponseAsync(messages, options, cancellationToken);

            // Track token usage if available
            if (response.Usage != null)
            {
                var totalTokens = (int)((response.Usage.InputTokenCount ?? 0) + (response.Usage.OutputTokenCount ?? 0));
                TrackTokenUsage(totalTokens);
            }

            return response;
        }
        finally
        {
            if (acquired)
            {
                _semaphore.Release();
            }
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnforceRateLimitAsync(cancellationToken);

        var acquired = false;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            acquired = true;

            var totalTokens = 0;

            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                // Estimate tokens from content (rough approximation)
                if (update.Text != null)
                {
                    totalTokens += EstimateTokens(update.Text);
                }

                yield return update;
            }

            // Track estimated token usage
            if (totalTokens > 0)
            {
                TrackTokenUsage(totalTokens);
            }
        }
        finally
        {
            if (acquired)
            {
                _semaphore.Release();
            }
        }
    }

    private async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        CleanupOldEntries(now);

        // Check requests per minute
        if (_options.RequestsPerMinute > 0)
        {
            var requestCount = _requestTimestamps.Count;

            if (requestCount >= _options.RequestsPerMinute)
            {
                var waitTime = await HandleRateLimitExceededAsync(
                    "Requests per minute",
                    requestCount,
                    _options.RequestsPerMinute,
                    cancellationToken);

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                    CleanupOldEntries(DateTimeOffset.UtcNow);
                }
            }
        }

        // Check tokens per minute
        if (_options.TokensPerMinute > 0)
        {
            var tokenCount = _tokenUsage.Sum(t => t.Tokens);

            if (tokenCount >= _options.TokensPerMinute)
            {
                var waitTime = await HandleRateLimitExceededAsync(
                    "Tokens per minute",
                    tokenCount,
                    _options.TokensPerMinute,
                    cancellationToken);

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                    CleanupOldEntries(DateTimeOffset.UtcNow);
                }
            }
        }

        // Record this request
        _requestTimestamps.Enqueue(now);
    }

    private async Task<TimeSpan> HandleRateLimitExceededAsync(
        string limitType,
        int current,
        int limit,
        CancellationToken cancellationToken)
    {
        LogRateLimitExceeded(_logger, limitType, current, limit, _options.Strategy);

        return _options.Strategy switch
        {
            RateLimitStrategy.Reject => throw new RateLimitExceededException(limitType, current, limit),
            RateLimitStrategy.Queue => CalculateWaitTime(),
            RateLimitStrategy.Throttle => CalculateWaitTime() / 2, // Shorter wait for throttling
            _ => TimeSpan.Zero
        };
    }

    private TimeSpan CalculateWaitTime()
    {
        if (_requestTimestamps.TryPeek(out var oldest))
        {
            var elapsed = DateTimeOffset.UtcNow - oldest;
            var remaining = TimeSpan.FromMinutes(1) - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1);
        }

        return TimeSpan.FromSeconds(1);
    }

    private void CleanupOldEntries(DateTimeOffset now)
    {
        var cutoff = now.AddMinutes(-1);

        // Clean up request timestamps
        while (_requestTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            _requestTimestamps.TryDequeue(out _);
        }

        // Clean up token usage
        while (_tokenUsage.TryPeek(out var usage) && usage.Timestamp < cutoff)
        {
            _tokenUsage.TryDequeue(out _);
        }
    }

    private void TrackTokenUsage(int tokens)
    {
        _tokenUsage.Enqueue((DateTimeOffset.UtcNow, tokens));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit exceeded: {LimitType} ({Current}/{Limit}). Strategy: {Strategy}")]
    private static partial void LogRateLimitExceeded(ILogger logger, string limitType, int current, int limit, RateLimitStrategy strategy);

    private int EstimateTokens(string text)
    {
        if (_tokenCounter is not null)
            return _tokenCounter.CountTokens(text);

        // Fallback: rough estimation ~4 characters per token
        return (text.Length + 3) / 4;
    }
}

/// <summary>
/// Configuration options for <see cref="RateLimitingMiddleware"/>.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Maximum requests per minute. Set to 0 to disable. Default is 60.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Maximum tokens per minute. Set to 0 to disable. Default is 100000.
    /// </summary>
    public int TokensPerMinute { get; set; } = 100000;

    /// <summary>
    /// Maximum concurrent requests. Default is 10.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Strategy when rate limit is exceeded. Default is Queue.
    /// </summary>
    public RateLimitStrategy Strategy { get; set; } = RateLimitStrategy.Queue;

    /// <summary>
    /// Creates options suitable for development/testing.
    /// </summary>
    public static RateLimitOptions Development => new()
    {
        RequestsPerMinute = 100,
        TokensPerMinute = 200000,
        MaxConcurrentRequests = 20,
        Strategy = RateLimitStrategy.Queue
    };

    /// <summary>
    /// Creates options suitable for production with conservative limits.
    /// </summary>
    public static RateLimitOptions Production => new()
    {
        RequestsPerMinute = 50,
        TokensPerMinute = 100000,
        MaxConcurrentRequests = 10,
        Strategy = RateLimitStrategy.Queue
    };

    /// <summary>
    /// Creates options suitable for strict rate limiting.
    /// </summary>
    public static RateLimitOptions Strict => new()
    {
        RequestsPerMinute = 20,
        TokensPerMinute = 40000,
        MaxConcurrentRequests = 5,
        Strategy = RateLimitStrategy.Reject
    };
}

/// <summary>
/// Strategy for handling rate limit exceeded scenarios.
/// </summary>
public enum RateLimitStrategy
{
    /// <summary>
    /// Queue requests and wait until rate limit window resets.
    /// </summary>
    Queue,

    /// <summary>
    /// Reject requests immediately when limit is exceeded.
    /// </summary>
    Reject,

    /// <summary>
    /// Throttle requests by adding delays.
    /// </summary>
    Throttle
}

/// <summary>
/// Exception thrown when rate limit is exceeded and strategy is Reject.
/// </summary>
public class RateLimitExceededException : Exception
{
    /// <summary>
    /// Type of limit that was exceeded.
    /// </summary>
    public string LimitType { get; }

    /// <summary>
    /// Current usage value.
    /// </summary>
    public int CurrentUsage { get; }

    /// <summary>
    /// Maximum allowed value.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitExceededException"/>.
    /// </summary>
    public RateLimitExceededException(string limitType, int currentUsage, int limit)
        : base($"Rate limit exceeded: {limitType} ({currentUsage}/{limit})")
    {
        LimitType = limitType;
        CurrentUsage = currentUsage;
        Limit = limit;
    }
}
