using Ironbees.Core.Middleware;
using Xunit;

namespace Ironbees.Core.Tests.Middleware;

public class FileSystemTokenUsageStoreTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileSystemTokenUsageStore _store;

    public FileSystemTokenUsageStoreTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ironbees-tests", Guid.NewGuid().ToString("N"));
        _store = new FileSystemTokenUsageStore(_testRoot);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _store.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void Constructor_CreatesRootDirectory()
    {
        Assert.True(Directory.Exists(_testRoot));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPath()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FileSystemTokenUsageStore(null!));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyPath()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FileSystemTokenUsageStore(""));
    }

    [Fact]
    public async Task RecordAsync_CreatesUsageFile()
    {
        // Arrange
        var usage = new TokenUsage
        {
            ModelId = "gpt-4",
            AgentName = "test-agent",
            InputTokens = 100,
            OutputTokens = 50,
            SessionId = "session-1"
        };

        // Act
        await _store.RecordAsync(usage);

        // Assert
        var files = Directory.GetFiles(_testRoot, "*.json", SearchOption.AllDirectories);
        Assert.Single(files);
        Assert.Contains($"{usage.Id}.json", files[0]);
    }

    [Fact]
    public async Task RecordAsync_OrganizesByDate()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var usage = new TokenUsage
        {
            ModelId = "gpt-4",
            AgentName = "test-agent",
            InputTokens = 100,
            OutputTokens = 50,
            Timestamp = timestamp
        };

        // Act
        await _store.RecordAsync(usage);

        // Assert
        var expectedPath = Path.Combine(_testRoot, "2025", "06", "15", $"{usage.Id}.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task RecordBatchAsync_CreatesMultipleFiles()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-3.5", AgentName = "agent2", InputTokens = 200, OutputTokens = 100 },
            new TokenUsage { ModelId = "claude", AgentName = "agent1", InputTokens = 150, OutputTokens = 75 }
        };

        // Act
        await _store.RecordBatchAsync(usages);

        // Assert
        var files = Directory.GetFiles(_testRoot, "*.json", SearchOption.AllDirectories);
        Assert.Equal(3, files.Length);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsAllRecords()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, Timestamp = now },
            new TokenUsage { ModelId = "gpt-3.5", AgentName = "agent2", InputTokens = 200, OutputTokens = 100, Timestamp = now }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var result = await _store.GetUsageAsync(now.AddMinutes(-1), now.AddMinutes(1));

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetUsageAsync_FiltersByDateRange()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var oldUsage = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddDays(-5) };
        var recentUsage = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 200, OutputTokens = 100, Timestamp = now.AddDays(-1) };
        var todayUsage = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 150, OutputTokens = 75, Timestamp = now };
        await _store.RecordBatchAsync([oldUsage, recentUsage, todayUsage]);

        // Act
        var result = await _store.GetUsageAsync(now.AddDays(-2), now.AddDays(1));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, u => u.Id == oldUsage.Id);
    }

    [Fact]
    public async Task GetUsageByAgentAsync_FiltersCorrectly()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent2", InputTokens = 200, OutputTokens = 100 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 150, OutputTokens = 75 }
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
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, SessionId = "session-a" },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 200, OutputTokens = 100, SessionId = "session-b" },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 150, OutputTokens = 75, SessionId = "session-a" }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var result = await _store.GetUsageBySessionAsync("session-a");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.Equal("session-a", u.SessionId));
    }

    [Fact]
    public async Task GetStatisticsAsync_CalculatesCorrectly()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent2", InputTokens = 200, OutputTokens = 100 },
            new TokenUsage { ModelId = "gpt-3.5", AgentName = "agent1", InputTokens = 150, OutputTokens = 75 }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var stats = await _store.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(450, stats.TotalInputTokens);
        Assert.Equal(225, stats.TotalOutputTokens);
        Assert.Equal(2, stats.ByModel.Count);
        Assert.Equal(2, stats.ByAgent.Count);
    }

    [Fact]
    public async Task GetStatisticsAsync_ByModelBreakdown()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 200, OutputTokens = 100 },
            new TokenUsage { ModelId = "gpt-3.5", AgentName = "agent1", InputTokens = 50, OutputTokens = 25 }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        var stats = await _store.GetStatisticsAsync();

        // Assert
        Assert.True(stats.ByModel.ContainsKey("gpt-4"));
        Assert.True(stats.ByModel.ContainsKey("gpt-3.5"));
        Assert.Equal(2, stats.ByModel["gpt-4"].Requests);
        Assert.Equal(300, stats.ByModel["gpt-4"].InputTokens);
        Assert.Equal(1, stats.ByModel["gpt-3.5"].Requests);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllFiles()
    {
        // Arrange
        var usages = new[]
        {
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50 },
            new TokenUsage { ModelId = "gpt-4", AgentName = "agent2", InputTokens = 200, OutputTokens = 100 }
        };
        await _store.RecordBatchAsync(usages);

        // Act
        await _store.ClearAsync();

        // Assert
        var files = Directory.GetFiles(_testRoot, "*.json", SearchOption.AllDirectories);
        Assert.Empty(files);
    }

    [Fact]
    public async Task ClearOlderThanAsync_RemovesOldRecords()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var oldUsage1 = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddDays(-10) };
        var oldUsage2 = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddDays(-8) };
        var recentUsage = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 200, OutputTokens = 100, Timestamp = now.AddDays(-1) };
        await _store.RecordBatchAsync([oldUsage1, oldUsage2, recentUsage]);

        // Act
        var deleted = await _store.ClearOlderThanAsync(now.AddDays(-5));

        // Assert
        Assert.Equal(2, deleted);
        var remaining = await _store.GetUsageAsync(now.AddDays(-2), now.AddDays(1));
        Assert.Single(remaining);
        Assert.Equal(recentUsage.Id, remaining[0].Id);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsOrderedByTimestamp()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var usage1 = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, Timestamp = now };
        var usage2 = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddHours(1) };
        var usage3 = new TokenUsage { ModelId = "gpt-4", AgentName = "agent1", InputTokens = 100, OutputTokens = 50, Timestamp = now.AddHours(2) };
        await _store.RecordBatchAsync([usage3, usage1, usage2]);

        // Act
        var result = await _store.GetUsageAsync(now.AddMinutes(-1), now.AddHours(3));

        // Assert
        Assert.Equal(usage1.Id, result[0].Id);
        Assert.Equal(usage2.Id, result[1].Id);
        Assert.Equal(usage3.Id, result[2].Id);
    }

    [Fact]
    public async Task RecordAsync_ThrowsAfterDispose()
    {
        // Arrange
        _store.Dispose();

        var usage = new TokenUsage
        {
            ModelId = "gpt-4",
            AgentName = "test-agent",
            InputTokens = 100,
            OutputTokens = 50
        };

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _store.RecordAsync(usage));
    }

    [Fact]
    public async Task GetUsageByAgentAsync_IsCaseInsensitive()
    {
        // Arrange
        var usage = new TokenUsage { ModelId = "gpt-4", AgentName = "TestAgent", InputTokens = 100, OutputTokens = 50 };
        await _store.RecordAsync(usage);

        // Act
        var result = await _store.GetUsageByAgentAsync("testagent");

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task GetStatisticsAsync_HandlesEmptyStore()
    {
        // Act
        var stats = await _store.GetStatisticsAsync();

        // Assert
        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.TotalInputTokens);
        Assert.Equal(0, stats.TotalOutputTokens);
        Assert.Empty(stats.ByModel);
        Assert.Empty(stats.ByAgent);
    }

    [Fact]
    public async Task RecordBatchAsync_HandlesEmptyCollection()
    {
        // Act
        await _store.RecordBatchAsync([]);

        // Assert
        var files = Directory.GetFiles(_testRoot, "*.json", SearchOption.AllDirectories);
        Assert.Empty(files);
    }
}
