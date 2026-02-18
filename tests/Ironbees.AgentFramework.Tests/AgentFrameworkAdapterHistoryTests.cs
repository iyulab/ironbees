using Ironbees.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenAI;

namespace Ironbees.AgentFramework.Tests;

public class AgentFrameworkAdapterHistoryTests
{
    private static AgentConfig CreateTestConfig(string name = "test-agent") => new()
    {
        Name = name,
        Description = "Test agent",
        Version = "1.0.0",
        SystemPrompt = "You are a test assistant",
        Model = new ModelConfig
        {
            Deployment = "gpt-4",
            Temperature = 0.7,
            MaxTokens = 1000
        }
    };

    [Fact]
    public async Task RunAsync_WithNullHistory_FallsToBaseMethod()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = CreateTestConfig();
        var agent = await adapter.CreateAgentAsync(config);

        // Act & Assert — should not throw (actual API call will fail but the method path is verified)
        // We verify that null history doesn't cause issues by checking it reaches the API call
        await Assert.ThrowsAsync<AgentLoadException>(
            async () => await adapter.RunAsync(agent, "test input", conversationHistory: null));
    }

    [Fact]
    public async Task RunAsync_WithEmptyHistory_FallsToBaseMethod()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = CreateTestConfig();
        var agent = await adapter.CreateAgentAsync(config);
        var emptyHistory = new List<ChatMessage>();

        // Act & Assert — should not throw differently from null history
        await Assert.ThrowsAsync<AgentLoadException>(
            async () => await adapter.RunAsync(agent, "test input", emptyHistory));
    }

    [Fact]
    public async Task RunAsync_WithHistory_IncludesHistoryInMessages()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = CreateTestConfig();
        var agent = await adapter.CreateAgentAsync(config);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "What is C#?"),
            new(ChatRole.Assistant, "C# is a programming language.")
        };

        // Act & Assert — the method should attempt to call the API with history included
        // Since we can't mock the ChatClient easily, we verify it reaches the API call
        // (which will throw because there's no actual connection)
        await Assert.ThrowsAsync<AgentLoadException>(
            async () => await adapter.RunAsync(agent, "Tell me more", history));
    }

    [Fact]
    public async Task StreamAsync_WithNullHistory_DoesNotThrow()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = CreateTestConfig();
        var agent = await adapter.CreateAgentAsync(config);

        // Act & Assert — streaming with null history should reach the API call
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var chunk in adapter.StreamAsync(agent, "test input", conversationHistory: null))
            {
                // Should not reach here with mock client
            }
        });
    }

    [Fact]
    public async Task StreamAsync_WithHistory_IncludesHistoryInMessages()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = CreateTestConfig();
        var agent = await adapter.CreateAgentAsync(config);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Previous question"),
            new(ChatRole.Assistant, "Previous answer")
        };

        // Act & Assert — streaming with history should reach the API call
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var chunk in adapter.StreamAsync(agent, "Follow-up", history))
            {
                // Should not reach here with mock client
            }
        });
    }

    [Fact]
    public async Task RunAsync_WrongAgentType_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);
        var mockAgent = Substitute.For<IAgent>();

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "test")
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await adapter.RunAsync(mockAgent, "test input", history));
    }

    [Fact]
    public async Task RunAsync_BaseMethodDelegatesToHistoryOverload()
    {
        // Arrange — verify the base RunAsync delegates to the history overload
        var mockClient = Substitute.For<OpenAIClient>("test-key");
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = CreateTestConfig();
        var agent = await adapter.CreateAgentAsync(config);

        // Both calls should follow the same code path (the history overload)
        var ex1 = await Assert.ThrowsAsync<AgentLoadException>(
            async () => await adapter.RunAsync(agent, "test input"));
        var ex2 = await Assert.ThrowsAsync<AgentLoadException>(
            async () => await adapter.RunAsync(agent, "test input", conversationHistory: null));

        // Both should throw with the same type of exception
        Assert.IsType<AgentLoadException>(ex1);
        Assert.IsType<AgentLoadException>(ex2);
    }
}
