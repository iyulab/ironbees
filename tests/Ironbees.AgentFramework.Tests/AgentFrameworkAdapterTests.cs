using Ironbees.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenAI;

namespace Ironbees.AgentFramework.Tests;

public class AgentFrameworkAdapterTests
{
    [Fact]
    public async Task CreateAgentAsync_ValidConfig_ReturnsAgentWrapper()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");

        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = new AgentConfig
        {
            Name = "test-agent",
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

        // Act
        var agent = await adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("test-agent", agent.Name);
        Assert.Equal("Test agent", agent.Description);
        Assert.IsAssignableFrom<IAgent>(agent);
    }

    [Fact]
    public async Task CreateAgentAsync_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");

        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await adapter.CreateAgentAsync(null!));
    }

    [Fact]
    public async Task RunAsync_NullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");

        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await adapter.RunAsync(null!, "test input"));
    }

    [Fact]
    public async Task RunAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");

        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = new AgentConfig
        {
            Name = "test-agent",
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

        var agent = await adapter.CreateAgentAsync(config);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await adapter.RunAsync(agent, ""));
    }

    [Fact]
    public async Task StreamAsync_NullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");

        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var chunk in adapter.StreamAsync(null!, "test input"))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task StreamAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");

        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
        var adapter = new AgentFrameworkAdapter(mockClient, mockLogger);

        var config = new AgentConfig
        {
            Name = "test-agent",
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

        var agent = await adapter.CreateAgentAsync(config);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var chunk in adapter.StreamAsync(agent, ""))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new AgentFrameworkAdapter(null!, mockLogger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = Substitute.For<OpenAIClient>("test-key");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new AgentFrameworkAdapter(mockClient, null!));
    }
}
