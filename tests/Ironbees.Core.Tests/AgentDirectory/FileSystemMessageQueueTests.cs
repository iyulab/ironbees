using Ironbees.Core.AgentDirectory;
using Xunit;

namespace Ironbees.Core.Tests.AgentDirectory;

public class FileSystemMessageQueueTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileSystemAgentDirectory _directory;
    private readonly FileSystemMessageQueue _queue;

    public FileSystemMessageQueueTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ironbees-tests", Guid.NewGuid().ToString("N"));
        var agentPath = Path.Combine(_testRoot, "test-agent");
        System.IO.Directory.CreateDirectory(agentPath);

        _directory = new FileSystemAgentDirectory("test-agent", agentPath);
        _directory.EnsureDirectoryStructureAsync().GetAwaiter().GetResult();

        _queue = new FileSystemMessageQueue(_directory);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _queue.Dispose();
        if (System.IO.Directory.Exists(_testRoot))
        {
            System.IO.Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void AgentName_ReturnsCorrectName()
    {
        Assert.Equal("test-agent", _queue.AgentName);
    }

    [Fact]
    public async Task EnqueueAsync_CreatesMessageFile()
    {
        // Arrange
        var message = new AgentMessage
        {
            ToAgent = "test-agent",
            MessageType = "request"
        };

        // Act
        var id = await _queue.EnqueueAsync(message);

        // Assert
        Assert.Equal(message.Id, id);
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Inbox, "*.json");
        Assert.Single(files);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsOldestPendingMessage()
    {
        // Arrange
        var message1 = new AgentMessage { ToAgent = "test-agent", MessageType = "first" };
        var message2 = new AgentMessage { ToAgent = "test-agent", MessageType = "second" };

        await _queue.EnqueueAsync(message1);
        await Task.Delay(50); // Ensure different timestamps
        await _queue.EnqueueAsync(message2);

        // Act
        var dequeued = await _queue.DequeueAsync();

        // Assert
        Assert.NotNull(dequeued);
        Assert.Equal("first", dequeued.MessageType);
        Assert.Equal(MessageStatus.Processing, dequeued.Status);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsNullWhenEmpty()
    {
        var result = await _queue.DequeueAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task DequeueAsync_RespectsMessagePriority()
    {
        // Arrange
        var normalMessage = new AgentMessage
        {
            ToAgent = "test-agent",
            MessageType = "normal",
            Priority = MessagePriority.Normal
        };

        var highMessage = new AgentMessage
        {
            ToAgent = "test-agent",
            MessageType = "high",
            Priority = MessagePriority.High
        };

        // Enqueue normal first, then high
        await _queue.EnqueueAsync(normalMessage);
        await Task.Delay(50);
        await _queue.EnqueueAsync(highMessage);

        // Act - should get high priority first despite being enqueued later
        var dequeued = await _queue.DequeueAsync();

        // Assert
        Assert.NotNull(dequeued);
        Assert.Equal("high", dequeued.MessageType);
    }

    [Fact]
    public async Task PeekAsync_ReturnsMessageWithoutRemoving()
    {
        // Arrange
        var message = new AgentMessage { ToAgent = "test-agent", MessageType = "test" };
        await _queue.EnqueueAsync(message);

        // Act
        var peeked1 = await _queue.PeekAsync();
        var peeked2 = await _queue.PeekAsync();

        // Assert
        Assert.NotNull(peeked1);
        Assert.NotNull(peeked2);
        Assert.Equal(peeked1.Id, peeked2.Id);
        Assert.Equal(MessageStatus.Pending, peeked1.Status);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_ReturnsAllPendingMessages()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await _queue.EnqueueAsync(new AgentMessage
            {
                ToAgent = "test-agent",
                MessageType = $"message-{i}"
            });
        }

        // Act
        var pending = await _queue.GetPendingMessagesAsync();

        // Assert
        Assert.Equal(5, pending.Count);
    }

    [Fact]
    public async Task GetPendingCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await _queue.EnqueueAsync(new AgentMessage { ToAgent = "test-agent", MessageType = "msg1" });
        await _queue.EnqueueAsync(new AgentMessage { ToAgent = "test-agent", MessageType = "msg2" });

        // Act
        var count = await _queue.GetPendingCountAsync();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CompleteAsync_MovesMessageToProcessed()
    {
        // Arrange
        var message = new AgentMessage { ToAgent = "test-agent", MessageType = "test" };
        await _queue.EnqueueAsync(message);

        // Act
        var result = await _queue.CompleteAsync(message.Id);

        // Assert
        Assert.True(result);
        var pending = await _queue.GetPendingCountAsync();
        Assert.Equal(0, pending);

        // Check processed directory
        var processedPath = Path.Combine(_directory.GetSubdirectoryPath(AgentSubdirectory.Inbox), ".processed");
        Assert.True(System.IO.Directory.Exists(processedPath));
        var processedFiles = System.IO.Directory.GetFiles(processedPath, "*.json");
        Assert.Single(processedFiles);
    }

    [Fact]
    public async Task FailAsync_MovesMessageToFailed()
    {
        // Arrange
        var message = new AgentMessage { ToAgent = "test-agent", MessageType = "test" };
        await _queue.EnqueueAsync(message);

        // Act
        var result = await _queue.FailAsync(message.Id, "Test error");

        // Assert
        Assert.True(result);
        var pending = await _queue.GetPendingCountAsync();
        Assert.Equal(0, pending);

        // Check failed directory
        var failedPath = Path.Combine(_directory.GetSubdirectoryPath(AgentSubdirectory.Inbox), ".failed");
        Assert.True(System.IO.Directory.Exists(failedPath));
    }

    [Fact]
    public async Task PublishResultAsync_WritesToOutbox()
    {
        // Arrange
        var message = new AgentMessage
        {
            ToAgent = "other-agent",
            MessageType = "result",
            FromAgent = "test-agent"
        };

        // Act
        var id = await _queue.PublishResultAsync(message);

        // Assert
        var files = await _directory.ListFilesAsync(AgentSubdirectory.Outbox, "*.json");
        Assert.Single(files);
    }

    [Fact]
    public async Task GetOutboxMessagesAsync_ReturnsPublishedMessages()
    {
        // Arrange
        await _queue.PublishResultAsync(new AgentMessage { ToAgent = "agent1", MessageType = "result1" });
        await _queue.PublishResultAsync(new AgentMessage { ToAgent = "agent2", MessageType = "result2" });

        // Act
        var messages = await _queue.GetOutboxMessagesAsync();

        // Assert
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task CleanupExpiredMessagesAsync_RemovesExpiredMessages()
    {
        // Arrange
        var expiredMessage = new AgentMessage
        {
            ToAgent = "test-agent",
            MessageType = "expired",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2),
            TimeToLive = TimeSpan.FromHours(1)
        };

        var validMessage = new AgentMessage
        {
            ToAgent = "test-agent",
            MessageType = "valid",
            TimeToLive = TimeSpan.FromHours(24)
        };

        await _queue.EnqueueAsync(expiredMessage);
        await _queue.EnqueueAsync(validMessage);

        // Act
        var cleaned = await _queue.CleanupExpiredMessagesAsync();

        // Assert
        Assert.Equal(1, cleaned);
        var pending = await _queue.GetPendingCountAsync();
        Assert.Equal(1, pending);
    }
}
