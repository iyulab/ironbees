using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;
using Moq;

namespace Ironbees.Core.Tests;

public class AgentStickinessTests
{
    private static Mock<IAgent> CreateMockAgent(string name)
    {
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns(name);
        mockAgent.Setup(a => a.Description).Returns($"Agent: {name}");
        return mockAgent;
    }

    private static AgentOrchestrator CreateOrchestrator(
        Mock<IAgentRegistry> registry,
        Mock<ILLMFrameworkAdapter> adapter,
        Mock<IAgentSelector> selector,
        Mock<IConversationStore>? conversationStore = null)
    {
        var mockLoader = new Mock<IAgentLoader>();
        return new AgentOrchestrator(
            mockLoader.Object,
            registry.Object,
            adapter.Object,
            selector.Object,
            agentsDirectory: null,
            conversationStore: conversationStore?.Object);
    }

    [Fact]
    public async Task SelectAgent_SameTopicContinues_KeepsCurrentAgent()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.Setup(r => r.ListAgents()).Returns(new List<string> { "code-agent", "math-agent" });
        registry.Setup(r => r.Get("code-agent")).Returns(codeAgent.Object);
        registry.Setup(r => r.Get("math-agent")).Returns(mathAgent.Object);

        // Code agent scores 0.7, math agent scores 0.8 — delta = 0.1 < threshold 0.2
        selector.Setup(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentScore>
            {
                new() { Agent = mathAgent.Object, Score = 0.8 },
                new() { Agent = codeAgent.Object, Score = 0.7 }
            });

