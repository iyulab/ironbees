using Ironbees.Core;
using Moq;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive.Tests;

public class IronhiveAgentWrapperTests
{
    [Fact]
    public void Constructor_ValidArgs_SetsProperties()
    {
        // Arrange
        var mockAgent = new Mock<IronHiveAgent>();
        var config = new AgentConfig
        {
            Name = "my-agent",
            Description = "My agent description",
            Version = "1.0.0",
            SystemPrompt = "Be helpful",
            Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4o" }
        };

        // Act
        var wrapper = new IronhiveAgentWrapper(mockAgent.Object, config);

        // Assert
        Assert.Equal("my-agent", wrapper.Name);
        Assert.Equal("My agent description", wrapper.Description);
        Assert.Same(config, wrapper.Config);
        Assert.Same(mockAgent.Object, wrapper.IronhiveAgent);
    }

    [Fact]
    public void Constructor_NullAgent_Throws()
    {
        var config = new AgentConfig
        {
            Name = "a",
            Description = "b",
            Version = "1.0.0",
            SystemPrompt = "",
            Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4o" }
        };

        Assert.Throws<ArgumentNullException>(() =>
            new IronhiveAgentWrapper(null!, config));
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        var mockAgent = new Mock<IronHiveAgent>();

        Assert.Throws<ArgumentNullException>(() =>
            new IronhiveAgentWrapper(mockAgent.Object, null!));
    }

    [Fact]
    public void ImplementsIAgent()
    {
        var mockAgent = new Mock<IronHiveAgent>();
        var config = new AgentConfig
        {
            Name = "agent",
            Description = "desc",
            Version = "1.0.0",
            SystemPrompt = "",
            Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4o" }
        };

        var wrapper = new IronhiveAgentWrapper(mockAgent.Object, config);

        Assert.IsAssignableFrom<IAgent>(wrapper);
    }
}
