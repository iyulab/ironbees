using Ironbees.Core;
using Ironbees.Core.Streaming;
using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using AgentInvokeOptions = IronHive.Abstractions.Agent.AgentInvokeOptions;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;
using IronHiveSuggestionMode = IronHive.Abstractions.Messages.SuggestionMode;

namespace Ironbees.Ironhive.Tests;

/// <summary>
/// Structured-surface tests for IronhiveAdapter: neutral option mapping to
/// AgentInvokeOptions, suggestion extraction from responses, and the text
/// projection contract.
/// </summary>
public class IronhiveAdapterStructuredTests
{
    private readonly IronhiveAdapter _adapter;

    public IronhiveAdapterStructuredTests()
    {
        _adapter = new IronhiveAdapter(
            Substitute.For<IHiveService>(),
            Substitute.For<IIronhiveOrchestratorFactory>(),
            new OrchestrationEventMapper(),
            NullLogger<IronhiveAdapter>.Instance);
    }

    private static AgentConfig CreateTestConfig() => new()
    {
        Name = "test-agent",
        Description = "Test agent",
        Version = "1.0.0",
        Model = new ModelConfig { Provider = "openai", Deployment = "gpt-test" },
        SystemPrompt = "You are a test agent."
    };

    private static MessageResponse TextResponse(string text, List<Suggestion>? suggestions = null) => new()
    {
        Message = new Message
        {
            Role = MessageRole.Assistant,
            Content = new List<MessageContent> { new TextMessageContent { Value = text } }
        },
        Suggestions = suggestions,
    };

