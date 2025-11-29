using System.Text.Json;
using Ironbees.Core.AgentDirectory;
using Xunit;

namespace Ironbees.Core.Tests.AgentDirectory;

public class AgentMessageTests
{
    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        var message1 = new AgentMessage { ToAgent = "agent1", MessageType = "test" };
        var message2 = new AgentMessage { ToAgent = "agent1", MessageType = "test" };

        Assert.NotNull(message1.Id);
        Assert.NotNull(message2.Id);
        Assert.NotEqual(message1.Id, message2.Id);
    }

    [Fact]
    public void Constructor_SetsTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var message = new AgentMessage { ToAgent = "agent1", MessageType = "test" };
        var after = DateTimeOffset.UtcNow;

        Assert.True(message.Timestamp >= before);
        Assert.True(message.Timestamp <= after);
    }

    [Fact]
    public void ToFileName_GeneratesCorrectFormat()
    {
        var message = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "test",
            Timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.Zero)
        };

        var fileName = message.ToFileName();

        Assert.StartsWith("20240115103045_", fileName);
        Assert.EndsWith(".json", fileName);
        Assert.Contains(message.Id, fileName);
    }

    [Fact]
    public void ParseIdFromFileName_ExtractsId()
    {
        var fileName = "20240115103045_abc123xyz.json";

        var id = AgentMessage.ParseIdFromFileName(fileName);

        Assert.Equal("abc123xyz", id);
    }

    [Fact]
    public void ParseIdFromFileName_ReturnsNullForInvalidFormat()
    {
        Assert.Null(AgentMessage.ParseIdFromFileName(""));
        Assert.Null(AgentMessage.ParseIdFromFileName("invalid.json"));
        Assert.Null(AgentMessage.ParseIdFromFileName(null!));
    }

    [Fact]
    public void ToJson_SerializesCorrectly()
    {
        var message = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "request",
            FromAgent = "agent2",
            Priority = MessagePriority.High
        };

        var json = message.ToJson();

        // Note: WriteIndented = true adds spaces around colons
        Assert.Contains("\"toAgent\": \"agent1\"", json);
        Assert.Contains("\"messageType\": \"request\"", json);
        Assert.Contains("\"fromAgent\": \"agent2\"", json);
        Assert.Contains("\"priority\": \"high\"", json);
    }

    [Fact]
    public void FromJson_DeserializesCorrectly()
    {
        var original = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "request",
            FromAgent = "agent2",
            Priority = MessagePriority.High
        };

        var json = original.ToJson();
        var deserialized = AgentMessage.FromJson(json);

        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.ToAgent, deserialized.ToAgent);
        Assert.Equal(original.MessageType, deserialized.MessageType);
        Assert.Equal(original.FromAgent, deserialized.FromAgent);
        Assert.Equal(original.Priority, deserialized.Priority);
    }

    [Fact]
    public void IsExpired_ReturnsFalseWithNoTTL()
    {
        var message = new AgentMessage { ToAgent = "agent1", MessageType = "test" };

        Assert.False(message.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsTrueWhenExpired()
    {
        var message = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "test",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2),
            TimeToLive = TimeSpan.FromHours(1)
        };

        Assert.True(message.IsExpired);
    }

    [Fact]
    public void IsExpired_ReturnsFalseWhenNotExpired()
    {
        var message = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "test",
            TimeToLive = TimeSpan.FromHours(1)
        };

        Assert.False(message.IsExpired);
    }

    [Fact]
    public void GetPayload_DeserializesTypedPayload()
    {
        var payload = new { Name = "Test", Value = 42 };
        var message = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "test",
            Payload = JsonSerializer.SerializeToElement(payload)
        };

        var result = message.GetPayload<TestPayload>();

        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void WithStatus_CreatesNewInstanceWithUpdatedStatus()
    {
        var original = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "test",
            Status = MessageStatus.Pending
        };

        var updated = original.WithStatus(MessageStatus.Processing);

        Assert.Equal(MessageStatus.Pending, original.Status);
        Assert.Equal(MessageStatus.Processing, updated.Status);
        Assert.Equal(original.Id, updated.Id);
    }

    [Fact]
    public void CreateReply_CreatesCorrectReplyMessage()
    {
        var original = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "request",
            FromAgent = "agent2",
            Priority = MessagePriority.High
        };

        var reply = original.CreateReply("agent1", "response");

        Assert.Equal("agent1", reply.FromAgent);
        Assert.Equal("agent2", reply.ToAgent);
        Assert.Equal("response", reply.MessageType);
        Assert.Equal(original.Id, reply.CorrelationId);
        Assert.Equal(MessagePriority.High, reply.Priority);
    }

    [Fact]
    public void CreateReply_UsesReplyToIfSet()
    {
        var original = new AgentMessage
        {
            ToAgent = "agent1",
            MessageType = "request",
            FromAgent = "agent2",
            ReplyTo = "agent3"
        };

        var reply = original.CreateReply("agent1", "response");

        Assert.Equal("agent3", reply.ToAgent);
    }

    private sealed class TestPayload
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }
}
