using Ironbees.Core;
using Ironbees.Core.Streaming;

namespace Ironbees.Core.Tests;

/// <summary>
/// Regression tests for the ILLMFrameworkAdapter structured-surface default
/// implementations: text delegation when no structured feature is requested,
/// and fail-loud (never silent drop) when suggestions are requested from an
/// adapter that has not implemented them.
/// </summary>
public class LLMFrameworkAdapterStructuredDefaultsTests
{
    /// <summary>
    /// Minimal adapter implementing only the text surface — structured methods
    /// come from the interface defaults.
    /// </summary>
    private sealed class TextOnlyAdapter : ILLMFrameworkAdapter
    {
        public Task<IAgent> CreateAgentAsync(AgentConfig config, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<string> RunAsync(IAgent agent, string input, CancellationToken cancellationToken = default)
            => Task.FromResult($"echo:{input}");

        public async IAsyncEnumerable<string> StreamAsync(
            IAgent agent,
            string input,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "echo:";
            yield return input;
            await Task.CompletedTask;
        }
    }

    private static IAgent DummyAgent => null!; // defaults never dereference the agent

    [Fact]
    public async Task RunStructuredAsync_Default_Should_Project_Text_When_No_Options()
    {
        ILLMFrameworkAdapter adapter = new TextOnlyAdapter();

        var result = await adapter.RunStructuredAsync(DummyAgent, "hi");

        Assert.Equal("echo:hi", result.Text);
        Assert.Null(result.Suggestions);
    }

    [Fact]
    public async Task RunStructuredAsync_Default_Should_FailLoud_When_Suggestions_Requested()
    {
        ILLMFrameworkAdapter adapter = new TextOnlyAdapter();
        var options = new AgentRunOptions { Suggestions = new SuggestionRequest() };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => adapter.RunStructuredAsync(DummyAgent, "hi", options: options));

        Assert.Contains("TextOnlyAdapter", ex.Message);
        Assert.Contains("suggestions", ex.Message);
    }

    [Fact]
    public async Task StreamStructuredAsync_Default_Should_Wrap_Text_Chunks_And_Complete()
    {
        ILLMFrameworkAdapter adapter = new TextOnlyAdapter();

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in adapter.StreamStructuredAsync(DummyAgent, "hi"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(3, chunks.Count);
        Assert.Equal("echo:", Assert.IsType<TextChunk>(chunks[0]).Content);
        Assert.Equal("hi", Assert.IsType<TextChunk>(chunks[1]).Content);
        Assert.IsType<CompletionChunk>(chunks[2]);
    }

    [Fact]
    public void StreamStructuredAsync_Default_Should_FailLoud_Eagerly_When_Suggestions_Requested()
    {
        ILLMFrameworkAdapter adapter = new TextOnlyAdapter();
        var options = new AgentRunOptions { Suggestions = new SuggestionRequest() };

        // Eager throw: the exception must surface at call time, before enumeration.
        var ex = Assert.Throws<NotSupportedException>(
            () => adapter.StreamStructuredAsync(DummyAgent, "hi", options: options));

        Assert.Contains("TextOnlyAdapter", ex.Message);
        Assert.Contains("suggestions", ex.Message);
    }

    [Fact]
    public async Task RunStructuredAsync_Default_Should_FailLoud_When_ThinkingEffort_Requested()
    {
        // Previously silently dropped — ThrowIfUnsupported only checked Suggestions,
        // contradicting AgentRunOptions' own "never silently drop" contract.
        ILLMFrameworkAdapter adapter = new TextOnlyAdapter();
        var options = new AgentRunOptions { ThinkingEffort = ThinkingEffort.Low };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => adapter.RunStructuredAsync(DummyAgent, "hi", options: options));

        Assert.Contains("TextOnlyAdapter", ex.Message);
        Assert.Contains("thinking effort", ex.Message);
    }

    [Fact]
    public async Task RunStructuredAsync_Default_Should_FailLoud_When_Tools_Requested()
    {
        ILLMFrameworkAdapter adapter = new TextOnlyAdapter();
        var options = new AgentRunOptions { Tools = [] };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => adapter.RunStructuredAsync(DummyAgent, "hi", options: options));

        Assert.Contains("TextOnlyAdapter", ex.Message);
        Assert.Contains("tools", ex.Message);
    }
}