        // Existing conversation with code-agent
        var existingState = new ConversationState
        {
            ConversationId = "conv-1",
            AgentName = "code-agent",
            Messages = new List<ConversationMessage>
            {
                new() { Role = "user", Content = "How do I write a loop?" },
                new() { Role = "assistant", Content = "Use a for loop." }
            }
        };

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        adapter.Setup(a => a.RunAsync(
            codeAgent.Object, // Should keep the code agent
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Sticky response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("Tell me more about loops", options);

        // Assert — should have called code-agent (sticky), not math-agent
        Assert.Equal("Sticky response", result);
        adapter.Verify(a => a.RunAsync(
            codeAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        adapter.Verify(a => a.RunAsync(
            mathAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SelectAgent_NewTopicHigherScore_SwitchesAgent()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.Setup(r => r.ListAgents()).Returns(new List<string> { "code-agent", "math-agent" });
        registry.Setup(r => r.Get("code-agent")).Returns(codeAgent.Object);
        registry.Setup(r => r.Get("math-agent")).Returns(mathAgent.Object);

        // Math agent scores much higher: 0.95 vs code agent 0.3 — delta = 0.65 > threshold 0.2
        selector.Setup(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentScore>
            {
                new() { Agent = mathAgent.Object, Score = 0.95 },
                new() { Agent = codeAgent.Object, Score = 0.3 }
            });

        // Existing conversation with code-agent
        var existingState = new ConversationState
        {
            ConversationId = "conv-1",
            AgentName = "code-agent",
            Messages = new List<ConversationMessage>
            {
                new() { Role = "user", Content = "How do I write a loop?" },
                new() { Role = "assistant", Content = "Use a for loop." }
            }
        };

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        adapter.Setup(a => a.RunAsync(
            mathAgent.Object, // Should switch to math agent
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Math response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("What is the integral of x^2?", options);

        // Assert — should have switched to math-agent
        Assert.Equal("Math response", result);
        adapter.Verify(a => a.RunAsync(
            mathAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectAgent_NewTopicMarginalScore_KeepsCurrentAgent()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.Setup(r => r.ListAgents()).Returns(new List<string> { "code-agent", "math-agent" });
        registry.Setup(r => r.Get("code-agent")).Returns(codeAgent.Object);
        registry.Setup(r => r.Get("math-agent")).Returns(mathAgent.Object);

        // Math agent scores only slightly higher: delta = 0.19 < threshold 0.2
        selector.Setup(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentScore>
            {
                new() { Agent = mathAgent.Object, Score = 0.69 },
                new() { Agent = codeAgent.Object, Score = 0.5 }
            });

        var existingState = new ConversationState
        {
            ConversationId = "conv-1",
            AgentName = "code-agent",
            Messages = new List<ConversationMessage>
            {
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", Content = "Hi!" }
            }
        };

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        adapter.Setup(a => a.RunAsync(
            codeAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Code response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("Something slightly mathy", options);

        // Assert — should keep code-agent (marginal difference)
        Assert.Equal("Code response", result);
        adapter.Verify(a => a.RunAsync(
            codeAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectAgent_NoConversation_NormalSelection()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.Setup(r => r.ListAgents()).Returns(new List<string> { "code-agent", "math-agent" });
        registry.Setup(r => r.Get("code-agent")).Returns(codeAgent.Object);
        registry.Setup(r => r.Get("math-agent")).Returns(mathAgent.Object);

        // Math agent scores highest
        selector.Setup(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentScore>
            {
                new() { Agent = mathAgent.Object, Score = 0.9 },
                new() { Agent = codeAgent.Object, Score = 0.3 }
            });

        // No existing conversation
        conversationStore.Setup(s => s.LoadAsync("conv-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationState?)null);

        adapter.Setup(a => a.RunAsync(
            mathAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Math response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-new" };

        // Act
        var result = await orchestrator.ProcessAsync("What is 2+2?", options);

        // Assert — first turn, no stickiness, should select best agent (math)
        Assert.Equal("Math response", result);
        adapter.Verify(a => a.RunAsync(
            mathAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectAgent_CustomStickinessThreshold_Respected()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.Setup(r => r.ListAgents()).Returns(new List<string> { "code-agent", "math-agent" });
        registry.Setup(r => r.Get("code-agent")).Returns(codeAgent.Object);
        registry.Setup(r => r.Get("math-agent")).Returns(mathAgent.Object);

        // Delta = 0.3, with custom threshold of 0.5, should keep current
        selector.Setup(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentScore>
            {
                new() { Agent = mathAgent.Object, Score = 0.8 },
                new() { Agent = codeAgent.Object, Score = 0.5 }
            });

        var existingState = new ConversationState
        {
            ConversationId = "conv-1",
            AgentName = "code-agent",
            Messages = new List<ConversationMessage>
            {
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", Content = "Hi!" }
            }
        };

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        adapter.Setup(a => a.RunAsync(
            codeAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Code response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions
        {
            ConversationId = "conv-1",
            StickinessThreshold = 0.5 // Higher threshold = harder to switch
        };

        // Act
        var result = await orchestrator.ProcessAsync("Math question", options);

        // Assert — delta 0.3 < threshold 0.5, should keep code-agent
        Assert.Equal("Code response", result);
        adapter.Verify(a => a.RunAsync(
            codeAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AgentSwitch_RecordsMetadata()
    {
        // Arrange
        var registry = new Mock<IAgentRegistry>();
        var adapter = new Mock<ILLMFrameworkAdapter>();
        var selector = new Mock<IAgentSelector>();
        var conversationStore = new Mock<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.Setup(r => r.ListAgents()).Returns(new List<string> { "code-agent", "math-agent" });
        registry.Setup(r => r.Get("code-agent")).Returns(codeAgent.Object);
        registry.Setup(r => r.Get("math-agent")).Returns(mathAgent.Object);

        // Strong switch signal: delta = 0.6 > threshold 0.2
        selector.Setup(s => s.ScoreAgentsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentScore>
            {
                new() { Agent = mathAgent.Object, Score = 0.9 },
                new() { Agent = codeAgent.Object, Score = 0.3 }
            });

        var existingState = new ConversationState
        {
            ConversationId = "conv-1",
            AgentName = "code-agent",
            Messages = new List<ConversationMessage>
            {
                new() { Role = "user", Content = "Hello" },
                new() { Role = "assistant", Content = "Hi!" }
            }
        };

        conversationStore.Setup(s => s.LoadAsync("conv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingState);

        adapter.Setup(a => a.RunAsync(
            mathAgent.Object,
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ChatMessage>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Math response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        await orchestrator.ProcessAsync("Calculate integral", options);

        // Assert — AppendMessageAsync should be called (agent switch updates existing state)
        conversationStore.Verify(s => s.AppendMessageAsync(
            "conv-1",
            It.Is<ConversationMessage>(m => m.Role == "user" && m.Content == "Calculate integral"),
            It.IsAny<CancellationToken>()), Times.Once);
        conversationStore.Verify(s => s.AppendMessageAsync(
            "conv-1",
            It.Is<ConversationMessage>(m => m.Role == "assistant" && m.Content == "Math response"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