    [Fact]
    public async Task RunStructuredAsync_Should_Map_SuggestionRequest_To_InvokeOptions()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        AgentInvokeOptions? captured = null;
        mockAgent
            .InvokeAsync(
                Arg.Any<IEnumerable<Message>>(),
                Arg.Do<AgentInvokeOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(TextResponse("answer"));
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        var options = new AgentRunOptions
        {
            Suggestions = new SuggestionRequest
            {
                Mode = Ironbees.Core.SuggestionMode.Always,
                MaxCount = 2,
                MinItems = 3,
                MaxItems = 5,
            },
        };

        // Act
        await _adapter.RunStructuredAsync(wrapper, "Hi", options: options, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(captured);
        Assert.NotNull(captured!.Suggestions);
        Assert.Equal(IronHiveSuggestionMode.Always, captured.Suggestions!.Mode);
        Assert.Equal(2, captured.Suggestions.MaxCount);
        Assert.Equal(3, captured.Suggestions.MinItems);
        Assert.Equal(5, captured.Suggestions.MaxItems);
    }

    [Fact]
    public async Task RunStructuredAsync_Should_Map_MaxToolTurns_To_MaxTurns_And_Report_Usage()
    {
        var mockAgent = Substitute.For<IronHiveAgent>();
        AgentInvokeOptions? captured = null;
        var response = TextResponse("answer");
        response.TokenUsage = new MessageTokenUsage { InputTokens = 7, OutputTokens = 3 };
        mockAgent
            .InvokeAsync(
                Arg.Any<IEnumerable<Message>>(),
                Arg.Do<AgentInvokeOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(response);
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        var result = await _adapter.RunStructuredAsync(
            wrapper, "Hi", options: new AgentRunOptions { MaxToolTurns = 4 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(4, captured!.MaxTurns);
        Assert.Equal(7, result.Usage!.InputTokenCount);
        Assert.Equal(3, result.Usage.OutputTokenCount);
    }

    [Fact]
    public async Task RunStructuredAsync_Should_Pass_Null_Options_When_Not_Requested()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        AgentInvokeOptions? captured = new AgentInvokeOptions();
        mockAgent
            .InvokeAsync(
                Arg.Any<IEnumerable<Message>>(),
                Arg.Do<AgentInvokeOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(TextResponse("answer"));
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        // Act
        await _adapter.RunStructuredAsync(wrapper, "Hi", cancellationToken: TestContext.Current.CancellationToken);

        // Assert — agent defaults apply, no synthetic options object
        Assert.Null(captured);
    }

    [Fact]
    public async Task RunStructuredAsync_Should_Map_ThinkingEffort_To_InvokeOptions_Without_Suggestions()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        AgentInvokeOptions? captured = null;
        mockAgent
            .InvokeAsync(
                Arg.Any<IEnumerable<Message>>(),
                Arg.Do<AgentInvokeOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(TextResponse("answer"));
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        // Act — ThinkingEffort alone must be enough to produce invoke options
        await _adapter.RunStructuredAsync(wrapper, "Hi", options: new AgentRunOptions { ThinkingEffort = ThinkingEffort.Medium }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(MessageThinkingEffort.Medium, captured!.ThinkingEffort);
        Assert.Null(captured.Suggestions);
    }

    [Fact]
    public async Task RunStructuredAsync_Should_Map_ThinkingEffort_None_As_Explicit_Off()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        AgentInvokeOptions? captured = null;
        mockAgent
            .InvokeAsync(
                Arg.Any<IEnumerable<Message>>(),
                Arg.Do<AgentInvokeOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(TextResponse("answer"));
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        // Act
        await _adapter.RunStructuredAsync(wrapper, "Hi", options: new AgentRunOptions { ThinkingEffort = ThinkingEffort.None }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — None is an explicit off request, distinct from "option not set" (null options)
        Assert.NotNull(captured);
        Assert.Equal(MessageThinkingEffort.None, captured!.ThinkingEffort);
    }

    [Fact]
    public async Task RunStructuredAsync_Should_Map_ThinkingEffort_And_Suggestions_Together()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        AgentInvokeOptions? captured = null;
        mockAgent
            .InvokeAsync(
                Arg.Any<IEnumerable<Message>>(),
                Arg.Do<AgentInvokeOptions?>(o => captured = o),
                Arg.Any<CancellationToken>())
            .Returns(TextResponse("answer"));
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        // Act
        await _adapter.RunStructuredAsync(wrapper, "Hi", options: new AgentRunOptions
        {
            ThinkingEffort = ThinkingEffort.High,
            Suggestions = new SuggestionRequest { MaxCount = 1 },
        }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(MessageThinkingEffort.High, captured!.ThinkingEffort);
        Assert.NotNull(captured.Suggestions);
        Assert.Equal(1, captured.Suggestions!.MaxCount);
    }

    [Fact]
    public async Task RunStructuredAsync_Should_Extract_Suggestions_From_Response()
    {
        // Arrange
        var suggestions = new List<Suggestion>
        {
            new() { Question = "Next topic?", Items = ["A", "B", "C"] },
        };
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(TextResponse("answer", suggestions));
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        // Act
        var result = await _adapter.RunStructuredAsync(wrapper, "Hi", options: new AgentRunOptions { Suggestions = new SuggestionRequest() }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("answer", result.Text);
        var suggestion = Assert.Single(result.Suggestions!);
        Assert.Equal("Next topic?", suggestion.Question);
        Assert.Equal(["A", "B", "C"], suggestion.Items);
    }

    [Fact]
    public async Task StreamStructuredAsync_Should_Yield_Text_Suggestions_Usage_And_Completion()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeStreamingAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream());
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        static async IAsyncEnumerable<StreamingMessageResponse> Stream()
        {
            yield return new StreamingContentDeltaResponse { Index = 0, Delta = new TextDeltaContent { Value = "Hel" } };
            yield return new StreamingContentDeltaResponse { Index = 0, Delta = new TextDeltaContent { Value = "lo" } };
            yield return new StreamingMessageDoneResponse
            {
                TokenUsage = new MessageTokenUsage { InputTokens = 10, OutputTokens = 20 },
                Suggestions = [new Suggestion { Question = "More?", Items = ["Yes", "No"] }],
            };
            await Task.CompletedTask;
        }

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _adapter.StreamStructuredAsync(wrapper, "Hi", options: new AgentRunOptions { Suggestions = new SuggestionRequest() }, cancellationToken: TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal("Hel", Assert.IsType<TextChunk>(chunks[0]).Content);
        Assert.Equal("lo", Assert.IsType<TextChunk>(chunks[1]).Content);
        var usage = Assert.IsType<UsageChunk>(chunks[2]);
        Assert.Equal(10, usage.InputTokens);
        Assert.Equal(20, usage.OutputTokens);
        var suggestionsChunk = Assert.IsType<SuggestionsChunk>(chunks[3]);
        var suggestion = Assert.Single(suggestionsChunk.Suggestions);
        Assert.Equal("More?", suggestion.Question);
        Assert.IsType<CompletionChunk>(chunks[4]);
        Assert.Equal(5, chunks.Count);
    }

    [Fact]
    public async Task StreamStructuredAsync_Should_Map_ThinkingDeltas_To_ThinkingChunks()
    {
        // Arrange — reasoning models interleave thinking deltas with text deltas
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeStreamingAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream());
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        static async IAsyncEnumerable<StreamingMessageResponse> Stream()
        {
            yield return new StreamingContentDeltaResponse { Index = 0, Delta = new ThinkingDeltaContent { Data = "reason-1" } };
            yield return new StreamingContentDeltaResponse { Index = 0, Delta = new ThinkingDeltaContent { Data = "reason-2" } };
            yield return new StreamingContentDeltaResponse { Index = 1, Delta = new TextDeltaContent { Value = "answer" } };
            yield return new StreamingMessageDoneResponse();
            await Task.CompletedTask;
        }

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _adapter.StreamStructuredAsync(wrapper, "Hi", cancellationToken: TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert — thinking arrives as ThinkingChunk, in stream order, before text
        Assert.Equal("reason-1", Assert.IsType<ThinkingChunk>(chunks[0]).Content);
        Assert.Equal("reason-2", Assert.IsType<ThinkingChunk>(chunks[1]).Content);
        Assert.Equal("answer", Assert.IsType<TextChunk>(chunks[2]).Content);
        Assert.IsType<CompletionChunk>(chunks[3]);
        Assert.Equal(4, chunks.Count);
    }

    [Fact]
    public async Task StreamAsync_Text_Projection_Should_Not_Leak_Thinking()
    {
        // Arrange — legacy string projection must remain text-only
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeStreamingAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream());
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        static async IAsyncEnumerable<StreamingMessageResponse> Stream()
        {
            yield return new StreamingContentDeltaResponse { Index = 0, Delta = new ThinkingDeltaContent { Data = "hidden reasoning" } };
            yield return new StreamingContentDeltaResponse { Index = 1, Delta = new TextDeltaContent { Value = "visible answer" } };
            yield return new StreamingMessageDoneResponse();
            await Task.CompletedTask;
        }

        // Act
        var parts = new List<string>();
        await foreach (var part in _adapter.StreamAsync(wrapper, "Hi", conversationHistory: null, cancellationToken: TestContext.Current.CancellationToken))
        {
            parts.Add(part);
        }

        // Assert
        Assert.Equal(["visible answer"], parts);
    }

    [Fact]
    public async Task StreamStructuredAsync_Should_Yield_Fatal_ErrorChunk_And_Stop()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeStreamingAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream());
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        static async IAsyncEnumerable<StreamingMessageResponse> Stream()
        {
            yield return new StreamingMessageErrorResponse { Code = "500", Message = "boom" };
            yield return new StreamingContentDeltaResponse { Index = 0, Delta = new TextDeltaContent { Value = "never" } };
            await Task.CompletedTask;
        }

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _adapter.StreamStructuredAsync(wrapper, "Hi", cancellationToken: TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert — fatal error terminates the stream without a CompletionChunk
        var error = Assert.IsType<ErrorChunk>(Assert.Single(chunks));
        Assert.Equal("boom", error.Error);
        Assert.Equal("500", error.ErrorCode);
        Assert.True(error.IsFatal);
    }

    [Fact]
    public async Task StreamAsync_Text_Projection_Should_Preserve_Legacy_Error_Format()
    {
        // Arrange
        var mockAgent = Substitute.For<IronHiveAgent>();
        mockAgent
            .InvokeStreamingAsync(Arg.Any<IEnumerable<Message>>(), Arg.Any<AgentInvokeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream());
        var wrapper = new IronhiveAgentWrapper(mockAgent, CreateTestConfig());

        static async IAsyncEnumerable<StreamingMessageResponse> Stream()
        {
            yield return new StreamingContentDeltaResponse { Index = 0, Delta = new TextDeltaContent { Value = "partial" } };
            yield return new StreamingMessageErrorResponse { Code = "429", Message = "rate limited" };
            await Task.CompletedTask;
        }

        // Act
        var parts = new List<string>();
        await foreach (var part in _adapter.StreamAsync(wrapper, "Hi", conversationHistory: null, cancellationToken: TestContext.Current.CancellationToken))
        {
            parts.Add(part);
        }

        // Assert
        Assert.Equal(["partial", "[Error 429]: rate limited"], parts);
    }
}
