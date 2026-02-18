using Ironbees.Core.Middleware;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Ironbees.Core.Tests.Middleware;

public class RateLimitingMiddlewareTests
{
    private readonly IChatClient _mockInnerClient;
    private readonly ChatResponse _defaultResponse;

    public RateLimitingMiddlewareTests()
    {
        _mockInnerClient = Substitute.For<IChatClient>();
        _defaultResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response"));
    }

    [Fact]
    public async Task GetResponseAsync_UnderLimit_ReturnsResponse()
    {
        // Arrange
        _mockInnerClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_defaultResponse);

        var options = new RateLimitOptions { RequestsPerMinute = 100, TokensPerMinute = 100000 };
        var middleware = new RateLimitingMiddleware(_mockInnerClient, options);

        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act
        var response = await middleware.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        await _mockInnerClient.Received(1).GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponseAsync_WithRejectStrategy_ThrowsWhenLimitExceeded()
    {
        // Arrange
        _mockInnerClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_defaultResponse);

        var options = new RateLimitOptions
        {
            RequestsPerMinute = 2,
            TokensPerMinute = 0, // Disable token limit
            Strategy = RateLimitStrategy.Reject
        };
        var middleware = new RateLimitingMiddleware(_mockInnerClient, options);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Make initial requests to hit the limit
        await middleware.GetResponseAsync(messages);
        await middleware.GetResponseAsync(messages);

        // Act & Assert
        await Assert.ThrowsAsync<RateLimitExceededException>(() =>
            middleware.GetResponseAsync(messages));
    }

    [Fact]
    public async Task GetResponseAsync_WithQueueStrategy_WaitsAndSucceeds()
    {
        // Arrange
        _mockInnerClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_defaultResponse);

        var options = new RateLimitOptions
        {
            RequestsPerMinute = 100,
            TokensPerMinute = 0,
            Strategy = RateLimitStrategy.Queue
        };
        var middleware = new RateLimitingMiddleware(_mockInnerClient, options);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act - Multiple requests should succeed
        var tasks = new List<Task<ChatResponse>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(middleware.GetResponseAsync(messages));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, responses.Length);
        Assert.All(responses, r => Assert.NotNull(r));
    }

    [Fact]
    public async Task GetResponseAsync_ConcurrentRequests_RespectsMaxConcurrent()
    {
        // Arrange
        var callCount = 0;
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        _mockInnerClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                lock (lockObj)
                {
                    currentConcurrent++;
                    if (currentConcurrent > maxConcurrent)
                        maxConcurrent = currentConcurrent;
                    callCount++;
                }

                await Task.Delay(50); // Simulate some processing time

                lock (lockObj)
                {
                    currentConcurrent--;
                }

                return _defaultResponse;
            });

        var options = new RateLimitOptions
        {
            RequestsPerMinute = 1000,
            TokensPerMinute = 0,
            MaxConcurrentRequests = 3
        };
        var middleware = new RateLimitingMiddleware(_mockInnerClient, options);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act - Launch 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => middleware.GetResponseAsync(messages))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, callCount);
        Assert.True(maxConcurrent <= 3, $"Max concurrent was {maxConcurrent}, expected <= 3");
    }

    [Fact]
    public void RateLimitOptions_Development_HasExpectedValues()
    {
        // Act
        var options = RateLimitOptions.Development;

        // Assert
        Assert.Equal(100, options.RequestsPerMinute);
        Assert.Equal(200000, options.TokensPerMinute);
        Assert.Equal(20, options.MaxConcurrentRequests);
        Assert.Equal(RateLimitStrategy.Queue, options.Strategy);
    }

    [Fact]
    public void RateLimitOptions_Production_HasExpectedValues()
    {
        // Act
        var options = RateLimitOptions.Production;

        // Assert
        Assert.Equal(50, options.RequestsPerMinute);
        Assert.Equal(100000, options.TokensPerMinute);
        Assert.Equal(10, options.MaxConcurrentRequests);
        Assert.Equal(RateLimitStrategy.Queue, options.Strategy);
    }

    [Fact]
    public void RateLimitOptions_Strict_HasExpectedValues()
    {
        // Act
        var options = RateLimitOptions.Strict;

        // Assert
        Assert.Equal(20, options.RequestsPerMinute);
        Assert.Equal(40000, options.TokensPerMinute);
        Assert.Equal(5, options.MaxConcurrentRequests);
        Assert.Equal(RateLimitStrategy.Reject, options.Strategy);
    }

    [Fact]
    public void RateLimitExceededException_ContainsCorrectInfo()
    {
        // Act
        var exception = new RateLimitExceededException("Requests per minute", 50, 40);

        // Assert
        Assert.Equal("Requests per minute", exception.LimitType);
        Assert.Equal(50, exception.CurrentUsage);
        Assert.Equal(40, exception.Limit);
        Assert.Contains("Requests per minute", exception.Message);
        Assert.Contains("50", exception.Message);
        Assert.Contains("40", exception.Message);
    }

    [Fact]
    public async Task GetResponseAsync_WithTokenUsage_TracksTokens()
    {
        // Arrange
        var responseWithUsage = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Response"))
        {
            Usage = new UsageDetails
            {
                InputTokenCount = 10,
                OutputTokenCount = 20
            }
        };

        _mockInnerClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(responseWithUsage);

        var options = new RateLimitOptions
        {
            RequestsPerMinute = 1000,
            TokensPerMinute = 90, // Low limit to test tracking (each request uses 30 tokens)
            Strategy = RateLimitStrategy.Reject
        };
        var middleware = new RateLimitingMiddleware(_mockInnerClient, options);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Make requests until token limit is exceeded (each uses 30 tokens)
        await middleware.GetResponseAsync(messages); // 30 tokens
        await middleware.GetResponseAsync(messages); // 60 tokens
        await middleware.GetResponseAsync(messages); // 90 tokens

        // Act & Assert - Next request should exceed 90 token limit (90 >= 90)
        await Assert.ThrowsAsync<RateLimitExceededException>(() =>
            middleware.GetResponseAsync(messages));
    }

    [Fact]
    public async Task GetResponseAsync_WithNullOptions_UsesDefaults()
    {
        // Arrange
        _mockInnerClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_defaultResponse);

        var middleware = new RateLimitingMiddleware(_mockInnerClient);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act
        var response = await middleware.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetResponseAsync_WithCancellation_PropagatesToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockInnerClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_defaultResponse);

        var middleware = new RateLimitingMiddleware(_mockInnerClient);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            middleware.GetResponseAsync(messages, null, cts.Token));
    }
}
