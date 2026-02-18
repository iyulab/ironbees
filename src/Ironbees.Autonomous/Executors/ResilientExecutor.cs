using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Executors;

/// <summary>
/// Wraps an executor with retry logic and exponential backoff.
/// Configuration-driven resilience for autonomous execution.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
public class ResilientExecutor<TRequest, TResult> : ITaskExecutor<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : class, ITaskResult
{
    private readonly ITaskExecutor<TRequest, TResult> _inner;
    private readonly ResilienceSettings _settings;
    private readonly IFallbackStrategy<TRequest, TResult>? _fallbackStrategy;
    private readonly List<string> _previousOutputs = [];

    public ResilientExecutor(
        ITaskExecutor<TRequest, TResult> inner,
        ResilienceSettings? settings = null,
        IFallbackStrategy<TRequest, TResult>? fallbackStrategy = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _settings = settings ?? ResilienceSettings.Default;
        _fallbackStrategy = fallbackStrategy;
    }

    public async Task<TResult> ExecuteAsync(
        TRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                var result = await _inner.ExecuteAsync(request, onOutput, cancellationToken);

                if (result != null && IsValidResult(result))
                {
                    _previousOutputs.Add(result.Output);
                    return result;
                }

                // Empty or invalid response
                if (attempt < _settings.MaxRetries)
                {
                    await DelayWithBackoff(attempt, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < _settings.MaxRetries)
                {
                    await DelayWithBackoff(attempt, cancellationToken);
                }
            }
        }

        // All retries failed, try fallback
        if (_fallbackStrategy != null)
        {
            var context = new FallbackContext<TRequest>
            {
                FailedRequest = request,
                Iteration = _previousOutputs.Count + 1,
                RetryAttempts = _settings.MaxRetries,
                ErrorMessage = lastException?.Message,
                PreviousOutputs = _previousOutputs.AsReadOnly()
            };

            if (_fallbackStrategy.CanProvideFallback(context))
            {
                var fallbackResult = await _fallbackStrategy.GetFallbackAsync(context, cancellationToken);
                if (fallbackResult != null)
                {
                    _previousOutputs.Add(fallbackResult.Output);
                    return fallbackResult;
                }
            }
        }

        throw new ExecutionFailedException(
            $"Execution failed after {_settings.MaxRetries} retries",
            lastException);
    }

    /// <summary>
    /// Check if result is valid (non-empty output)
    /// Override for custom validation logic
    /// </summary>
    protected virtual bool IsValidResult(TResult result)
    {
        return result.Success && !string.IsNullOrWhiteSpace(result.Output);
    }

    private async Task DelayWithBackoff(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(
            _settings.InitialDelayMs * Math.Pow(_settings.BackoffMultiplier, attempt - 1));

        if (delay > _settings.MaxDelay)
            delay = _settings.MaxDelay;

        await Task.Delay(delay, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _inner.DisposeAsync();
    }
}

/// <summary>
/// Resilience settings for ResilientExecutor
/// </summary>
public record ResilienceSettings
{
    /// <summary>Maximum retry attempts</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Initial delay in milliseconds</summary>
    public int InitialDelayMs { get; init; } = 500;

    /// <summary>Backoff multiplier (exponential)</summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>Maximum delay between retries</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Default settings</summary>
    public static ResilienceSettings Default { get; } = new();

    /// <summary>Create from YAML configuration</summary>
    public static ResilienceSettings FromConfig(ResilienceConfig? config)
    {
        if (config == null) return Default;

        return new ResilienceSettings
        {
            MaxRetries = config.MaxRetries,
            InitialDelayMs = config.InitialDelayMs,
            BackoffMultiplier = config.BackoffMultiplier,
            MaxDelay = TimeSpan.FromSeconds(config.MaxDelaySeconds)
        };
    }
}

/// <summary>
/// YAML configuration for resilience
/// </summary>
public record ResilienceConfig
{
    public int MaxRetries { get; init; } = 3;
    public int InitialDelayMs { get; init; } = 500;
    public double BackoffMultiplier { get; init; } = 2.0;
    public int MaxDelaySeconds { get; init; } = 10;
}

/// <summary>
/// Exception thrown when execution fails after all retries
/// </summary>
public class ExecutionFailedException : Exception
{
    public ExecutionFailedException(string message, Exception? inner = null)
        : base(message, inner) { }
}
