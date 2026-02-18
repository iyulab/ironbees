// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Orchestration;
using IronHive.Abstractions.Agent;
using IronHive.Core.Agent;
using Microsoft.Extensions.Logging;

namespace Ironbees.Ironhive.Orchestration;

/// <summary>
/// Factory for creating IronHive middleware instances from Ironbees middleware settings.
/// Bridges declarative middleware configuration to runtime middleware implementations.
/// </summary>
public partial class IronhiveMiddlewareFactory
{
    private readonly ILogger<IronhiveMiddlewareFactory>? _logger;

    public IronhiveMiddlewareFactory(ILogger<IronhiveMiddlewareFactory>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a list of IronHive middleware instances based on the provided settings.
    /// </summary>
    /// <param name="settings">The middleware settings to convert.</param>
    /// <returns>A list of configured middleware instances.</returns>
    public IList<IAgentMiddleware> Create(MiddlewareSettings? settings)
    {
        if (settings is null)
        {
            return [];
        }

        var middlewares = new List<IAgentMiddleware>();

        // Order matters: Logging → RateLimit → Bulkhead → CircuitBreaker → Retry → Timeout
        // This order ensures:
        // 1. Logging captures all requests
        // 2. Rate limiting is checked first
        // 3. Bulkhead prevents overload
        // 4. Circuit breaker prevents cascading failures
        // 5. Retry handles transient failures
        // 6. Timeout ensures requests don't hang

        if (settings.EnableLogging)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                LogAddingLoggingMiddleware(_logger);
            }
            middlewares.Add(new LoggingMiddleware());
        }

        if (settings.RateLimit is not null)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                LogAddingRateLimitMiddleware(_logger, settings.RateLimit.MaxRequests, settings.RateLimit.Window);
            }

            middlewares.Add(new RateLimitMiddleware(
                settings.RateLimit.MaxRequests,
                settings.RateLimit.Window));
        }

        if (settings.Bulkhead is not null)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                LogAddingBulkheadMiddleware(_logger, settings.Bulkhead.MaxConcurrency, settings.Bulkhead.MaxQueueSize);
            }

            middlewares.Add(new BulkheadMiddleware(
                settings.Bulkhead.MaxConcurrency,
                settings.Bulkhead.MaxQueueSize));
        }

        if (settings.CircuitBreaker is not null)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                LogAddingCircuitBreakerMiddleware(_logger, settings.CircuitBreaker.FailureThreshold, settings.CircuitBreaker.BreakDuration);
            }

            middlewares.Add(new CircuitBreakerMiddleware(
                settings.CircuitBreaker.FailureThreshold,
                settings.CircuitBreaker.BreakDuration));
        }

        if (settings.Retry is not null)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                LogAddingRetryMiddleware(_logger, settings.Retry.MaxRetries, settings.Retry.InitialDelay);
            }

            middlewares.Add(new RetryMiddleware(settings.Retry.MaxRetries));
        }

        if (settings.Timeout is not null)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                LogAddingTimeoutMiddleware(_logger, settings.Timeout.Duration);
            }

            middlewares.Add(new TimeoutMiddleware(settings.Timeout.Duration));
        }

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            LogCreatedMiddlewareInstances(_logger, middlewares.Count);
        }

        return middlewares;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding Logging middleware")]
    private static partial void LogAddingLoggingMiddleware(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding RateLimit middleware: {MaxRequests} requests per {Window}")]
    private static partial void LogAddingRateLimitMiddleware(ILogger logger, int maxRequests, TimeSpan window);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding Bulkhead middleware: MaxConcurrency={MaxConcurrency}, MaxQueue={MaxQueue}")]
    private static partial void LogAddingBulkheadMiddleware(ILogger logger, int maxConcurrency, int maxQueue);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding CircuitBreaker middleware: Threshold={Threshold}, BreakDuration={BreakDuration}")]
    private static partial void LogAddingCircuitBreakerMiddleware(ILogger logger, int threshold, TimeSpan breakDuration);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding Retry middleware: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}")]
    private static partial void LogAddingRetryMiddleware(ILogger logger, int maxRetries, TimeSpan initialDelay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding Timeout middleware: {Timeout}")]
    private static partial void LogAddingTimeoutMiddleware(ILogger logger, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {Count} middleware instances")]
    private static partial void LogCreatedMiddlewareInstances(ILogger logger, int count);
}
