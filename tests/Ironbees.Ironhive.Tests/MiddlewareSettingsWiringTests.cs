using System.Reflection;
using IronHive.Core.Agent;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Orchestration;

namespace Ironbees.Ironhive.Tests;

/// <summary>
/// Every retry and circuit-breaker setting a caller puts on <see cref="MiddlewareSettings"/> reaches the IronHive
/// middleware. The factory used the short constructors, so only MaxRetries (retry) and FailureThreshold/BreakDuration
/// (circuit breaker) arrived; InitialDelay, MaxDelay, BackoffMultiplier, JitterFactor and FailureWindow were dropped.
/// </summary>
public class MiddlewareSettingsWiringTests
{
    private static T OptionsOf<T>(object middleware) =>
        (T)middleware.GetType().GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(middleware)!;

    [Fact]
    public void Retry_settings_reach_the_retry_middleware()
    {
        var settings = new MiddlewareSettings
        {
            Retry = new RetrySettings
            {
                MaxRetries = 7,
                InitialDelay = TimeSpan.FromMilliseconds(250),
                MaxDelay = TimeSpan.FromSeconds(9),
                BackoffMultiplier = 3.5,
                JitterFactor = 0.05,
            },
        };

        var retry = new IronhiveMiddlewareFactory().Create(settings).OfType<RetryMiddleware>().Single();
        var options = OptionsOf<RetryMiddlewareOptions>(retry);

        Assert.Equal(7, options.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(9), options.MaxDelay);
        Assert.Equal(3.5, options.BackoffMultiplier);
        Assert.Equal(0.05, options.JitterFactor);
    }

    [Fact]
    public void Circuit_breaker_settings_reach_the_circuit_breaker_middleware()
    {
        var settings = new MiddlewareSettings
        {
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = 4,
                FailureWindow = TimeSpan.FromSeconds(20),
                BreakDuration = TimeSpan.FromSeconds(45),
            },
        };

        var breaker = new IronhiveMiddlewareFactory().Create(settings).OfType<CircuitBreakerMiddleware>().Single();
        var options = OptionsOf<CircuitBreakerMiddlewareOptions>(breaker);

        Assert.Equal(4, options.FailureThreshold);
        Assert.Equal(TimeSpan.FromSeconds(20), options.FailureWindow);
        Assert.Equal(TimeSpan.FromSeconds(45), options.BreakDuration);
    }
}
