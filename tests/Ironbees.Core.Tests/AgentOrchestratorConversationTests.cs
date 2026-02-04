using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;
using Moq;

namespace Ironbees.Core.Tests;

public class AgentOrchestratorConversationTests
{
    private static AgentOrchestrator CreateOrchestrator(
        Mock<IAgentRegistry>? registry = null,
        Mock<ILLMFrameworkAdapter>? adapter = null,
        Mock<IAgentSelector>? selector = null,
        Mock<IConversationStore>? conversationStore = null)
    {
        var mockLoader = new Mock<IAgentLoader>();
        registry ??= new Mock<IAgentRegistry>();
        adapter ??= new Mock<ILLMFrameworkAdapter>();
        selector ??= new Mock<IAgentSelector>();

        return new AgentOrchestrator(
            mockLoader.Object,
            registry.Object,
            adapter.Object,
            selector.Object,
            agentsDirectory: null,
            conversationStore: conversationStore?.Object);
    }

    private static Mock<IAgent> CreateMockAgent(string name = "test-agent")
    {
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns(name);
        mockAgent.Setup(a => a.Description).Returns($"Description for {name}");
        return mockAgent;
    }

    private static void SetupRegistryWithAgent(Mock<IAgentRegistry> registry, Mock<IAgent> agent)
    {
        registry.Setup(r => r.ListAgents()).Returns(new List<string> { agent.Object.Name });
        registry.Setup(r => r.Get(agent.Object.Name)).Returns(agent.Object);
    }

    private static void SetupSelectorForAgent(Mock<IAgentSelector> selector, Mock<IAgent> agent, double score = 0.9)
    {
        selector.Setup(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = agent.Object,
                ConfidenceScore = score,
                SelectionReason = "Test selection"
            });

        selector.Setup(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentScore>
            {
                new() { Agent = agent.Object, Score = score }
            });
    }

    [Fact]
    public async Task ProcessAsync_WithConversationId_SavesMessages()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        adapter.Setup(a => a.RunAsync(
            agent.Object,
            "Hello",
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hi there!");

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationState?)null);

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("Hello", options);

        // Assert
        Assert.Equal("Hi there!", result);
        conversationStore.Verify(s => s.SaveAsync(
            It.Is<ConversationState>(cs =>
                cs.ConversationId == "conv-1" &&
                cs.AgentName == "test-agent" &&
                cs.Messages.Count == 2 &&
                cs.Messages[0].Role == "user" &&
                cs.Messages[0].Content == "Hello" &&
                cs.Messages[1].Role == "assistant" &&
                cs.Messages[1].Content == "Hi there!"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithConversationId_LoadsHistory()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();
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

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        IReadOnlyList<ChatMessage>? capturedHistory = null;
        adapter.Setup(a => a.RunAsync(
            agent.Object,
            "Tell me more",
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .Callback<IAgent, string, IReadOnlyList<ChatMessage>?, CancellationToken>(
                (_, _, history, _) => capturedHistory = history)
            .ReturnsAsync("C# supports OOP, generics, and more.");

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
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        adapter.Setup(a => a.RunAsync(
            agent.Object,
            "Hello",
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hi!");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions(); // No ConversationId

        // Act
        var result = await orchestrator.ProcessAsync("Hello", options);

        // Assert
        Assert.Equal("Hi!", result);
        conversationStore.Verify(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        conversationStore.Verify(s => s.SaveAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Never);
        conversationStore.Verify(s => s.AppendMessageAsync(
            It.IsAny<string>(), It.IsAny<ConversationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithAgentName_UsesSpecifiedAgent()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var agent = CreateMockAgent("specific-agent");

        registry.Setup(r => r.Get("specific-agent")).Returns(agent.Object);

        adapter.Setup(a => a.RunAsync(
            agent.Object,
            "Hello",
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response from specific agent");

        var orchestrator = CreateOrchestrator(registry, adapter, selector);
        var options = new ProcessOptions { AgentName = "specific-agent" };

        // Act
        var result = await orchestrator.ProcessAsync("Hello", options);

        // Assert
        Assert.Equal("Response from specific agent", result);
        // Selector should NOT be called when AgentName is provided
        selector.Verify(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithMaxHistoryTurns_LimitsHistory()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();
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

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        IReadOnlyList<ChatMessage>? capturedHistory = null;
        adapter.Setup(a => a.RunAsync(
            agent.Object,
            "Turn 4",
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .Callback<IAgent, string, IReadOnlyList<ChatMessage>?, CancellationToken>(
                (_, _, history, _) => capturedHistory = history)
            .ReturnsAsync("Response 4");

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
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationState?)null);

        adapter.Setup(a => a.StreamAsync(
            agent.Object,
            "Hello",
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
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
        Assert.Equal(new[] { "Hi", " there", "!" }, chunks);
        conversationStore.Verify(s => s.SaveAsync(
            It.Is<ConversationState>(cs =>
                cs.ConversationId == "conv-1" &&
                cs.AgentName == "test-agent" &&
                cs.Messages.Count == 2 &&
                cs.Messages[0].Content == "Hello" &&
                cs.Messages[1].Content == "Hi there!"),
            It.IsAny<CancellationToken>()),
            Times.Once);
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
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var agent = CreateMockAgent();

        SetupRegistryWithAgent(registry, agent);
        SetupSelectorForAgent(selector, agent);

        adapter.Setup(a => a.RunAsync(
            agent.Object,
            "Hello",
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hi!");

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
