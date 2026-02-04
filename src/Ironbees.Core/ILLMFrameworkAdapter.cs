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
}
