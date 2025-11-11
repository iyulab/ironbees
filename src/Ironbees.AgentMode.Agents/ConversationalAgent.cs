using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Agents;

/// <summary>
/// Base class for conversational agents that provide request-response interactions.
/// Designed for simple Q and A agents, chatbots, domain experts, and assistants
/// with multi-provider LLM support.
/// </summary>
/// <remarks>
/// This class is independent of ICodingAgent and designed for stateless conversational interactions.
/// For workflow-based coding agents, use ICodingAgent instead.
///
/// Example usage:
/// <code>
/// public class CustomerSupportAgent : ConversationalAgent
/// {
///     public CustomerSupportAgent(IChatClient chatClient)
///         : base(chatClient, "You are a helpful customer support agent...")
///     {
///     }
/// }
///
/// var response = await agent.RespondAsync("How do I reset my password?");
/// </code>
/// </remarks>
public abstract class ConversationalAgent
{
    /// <summary>
    /// The chat client for LLM interactions (OpenAI, Azure, Anthropic, OpenAI-compatible).
    /// </summary>
    protected readonly IChatClient ChatClient;

    /// <summary>
    /// The system prompt that defines the agent's role and behavior.
    /// </summary>
    protected readonly string SystemPrompt;

    /// <summary>
    /// Initializes a new instance of the ConversationalAgent.
    /// </summary>
    /// <param name="chatClient">The chat client for LLM interactions. Supports multiple providers through Microsoft.Extensions.AI.</param>
    /// <param name="systemPrompt">The system prompt defining the agent's role and behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when chatClient or systemPrompt is null.</exception>
    protected ConversationalAgent(IChatClient chatClient, string systemPrompt)
    {
        ChatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        SystemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
    }

    /// <summary>
    /// Generates a response to a user message.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="options">Optional chat options for customizing the LLM behavior (temperature, max tokens, etc.).</param>
    /// <param name="cancellationToken">Cancellation token for interrupting the request.</param>
    /// <returns>The agent's response text.</returns>
    /// <exception cref="ArgumentException">Thrown when userMessage is null or whitespace.</exception>
    /// <remarks>
    /// This method is stateless - each call is independent. For conversation history management,
    /// override this method in derived classes to maintain context.
    /// </remarks>
    public virtual async Task<string> RespondAsync(
        string userMessage,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be null or empty.", nameof(userMessage));

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, userMessage)
        };

        var response = await ChatClient.GetResponseAsync(messages, options, cancellationToken);
        return response.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Generates a streaming response to a user message.
    /// Useful for real-time feedback in UI applications.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="options">Optional chat options for customizing the LLM behavior (temperature, max tokens, etc.).</param>
    /// <param name="cancellationToken">Cancellation token for interrupting the stream.</param>
    /// <returns>An async enumerable of text chunks as they are generated.</returns>
    /// <exception cref="ArgumentException">Thrown when userMessage is null or whitespace.</exception>
    /// <remarks>
    /// This method is stateless - each call is independent. For conversation history management,
    /// override this method in derived classes to maintain context.
    ///
    /// Example usage:
    /// <code>
    /// await foreach (var chunk in agent.StreamResponseAsync("Hello!"))
    /// {
    ///     Console.Write(chunk);
    /// }
    /// </code>
    /// </remarks>
    public virtual async IAsyncEnumerable<string> StreamResponseAsync(
        string userMessage,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be null or empty.", nameof(userMessage));

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, userMessage)
        };

        await foreach (var update in ChatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (update.Text is not null)
            {
                yield return update.Text;
            }
        }
    }
}
