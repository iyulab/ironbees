// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Orchestration;

/// <summary>
/// Configuration settings for orchestration middleware.
/// Allows declarative configuration of resilience patterns, logging, and other cross-cutting concerns.
/// </summary>
public sealed record MiddlewareSettings
{
    /// <summary>
    /// Retry configuration for handling transient failures.
    /// </summary>
    public RetrySettings? Retry { get; init; }

    /// <summary>
    /// Timeout configuration for agent execution.
    /// </summary>
    public TimeoutSettings? Timeout { get; init; }

    /// <summary>
    /// Circuit breaker configuration for handling cascading failures.
    /// </summary>
    public CircuitBreakerSettings? CircuitBreaker { get; init; }

    /// <summary>
    /// Bulkhead configuration for concurrency isolation.
    /// </summary>
    public BulkheadSettings? Bulkhead { get; init; }

    /// <summary>
    /// Rate limiting configuration for controlling request throughput.
    /// </summary>
    public RateLimitSettings? RateLimit { get; init; }

    /// <summary>
    /// Whether to enable logging middleware for request/response tracing.
    /// </summary>
    public bool EnableLogging { get; init; }
}

/// <summary>
/// Configuration for retry middleware with exponential backoff.
/// </summary>
public sealed record RetrySettings
{
    /// <summary>
    /// Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Initial delay before the first retry. Default is 1 second.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries. Default is 30 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Multiplier applied to delay after each retry for exponential backoff. Default is 2.0.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Random jitter factor (0.0-1.0) added to delays to prevent thundering herd. Default is 0.2.
    /// </summary>
    public double JitterFactor { get; init; } = 0.2;
}

/// <summary>
/// Configuration for timeout middleware.
/// </summary>
public sealed record TimeoutSettings
{
    /// <summary>
    /// Maximum duration for agent execution. Default is 30 seconds.
    /// </summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Configuration for circuit breaker middleware.
/// </summary>
public sealed record CircuitBreakerSettings
{
    /// <summary>
    /// Number of failures required to open the circuit. Default is 5.
    /// </summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>
    /// Time window for counting failures. Default is 1 minute.
    /// </summary>
    public TimeSpan FailureWindow { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Duration to keep the circuit open before testing recovery. Default is 30 seconds.
    /// </summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Configuration for bulkhead middleware (concurrency isolation).
/// </summary>
public sealed record BulkheadSettings
{
    /// <summary>
    /// Maximum number of concurrent executions allowed. Default is 10.
    /// </summary>
    public int MaxConcurrency { get; init; } = 10;

    /// <summary>
    /// Maximum number of requests that can wait in queue when at capacity. Default is 0 (no queue).
    /// </summary>
    public int MaxQueueSize { get; init; }
}

/// <summary>
/// Configuration for rate limiting middleware.
/// </summary>
public sealed record RateLimitSettings
{
    /// <summary>
    /// Maximum number of requests allowed within the window. Default is 60.
    /// </summary>
    public int MaxRequests { get; init; } = 60;

    /// <summary>
    /// Time window for rate limiting. Default is 1 minute.
    /// </summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
}
