using Ironbees.Core.Conversation;
using Ironbees.Core.Streaming;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace Ironbees.Core.Tests;

/// <summary>
/// Structured-surface tests for AgentOrchestrator: ProcessOptions.Suggestions
/// passthrough to the adapter, structured chunk delivery, text projection, and
/// text-only conversation persistence.
/// </summary>
public class AgentOrchestratorStructuredTests
{
    private readonly IAgentRegistry _registry = Substitute.For<IAgentRegistry>();
    private readonly ILLMFrameworkAdapter _adapter = Substitute.For<ILLMFrameworkAdapter>();
    private readonly IAgentSelector _selector = Substitute.For<IAgentSelector>();
    private readonly IConversationStore _store = Substitute.For<IConversationStore>();
    private readonly IAgent _agent;

    public AgentOrchestratorStructuredTests()
    {
        _agent = Substitute.For<IAgent>();
        _agent.Name.Returns("test-agent");
        _registry.ListAgents().Returns(new List<string> { "test-agent" });
        _registry.GetAgent("test-agent").Returns(_agent);
    }

    private AgentOrchestrator CreateOrchestrator(bool withStore = false) => new(
        Substitute.For<IAgentLoader>(),
        _registry,
        _adapter,
        _selector,
        agentsDirectory: null,
        conversationStore: withStore ? _store : null);

    private static readonly IReadOnlyList<AgentSuggestion> TestSuggestions =
        [new AgentSuggestion("Next?", ["A", "B"])];

    [Fact]
    public async Task ProcessStructuredAsync_Should_Pass_Suggestions_To_Adapter_And_Return_Them()
    {
        // Arrange
        AgentRunOptions? captured = null;
        _adapter.RunStructuredAsync(
                _agent, "Hi",
                Arg.Any<IReadOnlyList<ChatMessage>?>(),
                Arg.Do<AgentRunOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(new AgentRunResult { Text = "answer", Suggestions = TestSuggestions });

        var orchestrator = CreateOrchestrator();
        var request = new SuggestionRequest { MaxCount = 2 };

        // Act
        var result = await orchestrator.ProcessStructuredAsync(
            "Hi", new ProcessOptions { AgentName = "test-agent", Suggestions = request });

        // Assert
        Assert.Same(request, captured?.Suggestions);
        Assert.Equal("answer", result.Text);
        Assert.Same(TestSuggestions, result.Suggestions);
    }

    [Fact]
    public async Task ProcessStructuredAsync_Should_Pass_ThinkingEffort_To_Adapter_Without_Suggestions()
    {
        // Arrange
        AgentRunOptions? captured = null;
        _adapter.RunStructuredAsync(
                _agent, "Hi",
                Arg.Any<IReadOnlyList<ChatMessage>?>(),
                Arg.Do<AgentRunOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(new AgentRunResult { Text = "answer" });

        var orchestrator = CreateOrchestrator();

        // Act — ThinkingEffort alone must reach the adapter (no Suggestions required)
        await orchestrator.ProcessStructuredAsync(
            "Hi", new ProcessOptions { AgentName = "test-agent", ThinkingEffort = ThinkingEffort.Medium });

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(ThinkingEffort.Medium, captured!.ThinkingEffort);
        Assert.Null(captured.Suggestions);
    }

    [Fact]
    public async Task ProcessStructuredAsync_Should_Pass_Null_RunOptions_When_Not_Requested()
    {
        // Arrange
        AgentRunOptions? captured = new AgentRunOptions();
        _adapter.RunStructuredAsync(
                _agent, "Hi",
                Arg.Any<IReadOnlyList<ChatMessage>?>(),
                Arg.Do<AgentRunOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(new AgentRunResult { Text = "answer" });

        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.ProcessStructuredAsync("Hi", new ProcessOptions { AgentName = "test-agent" });

        // Assert — adapter defaults apply
        Assert.Null(captured);
    }

    [Fact]
    public async Task StreamStructuredAsync_Should_Yield_Chunks_And_Save_Text_Only()
    {
        // Arrange
        _adapter.StreamStructuredAsync(
                _agent, "Hi",
                Arg.Any<IReadOnlyList<ChatMessage>?>(),
                Arg.Any<AgentRunOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks());
        _store.LoadAsync("conv-1", Arg.Any<CancellationToken>()).Returns((ConversationState?)null);

        static async IAsyncEnumerable<StreamChunk> Chunks()
        {
            yield return new TextChunk("Hel");
            yield return new TextChunk("lo");
            yield return new SuggestionsChunk(TestSuggestions);
            yield return new CompletionChunk();
            await Task.CompletedTask;
        }

        var orchestrator = CreateOrchestrator(withStore: true);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in orchestrator.StreamStructuredAsync(
            "Hi", new ProcessOptions
            {
                AgentName = "test-agent",
                ConversationId = "conv-1",
                Suggestions = new SuggestionRequest(),
            }))
        {
            chunks.Add(chunk);
        }

        // Assert — all chunks delivered, saved assistant text excludes structured payloads
        Assert.Equal(4, chunks.Count);
        Assert.IsType<SuggestionsChunk>(chunks[2]);
        await _store.Received(1).SaveAsync(
            Arg.Is<ConversationState>(s =>
                s.Messages[1].Role == "assistant" && s.Messages[1].Content == "Hello"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_Text_Projection_Should_Filter_Structured_Chunks()
    {
        // Arrange
        _adapter.StreamStructuredAsync(
                _agent, "Hi",
                Arg.Any<IReadOnlyList<ChatMessage>?>(),
                Arg.Any<AgentRunOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Chunks());

        static async IAsyncEnumerable<StreamChunk> Chunks()
        {
            yield return new TextChunk("Hello");
            yield return new SuggestionsChunk(TestSuggestions);
            yield return new UsageChunk(1, 2);
            yield return new CompletionChunk();
            await Task.CompletedTask;
        }

        var orchestrator = CreateOrchestrator();

        // Act
        var parts = new List<string>();
        await foreach (var part in orchestrator.StreamAsync("Hi", new ProcessOptions { AgentName = "test-agent" }))
        {
            parts.Add(part);
        }

        // Assert — text only
        Assert.Equal(["Hello"], parts);
    }
}
