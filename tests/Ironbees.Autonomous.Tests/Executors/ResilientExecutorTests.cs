using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Executors;
using Xunit;

namespace Ironbees.Autonomous.Tests.Executors;

public class ResilientExecutorTests
{
    // --- Test helpers ---

    private sealed record TestRequest(string RequestId, string Prompt) : ITaskRequest;

    private sealed record TestResult(string RequestId, bool Success, string Output, string? ErrorOutput = null) : ITaskResult;

    private sealed class CallbackExecutor(Func<TestRequest, Task<TestResult>> callback)
        : ITaskExecutor<TestRequest, TestResult>
    {
        public Task<TestResult> ExecuteAsync(
            TestRequest request, Action<TaskOutput>? onOutput = null,
            CancellationToken cancellationToken = default)
            => callback(request);

        public ValueTask DisposeAsync() => default;
    }

    private sealed class CountingExecutor : ITaskExecutor<TestRequest, TestResult>
    {
        private readonly Func<int, TestRequest, TestResult> _factory;
        public int CallCount { get; private set; }

        public CountingExecutor(Func<int, TestRequest, TestResult> factory) => _factory = factory;

        public Task<TestResult> ExecuteAsync(
            TestRequest request, Action<TaskOutput>? onOutput = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_factory(CallCount, request));
        }

