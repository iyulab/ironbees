using Ironbees.Core.Middleware;
using Xunit;

namespace Ironbees.Core.Tests.Middleware;

public class InMemoryTokenUsageStoreTests
{
    private readonly InMemoryTokenUsageStore _store = new();

    [Fact]
    public async Task RecordAsync_StoresUsage()
    {
        // Arrange
        var usage = new TokenUsage
        {
            ModelId = "gpt-4",
            InputTokens = 100,
            OutputTokens = 50
        };

        // Act
        await _store.RecordAsync(usage);

        // Assert
        Assert.Equal(1, _store.Count);
    }

    [Fact]
    public async Task RecordBatchAsync_StoresMultipleUsages()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-3.5-turbo", InputTokens = 200, OutputTokens = 100 }
        };

        // Act
        await _store.RecordBatchAsync(usages);

        // Assert
        Assert.Equal(2, _store.Count);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsUsagesInTimeRange()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddHours(-2) },
            new TokenUsage { ModelId = "gpt-4", InputTokens = 150, OutputTokens = 75, Timestamp = now.AddHours(-1) },
            new TokenUsage { ModelId = "gpt-4", InputTokens = 200, OutputTokens = 100, Timestamp = now.AddDays(-2) }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var result = await _store.GetUsageAsync(now.AddDays(-1), now);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetUsageByAgentAsync_FiltersCorrectly()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent2", InputTokens = 150, OutputTokens = 75 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 200, OutputTokens = 100 }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var result = await _store.GetUsageByAgentAsync("agent1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.Equal("agent1", u.AgentName));
    }

    [Fact]
    public async Task GetUsageBySessionAsync_FiltersCorrectly()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", SessionId = "session1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", SessionId = "session2", InputTokens = 150, OutputTokens = 75 },
            new TokenUsage { ModelId = "gpt-4", SessionId = "session1", InputTokens = 200, OutputTokens = 100 }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var result = await _store.GetUsageBySessionAsync("session1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.Equal("session1", u.SessionId));
    }

    [Fact]
    public async Task GetStatisticsAsync_CalculatesCorrectly()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 150, OutputTokens = 75 },
            new TokenUsage { ModelId = "gpt-3.5-turbo", AgentName = "agent2", InputTokens = 200, OutputTokens = 100 }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var stats = await _store.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(450, stats.TotalInputTokens);
        Assert.Equal(225, stats.TotalOutputTokens);
        Assert.Equal(675, stats.TotalTokens);
        Assert.Equal(2, stats.ByModel.Count);
        Assert.Equal(2, stats.ByAgent.Count);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllUsages()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", InputTokens = 150, OutputTokens = 75 }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        await _store.ClearAsync();

        // Assert
        Assert.Equal(0, _store.Count);
    }

    [Fact]
    public async Task ClearOlderThanAsync_RemovesOldUsages()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddDays(-5) },
            new TokenUsage { ModelId = "gpt-4", InputTokens = 150, OutputTokens = 75, Timestamp = now.AddDays(-1) },
            new TokenUsage { ModelId = "gpt-4", InputTokens = 200, OutputTokens = 100, Timestamp = now }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var removed = await _store.ClearOlderThanAsync(now.AddDays(-2));

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(2, _store.Count);
    }

    [Fact]
    public void TokenUsage_TotalTokens_CalculatesCorrectly()
    {
        // Arrange
        var usage = new TokenUsage
        {
            ModelId = "gpt-4",
            InputTokens = 100,
            OutputTokens = 50
        };

        // Assert
        Assert.Equal(150, usage.TotalTokens);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithTimeRange_FiltersCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddDays(-5) },
            new TokenUsage { ModelId = "gpt-4", InputTokens = 150, OutputTokens = 75, Timestamp = now.AddDays(-1) },
            new TokenUsage { ModelId = "gpt-4", InputTokens = 200, OutputTokens = 100, Timestamp = now }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var stats = await _store.GetStatisticsAsync(now.AddDays(-2), now.AddDays(1));

        // Assert
        Assert.Equal(2, stats.TotalRequests);
        Assert.Equal(350, stats.TotalInputTokens);
        Assert.Equal(now.AddDays(-2), stats.From);
    }
}
