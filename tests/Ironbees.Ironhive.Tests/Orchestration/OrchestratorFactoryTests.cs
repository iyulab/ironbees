// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Orchestration;
using Ironbees.AgentMode.Goals;
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

    public static TheoryData<OrchestratorType> EveryOrchestratorType() =>
    [
        OrchestratorType.Sequential, OrchestratorType.Parallel, OrchestratorType.HubSpoke,
        OrchestratorType.Handoff, OrchestratorType.GroupChat, OrchestratorType.Graph,
    ];

    /// <summary>
    /// IronhiveOptions.ApprovalHandler is documented as the HITL approval gate. It used to be read by nothing, and the
    /// factory rebuilt each orchestrator's options from a copy that dropped the handler anyway, so an application that
    /// set it had no gate on any orchestrator type.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryOrchestratorType))]
    public async Task ApprovalHandler_Refusal_StopsTheRunBeforeTheAgentExecutes(OrchestratorType type)
    {
        HitlRequestDetails? asked = null;
        var factory = new IronhiveOrchestratorFactory(
            NullLogger<IronhiveOrchestratorFactory>.Instance,
            options: new IronhiveOptions
            {
                ApprovalHandler = request =>
                {
                    asked ??= request;
                    return Task.FromResult(false);
                },
            });
        var agents = new List<IAgent> { CreateIronhiveAgentWrapper("agent1"), CreateIronhiveAgentWrapper("agent2") };
        var orchestrator = factory.CreateOrchestrator(SettingsFor(type), agents);

        var result = await orchestrator.RunAsync("draft the release notes", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(asked);
        Assert.Equal(HitlRequestType.Approval, asked!.RequestType);
        Assert.Contains(asked.Context!["agentName"], new object[] { "agent1", "agent2" });
        Assert.All(agents.Cast<IronhiveAgentWrapper>(), agent => Assert.Equal(0, InvocationsOf(agent)));
    }

    [Fact]
    public async Task ApprovalHandler_Approval_LetsTheAgentRunAndNamesIt()
    {
        var asked = new List<string>();
        var factory = new IronhiveOrchestratorFactory(
            NullLogger<IronhiveOrchestratorFactory>.Instance,
            options: new IronhiveOptions
            {
                ApprovalHandler = request =>
                {
                    asked.Add((string)request.Context!["agentName"]);
                    return Task.FromResult(true);
                },
            });
        var agent = CreateIronhiveAgentWrapper("writer");
        var orchestrator = factory.CreateOrchestrator(
            new OrchestratorSettings { Type = OrchestratorType.Sequential }, new List<IAgent> { agent });

        await orchestrator.RunAsync("draft the release notes", TestContext.Current.CancellationToken);

        Assert.Equal(["writer"], asked);
        Assert.Equal(1, InvocationsOf(agent));
    }

    /// <summary>
    /// The factory rebuilds each orchestrator's options from the settings, per type. That reconstruction is where options
    /// go missing: the approval handler once did on every type, and middleware and StopOnAgentFailure did on Handoff and
    /// GroupChat, whose builders could not take them. Every type must carry every common option the settings express.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryOrchestratorType))]
    public void EveryOrchestratorType_CarriesTheCommonOptions(OrchestratorType type)
    {
        var factory = new IronhiveOrchestratorFactory(
            NullLogger<IronhiveOrchestratorFactory>.Instance,
            new IronhiveMiddlewareFactory(),
            new IronhiveOptions { ApprovalHandler = _ => Task.FromResult(true) });
        var settings = SettingsFor(type) with
        {
            Timeout = TimeSpan.FromSeconds(123),
            AgentTimeout = TimeSpan.FromSeconds(45),
            StopOnAgentFailure = false,
            Middleware = new MiddlewareSettings { EnableLogging = true },
        };

        var wrapper = (IronhiveOrchestratorWrapper)factory.CreateOrchestrator(
            settings, [CreateIronhiveAgentWrapper("agent1"), CreateIronhiveAgentWrapper("agent2")]);
        // Derived orchestrators re-declare Options with their own type, so read the one the base class declares.
        var options = (IronHive.Abstractions.Agent.Orchestration.OrchestratorOptions)typeof(IronHive.Core.Agent.Orchestration.OrchestratorBase)
            .GetProperty("Options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)!
            .GetValue(wrapper.IronhiveOrchestrator)!;

        Assert.Equal(TimeSpan.FromSeconds(123), options.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(45), options.AgentTimeout);
        Assert.False(options.StopOnAgentFailure);
        Assert.NotNull(options.AgentMiddlewares);
        Assert.NotEmpty(options.AgentMiddlewares);
        Assert.NotNull(options.ApprovalHandler);
    }

    private static IronHive.Abstractions.Agent.Orchestration.OrchestratorOptions OptionsOf(IMultiAgentOrchestrator orchestrator) =>
        (IronHive.Abstractions.Agent.Orchestration.OrchestratorOptions)typeof(IronHive.Core.Agent.Orchestration.OrchestratorBase)
            .GetProperty("Options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)!
            .GetValue(((IronhiveOrchestratorWrapper)orchestrator).IronhiveOrchestrator)!;

    /// <summary>
    /// OrchestratorSettings.EnableCheckpointing was read by nothing and the factory never received a checkpoint store, so
    /// no factory-built orchestrator ever checkpointed. Enabled, every type now carries the registered store.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryOrchestratorType))]
    public void EnableCheckpointing_HandsTheCheckpointStoreToEveryOrchestratorType(OrchestratorType type)
    {
        var store = Substitute.For<IronHive.Abstractions.Agent.Orchestration.ICheckpointStore>();
        var factory = new IronhiveOrchestratorFactory(NullLogger<IronhiveOrchestratorFactory>.Instance, checkpointStore: store);

        var orchestrator = factory.CreateOrchestrator(
            SettingsFor(type) with { EnableCheckpointing = true },
            [CreateIronhiveAgentWrapper("agent1"), CreateIronhiveAgentWrapper("agent2")]);

        Assert.Same(store, OptionsOf(orchestrator).CheckpointStore);
    }

    [Fact]
    public void Checkpointing_IsOffByDefault_EvenWithAStoreRegistered()
    {
        var store = Substitute.For<IronHive.Abstractions.Agent.Orchestration.ICheckpointStore>();
        var factory = new IronhiveOrchestratorFactory(NullLogger<IronhiveOrchestratorFactory>.Instance, checkpointStore: store);

        var orchestrator = factory.CreateOrchestrator(new OrchestratorSettings(), [CreateIronhiveAgentWrapper("agent1")]);

        Assert.False(new OrchestratorSettings().EnableCheckpointing);
        Assert.Null(OptionsOf(orchestrator).CheckpointStore);
    }

    [Fact]
    public void RequireApproval_WithoutAnApprovalHandler_FailsInsteadOfRunningUnapproved()
    {
        var factory = new IronhiveOrchestratorFactory(NullLogger<IronhiveOrchestratorFactory>.Instance);

        var act = () => factory.CreateOrchestrator(
            new OrchestratorSettings { RequireApproval = true }, [CreateIronhiveAgentWrapper("agent1")]);

        var error = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("ApprovalHandler", error.Message);
    }

    [Fact]
    public void RequireApproval_WithAnApprovalHandler_GatesTheRun()
    {
        var factory = new IronhiveOrchestratorFactory(
            NullLogger<IronhiveOrchestratorFactory>.Instance,
            options: new IronhiveOptions { ApprovalHandler = _ => Task.FromResult(true) });

        var orchestrator = factory.CreateOrchestrator(
            new OrchestratorSettings { RequireApproval = true }, [CreateIronhiveAgentWrapper("agent1")]);

        Assert.NotNull(OptionsOf(orchestrator).ApprovalHandler);
    }

    /// <summary>
    /// Calls that run the agent, whichever path the orchestrator takes — the wrapper streams, so counting only
    /// <c>InvokeAsync</c> would make "the agent did not run" true of every run.
    /// </summary>
    private static int InvocationsOf(IronhiveAgentWrapper agent) =>
        agent.IronhiveAgent.ReceivedCalls().Count(call => call.GetMethodInfo().Name.StartsWith("Invoke", StringComparison.Ordinal));

    private static OrchestratorSettings SettingsFor(OrchestratorType type) => type switch
    {
        OrchestratorType.HubSpoke => new OrchestratorSettings { Type = type, HubAgent = "agent1" },
        OrchestratorType.Handoff => new OrchestratorSettings { Type = type, InitialAgent = "agent1" },
        OrchestratorType.Graph => new OrchestratorSettings
        {
            Type = type,
            Graph = new GraphSettings
            {
                Nodes = [new GraphNodeDefinition { Id = "node1", Agent = "agent1" }, new GraphNodeDefinition { Id = "node2", Agent = "agent2" }],
                Edges = [new GraphEdgeDefinition { From = "node1", To = "node2" }],
                StartNode = "node1",
                OutputNode = "node2",
            },
        },
        _ => new OrchestratorSettings { Type = type },
    };

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
