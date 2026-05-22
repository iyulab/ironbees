using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Ironbees.Core.Tests;

public class ModelOverrideTests
{
    private static AgentConfig MakeConfig(string name, string deployment = "default-model") =>
        new()
        {
            Name = name,
            Description = $"Description for {name}",
            Version = "1.0.0",
            SystemPrompt = "original prompt",
            Model = new ModelConfig { Provider = "test", Deployment = deployment }
        };

    private static IAgent MakeRegistryAgent(string name, string deployment = "default-model")
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns($"Description for {name}");
        agent.Config.Returns(MakeConfig(name, deployment));
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
    public async Task ProcessAsync_WithModelOverride_CreatesAgentWithOverriddenDeployment()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Deployment == "runtime-model"),
            Arg.Any<CancellationToken>())
            .Returns(tempAgent);
        adapter.RunAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns("response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        var result = await orchestrator.ProcessAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            ModelOverride = "runtime-model"
        });

        // Assert
        Assert.Equal("response", result);
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Deployment == "runtime-model"),
            Arg.Any<CancellationToken>());
        await adapter.Received(1).RunAsync(
            tempAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithModelOverride_PreservesOtherModelFields()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        AgentConfig? capturedConfig = null;
        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(
            Arg.Any<AgentConfig>(),
            Arg.Any<CancellationToken>())
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
            ModelOverride = "runtime-model"
        });

        // Assert — only Deployment changes; Provider and other fields remain from original config
        Assert.NotNull(capturedConfig);
        Assert.Equal("runtime-model", capturedConfig!.Model.Deployment);
        Assert.Equal("test", capturedConfig.Model.Provider);
        Assert.Equal("original prompt", capturedConfig.SystemPrompt);
    }

    [Fact]
    public async Task ProcessAsync_WithoutModelOverride_UsesRegistryAgentDirectly()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model");
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
    public async Task ProcessAsync_WithBothOverrides_AppliesBothInSingleCreateAgentCall()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model");
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
            ModelOverride = "runtime-model"
        });

        // Assert — CreateAgentAsync called exactly once with both overrides applied
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c =>
                c.SystemPrompt == "injected context" &&
                c.Model.Deployment == "runtime-model"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_WithModelOverride_CreatesAgentWithOverriddenDeployment()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "yaml-model");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Deployment == "runtime-model"),
            Arg.Any<CancellationToken>())
            .Returns(tempAgent);
        adapter.StreamAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await foreach (var _ in orchestrator.StreamAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            ModelOverride = "runtime-model"
        })) { }

        // Assert
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Deployment == "runtime-model"),
            Arg.Any<CancellationToken>());
    }
}
