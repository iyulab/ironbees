using Ironbees.Core;
using Ironbees.Samples.Shared;

namespace Ironbees.Core.Tests;

public class GpuStackAdapterTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        // Assert
        Assert.NotNull(adapter);
    }

    [Fact]
    public void Constructor_NullEndpoint_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new GpuStackAdapter(null!, "test-key", "llama3.2"));
    }

    [Fact]
    public void Constructor_EmptyEndpoint_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new GpuStackAdapter("", "test-key", "llama3.2"));
    }

    [Fact]
    public void Constructor_NullApiKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GpuStackAdapter("http://localhost:8080", null!, "llama3.2"));
    }

    [Fact]
    public async Task CreateAgentAsync_ValidConfig_ReturnsAgent()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        var config = new AgentConfig
        {
            Name = "test-agent",
            Description = "Test GPU-Stack agent",
            Version = "1.0.0",
            SystemPrompt = "You are a helpful assistant",
            Model = new ModelConfig
            {
                Deployment = "llama3.2",
                Temperature = 0.7,
                MaxTokens = 1000
            }
        };

        // Act
        var agent = await adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("test-agent", agent.Name);
        Assert.Equal("Test GPU-Stack agent", agent.Description);
        Assert.IsAssignableFrom<IAgent>(agent);
    }

    [Fact]
    public async Task CreateAgentAsync_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await adapter.CreateAgentAsync(null!));
    }

    [Fact]
    public async Task CreateAgentAsync_ConfigWithoutModel_UsesDefaultModel()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "default-model");

        var config = new AgentConfig
        {
            Name = "test-agent",
            Description = "Test agent without model config",
            Version = "1.0.0",
            SystemPrompt = "You are a helpful assistant",
            Model = new ModelConfig { Deployment = "default-model" }
        };

        // Act
        var agent = await adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("test-agent", agent.Name);
    }

    [Fact]
    public async Task RunAsync_NullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await adapter.RunAsync(null!, "test input"));
    }

    [Fact]
    public async Task RunAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        var config = new AgentConfig
        {
            Name = "test-agent",
            Description = "Test agent",
            Version = "1.0.0",
            SystemPrompt = "You are a helpful assistant",
            Model = new ModelConfig { Deployment = "llama3.2" }
        };

        var agent = await adapter.CreateAgentAsync(config);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await adapter.RunAsync(agent, ""));
    }

    [Fact]
    public async Task RunAsync_InvalidAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        var mockAgent = new MockAgent();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await adapter.RunAsync(mockAgent, "test input"));
    }

    [Fact]
    public async Task StreamAsync_NullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in adapter.StreamAsync(null!, "test input"))
            {
                // Should throw before reaching here
            }
        });
    }

    [Fact]
    public async Task StreamAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        var config = new AgentConfig
        {
            Name = "test-agent",
            Description = "Test agent",
            Version = "1.0.0",
            SystemPrompt = "You are a helpful assistant",
            Model = new ModelConfig { Deployment = "llama3.2" }
        };

        var agent = await adapter.CreateAgentAsync(config);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in adapter.StreamAsync(agent, ""))
            {
                // Should throw before reaching here
            }
        });
    }

    [Fact]
    public async Task StreamAsync_InvalidAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var adapter = new GpuStackAdapter(
            "http://localhost:8080",
            "test-api-key",
            "llama3.2");

        var mockAgent = new MockAgent();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in adapter.StreamAsync(mockAgent, "test input"))
            {
                // Should throw before reaching here
            }
        });
    }

    // Mock agent for testing invalid agent type
    private sealed class MockAgent : IAgent
    {
        public string Name => "mock-agent";
        public string Description => "Mock agent for testing";
        public AgentConfig Config => new AgentConfig
        {
            Name = Name,
            Description = Description,
            Version = "1.0.0",
            SystemPrompt = "Mock",
            Model = new ModelConfig { Deployment = "mock-model" }
        };
    }
}
