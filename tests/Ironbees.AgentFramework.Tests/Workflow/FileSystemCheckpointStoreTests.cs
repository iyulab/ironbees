using Ironbees.AgentFramework.Workflow;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// Unit tests for FileSystemCheckpointStore.
/// Tests verify checkpoint storage, retrieval, and cleanup operations.
/// </summary>
public class FileSystemCheckpointStoreTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly ILogger<FileSystemCheckpointStore> _mockLogger;
    private readonly FileSystemCheckpointStore _store;

    public FileSystemCheckpointStoreTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"ironbees-checkpoint-tests-{Guid.NewGuid()}");
        _mockLogger = Substitute.For<ILogger<FileSystemCheckpointStore>>();
        _store = new FileSystemCheckpointStore(_testRootPath, logger: _mockLogger);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _store.Dispose();

        // Clean up test directory
        if (Directory.Exists(_testRootPath))
        {
            try
            {
                Directory.Delete(_testRootPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPath_CreatesDirectory()
    {
        // Arrange & Act
        var rootPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");
        using var store = new FileSystemCheckpointStore(rootPath);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(rootPath, "checkpoints")));

        // Cleanup
        Directory.Delete(rootPath, recursive: true);
    }

    [Fact]
    public void Constructor_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        Assert.Throws<ArgumentNullException>(() => new FileSystemCheckpointStore(null!));
    }

    [Fact]
    public void Constructor_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new FileSystemCheckpointStore(""));
    }

    [Fact]
    public void Constructor_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new FileSystemCheckpointStore("   "));
    }

    #endregion

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_WithValidCheckpoint_CreatesFile()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();

        // Act
        await _store.SaveAsync(checkpoint);

        // Assert
        var expectedPath = Path.Combine(
            _testRootPath,
            "checkpoints",
            checkpoint.ExecutionId,
            $"{checkpoint.CheckpointId}.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task SaveAsync_WithNullCheckpoint_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveAsync(null!));
    }

    [Fact]
    public async Task SaveAsync_MultipleCheckpoints_SavesAll()
    {
        // Arrange
        var checkpoint1 = CreateTestCheckpoint("checkpoint-1", "execution-1");
        var checkpoint2 = CreateTestCheckpoint("checkpoint-2", "execution-1");
        var checkpoint3 = CreateTestCheckpoint("checkpoint-3", "execution-2");

        // Act
        await _store.SaveAsync(checkpoint1);
        await _store.SaveAsync(checkpoint2);
        await _store.SaveAsync(checkpoint3);

        // Assert
        Assert.True(await _store.ExistsAsync("checkpoint-1"));
        Assert.True(await _store.ExistsAsync("checkpoint-2"));
        Assert.True(await _store.ExistsAsync("checkpoint-3"));
    }

    [Fact]
    public async Task SaveAsync_SameCheckpointTwice_OverwritesFile()
    {
        // Arrange
        var checkpoint1 = CreateTestCheckpoint("checkpoint-1", "execution-1") with
        {
            CurrentStateId = "STATE_A"
        };
        var checkpoint2 = CreateTestCheckpoint("checkpoint-1", "execution-1") with
        {
            CurrentStateId = "STATE_B"
        };

        // Act
        await _store.SaveAsync(checkpoint1);
        await _store.SaveAsync(checkpoint2);

        // Assert
        var retrieved = await _store.GetAsync("checkpoint-1");
        Assert.NotNull(retrieved);
        Assert.Equal("STATE_B", retrieved.CurrentStateId);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_ExistingCheckpoint_ReturnsCheckpoint()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();
        await _store.SaveAsync(checkpoint);

        // Act
        var retrieved = await _store.GetAsync(checkpoint.CheckpointId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(checkpoint.CheckpointId, retrieved.CheckpointId);
        Assert.Equal(checkpoint.ExecutionId, retrieved.ExecutionId);
        Assert.Equal(checkpoint.WorkflowName, retrieved.WorkflowName);
    }

    [Fact]
    public async Task GetAsync_NonExistentCheckpoint_ReturnsNull()
    {
        // Act
        var retrieved = await _store.GetAsync("non-existent-id");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetAsync_WithNullId_ThrowsArgumentNullException()
    {
        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.GetAsync(null!));
    }

    [Fact]
    public async Task GetAsync_WithEmptyId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _store.GetAsync(""));
    }

    [Fact]
    public async Task GetAsync_PreservesAllProperties()
    {
        // Arrange
        var checkpoint = new CheckpointData
        {
            CheckpointId = "checkpoint-full",
            ExecutionId = "execution-full",
            WorkflowName = "TestWorkflow",
            CurrentStateId = "STATE_1",
            MafCheckpointJson = "{\"key\": \"value\"}",
            Input = "test input",
            ContextJson = "{\"context\": \"data\"}",
            ExecutionStartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        await _store.SaveAsync(checkpoint);

        // Act
        var retrieved = await _store.GetAsync(checkpoint.CheckpointId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(checkpoint.CheckpointId, retrieved.CheckpointId);
        Assert.Equal(checkpoint.ExecutionId, retrieved.ExecutionId);
        Assert.Equal(checkpoint.WorkflowName, retrieved.WorkflowName);
        Assert.Equal(checkpoint.CurrentStateId, retrieved.CurrentStateId);
        Assert.Equal(checkpoint.MafCheckpointJson, retrieved.MafCheckpointJson);
        Assert.Equal(checkpoint.Input, retrieved.Input);
        Assert.Equal(checkpoint.ContextJson, retrieved.ContextJson);
        Assert.NotNull(retrieved.Metadata);
        Assert.Equal("value1", retrieved.Metadata["key1"]);
        Assert.Equal("value2", retrieved.Metadata["key2"]);
    }

    #endregion

    #region GetLatestForExecutionAsync Tests

    [Fact]
    public async Task GetLatestForExecutionAsync_WithCheckpoints_ReturnsLatest()
    {
        // Arrange
        var checkpoint1 = CreateTestCheckpoint("checkpoint-1", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var checkpoint2 = CreateTestCheckpoint("checkpoint-2", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var checkpoint3 = CreateTestCheckpoint("checkpoint-3", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveAsync(checkpoint1);
        await _store.SaveAsync(checkpoint2);
        await _store.SaveAsync(checkpoint3);

        // Act
        var latest = await _store.GetLatestForExecutionAsync("execution-1");

        // Assert
        Assert.NotNull(latest);
        Assert.Equal("checkpoint-3", latest.CheckpointId);
    }

    [Fact]
    public async Task GetLatestForExecutionAsync_NoCheckpoints_ReturnsNull()
    {
        // Act
        var latest = await _store.GetLatestForExecutionAsync("non-existent-execution");

        // Assert
        Assert.Null(latest);
    }

    [Fact]
    public async Task GetLatestForExecutionAsync_WithNullExecutionId_ThrowsArgumentNullException()
    {
        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.GetLatestForExecutionAsync(null!));
    }

    #endregion

    #region GetAllForExecutionAsync Tests

    [Fact]
    public async Task GetAllForExecutionAsync_WithCheckpoints_ReturnsAllOrdered()
    {
        // Arrange
        var checkpoint1 = CreateTestCheckpoint("checkpoint-1", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var checkpoint2 = CreateTestCheckpoint("checkpoint-2", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow
        };
        var checkpoint3 = CreateTestCheckpoint("checkpoint-3", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        await _store.SaveAsync(checkpoint1);
        await _store.SaveAsync(checkpoint2);
        await _store.SaveAsync(checkpoint3);

        // Act
        var all = await _store.GetAllForExecutionAsync("execution-1");

        // Assert
        Assert.Equal(3, all.Count);
        Assert.Equal("checkpoint-1", all[0].CheckpointId); // Oldest first
        Assert.Equal("checkpoint-3", all[1].CheckpointId);
        Assert.Equal("checkpoint-2", all[2].CheckpointId); // Newest last
    }

    [Fact]
    public async Task GetAllForExecutionAsync_NoCheckpoints_ReturnsEmptyList()
    {
        // Act
        var all = await _store.GetAllForExecutionAsync("non-existent-execution");

        // Assert
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllForExecutionAsync_DifferentExecutions_ReturnsSeparate()
    {
        // Arrange
        var checkpoint1 = CreateTestCheckpoint("checkpoint-1", "execution-1");
        var checkpoint2 = CreateTestCheckpoint("checkpoint-2", "execution-1");
        var checkpoint3 = CreateTestCheckpoint("checkpoint-3", "execution-2");

        await _store.SaveAsync(checkpoint1);
        await _store.SaveAsync(checkpoint2);
        await _store.SaveAsync(checkpoint3);

        // Act
        var exec1Checkpoints = await _store.GetAllForExecutionAsync("execution-1");
        var exec2Checkpoints = await _store.GetAllForExecutionAsync("execution-2");

        // Assert
        Assert.Equal(2, exec1Checkpoints.Count);
        Assert.Single(exec2Checkpoints);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingCheckpoint_ReturnsTrueAndDeletes()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();
        await _store.SaveAsync(checkpoint);

        // Act
        var result = await _store.DeleteAsync(checkpoint.CheckpointId);

        // Assert
        Assert.True(result);
        Assert.False(await _store.ExistsAsync(checkpoint.CheckpointId));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentCheckpoint_ReturnsFalse()
    {
        // Act
        var result = await _store.DeleteAsync("non-existent-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_WithNullId_ThrowsArgumentNullException()
    {
        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.DeleteAsync(null!));
    }

    #endregion

    #region DeleteAllForExecutionAsync Tests

    [Fact]
    public async Task DeleteAllForExecutionAsync_WithCheckpoints_DeletesAllAndReturnsCount()
    {
        // Arrange
        var checkpoint1 = CreateTestCheckpoint("checkpoint-1", "execution-1");
        var checkpoint2 = CreateTestCheckpoint("checkpoint-2", "execution-1");
        var checkpoint3 = CreateTestCheckpoint("checkpoint-3", "execution-2");

        await _store.SaveAsync(checkpoint1);
        await _store.SaveAsync(checkpoint2);
        await _store.SaveAsync(checkpoint3);

        // Act
        var deletedCount = await _store.DeleteAllForExecutionAsync("execution-1");

        // Assert
        Assert.Equal(2, deletedCount);
        Assert.False(await _store.ExistsAsync("checkpoint-1"));
        Assert.False(await _store.ExistsAsync("checkpoint-2"));
        Assert.True(await _store.ExistsAsync("checkpoint-3")); // Different execution
    }

    [Fact]
    public async Task DeleteAllForExecutionAsync_NoCheckpoints_ReturnsZero()
    {
        // Act
        var deletedCount = await _store.DeleteAllForExecutionAsync("non-existent-execution");

        // Assert
        Assert.Equal(0, deletedCount);
    }

    [Fact]
    public async Task DeleteAllForExecutionAsync_WithNullExecutionId_ThrowsArgumentNullException()
    {
        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.DeleteAllForExecutionAsync(null!));
    }

    #endregion

    #region CleanupOlderThanAsync Tests

    [Fact]
    public async Task CleanupOlderThanAsync_WithOldCheckpoints_DeletesOldOnly()
    {
        // Arrange
        var oldCheckpoint = CreateTestCheckpoint("old-checkpoint", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var recentCheckpoint = CreateTestCheckpoint("recent-checkpoint", "execution-1") with
        {
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveAsync(oldCheckpoint);
        await _store.SaveAsync(recentCheckpoint);

        // Act
        var deletedCount = await _store.CleanupOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-5));

        // Assert
        Assert.Equal(1, deletedCount);
        Assert.False(await _store.ExistsAsync("old-checkpoint"));
        Assert.True(await _store.ExistsAsync("recent-checkpoint"));
    }

    [Fact]
    public async Task CleanupOlderThanAsync_NoOldCheckpoints_ReturnsZero()
    {
        // Arrange
        var recentCheckpoint = CreateTestCheckpoint() with
        {
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _store.SaveAsync(recentCheckpoint);

        // Act
        var deletedCount = await _store.CleanupOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-5));

        // Assert
        Assert.Equal(0, deletedCount);
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ExistingCheckpoint_ReturnsTrue()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();
        await _store.SaveAsync(checkpoint);

        // Act
        var exists = await _store.ExistsAsync(checkpoint.CheckpointId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_NonExistentCheckpoint_ReturnsFalse()
    {
        // Act
        var exists = await _store.ExistsAsync("non-existent-id");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_WithNullId_ThrowsArgumentNullException()
    {
        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.ExistsAsync(null!));
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task SaveAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var checkpoint = CreateTestCheckpoint();
        _store.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _store.SaveAsync(checkpoint));
    }

    [Fact]
    public async Task GetAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _store.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _store.GetAsync("id"));
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentSaves_NoDataCorruption()
    {
        // Arrange
        var tasks = new List<Task>();
        var executionId = "concurrent-execution";

        // Act - Save multiple checkpoints concurrently
        for (int i = 0; i < 10; i++)
        {
            var checkpoint = CreateTestCheckpoint($"checkpoint-{i}", executionId);
            tasks.Add(_store.SaveAsync(checkpoint));
        }

        await Task.WhenAll(tasks);

        // Assert
        var all = await _store.GetAllForExecutionAsync(executionId);
        Assert.Equal(10, all.Count);
    }

    #endregion

    #region Helper Methods

    private static CheckpointData CreateTestCheckpoint(
        string? checkpointId = null,
        string? executionId = null)
    {
        return new CheckpointData
        {
            CheckpointId = checkpointId ?? $"checkpoint-{Guid.NewGuid()}",
            ExecutionId = executionId ?? $"execution-{Guid.NewGuid()}",
            WorkflowName = "TestWorkflow",
            CurrentStateId = "STATE_A",
            Input = "test input"
        };
    }

    #endregion
}
