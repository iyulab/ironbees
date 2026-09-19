using Ironbees.Core.Streaming;
using Microsoft.Extensions.AI;

namespace Ironbees.Core;

/// <summary>
/// Adapter interface for different LLM frameworks (MS Agent Framework, Semantic Kernel, etc.)
/// </summary>
public interface ILLMFrameworkAdapter
{
    /// <summary>
    /// Create an agent from configuration
    /// </summary>
    /// <param name="config">Agent configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created agent instance</returns>
    Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run agent with input and get response
    /// </summary>
    /// <param name="agent">Agent to run</param>
    /// <param name="input">User input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response</returns>
    Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run agent with input, conversation history, and get response.
    /// Default implementation ignores history and delegates to the base RunAsync.
    /// </summary>
    /// <param name="agent">Agent to run</param>
    /// <param name="input">User input</param>
    /// <param name="conversationHistory">Previous conversation messages (user/assistant pairs)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response</returns>
    Task<string> RunAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory,
        CancellationToken cancellationToken = default)
        => RunAsync(agent, input, cancellationToken);

    /// <summary>
    /// Stream agent response
    /// </summary>
    /// <param name="agent">Agent to run</param>
    /// <param name="input">User input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of response chunks</returns>
    /// <remarks>
    /// Implementations should use [EnumeratorCancellation] attribute on the cancellationToken parameter
    /// when implementing this method as an async iterator.
    /// </remarks>
    IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream agent response with conversation history.
    /// Default implementation ignores history and delegates to the base StreamAsync.
    /// </summary>
    /// <param name="agent">Agent to run</param>
    /// <param name="input">User input</param>
    /// <param name="conversationHistory">Previous conversation messages (user/assistant pairs)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of response chunks</returns>
    IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory,
        CancellationToken cancellationToken = default)
        => StreamAsync(agent, input, cancellationToken);

    /// <summary>
    /// Run agent and get a structured result (text plus optional structured data
    /// such as suggestions). The text-only RunAsync surface is a projection of this.
    /// Default implementation delegates to the text RunAsync and fails loud
    /// (<see cref="NotSupportedException"/>) when <paramref name="options"/> requests
    /// a feature the adapter has not implemented — options are never silently dropped.
    /// </summary>
    /// <param name="agent">Agent to run</param>
    /// <param name="input">User input</param>
    /// <param name="conversationHistory">Previous conversation messages (user/assistant pairs)</param>
    /// <param name="options">Per-invoke options. Null applies adapter defaults.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Structured agent run result</returns>
    async Task<AgentRunResult> RunStructuredAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnsupported(options);
        var text = await RunAsync(agent, input, conversationHistory, cancellationToken).ConfigureAwait(false);
        return new AgentRunResult { Text = text };
    }

    /// <summary>
    /// Stream agent response as typed <see cref="StreamChunk"/> events (text deltas,
    /// suggestions, usage, completion). The text-only StreamAsync surface is a
    /// projection of this. Default implementation wraps the text StreamAsync in
    /// <see cref="TextChunk"/> events and fails loud (<see cref="NotSupportedException"/>,
    /// thrown eagerly at call time) when <paramref name="options"/> requests a feature
    /// the adapter has not implemented — options are never silently dropped.
    /// </summary>
    /// <param name="agent">Agent to run</param>
    /// <param name="input">User input</param>
    /// <param name="conversationHistory">Previous conversation messages (user/assistant pairs)</param>
    /// <param name="options">Per-invoke options. Null applies adapter defaults.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of typed chunks</returns>
    IAsyncEnumerable<StreamChunk> StreamStructuredAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnsupported(options);
        return StreamAsTextChunksAsync(agent, input, conversationHistory, cancellationToken);
    }

    private async IAsyncEnumerable<StreamChunk> StreamAsTextChunksAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chunk in StreamAsync(agent, input, conversationHistory, cancellationToken).ConfigureAwait(false))
        {
            yield return new TextChunk(chunk);
        }

        yield return new CompletionChunk();
    }

    private void ThrowIfUnsupported(AgentRunOptions? options)
    {
        if (options?.Suggestions is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support structured suggestions. " +
                "Override RunStructuredAsync/StreamStructuredAsync to honor AgentRunOptions.Suggestions.");
        }

        if (options?.ThinkingEffort is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support per-invoke thinking effort. " +
                "Override RunStructuredAsync/StreamStructuredAsync to honor AgentRunOptions.ThinkingEffort.");
        }

        if (options?.Tools is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support per-invoke tools. " +
                "Override RunStructuredAsync/StreamStructuredAsync to honor AgentRunOptions.Tools.");
        }

        if (options?.MaxTokens is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support a per-invoke output token cap. " +
                "Override RunStructuredAsync/StreamStructuredAsync to honor AgentRunOptions.MaxTokens.");
        }

        if (options?.MaxToolTurns is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support a per-invoke tool-turn limit. " +
                "Override RunStructuredAsync/StreamStructuredAsync to honor AgentRunOptions.MaxToolTurns.");
        }
    }
}
