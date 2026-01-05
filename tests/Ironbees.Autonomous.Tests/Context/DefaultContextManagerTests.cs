using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Context;
using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests.Context;

public class DefaultContextManagerTests
{
    [Fact]
    public async Task RecordOutput_AddsToContext()
    {
        // Arrange
        var manager = DefaultContextManager.Create();

        // Act
        await manager.RecordOutputAsync("Test output", new ContextMetadata { OutputType = "test" });
        var context = await manager.GetRelevantContextAsync("query", 1);

        // Assert
        Assert.Single(context);
        Assert.Equal("Test output", context[0].Content);
        Assert.Equal("test", context[0].Type);
    }

    [Fact]
    public async Task GetRelevantContext_ReturnsRecentItems()
    {
        // Arrange
        var manager = DefaultContextManager.Create();

        // Act - Add 10 items
        for (int i = 1; i <= 10; i++)
        {
            await manager.RecordOutputAsync($"Output {i}");
            await Task.Delay(10); // Ensure different timestamps
        }

        var context = await manager.GetRelevantContextAsync("query", 1);

        // Assert - Should return at most 7 (working memory limit)
        Assert.True(context.Count <= 7);
        Assert.Contains(context, c => c.Content == "Output 10"); // Most recent should be included
    }

    [Fact]
    public async Task ClearSession_RemovesAllContext()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        await manager.RecordOutputAsync("Test output");

        // Act
        await manager.ClearSessionAsync();
        var context = await manager.GetRelevantContextAsync("query", 1);

