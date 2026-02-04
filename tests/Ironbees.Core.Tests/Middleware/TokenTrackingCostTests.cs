using Ironbees.Core.Middleware;
using Microsoft.Extensions.AI;
using Moq;
using TokenMeter;

namespace Ironbees.Core.Tests.Middleware;

public class TokenTrackingCostTests
{
    private readonly Mock<IChatClient> _mockInnerClient;
    private readonly InMemoryTokenUsageStore _store;

    public TokenTrackingCostTests()
    {
        _mockInnerClient = new Mock<IChatClient>();
        _store = new InMemoryTokenUsageStore();
    }

    [Fact]
    public async Task WithCostCalculator_RecordsCost()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello"))
        {
            ModelId = "gpt-4o",
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 }
        };

        _mockInnerClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var costCalculator = CostCalculator.Default();
        var options = new TokenTrackingOptions { EnableCostTracking = true };
        var middleware = new TokenTrackingMiddleware(
            _mockInnerClient.Object, _store, options, costCalculator);

        // Act
        await middleware.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        // Assert
        var stats = await _store.GetStatisticsAsync();
        Assert.Equal(1, stats.TotalRequests);
        Assert.True(stats.TotalEstimatedCost > 0, "Expected cost > 0 when cost calculator is provided");

        var usages = await _store.GetUsageAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var usage = Assert.Single(usages);
        Assert.NotNull(usage.EstimatedCost);
        Assert.True(usage.EstimatedCost > 0);
    }

    [Fact]
    public async Task WithoutCostCalculator_NoCost()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello"))
        {
            ModelId = "gpt-4o",
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 }
        };

        _mockInnerClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var middleware = new TokenTrackingMiddleware(_mockInnerClient.Object, _store);

        // Act
        await middleware.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        // Assert
        var usages = await _store.GetUsageAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var usage = Assert.Single(usages);
        Assert.Null(usage.EstimatedCost);
    }

    [Fact]
    public async Task WithCostCalculator_DisabledTracking_NoCost()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello"))
        {
            ModelId = "gpt-4o",
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 }
        };

        _mockInnerClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var costCalculator = CostCalculator.Default();
        var options = new TokenTrackingOptions { EnableCostTracking = false };
        var middleware = new TokenTrackingMiddleware(
            _mockInnerClient.Object, _store, options, costCalculator);

        // Act
        await middleware.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        // Assert
        var usages = await _store.GetUsageAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var usage = Assert.Single(usages);
        Assert.Null(usage.EstimatedCost);
    }

    [Fact]
    public async Task CostAggregation_InStatistics()
    {
        // Arrange
        var costCalculator = CostCalculator.Default();
        var options = new TokenTrackingOptions { EnableCostTracking = true };
        var middleware = new TokenTrackingMiddleware(
            _mockInnerClient.Object, _store, options, costCalculator);

        for (int i = 0; i < 3; i++)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Response {i}"))
            {
                ModelId = "gpt-4o",
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 }
            };

            _mockInnerClient
                .Setup(c => c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            await middleware.GetResponseAsync([new ChatMessage(ChatRole.User, $"Hi {i}")]);
        }

        // Act
        var stats = await _store.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalRequests);
        Assert.True(stats.TotalEstimatedCost > 0);
        Assert.True(stats.ByModel.ContainsKey("gpt-4o"));
        Assert.True(stats.ByModel["gpt-4o"].EstimatedCost > 0);
    }

    [Fact]
    public async Task UnknownModel_CostIsNull()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello"))
        {
            ModelId = "unknown-model-xyz",
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 }
        };

        _mockInnerClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var costCalculator = CostCalculator.Default();
        var options = new TokenTrackingOptions { EnableCostTracking = true };
        var middleware = new TokenTrackingMiddleware(
            _mockInnerClient.Object, _store, options, costCalculator);

        // Act
        await middleware.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        // Assert
        var usages = await _store.GetUsageAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var usage = Assert.Single(usages);
        // CostCalculator returns null for unknown models
        Assert.Null(usage.EstimatedCost);
    }
}