        public ValueTask DisposeAsync() => default;
    }

    private sealed class ThrowingExecutor : ITaskExecutor<TestRequest, TestResult>
    {
        private readonly int _succeedOnAttempt;
        public int CallCount { get; private set; }

        public ThrowingExecutor(int succeedOnAttempt = int.MaxValue) => _succeedOnAttempt = succeedOnAttempt;

        public Task<TestResult> ExecuteAsync(
            TestRequest request, Action<TaskOutput>? onOutput = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount < _succeedOnAttempt)
                throw new InvalidOperationException($"Attempt {CallCount} failed");
            return Task.FromResult(new TestResult(request.RequestId, true, $"Success on attempt {CallCount}"));
        }

        public ValueTask DisposeAsync() => default;
    }

    private sealed class SimpleFallback : IFallbackStrategy<TestRequest, TestResult>
    {
        private readonly bool _canProvide;
        private readonly TestResult? _result;

        public SimpleFallback(bool canProvide, TestResult? result = null)
        {
            _canProvide = canProvide;
            _result = result;
        }

        public bool CanProvideFallback(FallbackContext<TestRequest> context) => _canProvide;

        public Task<TestResult?> GetFallbackAsync(
            FallbackContext<TestRequest> context, CancellationToken ct = default)
            => Task.FromResult(_result);
    }

    private static ResilienceSettings FastSettings(int maxRetries = 3) => new()
    {
        MaxRetries = maxRetries,
        InitialDelayMs = 1,
        BackoffMultiplier = 1.0,
        MaxDelay = TimeSpan.FromMilliseconds(5)
    };

    private static TestRequest DefaultRequest => new("req-1", "test prompt");

    // --- Constructor ---

    [Fact]
    public void Constructor_NullInner_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ResilientExecutor<TestRequest, TestResult>(null!));
    }

    // --- Execute success ---

    [Fact]
    public async Task Execute_SuccessFirstAttempt_ShouldReturnResult()
    {
        var inner = new CallbackExecutor(_ =>
            Task.FromResult(new TestResult("req-1", true, "ok")));
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings());

        var result = await executor.ExecuteAsync(DefaultRequest);

        Assert.True(result.Success);
        Assert.Equal("ok", result.Output);
    }

    // --- Retry on exception ---

    [Fact]
    public async Task Execute_FailThenSucceed_ShouldRetry()
    {
        var inner = new ThrowingExecutor(succeedOnAttempt: 2);
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings());

        var result = await executor.ExecuteAsync(DefaultRequest);

        Assert.True(result.Success);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Execute_AllFail_ShouldThrowExecutionFailed()
    {
        var inner = new ThrowingExecutor(); // never succeeds
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings(maxRetries: 2));

        await Assert.ThrowsAsync<ExecutionFailedException>(() =>
            executor.ExecuteAsync(DefaultRequest));

        Assert.Equal(2, inner.CallCount);
    }

    // --- Retry on invalid result ---

    [Fact]
    public async Task Execute_InvalidResultThenValid_ShouldRetry()
    {
        var inner = new CountingExecutor((count, req) =>
            count < 2
                ? new TestResult(req.RequestId, false, "") // invalid (Success=false)
                : new TestResult(req.RequestId, true, "valid output"));
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings());

        var result = await executor.ExecuteAsync(DefaultRequest);

        Assert.True(result.Success);
        Assert.Equal("valid output", result.Output);
        Assert.Equal(2, inner.CallCount);
    }

    // --- Fallback ---

    [Fact]
    public async Task Execute_AllFailWithFallback_ShouldUseFallback()
    {
        var inner = new ThrowingExecutor();
        var fallbackResult = new TestResult("req-1", true, "fallback answer");
        var fallback = new SimpleFallback(canProvide: true, result: fallbackResult);
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings(maxRetries: 1), fallback);

        var result = await executor.ExecuteAsync(DefaultRequest);

        Assert.Equal("fallback answer", result.Output);
    }

    [Fact]
    public async Task Execute_FallbackCannotProvide_ShouldThrow()
    {
        var inner = new ThrowingExecutor();
        var fallback = new SimpleFallback(canProvide: false);
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings(maxRetries: 1), fallback);

        await Assert.ThrowsAsync<ExecutionFailedException>(() =>
            executor.ExecuteAsync(DefaultRequest));
    }

    [Fact]
    public async Task Execute_FallbackReturnsNull_ShouldThrow()
    {
        var inner = new ThrowingExecutor();
        var fallback = new SimpleFallback(canProvide: true, result: null);
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings(maxRetries: 1), fallback);

        await Assert.ThrowsAsync<ExecutionFailedException>(() =>
            executor.ExecuteAsync(DefaultRequest));
    }

    // --- Cancellation ---

    [Fact]
    public async Task Execute_OperationCanceled_ShouldPropagateWithoutRetry()
    {
        // OperationCanceledException should be propagated directly, not retried
        var inner = new CallbackExecutor(_ =>
            Task.FromException<TestResult>(new OperationCanceledException("cancelled")));
        var executor = new ResilientExecutor<TestRequest, TestResult>(inner, FastSettings());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(DefaultRequest));
    }

    // --- ResilienceSettings ---

    [Fact]
    public void ResilienceSettings_Default_ShouldBeCorrect()
    {
        var settings = ResilienceSettings.Default;

        Assert.Equal(3, settings.MaxRetries);
        Assert.Equal(500, settings.InitialDelayMs);
        Assert.Equal(2.0, settings.BackoffMultiplier);
        Assert.Equal(TimeSpan.FromSeconds(10), settings.MaxDelay);
    }

    [Fact]
    public void ResilienceSettings_FromConfig_ShouldMap()
    {
        var config = new ResilienceConfig
        {
            MaxRetries = 5,
            InitialDelayMs = 200,
            BackoffMultiplier = 1.5,
            MaxDelaySeconds = 30
        };

        var settings = ResilienceSettings.FromConfig(config);

        Assert.Equal(5, settings.MaxRetries);
        Assert.Equal(200, settings.InitialDelayMs);
        Assert.Equal(1.5, settings.BackoffMultiplier);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.MaxDelay);
    }

    [Fact]
    public void ResilienceSettings_FromNullConfig_ShouldReturnDefault()
    {
        var settings = ResilienceSettings.FromConfig(null);

        Assert.Equal(ResilienceSettings.Default.MaxRetries, settings.MaxRetries);
    }

    // --- ExecutionFailedException ---

    [Fact]
    public void ExecutionFailedException_ShouldPreserveInnerException()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new ExecutionFailedException("Execution failed", inner);

        Assert.Equal("Execution failed", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