        // Assert
        Assert.Empty(context);
    }

    [Fact]
    public async Task StoreMemory_CanRetrieve()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        var memory = new MemoryUnit
        {
            Content = "Remember this fact",
            Type = MemoryType.Semantic,
            Importance = 0.8
        };

        // Act
        var id = await manager.StoreAsync(memory);
        var retrieved = await manager.GetByIdAsync(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Remember this fact", retrieved.Content);
    }

    [Fact]
    public async Task RetrieveMemory_FindsByKeyword()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        await manager.StoreAsync(new MemoryUnit { Content = "The elephant is a large mammal" });
        await manager.StoreAsync(new MemoryUnit { Content = "Python is a programming language" });
        await manager.StoreAsync(new MemoryUnit { Content = "Elephants have trunks" });

        // Act
        var results = await manager.RetrieveAsync("elephant", maxResults: 5);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("elephant", r.Content.ToLower()));
    }

    [Fact]
    public async Task DeleteMemory_RemovesFromStore()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        var id = await manager.StoreAsync(new MemoryUnit { Content = "Test memory" });

        // Act
        var deleted = await manager.DeleteAsync(id);
        var retrieved = await manager.GetByIdAsync(id);

        // Assert
        Assert.True(deleted);
        Assert.Null(retrieved);
    }

    [Fact]
    public void RecordUsage_UpdatesSaturation()
    {
        // Arrange
        var manager = DefaultContextManager.Create(opt => opt.Saturation = new SaturationConfig { MaxTokens = 1000 });

        // Act
        manager.RecordUsage(500, "prompt");
        manager.RecordUsage(200, "response");

        // Assert
        Assert.Equal(700, manager.CurrentState.CurrentTokens);
        Assert.Equal(70f, manager.CurrentState.Percentage, 0.1);
    }

    [Fact]
    public void RecordUsage_TriggersLevelChange()
    {
        // Arrange
        var manager = DefaultContextManager.Create(opt => opt.Saturation = new SaturationConfig { MaxTokens = 1000 });
        SaturationLevel? newLevel = null;
        manager.SaturationChanged += (_, e) => newLevel = e.NewLevel;

        // Act - Push to High level (75%+)
        manager.RecordUsage(800, "prompt");

        // Assert
        Assert.Equal(SaturationLevel.High, newLevel);
        Assert.Equal(SaturationLevel.High, manager.CurrentState.Level);
    }

    [Fact]
    public void ResetIteration_ClearsTokenUsage()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        manager.RecordUsage(500, "prompt");

        // Act
        manager.ResetIteration();

        // Assert
        Assert.Equal(0, manager.CurrentState.CurrentTokens);
        Assert.Equal(SaturationLevel.Normal, manager.CurrentState.Level);
    }

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        await manager.StoreAsync(new MemoryUnit { Content = "Memory 1", Tier = MemoryTier.Working });
        await manager.StoreAsync(new MemoryUnit { Content = "Memory 2", Tier = MemoryTier.Session });
        await manager.StoreAsync(new MemoryUnit { Content = "Memory 3", Tier = MemoryTier.Session });

        // Act
        var stats = await manager.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(1, stats.CountByTier[MemoryTier.Working]);
        Assert.Equal(2, stats.CountByTier[MemoryTier.Session]);
    }

    [Fact]
    public async Task UpdateMemory_ModifiesContent()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        var id = await manager.StoreAsync(new MemoryUnit { Content = "Original content" });

        // Act
        await manager.UpdateAsync(id, new MemoryUpdate { Content = "Updated content" });
        var memory = await manager.GetByIdAsync(id);

        // Assert
        Assert.Equal("Updated content", memory?.Content);
    }

    [Fact]
    public async Task MaxMemories_EvictsOldest()
    {
        // Arrange
        var manager = DefaultContextManager.Create(opt => opt.MaxMemories = 3);

        // Act - Add 5 memories
        for (int i = 1; i <= 5; i++)
        {
            await manager.StoreAsync(new MemoryUnit { Content = $"Memory {i}" });
        }

        var stats = await manager.GetStatisticsAsync();

        // Assert - Should only have 3 (max)
        Assert.Equal(3, stats.TotalCount);
    }

    [Fact]
    public async Task GetExecutionSummary_RespectsTokenLimit()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        for (int i = 1; i <= 20; i++)
        {
            await manager.RecordOutputAsync(new string('x', 100)); // ~25 tokens each
        }

        // Act - Limit to 100 tokens (~4 items)
        var summary = await manager.GetExecutionSummaryAsync(maxTokens: 100);

        // Assert - Should be truncated
        var lineCount = summary.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(lineCount <= 5); // Approximate based on token estimation
    }

    [Fact]
    public async Task RetrieveWithFilter_FiltersByTier()
    {
        // Arrange
        var manager = DefaultContextManager.Create();
        await manager.StoreAsync(new MemoryUnit { Content = "Working memory item", Tier = MemoryTier.Working });
        await manager.StoreAsync(new MemoryUnit { Content = "Session memory item", Tier = MemoryTier.Session });
        await manager.StoreAsync(new MemoryUnit { Content = "Long-term memory item", Tier = MemoryTier.LongTerm });

        // Act
        var results = await manager.RetrieveAsync("memory", filter: new MemoryFilter { Tier = MemoryTier.Session });

        // Assert
        Assert.Single(results);
        Assert.Equal(MemoryTier.Session, results[0].Tier);
    }

    [Fact]
    public void ImplementsAllInterfaces()
    {
        // Arrange & Act
        var manager = DefaultContextManager.Create();

        // Assert
        Assert.IsAssignableFrom<IAutonomousContextProvider>(manager);
        Assert.IsAssignableFrom<IAutonomousMemoryStore>(manager);
        Assert.IsAssignableFrom<IContextSaturationMonitor>(manager);
    }

    [Fact]
    public void Builder_CreatesDefaultContextManager_ByDefault()
    {
        // Arrange
        var executor = new MockTaskExecutor();

        // Act - Build without explicitly calling WithDefaultContext()
        var orchestrator = AutonomousOrchestrator.Create<MockRequest, MockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new MockRequest(id, prompt))
            .Build();

        // Assert - Context management should be enabled by default
        Assert.NotNull(orchestrator.ContextProvider);
        Assert.NotNull(orchestrator.MemoryStore);
        Assert.NotNull(orchestrator.SaturationMonitor);
    }

    [Fact]
    public void Builder_WithoutContext_DisablesContextManagement()
    {
        // Arrange
        var executor = new MockTaskExecutor();

        // Act
        var orchestrator = AutonomousOrchestrator.Create<MockRequest, MockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new MockRequest(id, prompt))
            .WithoutContext()
            .Build();

        // Assert
        Assert.Null(orchestrator.ContextProvider);
        Assert.Null(orchestrator.MemoryStore);
        Assert.Null(orchestrator.SaturationMonitor);
    }

    [Fact]
    public void Builder_GetContextProvider_ReturnsDefaultAfterBuild()
    {
        // Arrange
        var executor = new MockTaskExecutor();

        // Act
        var builder = AutonomousOrchestrator.Create<MockRequest, MockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new MockRequest(id, prompt));

        // Before build - null (not yet created)
        Assert.Null(builder.GetContextProvider());

        // Build triggers auto-creation
        var orchestrator = builder.Build();

        // After build - auto-created DefaultContextManager available via orchestrator
        Assert.NotNull(orchestrator.ContextProvider);
    }
}

// Test helpers
file record MockRequest(string RequestId, string Prompt) : ITaskRequest;

file record MockResult(string RequestId) : ITaskResult
{
    public bool Success => true;
    public string Output => "Test output";
    public string? ErrorOutput => null;
}

file class MockTaskExecutor : ITaskExecutor<MockRequest, MockResult>
{
    public Task<MockResult> ExecuteAsync(MockRequest request, Action<TaskOutput>? onOutput = null, CancellationToken cancellationToken = default)
    {
        onOutput?.Invoke(new TaskOutput { RequestId = request.RequestId, Content = "Test output" });
        return Task.FromResult(new MockResult(request.RequestId));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
