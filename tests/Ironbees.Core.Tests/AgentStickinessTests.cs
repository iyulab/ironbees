using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Ironbees.Core.Tests;

public class AgentStickinessTests
{
    private static IAgent CreateMockAgent(string name)
    {
        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns(name);
        mockAgent.Description.Returns($"Agent: {name}");
        return mockAgent;
    }

    private static AgentOrchestrator CreateOrchestrator(
        IAgentRegistry registry,
        ILLMFrameworkAdapter adapter,
        IAgentSelector selector,
        IConversationStore? conversationStore = null)
    {
        var mockLoader = Substitute.For<IAgentLoader>();
        return new AgentOrchestrator(
            mockLoader,
            registry,
            adapter,
            selector,
            agentsDirectory: null,
            conversationStore: conversationStore);
    }

    [Fact]
    public async Task SelectAgent_SameTopicContinues_KeepsCurrentAgent()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.ListAgents().Returns(new List<string> { "code-agent", "math-agent" });
        registry.GetAgent("code-agent").Returns(codeAgent);
        registry.GetAgent("math-agent").Returns(mathAgent);

        // Code agent scores 0.7, math agent scores 0.8 — delta = 0.1 < threshold 0.2
        selector.ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AgentScore>
            {
                new() { Agent = mathAgent, Score = 0.8 },
                new() { Agent = codeAgent, Score = 0.7 }
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

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns(existingState);

        adapter.RunAsync(
            codeAgent, // Should keep the code agent
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Sticky response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("Tell me more about loops", options);

        // Assert — should have called code-agent (sticky), not math-agent
        Assert.Equal("Sticky response", result);
        await adapter.Received(1).RunAsync(
            codeAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
        await adapter.DidNotReceive().RunAsync(
            mathAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAgent_NewTopicHigherScore_SwitchesAgent()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.ListAgents().Returns(new List<string> { "code-agent", "math-agent" });
        registry.GetAgent("code-agent").Returns(codeAgent);
        registry.GetAgent("math-agent").Returns(mathAgent);

        // Math agent scores much higher: 0.95 vs code agent 0.3 — delta = 0.65 > threshold 0.2
        selector.ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AgentScore>
            {
                new() { Agent = mathAgent, Score = 0.95 },
                new() { Agent = codeAgent, Score = 0.3 }
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

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns(existingState);

        adapter.RunAsync(
            mathAgent, // Should switch to math agent
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Math response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("What is the integral of x^2?", options);

        // Assert — should have switched to math-agent
        Assert.Equal("Math response", result);
        await adapter.Received(1).RunAsync(
            mathAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAgent_NewTopicMarginalScore_KeepsCurrentAgent()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.ListAgents().Returns(new List<string> { "code-agent", "math-agent" });
        registry.GetAgent("code-agent").Returns(codeAgent);
        registry.GetAgent("math-agent").Returns(mathAgent);

        // Math agent scores only slightly higher: delta = 0.19 < threshold 0.2
        selector.ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AgentScore>
            {
                new() { Agent = mathAgent, Score = 0.69 },
                new() { Agent = codeAgent, Score = 0.5 }
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

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns(existingState);

        adapter.RunAsync(
            codeAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Code response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        var result = await orchestrator.ProcessAsync("Something slightly mathy", options);

        // Assert — should keep code-agent (marginal difference)
        Assert.Equal("Code response", result);
        await adapter.Received(1).RunAsync(
            codeAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAgent_NoConversation_NormalSelection()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.ListAgents().Returns(new List<string> { "code-agent", "math-agent" });
        registry.GetAgent("code-agent").Returns(codeAgent);
        registry.GetAgent("math-agent").Returns(mathAgent);

        // Math agent scores highest
        selector.ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AgentScore>
            {
                new() { Agent = mathAgent, Score = 0.9 },
                new() { Agent = codeAgent, Score = 0.3 }
            });

        // No existing conversation
        conversationStore.LoadAsync("conv-new", Arg.Any<CancellationToken>())
            .Returns((ConversationState?)null);

        adapter.RunAsync(
            mathAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Math response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-new" };

        // Act
        var result = await orchestrator.ProcessAsync("What is 2+2?", options);

        // Assert — first turn, no stickiness, should select best agent (math)
        Assert.Equal("Math response", result);
        await adapter.Received(1).RunAsync(
            mathAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAgent_CustomStickinessThreshold_Respected()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.ListAgents().Returns(new List<string> { "code-agent", "math-agent" });
        registry.GetAgent("code-agent").Returns(codeAgent);
        registry.GetAgent("math-agent").Returns(mathAgent);

        // Delta = 0.3, with custom threshold of 0.5, should keep current
        selector.ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AgentScore>
            {
                new() { Agent = mathAgent, Score = 0.8 },
                new() { Agent = codeAgent, Score = 0.5 }
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

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns(existingState);

        adapter.RunAsync(
            codeAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Code response");

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
        await adapter.Received(1).RunAsync(
            codeAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgentSwitch_RecordsMetadata()
    {
        // Arrange
        var registry = Substitute.For<IAgentRegistry>();
        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var selector = Substitute.For<IAgentSelector>();
        var conversationStore = Substitute.For<IConversationStore>();

        var codeAgent = CreateMockAgent("code-agent");
        var mathAgent = CreateMockAgent("math-agent");

        registry.ListAgents().Returns(new List<string> { "code-agent", "math-agent" });
        registry.GetAgent("code-agent").Returns(codeAgent);
        registry.GetAgent("math-agent").Returns(mathAgent);

        // Strong switch signal: delta = 0.6 > threshold 0.2
        selector.ScoreAgentsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AgentScore>
            {
                new() { Agent = mathAgent, Score = 0.9 },
                new() { Agent = codeAgent, Score = 0.3 }
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

        conversationStore.LoadAsync("conv-1", Arg.Any<CancellationToken>())
            .Returns(existingState);

        adapter.RunAsync(
            mathAgent,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ChatMessage>?>(),
            Arg.Any<CancellationToken>())
            .Returns("Math response");

        var orchestrator = CreateOrchestrator(registry, adapter, selector, conversationStore);
        var options = new ProcessOptions { ConversationId = "conv-1" };

        // Act
        await orchestrator.ProcessAsync("Calculate integral", options);

        // Assert — AppendMessageAsync should be called (agent switch updates existing state)
        await conversationStore.Received(1).AppendMessageAsync(
            "conv-1",
            Arg.Is<ConversationMessage>(m => m.Role == "user" && m.Content == "Calculate integral"),
            Arg.Any<CancellationToken>());
        await conversationStore.Received(1).AppendMessageAsync(
            "conv-1",
            Arg.Is<ConversationMessage>(m => m.Role == "assistant" && m.Content == "Math response"),
            Arg.Any<CancellationToken>());
    }
}
