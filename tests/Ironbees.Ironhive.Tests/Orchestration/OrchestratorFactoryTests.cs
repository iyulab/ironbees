// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Orchestration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive.Tests.Orchestration;

public class OrchestratorFactoryTests
{
    private readonly IronhiveOrchestratorFactory _factory;

    public OrchestratorFactoryTests()
    {
        _factory = new IronhiveOrchestratorFactory(
            NullLogger<IronhiveOrchestratorFactory>.Instance);
    }

    [Fact]
    public void CreateOrchestrator_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var agents = new List<IAgent> { CreateIronhiveAgentWrapper("agent1") };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateOrchestrator(null!, agents));
    }

    [Fact]
    public void CreateOrchestrator_NullAgents_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.Sequential };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateOrchestrator(settings, null!));
    }

    [Fact]
    public void CreateOrchestrator_EmptyAgents_ThrowsArgumentException()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.Sequential };
        var agents = new List<IAgent>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _factory.CreateOrchestrator(settings, agents));
        Assert.Contains("At least one agent", ex.Message);
    }

    [Fact]
    public void CreateOrchestrator_Sequential_ReturnsOrchestrator()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.Sequential };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1"),
            CreateIronhiveAgentWrapper("agent2")
        };

        // Act
        var orchestrator = _factory.CreateOrchestrator(settings, agents);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void CreateOrchestrator_Parallel_ReturnsOrchestrator()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.Parallel };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1"),
            CreateIronhiveAgentWrapper("agent2")
        };

        // Act
        var orchestrator = _factory.CreateOrchestrator(settings, agents);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void CreateOrchestrator_HubSpoke_WithoutHubAgent_ThrowsArgumentException()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.HubSpoke };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1"),
            CreateIronhiveAgentWrapper("agent2")
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _factory.CreateOrchestrator(settings, agents));
        Assert.Contains("HubAgent must be specified", ex.Message);
    }

    [Fact]
    public void CreateOrchestrator_HubSpoke_WithValidHubAgent_ReturnsOrchestrator()
    {
        // Arrange
        var settings = new OrchestratorSettings
        {
            Type = OrchestratorType.HubSpoke,
            HubAgent = "hub"
        };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("hub"),
            CreateIronhiveAgentWrapper("spoke1"),
            CreateIronhiveAgentWrapper("spoke2")
        };

        // Act
        var orchestrator = _factory.CreateOrchestrator(settings, agents);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void CreateOrchestrator_HubSpoke_HubAgentNotFound_ThrowsArgumentException()
    {
        // Arrange
        var settings = new OrchestratorSettings
        {
            Type = OrchestratorType.HubSpoke,
            HubAgent = "nonexistent"
        };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1"),
            CreateIronhiveAgentWrapper("agent2")
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _factory.CreateOrchestrator(settings, agents));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void CreateOrchestrator_Handoff_WithoutInitialAgent_ThrowsArgumentException()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.Handoff };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1"),
            CreateIronhiveAgentWrapper("agent2")
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _factory.CreateOrchestrator(settings, agents));
        Assert.Contains("InitialAgent must be specified", ex.Message);
    }

    [Fact]
    public void CreateOrchestrator_Handoff_WithValidInitialAgent_ReturnsOrchestrator()
    {
        // Arrange
        var settings = new OrchestratorSettings
        {
            Type = OrchestratorType.Handoff,
            InitialAgent = "initial",
            MaxTransitions = 5
        };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("initial"),
            CreateIronhiveAgentWrapper("target")
        };

        // Act
        var orchestrator = _factory.CreateOrchestrator(settings, agents);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void CreateOrchestrator_GroupChat_ReturnsOrchestrator()
    {
        // Arrange
        var settings = new OrchestratorSettings
        {
            Type = OrchestratorType.GroupChat,
            MaxRounds = 5
        };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1"),
            CreateIronhiveAgentWrapper("agent2"),
            CreateIronhiveAgentWrapper("agent3")
        };

        // Act
        var orchestrator = _factory.CreateOrchestrator(settings, agents);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void CreateOrchestrator_Graph_WithoutGraphSettings_ThrowsArgumentException()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.Graph };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1")
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            _factory.CreateOrchestrator(settings, agents));
        Assert.Contains("Graph settings must be specified", ex.Message);
    }

    [Fact]
    public void CreateOrchestrator_Graph_WithValidSettings_ReturnsOrchestrator()
    {
        // Arrange
        var settings = new OrchestratorSettings
        {
            Type = OrchestratorType.Graph,
            Graph = new GraphSettings
            {
                Nodes =
                [
                    new GraphNodeDefinition { Id = "node1", Agent = "agent1" },
                    new GraphNodeDefinition { Id = "node2", Agent = "agent2" }
                ],
                Edges =
                [
                    new GraphEdgeDefinition { From = "node1", To = "node2" }
                ],
                StartNode = "node1",
                OutputNode = "node2"
            }
        };
        var agents = new List<IAgent>
        {
            CreateIronhiveAgentWrapper("agent1"),
            CreateIronhiveAgentWrapper("agent2")
        };

        // Act
        var orchestrator = _factory.CreateOrchestrator(settings, agents);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void CreateOrchestrator_NonIronhiveAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new OrchestratorSettings { Type = OrchestratorType.Sequential };
        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("mock-agent");
        var agents = new List<IAgent> { mockAgent };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _factory.CreateOrchestrator(settings, agents));
        Assert.Contains("not an IronHive agent", ex.Message);
    }

    private static IronhiveAgentWrapper CreateIronhiveAgentWrapper(string name)
    {
        var mockIronhiveAgent = Substitute.For<IronHiveAgent>();
        mockIronhiveAgent.Name.Returns(name);

        var config = new AgentConfig
        {
            Name = name,
            Description = $"Test agent {name}",
            Version = "1.0.0",
            SystemPrompt = "Test system prompt",
            Model = new ModelConfig { Provider = "test", Deployment = "test-model" }
        };

        return new IronhiveAgentWrapper(mockIronhiveAgent, config);
    }
}
