// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Checkpoint;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ironbees.Ironhive.Tests.Checkpoint;

public class CheckpointStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemIronhiveCheckpointStore _store;

    public CheckpointStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ironbees-checkpoint-tests-" + Guid.NewGuid().ToString("N"));
        _store = new FileSystemIronhiveCheckpointStore(
            _testDirectory,
            NullLogger<FileSystemIronhiveCheckpointStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCheckpointAsync_ValidCheckpoint_CreatesFile()
    {
        // Arrange
        var orchestrationId = "orch-1";
        var checkpoint = CreateTestCheckpoint("chk-1", orchestrationId);

        // Act
        await _store.SaveCheckpointAsync(orchestrationId, checkpoint);

        // Assert
        var loaded = await _store.LoadCheckpointAsync(orchestrationId, "chk-1");
        Assert.NotNull(loaded);
        Assert.Equal("chk-1", loaded.CheckpointId);
        Assert.Equal(orchestrationId, loaded.OrchestrationId);
    }

    [Fact]
    public async Task SaveCheckpointAsync_NullOrchestrationId_Throws()
    {
        var checkpoint = CreateTestCheckpoint("chk-1", "orch-1");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.SaveCheckpointAsync(null!, checkpoint));
    }

    [Fact]
    public async Task SaveCheckpointAsync_NullCheckpoint_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.SaveCheckpointAsync("orch-1", null!));
    }

    [Fact]
    public async Task LoadCheckpointAsync_NonExistent_ReturnsNull()
    {
        // Act
        var result = await _store.LoadCheckpointAsync("nonexistent", "chk-1");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadCheckpointAsync_WithoutCheckpointId_ReturnsLatest()
    {
        // Arrange
        var orchestrationId = "orch-1";
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-1", orchestrationId, "State1"));

        await Task.Delay(10); // Ensure different timestamps

        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-2", orchestrationId, "State2"));

        // Act
        var result = await _store.LoadCheckpointAsync(orchestrationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("chk-2", result.CheckpointId);
        Assert.Equal("State2", result.CurrentState);
    }

    [Fact]
    public async Task ListCheckpointsAsync_MultipleCheckpoints_ReturnsAll()
    {
        // Arrange
        var orchestrationId = "orch-1";
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-1", orchestrationId));
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-2", orchestrationId));
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-3", orchestrationId));

        // Act
        var result = await _store.ListCheckpointsAsync(orchestrationId);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task ListCheckpointsAsync_NoCheckpoints_ReturnsEmpty()
    {
        // Act
        var result = await _store.ListCheckpointsAsync("nonexistent");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task DeleteCheckpointAsync_ExistingCheckpoint_RemovesFile()
    {
        // Arrange
        var orchestrationId = "orch-1";
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-1", orchestrationId));

        // Act
        await _store.DeleteCheckpointAsync(orchestrationId, "chk-1");

        // Assert
        var loaded = await _store.LoadCheckpointAsync(orchestrationId, "chk-1");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteCheckpointAsync_NonExistent_DoesNotThrow()
    {
        // Act & Assert - should not throw
        await _store.DeleteCheckpointAsync("orch-1", "nonexistent");
    }

    [Fact]
    public async Task DeleteAllCheckpointsAsync_RemovesAllForOrchestration()
    {
        // Arrange
        var orchestrationId = "orch-1";
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-1", orchestrationId));
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-2", orchestrationId));
        await _store.SaveCheckpointAsync(orchestrationId,
            CreateTestCheckpoint("chk-3", orchestrationId));

        // Act
        await _store.DeleteAllCheckpointsAsync(orchestrationId);

        // Assert
        var result = await _store.ListCheckpointsAsync(orchestrationId);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesAllFields()
    {
        // Arrange
        var orchestrationId = "orch-test";
        var checkpoint = new OrchestrationCheckpoint
        {
            CheckpointId = "full-chk",
            OrchestrationId = orchestrationId,
            CurrentState = "processing",
            CurrentAgent = "analyzer",
            SerializedState = "{\"step\": 3}",
            AgentResults = new Dictionary<string, string>
            {
                ["agent1"] = "result1",
                ["agent2"] = "result2"
            },
            Messages = new List<CheckpointMessage>
            {
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", AgentName = "agent1", Content = "Hi there" }
            },
            TokenUsage = new TokenUsageInfo { InputTokens = 100, OutputTokens = 50 },
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1"
            }
        };

        // Act
        await _store.SaveCheckpointAsync(orchestrationId, checkpoint);
        var loaded = await _store.LoadCheckpointAsync(orchestrationId, "full-chk");

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("full-chk", loaded.CheckpointId);
        Assert.Equal(orchestrationId, loaded.OrchestrationId);
        Assert.Equal("processing", loaded.CurrentState);
        Assert.Equal("analyzer", loaded.CurrentAgent);
        Assert.Equal("{\"step\": 3}", loaded.SerializedState);
        Assert.NotNull(loaded.AgentResults);
        Assert.Equal(2, loaded.AgentResults.Count);
        Assert.NotNull(loaded.Messages);
        Assert.Equal(2, loaded.Messages.Count);
        Assert.NotNull(loaded.TokenUsage);
        Assert.Equal(100, loaded.TokenUsage.InputTokens);
        Assert.Equal(50, loaded.TokenUsage.OutputTokens);
    }

    private static OrchestrationCheckpoint CreateTestCheckpoint(
        string checkpointId,
        string orchestrationId,
        string? state = null)
    {
        return new OrchestrationCheckpoint
        {
            CheckpointId = checkpointId,
            OrchestrationId = orchestrationId,
            CurrentState = state ?? "test-state",
            CurrentAgent = "test-agent"
        };
    }
}
