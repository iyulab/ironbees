using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Ironbees.Core.Middleware;

/// <summary>
/// Middleware that adds resilience patterns (retry, circuit breaker, timeout) to LLM API calls.
/// Uses Polly v8 for implementing resilience strategies.
/// </summary>
/// <remarks>
/// This middleware helps handle transient failures, rate limiting, and service
/// unavailability by implementing standard resilience patterns.
/// </remarks>
public sealed partial class ResilienceMiddleware : DelegatingChatClient
{
    private readonly ResiliencePipeline<ChatResponse> _pipeline;
    private readonly ILogger<ResilienceMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ResilienceMiddleware"/> with a custom pipeline.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="pipeline">A pre-configured Polly resilience pipeline.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ResilienceMiddleware(
        IChatClient innerClient,
        ResiliencePipeline<ChatResponse> pipeline,
        ILogger<ResilienceMiddleware>? logger = null)
        : base(innerClient)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = logger ?? NullLogger<ResilienceMiddleware>.Instance;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ResilienceMiddleware"/> with default or custom options.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="options">Optional resilience configuration.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ResilienceMiddleware(
        IChatClient innerClient,
        ResilienceOptions? options = null,
        ILogger<ResilienceMiddleware>? logger = null)
        : base(innerClient)
    {
        options ??= new ResilienceOptions();
        _logger = logger ?? NullLogger<ResilienceMiddleware>.Instance;
        _pipeline = CreatePipeline(options);
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(
            async ct => await base.GetResponseAsync(messages, options, ct),
            cancellationToken);
    }

    private ResiliencePipeline<ChatResponse> CreatePipeline(ResilienceOptions options)
    {
        var builder = new ResiliencePipelineBuilder<ChatResponse>();

        // Add timeout if configured
        if (options.TimeoutSeconds > 0)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                OnTimeout = args =>
                {
                    LogRequestTimedOut(_logger, args.Timeout.TotalSeconds);
                    return default;
                }
            });
        }

        // Add retry if configured
        if (options.MaxRetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<ChatResponse>
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = options.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds),
                ShouldHandle = new PredicateBuilder<ChatResponse>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => IsTransientError(r)),
                OnRetry = args =>
                {
                    LogRetryAttempt(_logger, args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message ?? "Transient error");
                    return default;
                }
            });
        }

        // Add circuit breaker if configured
        if (options.EnableCircuitBreaker)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<ChatResponse>
            {
                FailureRatio = options.CircuitBreakerFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds),
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds),
                ShouldHandle = new PredicateBuilder<ChatResponse>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => IsTransientError(r)),
                OnOpened = args =>
                {
                    LogCircuitBreakerOpened(_logger, args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    LogCircuitBreakerClosed(_logger);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    LogCircuitBreakerHalfOpened(_logger);
                    return default;
                }
            });
        }

        return builder.Build();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request timed out after {Timeout}s")]
    private static partial void LogRequestTimedOut(ILogger logger, double timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retry attempt {AttemptNumber} after {Delay}ms due to: {Exception}")]
    private static partial void LogRetryAttempt(ILogger logger, int attemptNumber, double delay, string exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Circuit breaker opened for {Duration}s")]
    private static partial void LogCircuitBreakerOpened(ILogger logger, double duration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker closed")]
    private static partial void LogCircuitBreakerClosed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker half-opened")]
    private static partial void LogCircuitBreakerHalfOpened(ILogger logger);

    private static bool IsTransientError(ChatResponse response)
    {
        // Check for rate limiting or server errors in response
        // This can be extended based on specific provider error patterns
        if (response.AdditionalProperties?.TryGetValue("StatusCode", out var statusCode) == true)
        {
            if (statusCode is int code)
            {
                // Retry on rate limiting (429) or server errors (5xx)
                return code == 429 || (code >= 500 && code < 600);
            }
        }
        return false;
    }
}

/// <summary>
/// Configuration options for <see cref="ResilienceMiddleware"/>.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>
    /// Maximum number of retry attempts. Set to 0 to disable retries. Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds. Default is 1000.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Whether to use exponential backoff for retries. Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Request timeout in seconds. Set to 0 to disable timeout. Default is 120.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Whether to enable circuit breaker. Default is true.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Failure ratio threshold for circuit breaker. Default is 0.5 (50%).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Sampling duration for circuit breaker in seconds. Default is 30.
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum throughput before circuit breaker activates. Default is 10.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Duration the circuit stays open in seconds. Default is 30.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Creates options optimized for development/testing.
    /// </summary>
    public static ResilienceOptions Development => new()
    {
        MaxRetryAttempts = 1,
        RetryDelayMilliseconds = 500,
        TimeoutSeconds = 60,
        EnableCircuitBreaker = false
    };

    /// <summary>
    /// Creates options optimized for production use.
    /// </summary>
    public static ResilienceOptions Production => new()
    {
        MaxRetryAttempts = 3,
        RetryDelayMilliseconds = 1000,
        UseExponentialBackoff = true,
        TimeoutSeconds = 120,
        EnableCircuitBreaker = true,
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerSamplingDurationSeconds = 30,
        CircuitBreakerMinimumThroughput = 10,
        CircuitBreakerDurationSeconds = 30
    };
}
