using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Context;
using Xunit;

namespace Ironbees.Autonomous.Tests.Context;

public class InMemoryMemoryStoreTests
{
    private static MemoryUnit CreateMemory(
        string content,
        string? id = null,
        MemoryType type = MemoryType.Episodic,
        MemoryTier tier = MemoryTier.Session,
        double importance = 0.5,
        double retention = 1.0,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? lastAccessedAt = null) => new()
    {
        Id = id ?? "",
        Content = content,
        Type = type,
        Tier = tier,
        Importance = importance,
        Retention = retention,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        LastAccessedAt = lastAccessedAt ?? DateTimeOffset.UtcNow
    };

    // Store tests

    [Fact]
    public async Task StoreAsync_EmptyId_ShouldGenerateNewId()
    {
        var store = new InMemoryMemoryStore();
        var memory = CreateMemory("test content");

        var id = await store.StoreAsync(memory, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(id));
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public async Task StoreAsync_WithId_ShouldUseProvidedId()
    {
        var store = new InMemoryMemoryStore();
        var memory = CreateMemory("test content", id: "my-custom-id");

        var id = await store.StoreAsync(memory, TestContext.Current.CancellationToken);

        Assert.Equal("my-custom-id", id);
    }

    [Fact]
    public async Task StoreAsync_ShouldBeRetrievableById()
    {
        var store = new InMemoryMemoryStore();
        var id = await store.StoreAsync(CreateMemory("stored content", id: "abc"), TestContext.Current.CancellationToken);

        var retrieved = await store.GetByIdAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("stored content", retrieved.Content);
    }

    [Fact]
    public async Task StoreAsync_OverCapacity_ShouldEvictOldest()
    {
        var store = new InMemoryMemoryStore(maxMemories: 2);
        var oldest = DateTimeOffset.UtcNow.AddDays(-3);
        var middle = DateTimeOffset.UtcNow.AddDays(-1);
        var newest = DateTimeOffset.UtcNow;

        await store.StoreAsync(CreateMemory("first", id: "m1", lastAccessedAt: oldest), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("second", id: "m2", lastAccessedAt: middle), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("third", id: "m3", lastAccessedAt: newest), TestContext.Current.CancellationToken);

        // Oldest (m1) should have been evicted
        Assert.Null(await store.GetByIdAsync("m1", TestContext.Current.CancellationToken));
        Assert.NotNull(await store.GetByIdAsync("m2", TestContext.Current.CancellationToken));
        Assert.NotNull(await store.GetByIdAsync("m3", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoreAsync_OverCapacity_ShouldEvictByRetentionThenAccess()
    {
        var store = new InMemoryMemoryStore(maxMemories: 2);
        var sameTime = DateTimeOffset.UtcNow.AddDays(-1);

        await store.StoreAsync(CreateMemory("low retention", id: "m1",
            lastAccessedAt: sameTime, retention: 0.1), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("high retention", id: "m2",
            lastAccessedAt: sameTime, retention: 0.9), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("new", id: "m3"), TestContext.Current.CancellationToken);

        // m1 has same LastAccessedAt but lower Retention → evicted first
        Assert.Null(await store.GetByIdAsync("m1", TestContext.Current.CancellationToken));
        Assert.NotNull(await store.GetByIdAsync("m2", TestContext.Current.CancellationToken));
    }

    // GetById tests

    [Fact]
    public async Task GetByIdAsync_NonExisting_ShouldReturnNull()
    {
        var store = new InMemoryMemoryStore();

        var result = await store.GetByIdAsync("nonexistent", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ShouldReturnMemory()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("hello world", id: "test-id"), TestContext.Current.CancellationToken);

        var result = await store.GetByIdAsync("test-id", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("hello world", result.Content);
        Assert.Equal("test-id", result.Id);
    }

    // Retrieve tests

    [Fact]
    public async Task RetrieveAsync_MatchingKeyword_ShouldReturnResults()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("the cat sat on the mat", id: "m1"), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("dogs are friendly animals", id: "m2"), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("cat", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("m1", results[0].Id);
    }

    [Fact]
    public async Task RetrieveAsync_NoMatch_ShouldReturnEmpty()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("hello world", id: "m1"), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("nonexistent", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task RetrieveAsync_MaxResults_ShouldLimit()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("topic alpha one", id: "m1"), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("topic alpha two", id: "m2"), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("topic alpha three", id: "m3"), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("topic alpha", maxResults: 2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task RetrieveAsync_CaseInsensitive_ShouldMatch()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("Important DOCUMENT here", id: "m1"), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("important document", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(results);
    }

    [Fact]
    public async Task RetrieveAsync_WithTypeFilter_ShouldFilterByType()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("episodic data", id: "m1",
            type: MemoryType.Episodic), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("semantic data", id: "m2",
            type: MemoryType.Semantic), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("data", filter: new MemoryFilter { Type = MemoryType.Semantic }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("m2", results[0].Id);
    }

    [Fact]
    public async Task RetrieveAsync_WithTierFilter_ShouldFilterByTier()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("working memory data", id: "m1",
            tier: MemoryTier.Working), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("session memory data", id: "m2",
            tier: MemoryTier.Session), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("memory data", filter: new MemoryFilter { Tier = MemoryTier.Working }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("m1", results[0].Id);
    }

    [Fact]
    public async Task RetrieveAsync_WithMinImportanceFilter_ShouldFilter()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("low importance data", id: "m1",
            importance: 0.2), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("high importance data", id: "m2",
            importance: 0.9), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("importance data", filter: new MemoryFilter { MinImportance = 0.5 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("m2", results[0].Id);
    }

    // Update tests

    [Fact]
    public async Task UpdateAsync_Existing_ShouldUpdateContent()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("original", id: "m1"), TestContext.Current.CancellationToken);

        var result = await store.UpdateAsync("m1", new MemoryUpdate { Content = "updated" }, TestContext.Current.CancellationToken);

        Assert.True(result);
        var memory = await store.GetByIdAsync("m1", TestContext.Current.CancellationToken);
        Assert.Equal("updated", memory!.Content);
    }

    [Fact]
    public async Task UpdateAsync_NonExisting_ShouldReturnFalse()
    {
        var store = new InMemoryMemoryStore();

        var result = await store.UpdateAsync("nonexistent", new MemoryUpdate { Content = "updated" }, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAsync_PartialUpdate_ShouldPreserveOtherFields()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("original", id: "m1",
            importance: 0.8, tier: MemoryTier.Working), TestContext.Current.CancellationToken);

        await store.UpdateAsync("m1", new MemoryUpdate { Content = "new content" }, TestContext.Current.CancellationToken);

        var memory = await store.GetByIdAsync("m1", TestContext.Current.CancellationToken);
        Assert.Equal("new content", memory!.Content);
        Assert.Equal(0.8, memory.Importance);
        Assert.Equal(MemoryTier.Working, memory.Tier);
    }

    [Fact]
    public async Task UpdateAsync_RecordAccess_ShouldIncrementAccessCount()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("content", id: "m1"), TestContext.Current.CancellationToken);

        await store.UpdateAsync("m1", new MemoryUpdate { RecordAccess = true }, TestContext.Current.CancellationToken);

        var memory = await store.GetByIdAsync("m1", TestContext.Current.CancellationToken);
        Assert.Equal(2, memory!.AccessCount);
    }

    [Fact]
    public async Task UpdateAsync_MergeMetadata_ShouldCombine()
    {
        var store = new InMemoryMemoryStore();
        var original = new MemoryUnit
        {
            Id = "m1",
            Content = "content",
            Metadata = new Dictionary<string, object> { ["key1"] = "val1" }
        };
        await store.StoreAsync(original, TestContext.Current.CancellationToken);

        await store.UpdateAsync("m1", new MemoryUpdate
        {
            Metadata = new Dictionary<string, object> { ["key2"] = "val2" }
        }, TestContext.Current.CancellationToken);

        var memory = await store.GetByIdAsync("m1", TestContext.Current.CancellationToken);
        Assert.NotNull(memory!.Metadata);
        Assert.Equal("val1", memory.Metadata["key1"]);
        Assert.Equal("val2", memory.Metadata["key2"]);
    }

    [Fact]
    public async Task UpdateAsync_MergeMetadata_ShouldOverwriteExistingKeys()
    {
        var store = new InMemoryMemoryStore();
        var original = new MemoryUnit
        {
            Id = "m1",
            Content = "content",
            Metadata = new Dictionary<string, object> { ["key1"] = "old" }
        };
        await store.StoreAsync(original, TestContext.Current.CancellationToken);

        await store.UpdateAsync("m1", new MemoryUpdate
        {
            Metadata = new Dictionary<string, object> { ["key1"] = "new" }
        }, TestContext.Current.CancellationToken);

        var memory = await store.GetByIdAsync("m1", TestContext.Current.CancellationToken);
        Assert.Equal("new", memory!.Metadata!["key1"]);
    }

    // Delete tests

    [Fact]
    public async Task DeleteAsync_Existing_ShouldReturnTrue()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("content", id: "m1"), TestContext.Current.CancellationToken);

        var result = await store.DeleteAsync("m1", TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Null(await store.GetByIdAsync("m1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_NonExisting_ShouldReturnFalse()
    {
        var store = new InMemoryMemoryStore();

        var result = await store.DeleteAsync("nonexistent", TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    // Statistics tests

    [Fact]
    public async Task GetStatisticsAsync_Empty_ShouldReturnZeros()
    {
        var store = new InMemoryMemoryStore();

        var stats = await store.GetStatisticsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, stats.TotalCount);
        Assert.Equal(0, stats.EstimatedTokens);
        Assert.Equal(0, stats.AverageRetention);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithMemories_ShouldReturnCorrectCounts()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("working memory one", id: "m1",
            tier: MemoryTier.Working, retention: 0.8), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("session memory one", id: "m2",
            tier: MemoryTier.Session, retention: 0.6), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("working memory two", id: "m3",
            tier: MemoryTier.Working, retention: 1.0), TestContext.Current.CancellationToken);

        var stats = await store.GetStatisticsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(2, stats.CountByTier[MemoryTier.Working]);
        Assert.Equal(1, stats.CountByTier[MemoryTier.Session]);
        Assert.True(stats.EstimatedTokens > 0);
        Assert.Equal(0.8, stats.AverageRetention, 0.01);
    }

    // Filter edge cases

    [Fact]
    public async Task RetrieveAsync_NullFilter_ShouldReturnAllMatching()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("data alpha", id: "m1"), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("data beta", id: "m2"), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("data", filter: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task RetrieveAsync_MinRetentionFilter_ShouldFilter()
    {
        var store = new InMemoryMemoryStore();
        await store.StoreAsync(CreateMemory("low retention data", id: "m1",
            retention: 0.2), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("high retention data", id: "m2",
            retention: 0.9), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("retention data", filter: new MemoryFilter { MinRetention = 0.5 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("m2", results[0].Id);
    }

    [Fact]
    public async Task RetrieveAsync_CreatedAfterFilter_ShouldFilter()
    {
        var store = new InMemoryMemoryStore();
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        var tomorrow = DateTimeOffset.UtcNow.AddDays(1);

        await store.StoreAsync(CreateMemory("old data", id: "m1",
            createdAt: yesterday), TestContext.Current.CancellationToken);
        await store.StoreAsync(CreateMemory("new data", id: "m2",
            createdAt: tomorrow), TestContext.Current.CancellationToken);

        var results = await store.RetrieveAsync("data", filter: new MemoryFilter { CreatedAfter = DateTimeOffset.UtcNow }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("m2", results[0].Id);
    }
}
