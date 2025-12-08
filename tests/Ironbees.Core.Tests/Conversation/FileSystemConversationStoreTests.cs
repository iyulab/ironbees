using Ironbees.Core.Conversation;

namespace Ironbees.Core.Tests.Conversation;

public class FileSystemConversationStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemConversationStore _store;

    public FileSystemConversationStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ironbees_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _store = new FileSystemConversationStore(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_NewConversation_CreatesFile()
    {
        // Arrange
        var state = new ConversationState
        {
            ConversationId = "test-conv-1",
            Messages =
            [
                new ConversationMessage { Role = "user", Content = "Hello" },
                new ConversationMessage { Role = "assistant", Content = "Hi there!" }
            ]
        };

        // Act
        await _store.SaveAsync(state);

        // Assert
        var filePath = Path.Combine(_testDirectory, "test-conv-1.json");
        Assert.True(File.Exists(filePath));

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("test-conv-1", content);
        Assert.Contains("Hello", content);
    }

    [Fact]
    public async Task SaveAsync_WithAgentName_CreatesInSubdirectory()
    {
        // Arrange
        var state = new ConversationState
        {
            ConversationId = "test-conv-2",
            AgentName = "my-agent",
            Messages =
            [
                new ConversationMessage { Role = "user", Content = "Test message" }
            ]
        };

        // Act
        await _store.SaveAsync(state);

        // Assert
        var filePath = Path.Combine(_testDirectory, "my-agent", "test-conv-2.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task LoadAsync_ExistingConversation_ReturnsState()
    {
        // Arrange
        var originalState = new ConversationState
        {
            ConversationId = "test-conv-3",
            Messages =
            [
                new ConversationMessage { Role = "user", Content = "Question" },
                new ConversationMessage { Role = "assistant", Content = "Answer" }
            ]
        };
        await _store.SaveAsync(originalState);

        // Act
        var loadedState = await _store.LoadAsync("test-conv-3");

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal("test-conv-3", loadedState.ConversationId);
        Assert.Equal(2, loadedState.Messages.Count);
        Assert.Equal("Question", loadedState.Messages[0].Content);
        Assert.Equal("Answer", loadedState.Messages[1].Content);
    }

    [Fact]
    public async Task LoadAsync_NonExistentConversation_ReturnsNull()
    {
        // Act
        var result = await _store.LoadAsync("non-existent-conv");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ExistingConversation_ReturnsTrue()
    {
        // Arrange
        var state = new ConversationState
        {
            ConversationId = "test-conv-4",
            Messages = []
        };
        await _store.SaveAsync(state);

        // Act
        var result = await _store.DeleteAsync("test-conv-4");

        // Assert
        Assert.True(result);
        Assert.False(await _store.ExistsAsync("test-conv-4"));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentConversation_ReturnsFalse()
    {
        // Act
        var result = await _store.DeleteAsync("non-existent-conv");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ListAsync_MultipleConversations_ReturnsAll()
    {
        // Arrange
        await _store.SaveAsync(new ConversationState { ConversationId = "conv-a", Messages = [] });
        await _store.SaveAsync(new ConversationState { ConversationId = "conv-b", Messages = [] });
        await _store.SaveAsync(new ConversationState { ConversationId = "conv-c", Messages = [] });

        // Act
        var list = await _store.ListAsync();

        // Assert
        Assert.Equal(3, list.Count);
        Assert.Contains("conv-a", list);
        Assert.Contains("conv-b", list);
        Assert.Contains("conv-c", list);
    }

    [Fact]
    public async Task ListAsync_WithAgentFilter_ReturnsFiltered()
    {
        // Arrange
        await _store.SaveAsync(new ConversationState { ConversationId = "conv-1", AgentName = "agent-a", Messages = [] });
        await _store.SaveAsync(new ConversationState { ConversationId = "conv-2", AgentName = "agent-a", Messages = [] });
        await _store.SaveAsync(new ConversationState { ConversationId = "conv-3", AgentName = "agent-b", Messages = [] });

        // Act
        var list = await _store.ListAsync("agent-a");

        // Assert
        Assert.Equal(2, list.Count);
        Assert.Contains("conv-1", list);
        Assert.Contains("conv-2", list);
    }

    [Fact]
    public async Task ExistsAsync_ExistingConversation_ReturnsTrue()
    {
        // Arrange
        await _store.SaveAsync(new ConversationState { ConversationId = "test-conv-5", Messages = [] });

        // Act
        var result = await _store.ExistsAsync("test-conv-5");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistentConversation_ReturnsFalse()
    {
        // Act
        var result = await _store.ExistsAsync("non-existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AppendMessageAsync_NewConversation_CreatesAndAppendsMessage()
    {
        // Arrange
        var message = new ConversationMessage { Role = "user", Content = "First message" };

        // Act
        await _store.AppendMessageAsync("new-conv", message);

        // Assert
        var loaded = await _store.LoadAsync("new-conv");
        Assert.NotNull(loaded);
        Assert.Single(loaded.Messages);
        Assert.Equal("First message", loaded.Messages[0].Content);
    }

    [Fact]
    public async Task AppendMessageAsync_ExistingConversation_AppendsMessage()
    {
        // Arrange
        await _store.SaveAsync(new ConversationState
        {
            ConversationId = "existing-conv",
            Messages =
            [
                new ConversationMessage { Role = "user", Content = "Original message" }
            ]
        });

        var newMessage = new ConversationMessage { Role = "assistant", Content = "New response" };

        // Act
        await _store.AppendMessageAsync("existing-conv", newMessage);

        // Assert
        var loaded = await _store.LoadAsync("existing-conv");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Messages.Count);
        Assert.Equal("Original message", loaded.Messages[0].Content);
        Assert.Equal("New response", loaded.Messages[1].Content);
    }

    [Fact]
    public async Task GetMessageCountAsync_ExistingConversation_ReturnsCorrectCount()
    {
        // Arrange
        await _store.SaveAsync(new ConversationState
        {
            ConversationId = "count-conv",
            Messages =
            [
                new ConversationMessage { Role = "user", Content = "1" },
                new ConversationMessage { Role = "assistant", Content = "2" },
                new ConversationMessage { Role = "user", Content = "3" }
            ]
        });

        // Act
        var count = await _store.GetMessageCountAsync("count-conv");

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetMessageCountAsync_NonExistentConversation_ReturnsZero()
    {
        // Act
        var count = await _store.GetMessageCountAsync("non-existent");

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SaveAsync_UpdatesLastUpdatedAt()
    {
        // Arrange
        var originalState = new ConversationState
        {
            ConversationId = "update-test",
            Messages = [],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        await _store.SaveAsync(originalState);

        // Act
        await _store.AppendMessageAsync("update-test", new ConversationMessage { Role = "user", Content = "New" });

        // Assert
        var loaded = await _store.LoadAsync("update-test");
        Assert.NotNull(loaded);
        Assert.True(loaded.LastUpdatedAt > originalState.LastUpdatedAt);
    }

    [Fact]
    public void Constructor_NullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new FileSystemConversationStore(null!));
        Assert.Throws<ArgumentException>(() => new FileSystemConversationStore(""));
        Assert.Throws<ArgumentException>(() => new FileSystemConversationStore("   "));
    }

    [Fact]
    public async Task SaveAsync_NullState_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveAsync(null!));
    }

    [Fact]
    public async Task LoadAsync_EmptyConversationId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _store.LoadAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _store.LoadAsync(null!));
    }
}
