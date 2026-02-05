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
public class IronhiveMiddlewareFactory
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
            _logger?.LogDebug("Adding Logging middleware");
            middlewares.Add(new LoggingMiddleware());
        }

        if (settings.RateLimit is not null)
        {
            _logger?.LogDebug("Adding RateLimit middleware: {MaxRequests} requests per {Window}",
                settings.RateLimit.MaxRequests, settings.RateLimit.Window);

            middlewares.Add(new RateLimitMiddleware(
                settings.RateLimit.MaxRequests,
                settings.RateLimit.Window));
        }

        if (settings.Bulkhead is not null)
        {
            _logger?.LogDebug("Adding Bulkhead middleware: MaxConcurrency={MaxConcurrency}, MaxQueue={MaxQueue}",
                settings.Bulkhead.MaxConcurrency, settings.Bulkhead.MaxQueueSize);

            middlewares.Add(new BulkheadMiddleware(
                settings.Bulkhead.MaxConcurrency,
                settings.Bulkhead.MaxQueueSize));
        }

        if (settings.CircuitBreaker is not null)
        {
            _logger?.LogDebug("Adding CircuitBreaker middleware: Threshold={Threshold}, BreakDuration={BreakDuration}",
                settings.CircuitBreaker.FailureThreshold, settings.CircuitBreaker.BreakDuration);

            middlewares.Add(new CircuitBreakerMiddleware(
                settings.CircuitBreaker.FailureThreshold,
                settings.CircuitBreaker.BreakDuration));
        }

        if (settings.Retry is not null)
        {
            _logger?.LogDebug("Adding Retry middleware: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}",
                settings.Retry.MaxRetries, settings.Retry.InitialDelay);

            middlewares.Add(new RetryMiddleware(settings.Retry.MaxRetries));
        }

        if (settings.Timeout is not null)
        {
            _logger?.LogDebug("Adding Timeout middleware: {Timeout}", settings.Timeout.Duration);

            middlewares.Add(new TimeoutMiddleware(settings.Timeout.Duration));
        }

        _logger?.LogInformation("Created {Count} middleware instances", middlewares.Count);

        return middlewares;
    }
}
