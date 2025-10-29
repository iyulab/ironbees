using Ironbees.Core;
using Xunit;

namespace Ironbees.Core.Tests;

public class AgentRegistryTests
{
    private class TestAgent : IAgent
    {
        public TestAgent(string name, string description, AgentConfig config)
        {
            Name = name;
            Description = description;
            Config = config;
        }

        public string Name { get; }
        public string Description { get; }
        public AgentConfig Config { get; }
    }

    private static AgentConfig CreateTestConfig(string name)
    {
        return new AgentConfig
        {
            Name = name,
            Description = "Test agent",
            Version = "1.0.0",
            SystemPrompt = "You are a test agent",
            Model = new ModelConfig
            {
                Deployment = "gpt-4o"
            }
        };
    }

    [Fact]
    public void Register_ValidAgent_Success()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config = CreateTestConfig("test-agent");
        var agent = new TestAgent("test-agent", "Test", config);

        // Act
        registry.Register("test-agent", agent);

        // Assert
        Assert.True(registry.Contains("test-agent"));
    }

    [Fact]
    public void Register_DuplicateAgent_ThrowsException()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config = CreateTestConfig("test-agent");
        var agent1 = new TestAgent("test-agent", "Test 1", config);
        var agent2 = new TestAgent("test-agent", "Test 2", config);

        registry.Register("test-agent", agent1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => registry.Register("test-agent", agent2));
    }

    [Fact]
    public void Get_ExistingAgent_ReturnsAgent()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config = CreateTestConfig("test-agent");
        var agent = new TestAgent("test-agent", "Test", config);
        registry.Register("test-agent", agent);

        // Act
        var result = registry.Get("test-agent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-agent", result.Name);
    }

    [Fact]
    public void Get_NonExistingAgent_ReturnsNull()
    {
        // Arrange
        var registry = new AgentRegistry();

        // Act
        var result = registry.Get("non-existing");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGet_ExistingAgent_ReturnsTrue()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config = CreateTestConfig("test-agent");
        var agent = new TestAgent("test-agent", "Test", config);
        registry.Register("test-agent", agent);

        // Act
        var success = registry.TryGet("test-agent", out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("test-agent", result.Name);
    }

    [Fact]
    public void TryGet_NonExistingAgent_ReturnsFalse()
    {
        // Arrange
        var registry = new AgentRegistry();

        // Act
        var success = registry.TryGet("non-existing", out var result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void ListAgents_MultipleAgents_ReturnsAllNames()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config1 = CreateTestConfig("agent1");
        var config2 = CreateTestConfig("agent2");
        var agent1 = new TestAgent("agent1", "Test 1", config1);
        var agent2 = new TestAgent("agent2", "Test 2", config2);

        registry.Register("agent1", agent1);
        registry.Register("agent2", agent2);

        // Act
        var names = registry.ListAgents();

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Contains("agent1", names);
        Assert.Contains("agent2", names);
    }

    [Fact]
    public void Unregister_ExistingAgent_RemovesAgent()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config = CreateTestConfig("test-agent");
        var agent = new TestAgent("test-agent", "Test", config);
        registry.Register("test-agent", agent);

        // Act
        registry.Unregister("test-agent");

        // Assert
        Assert.False(registry.Contains("test-agent"));
    }

    [Fact]
    public void Clear_MultipleAgents_RemovesAll()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config1 = CreateTestConfig("agent1");
        var config2 = CreateTestConfig("agent2");
        var agent1 = new TestAgent("agent1", "Test 1", config1);
        var agent2 = new TestAgent("agent2", "Test 2", config2);

        registry.Register("agent1", agent1);
        registry.Register("agent2", agent2);

        // Act
        registry.Clear();

        // Assert
        Assert.Empty(registry.ListAgents());
    }

    [Fact]
    public void Register_CaseInsensitive_ThrowsException()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config = CreateTestConfig("test-agent");
        var agent1 = new TestAgent("test-agent", "Test 1", config);
        var agent2 = new TestAgent("TEST-AGENT", "Test 2", config);

        registry.Register("test-agent", agent1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => registry.Register("TEST-AGENT", agent2));
    }

    [Fact]
    public void Get_CaseInsensitive_ReturnsAgent()
    {
        // Arrange
        var registry = new AgentRegistry();
        var config = CreateTestConfig("test-agent");
        var agent = new TestAgent("test-agent", "Test", config);
        registry.Register("test-agent", agent);

        // Act
        var result = registry.Get("TEST-AGENT");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-agent", result.Name);
    }
}
