using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Ironbees.Core.Tests;

public class EndpointOverrideTests
{
    private static AgentConfig MakeConfig(string name, string deployment = "default-model", string provider = "test") =>
        new()
        {
            Name = name,
            Description = $"Description for {name}",
            Version = "1.0.0",
            SystemPrompt = "original prompt",
            Model = new ModelConfig { Provider = provider, Deployment = deployment }
        };

    private static IAgent MakeRegistryAgent(string name, string deployment = "default-model", string provider = "test")
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns($"Description for {name}");
        agent.Config.Returns(MakeConfig(name, deployment, provider));
        return agent;
    }

    private static AgentOrchestrator CreateOrchestrator(
        IAgentRegistry registry,
        ILLMFrameworkAdapter adapter,
        IAgentSelector selector)
    {
        return new AgentOrchestrator(
            Substitute.For<IAgentLoader>(),
            registry,
            adapter,
            selector,
            agentsDirectory: null,
            conversationStore: null);
    }

    [Fact]
    public async Task ProcessAsync_WithEndpointOverride_CreatesAgentWithEndpointInModelConfig()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        AgentConfig? capturedConfig = null;
        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedConfig = callInfo.ArgAt<AgentConfig>(0);
                return tempAgent;
            });
        adapter.RunAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns("response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await orchestrator.ProcessAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            EndpointOverride = "http://new-server:8080"
        });

        // Assert — AgentConfig.Model.Endpoint contains the new endpoint
        Assert.NotNull(capturedConfig);
        Assert.Equal("http://new-server:8080", capturedConfig!.Model.Endpoint);
    }

    [Fact]
    public async Task ProcessAsync_WithEndpointOverride_PreservesOtherFields()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model", "gpustack");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        AgentConfig? capturedConfig = null;
        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedConfig = callInfo.ArgAt<AgentConfig>(0);
                return tempAgent;
            });
        adapter.RunAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns("response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await orchestrator.ProcessAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            EndpointOverride = "http://new-server:8080"
        });

        // Assert — only Endpoint changes; Provider, Deployment, and SystemPrompt remain unchanged
        Assert.NotNull(capturedConfig);
        Assert.Equal("yaml-model", capturedConfig!.Model.Deployment);
        Assert.Equal("gpustack", capturedConfig.Model.Provider);
        Assert.Equal("original prompt", capturedConfig.SystemPrompt);
        Assert.Null(capturedConfig.Model.Endpoint == "http://new-server:8080" ? null : "should not be null"); // endpoint set
        Assert.Equal("http://new-server:8080", capturedConfig.Model.Endpoint);
    }

    [Fact]
    public async Task ProcessAsync_WithoutEndpointOverride_DoesNotRecreateAgent()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent");
        registry.GetAgent("rag-agent").Returns(registryAgent);
        adapter.RunAsync(registryAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns("response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await orchestrator.ProcessAsync("question", new ProcessOptions { AgentName = "rag-agent" });

        // Assert — CreateAgentAsync should NOT be called (no override)
        await adapter.DidNotReceive().CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithBothModelAndEndpointOverrides_SingleCreateAgentCall()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model", "gpustack");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        AgentConfig? capturedConfig = null;
        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedConfig = callInfo.ArgAt<AgentConfig>(0);
                return tempAgent;
            });
        adapter.RunAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns("response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await orchestrator.ProcessAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            ModelOverride = "runtime-model",
            EndpointOverride = "http://new-server:8080"
        });

        // Assert — CreateAgentAsync called exactly once with both overrides applied
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c =>
                c.Model.Deployment == "runtime-model" &&
                c.Model.Endpoint == "http://new-server:8080"),
            Arg.Any<CancellationToken>());
        Assert.NotNull(capturedConfig);
        Assert.Equal("runtime-model", capturedConfig!.Model.Deployment);
        Assert.Equal("http://new-server:8080", capturedConfig.Model.Endpoint);
    }

    [Fact]
    public async Task StreamAsync_WithEndpointOverride_CreatesAgentWithEndpointInModelConfig()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Endpoint == "http://new-server:8080"),
            Arg.Any<CancellationToken>())
            .Returns(tempAgent);
        adapter.StreamAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await foreach (var _ in orchestrator.StreamAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            EndpointOverride = "http://new-server:8080"
        })) { }

        // Assert
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Endpoint == "http://new-server:8080"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithAllThreeOverrides_SingleCreateAgentCallWithAllApplied()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model", "gpustack");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns(tempAgent);
        adapter.RunAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns("response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await orchestrator.ProcessAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            SystemPromptOverride = "injected context",
            ModelOverride = "runtime-model",
            EndpointOverride = "http://new-server:8080"
        });

        // Assert — CreateAgentAsync called exactly once with all three overrides applied
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c =>
                c.SystemPrompt == "injected context" &&
                c.Model.Deployment == "runtime-model" &&
                c.Model.Endpoint == "http://new-server:8080"),
            Arg.Any<CancellationToken>());
    }
}
