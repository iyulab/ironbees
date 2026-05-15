using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Ironbees.Core.Tests;

public class SystemPromptOverrideTests
{
    private static AgentConfig MakeConfig(string name, string systemPrompt = "original prompt") =>
        new()
        {
            Name = name,
            Description = $"Description for {name}",
            Version = "1.0.0",
            SystemPrompt = systemPrompt,
            Model = new ModelConfig { Provider = "test", Deployment = "model" }
        };

    private static IAgent MakeRegistryAgent(string name, string systemPrompt = "original prompt")
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns($"Description for {name}");
        agent.Config.Returns(MakeConfig(name, systemPrompt));
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
    public async Task ProcessAsync_WithSystemPromptOverride_CreatesAgentWithOverriddenPrompt()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "original prompt");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.SystemPrompt == "dynamic RAG context"),
            Arg.Any<CancellationToken>())
            .Returns(tempAgent);
        adapter.RunAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns("response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        var result = await orchestrator.ProcessAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            SystemPromptOverride = "dynamic RAG context"
        });

        // Assert
        Assert.Equal("response", result);
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.SystemPrompt == "dynamic RAG context"),
            Arg.Any<CancellationToken>());
        await adapter.Received(1).RunAsync(
            tempAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithoutSystemPromptOverride_UsesRegistryAgentDirectly()
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
    public async Task StreamAsync_WithSystemPromptOverride_CreatesAgentWithOverriddenPrompt()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();

        var registryAgent = MakeRegistryAgent("rag-agent", "original");
        registry.GetAgent("rag-agent").Returns(registryAgent);

        var tempAgent = Substitute.For<IAgent>();
        tempAgent.Name.Returns("rag-agent");
        adapter.CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.SystemPrompt == "injected prompt"),
            Arg.Any<CancellationToken>())
            .Returns(tempAgent);
        adapter.StreamAsync(tempAgent, Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<string>());

        var orchestrator = CreateOrchestrator(registry, adapter, selector);

        // Act
        await foreach (var _ in orchestrator.StreamAsync("question", new ProcessOptions
        {
            AgentName = "rag-agent",
            SystemPromptOverride = "injected prompt"
        })) { }

        // Assert
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.SystemPrompt == "injected prompt"),
            Arg.Any<CancellationToken>());
    }
}
