using Ironbees.Core;
using Moq;

namespace Ironbees.AgentFramework.Tests;

public class AgentOrchestratorTests
{
    [Fact]
    public async Task LoadAgentsAsync_WithValidConfigs_RegistersAgents()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var configs = new List<AgentConfig>
        {
            new AgentConfig
            {
                Name = "agent1",
                Description = "Test agent 1",
                Version = "1.0.0",
                SystemPrompt = "You are agent 1",
                Model = new ModelConfig
                {
                    Deployment = "gpt-4",
                    Temperature = 0.7,
                    MaxTokens = 1000
                }
            },
            new AgentConfig
            {
                Name = "agent2",
                Description = "Test agent 2",
                Version = "1.0.0",
                SystemPrompt = "You are agent 2",
                Model = new ModelConfig
                {
                    Deployment = "gpt-4",
                    Temperature = 0.7,
                    MaxTokens = 1000
                }
            }
        };

        mockLoader.Setup(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var mockAgent1 = new Mock<IAgent>();
        mockAgent1.Setup(a => a.Name).Returns("agent1");
        mockAgent1.Setup(a => a.Description).Returns("Test agent 1");

        var mockAgent2 = new Mock<IAgent>();
        mockAgent2.Setup(a => a.Name).Returns("agent2");
        mockAgent2.Setup(a => a.Description).Returns("Test agent 2");

        mockAdapter.SetupSequence(a => a.CreateAgentAsync(It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAgent1.Object)
            .ReturnsAsync(mockAgent2.Object);

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "agent1", "agent2" });

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        await orchestrator.LoadAgentsAsync();

        // Assert
        mockLoader.Verify(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockAdapter.Verify(a => a.CreateAgentAsync(It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockRegistry.Verify(r => r.Register("agent1", It.IsAny<IAgent>()), Times.Once);
        mockRegistry.Verify(r => r.Register("agent2", It.IsAny<IAgent>()), Times.Once);
    }

    [Fact]
    public async Task LoadAgentsAsync_NoConfigs_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockLoader.Setup(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentConfig>());

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await orchestrator.LoadAgentsAsync());
    }

    [Fact]
    public async Task LoadAgentsAsync_AllAgentsFail_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var configs = new List<AgentConfig>
        {
            new AgentConfig
            {
                Name = "agent1",
                Description = "Test agent 1",
                Version = "1.0.0",
                SystemPrompt = "You are agent 1",
                Model = new ModelConfig
                {
                    Deployment = "gpt-4",
                    Temperature = 0.7,
                    MaxTokens = 1000
                }
            }
        };

        mockLoader.Setup(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        mockAdapter.Setup(a => a.CreateAgentAsync(It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Failed to create agent"));

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string>());

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await orchestrator.LoadAgentsAsync());
    }

    [Fact]
    public async Task ProcessAsync_WithAgentName_CallsFrameworkAdapter()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");

        mockRegistry.Setup(r => r.Get("test-agent"))
            .Returns(mockAgent.Object);

        mockAdapter.Setup(a => a.RunAsync(It.IsAny<IAgent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test response");

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = await orchestrator.ProcessAsync("test input", "test-agent");

        // Assert
        Assert.Equal("Test response", result);
        mockRegistry.Verify(r => r.Get("test-agent"), Times.Once);
        mockAdapter.Verify(a => a.RunAsync(mockAgent.Object, "test input", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_AgentNotFound_ThrowsAgentNotFoundException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.Get("nonexistent-agent"))
            .Returns((IAgent?)null);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<AgentNotFoundException>(
            async () => await orchestrator.ProcessAsync("test input", "nonexistent-agent"));
    }

    [Fact]
    public async Task ProcessAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await orchestrator.ProcessAsync("", "test-agent"));
    }

    [Fact]
    public async Task ProcessAsync_AutoSelect_UsesFirstAgent()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("first-agent");

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "first-agent", "second-agent" });

        mockRegistry.Setup(r => r.Get("first-agent"))
            .Returns(mockAgent.Object);

        mockAdapter.Setup(a => a.RunAsync(It.IsAny<IAgent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test response");

        var mockSelector = new Mock<IAgentSelector>();
        mockSelector.Setup(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = mockAgent.Object,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = new List<AgentScore>()
            });

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = await orchestrator.ProcessAsync("test input");

        // Assert
        Assert.Equal("Test response", result);
        mockRegistry.Verify(r => r.ListAgents(), Times.Once);
        mockSelector.Verify(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_AutoSelectNoAgents_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string>());

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await orchestrator.ProcessAsync("test input"));
    }

    [Fact(Skip = "Requires proper mocking of IAsyncEnumerable")]
    public void StreamAsync_WithAgentName_ReturnsStream()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");

        mockRegistry.Setup(r => r.Get("test-agent"))
            .Returns(mockAgent.Object);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var stream = orchestrator.StreamAsync("test input", "test-agent");

        // Assert
        Assert.NotNull(stream);
        mockRegistry.Verify(r => r.Get("test-agent"), Times.Once);
    }

    [Fact]
    public void StreamAsync_AgentNotFound_ThrowsAgentNotFoundException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.Get("nonexistent-agent"))
            .Returns((IAgent?)null);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        Assert.Throws<AgentNotFoundException>(
            () => orchestrator.StreamAsync("test input", "nonexistent-agent"));
    }

    [Fact]
    public void ListAgents_ReturnsRegisteredAgents()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var agents = new List<string> { "agent1", "agent2", "agent3" };
        mockRegistry.Setup(r => r.ListAgents())
            .Returns(agents);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = orchestrator.ListAgents();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("agent1", result);
        Assert.Contains("agent2", result);
        Assert.Contains("agent3", result);
    }

    [Fact]
    public void GetAgent_ExistingAgent_ReturnsAgent()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");

        mockRegistry.Setup(r => r.Get("test-agent"))
            .Returns(mockAgent.Object);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = orchestrator.GetAgent("test-agent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-agent", result.Name);
    }

    [Fact]
    public void GetAgent_NonexistentAgent_ReturnsNull()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.Get("nonexistent-agent"))
            .Returns((IAgent?)null);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = orchestrator.GetAgent("nonexistent-agent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_NullLoader_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        // Act & Assert
        var mockSelector = new Mock<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(null!, mockRegistry.Object, mockAdapter.Object, mockSelector.Object));
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        // Act & Assert
        var mockSelector = new Mock<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader.Object, null!, mockAdapter.Object, mockSelector.Object));
    }

    [Fact]
    public void Constructor_NullAdapter_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();

        // Act & Assert
        var mockSelector = new Mock<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader.Object, mockRegistry.Object, null!, mockSelector.Object));
    }

    [Fact]
    public void Constructor_NullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader.Object, mockRegistry.Object, mockAdapter.Object, null!));
    }
}
