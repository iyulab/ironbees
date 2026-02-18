using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Ironbees.Core.Tests;

public class AgentOrchestratorConversationTests
{
    private static readonly string[] ExpectedStreamChunks = ["Hi", " there", "!"];

    private static AgentOrchestrator CreateOrchestrator(
        IAgentRegistry? registry = null,
        ILLMFrameworkAdapter? adapter = null,
        IAgentSelector? selector = null,
        IConversationStore? conversationStore = null)
    {
        var mockLoader = Substitute.For<IAgentLoader>();
        registry ??= Substitute.For<IAgentRegistry>();
        adapter ??= Substitute.For<ILLMFrameworkAdapter>();
        selector ??= Substitute.For<IAgentSelector>();

        return new AgentOrchestrator(
            mockLoader,
            registry,
            adapter,
            selector,
            agentsDirectory: null,
            conversationStore: conversationStore);
    }

    private static IAgent CreateMockAgent(string name = "test-agent")
    {
        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns(name);
        mockAgent.Description.Returns($"Description for {name}");
        return mockAgent;
    }

    private static void SetupRegistryWithAgent(IAgentRegistry registry, IAgent agent)
    {
        var agentName = agent.Name;
        registry.ListAgents().Returns(new List<string> { agentName });
        registry.GetAgent(agentName).Returns(agent);
    }

    private static void SetupSelectorForAgent(IAgentSelector selector, IAgent agent, double score = 0.9)
    {
        selector.SelectAgentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new AgentSelectionResult
            {
                SelectedAgent = agent,
                ConfidenceScore = score,
                SelectionReason = "Test selection"
            });

        selector.ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AgentScore>
            {
                new() { Agent = agent, Score = score }
            });
    }

    [Fact]
    public async Task ProcessAsync_WithConversationId_SavesMessages()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        adapter.RunAsync(
            agent,
            "Hello",
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Hi there!");

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns((ConversationState?)null);

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("Hello", options);

        // Assert
        Assert.Equal("Hi there!", result);
        await conversationStore.Received(1).SaveAsync(
            Arg.Is<ConversationState>(cs =>
                cs.ConversationId == "conv-1" &&
                cs.AgentName == "test-agent" &&
                cs.Messages.Count == 2 &&
                cs.Messages[0].Role == "user" &&
                cs.Messages[0].Content == "Hello" &&
                cs.Messages[1].Role == "assistant" &&
                cs.Messages[1].Content == "Hi there!"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithConversationId_LoadsHistory()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        var existingState = new ConversationState
        {
            ConversationId = "conv-1",
            AgentName = "test-agent",
            Messages = new List<ConversationMessage>
            {
                new() { Role = "user", Content = "What is C#?" },
                new() { Role = "assistant", Content = "C# is a programming language." }
            }
        };

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns(existingState);

        IReadOnlyList<ChatMessage>? capturedHistory = null;
        adapter.RunAsync(
            agent,
            "Tell me more",
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedHistory = callInfo.ArgAt<IReadOnlyList<ChatMessage>?>(2);
                return "C# supports OOP, generics, and more.";
            });

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("Tell me more", options);

        // Assert
        Assert.Equal("C# supports OOP, generics, and more.", result);
        Assert.NotNull(capturedHistory);
        Assert.Equal(2, capturedHistory!.Count);
        Assert.Equal(ChatRole.User, capturedHistory[0].Role);
        Assert.Equal("What is C#?", capturedHistory[0].Text);
        Assert.Equal(ChatRole.Assistant, capturedHistory[1].Role);
        Assert.Equal("C# is a programming language.", capturedHistory[1].Text);
    }

    [Fact]
    public async Task ProcessAsync_WithoutConversationId_SkipsStore()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        adapter.RunAsync(
            agent,
            "Hello",
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Hi!");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions(); // No ConversationId

        // Act
        var result = await orchestrator.ProcessAsync("Hello", options);

        // Assert
        Assert.Equal("Hi!", result);
        await conversationStore.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await conversationStore.DidNotReceive().SaveAsync(Arg.Any<ConversationState>(), Arg.Any<CancellationToken>());
        await conversationStore.DidNotReceive().AppendMessageAsync(
            Arg.Any<string>(), Arg.Any<ConversationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithAgentName_UsesSpecifiedAgent()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var agent = CreateMockAgent("specific-agent");

        registry.GetAgent("specific-agent").Returns(agent);

        adapter.RunAsync(
            agent,
            "Hello",
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Response from specific agent");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);
        var options = new ProcessOptions { AgentName = "specific-agent" };

        // Act
        var result = await orchestrator.ProcessAsync("Hello", options);

        // Assert
        Assert.Equal("Response from specific agent", result);
        // Selector should NOT be called when AgentName is provided
        await selector.DidNotReceive().ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithMaxHistoryTurns_LimitsHistory()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        var existingState = new ConversationState
        {
            ConversationId = "conv-1",
            AgentName = "test-agent",
            Messages = new List<ConversationMessage>
            {
                new() { Role = "user", Content = "Turn 1" },
                new() { Role = "assistant", Content = "Response 1" },
                new() { Role = "user", Content = "Turn 2" },
                new() { Role = "assistant", Content = "Response 2" },
                new() { Role = "user", Content = "Turn 3" },
                new() { Role = "assistant", Content = "Response 3" }
            }
        };

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns(existingState);

        IReadOnlyList<ChatMessage>? capturedHistory = null;
        adapter.RunAsync(
            agent,
            "Turn 4",
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedHistory = callInfo.ArgAt<IReadOnlyList<ChatMessage>?>(2);
                return "Response 4";
            });

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions
        {
            ConversationId = "conv-1",
            MaxHistoryTurns = 2 // Only last 2 turns (4 messages)
        };

        // Act
        await orchestrator.ProcessAsync("Turn 4", options);

        // Assert — should only include last 2 turns (Turn 2/Response 2, Turn 3/Response 3)
        Assert.NotNull(capturedHistory);
        Assert.Equal(4, capturedHistory!.Count);
        Assert.Equal("Turn 2", capturedHistory[0].Text);
        Assert.Equal("Response 2", capturedHistory[1].Text);
        Assert.Equal("Turn 3", capturedHistory[2].Text);
        Assert.Equal("Response 3", capturedHistory[3].Text);
    }

    [Fact]
    public async Task StreamAsync_WithConversationId_SavesMessages()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns((ConversationState?)null);

        adapter.StreamAsync(
            agent,
            "Hello",
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable("Hi", " there", "!"));

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in orchestrator.StreamAsync("Hello", options))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(ExpectedStreamChunks, chunks);
        await conversationStore.Received(1).SaveAsync(
            Arg.Is<ConversationState>(cs =>
                cs.ConversationId == "conv-1" &&
                cs.AgentName == "test-agent" &&
                cs.Messages.Count == 2 &&
                cs.Messages[0].Content == "Hello" &&
                cs.Messages[1].Content == "Hi there!"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        var orchestrator = CreateOrchestrator();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await orchestrator.ProcessAsync("Hello", (ProcessOptions)null!));
    }

    [Fact]
    public async Task ProcessAsync_WithConversationId_NoConversationStore_SkipsStore()
    {
        // Arrange — orchestrator created WITHOUT conversation store
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        adapter.RunAsync(
            agent,
            "Hello",
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Hi!");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore: null);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act — should work fine without conversation store
        var result = await orchestrator.ProcessAsync("Hello", options);

        // Assert
        Assert.Equal("Hi!", result);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
