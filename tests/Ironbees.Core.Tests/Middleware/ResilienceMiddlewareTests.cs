using Ironbees.Core.Middleware;
using Microsoft.Extensions.AI;
using NSubstitute;
using Polly;

namespace Ironbees.Core.Tests.Middleware;

public class ResilienceMiddlewareTests
{
    private readonly IChatClient _innerClient;
    private readonly ChatResponse _defaultResponse;

    public ResilienceMiddlewareTests()
    {
        _innerClient = Substitute.For<IChatClient>();
        _defaultResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response"));

        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_defaultResponse);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullPipeline_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ResilienceMiddleware(_innerClient, (ResiliencePipeline<ChatResponse>)null!));
    }

    [Fact]
    public void Constructor_DefaultOptions_ShouldNotThrow()
    {
        var middleware = new ResilienceMiddleware(_innerClient);
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Constructor_CustomOptions_ShouldNotThrow()
    {
        var middleware = new ResilienceMiddleware(_innerClient, new ResilienceOptions
        {
            MaxRetryAttempts = 5,
            TimeoutSeconds = 60,
            EnableCircuitBreaker = false
        });
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Constructor_EmptyPipeline_ShouldNotThrow()
    {
        var pipeline = ResiliencePipeline<ChatResponse>.Empty;
        var middleware = new ResilienceMiddleware(_innerClient, pipeline);
        Assert.NotNull(middleware);
    }

    // --- GetResponseAsync ---

    [Fact]
    public async Task GetResponse_ShouldDelegateToInnerClient()
    {
        var middleware = CreateMiddleware(noResilience: true);

        var response = await middleware.GetResponseAsync(Messages("Hello"));

        Assert.Equal("Test response", response.Messages[0].Text);
        await _innerClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponse_EmptyPipeline_ShouldPassThrough()
    {
        var pipeline = ResiliencePipeline<ChatResponse>.Empty;
        var middleware = new ResilienceMiddleware(_innerClient, pipeline);

        var response = await middleware.GetResponseAsync(Messages("Hello"));

        Assert.Equal("Test response", response.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponse_WithRetry_ShouldRetryOnHttpException()
    {
        var callCount = 0;
        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount <= 1)
                    throw new HttpRequestException("Transient error");
                return _defaultResponse;
            });

        var middleware = CreateMiddleware(retry: true);

        var response = await middleware.GetResponseAsync(Messages("Hello"));

        Assert.Equal("Test response", response.Messages[0].Text);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetResponse_WithRetry_ShouldRetryOnTaskCanceledException()
    {
        var callCount = 0;
        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount <= 1)
                    throw new TaskCanceledException("Timeout");
                return _defaultResponse;
            });

        var middleware = CreateMiddleware(retry: true);

        await middleware.GetResponseAsync(Messages("Hello"));

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetResponse_NoRetry_ShouldThrowOnException()
    {
        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns<ChatResponse>(_ => throw new HttpRequestException("Error"));

        var middleware = CreateMiddleware(noResilience: true);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            middleware.GetResponseAsync(Messages("Hello")));
    }

    [Fact]
    public async Task GetResponse_ExhaustedRetries_ShouldThrow()
    {
        _innerClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns<ChatResponse>(_ => throw new HttpRequestException("Always fails"));

        var middleware = CreateMiddleware(retry: true);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            middleware.GetResponseAsync(Messages("Hello")));
    }

    // --- ResilienceOptions presets ---

    [Fact]
    public void ResilienceOptions_Defaults_ShouldBeCorrect()
    {
        var options = new ResilienceOptions();

        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(1000, options.RetryDelayMilliseconds);
        Assert.True(options.UseExponentialBackoff);
        Assert.Equal(120, options.TimeoutSeconds);
        Assert.True(options.EnableCircuitBreaker);
        Assert.Equal(0.5, options.CircuitBreakerFailureRatio);
        Assert.Equal(30, options.CircuitBreakerSamplingDurationSeconds);
        Assert.Equal(10, options.CircuitBreakerMinimumThroughput);
        Assert.Equal(30, options.CircuitBreakerDurationSeconds);
    }

    [Fact]
    public void ResilienceOptions_Development_ShouldHaveCorrectValues()
    {
        var options = ResilienceOptions.Development;

        Assert.Equal(1, options.MaxRetryAttempts);
        Assert.Equal(500, options.RetryDelayMilliseconds);
        Assert.Equal(60, options.TimeoutSeconds);
        Assert.False(options.EnableCircuitBreaker);
    }

    [Fact]
    public void ResilienceOptions_Production_ShouldHaveCorrectValues()
    {
        var options = ResilienceOptions.Production;

        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(1000, options.RetryDelayMilliseconds);
        Assert.True(options.UseExponentialBackoff);
        Assert.Equal(120, options.TimeoutSeconds);
        Assert.True(options.EnableCircuitBreaker);
        Assert.Equal(0.5, options.CircuitBreakerFailureRatio);
        Assert.Equal(30, options.CircuitBreakerSamplingDurationSeconds);
        Assert.Equal(10, options.CircuitBreakerMinimumThroughput);
        Assert.Equal(30, options.CircuitBreakerDurationSeconds);
    }

    // --- Helpers ---

    private ResilienceMiddleware CreateMiddleware(bool noResilience = false, bool retry = false)
    {
        if (noResilience)
        {
            return new ResilienceMiddleware(_innerClient, new ResilienceOptions
            {
                MaxRetryAttempts = 0,
                TimeoutSeconds = 0,
                EnableCircuitBreaker = false
            });
        }

        if (retry)
        {
            return new ResilienceMiddleware(_innerClient, new ResilienceOptions
            {
                MaxRetryAttempts = 2,
                RetryDelayMilliseconds = 1,
                UseExponentialBackoff = false,
                TimeoutSeconds = 0,
                EnableCircuitBreaker = false
            });
        }

        return new ResilienceMiddleware(_innerClient);
    }

    private static List<ChatMessage> Messages(string text)
    {
        return [new ChatMessage(ChatRole.User, text)];
    }
}
