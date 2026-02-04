using System.Runtime.CompilerServices;
using Ironbees.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace Ironbees.AgentFramework;

/// <summary>
/// Adapter for OpenAI-compatible ChatClient (supports both plain OpenAI and Azure OpenAI)
/// </summary>
public class AgentFrameworkAdapter : ILLMFrameworkAdapter
{
    private readonly OpenAIClient _client;
    private readonly ILogger<AgentFrameworkAdapter> _logger;

    public AgentFrameworkAdapter(
        OpenAIClient client,
        ILogger<AgentFrameworkAdapter> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        _logger.LogInformation("Creating agent '{AgentName}' with model '{Model}'",
            config.Name,
            config.Model.Deployment);

        try
        {
            // Get ChatClient for the specified deployment
            var chatClient = _client.GetChatClient(config.Model.Deployment);

            _logger.LogInformation("Successfully created agent '{AgentName}'", config.Name);

            return Task.FromResult<IAgent>(new AgentWrapper(chatClient, config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create agent '{AgentName}'", config.Name);
            throw new AgentLoadException($"Failed to create agent '{config.Name}'", ex);
        }
    }

    /// <inheritdoc />
    public Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(agent, input, conversationHistory: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (agent is not AgentWrapper wrapper)
        {
            throw new ArgumentException("Agent must be created by AgentFrameworkAdapter", nameof(agent));
        }

        _logger.LogDebug("Running agent '{AgentName}' with input length {InputLength}",
            agent.Name,
            input.Length);

        try
        {
            // Build messages with system prompt, optional history, and user input
            var messages = BuildMessages(wrapper.Config.SystemPrompt, input, conversationHistory);

            // Create chat options
            var options = BuildChatOptions(wrapper.Config.Model);

            // Call chat completion
            var response = await wrapper.ChatClient.CompleteChatAsync(messages, options, cancellationToken);

            var content = response.Value.Content[0].Text;

            _logger.LogDebug("Agent '{AgentName}' completed successfully", agent.Name);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running agent '{AgentName}'", agent.Name);
            throw new AgentLoadException($"Failed to run agent '{agent.Name}'", ex);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        return StreamAsync(agent, input, conversationHistory: null, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (agent is not AgentWrapper wrapper)
        {
            throw new ArgumentException("Agent must be created by AgentFrameworkAdapter", nameof(agent));
        }

        _logger.LogDebug("Streaming agent '{AgentName}' with input length {InputLength}",
            agent.Name,
            input.Length);

        // Build messages with system prompt, optional history, and user input
        var messages = BuildMessages(wrapper.Config.SystemPrompt, input, conversationHistory);

        // Create chat options
        var options = BuildChatOptions(wrapper.Config.Model);

        // Stream chat completion
        var streamingResponse = wrapper.ChatClient.CompleteChatStreamingAsync(messages, options, cancellationToken);

        await foreach (var update in streamingResponse)
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }

        _logger.LogDebug("Agent '{AgentName}' streaming completed", agent.Name);
    }

    /// <summary>
    /// Builds the message list: System + optional history + User input.
    /// </summary>
    private static List<ChatMessage> BuildMessages(
        string systemPrompt,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory)
    {
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

        if (conversationHistory is { Count: > 0 })
        {
            foreach (var historyMsg in conversationHistory)
            {
                if (historyMsg.Role == ChatRole.User)
                {
                    messages.Add(new UserChatMessage(historyMsg.Text ?? string.Empty));
                }
                else if (historyMsg.Role == ChatRole.Assistant)
                {
                    messages.Add(new AssistantChatMessage(historyMsg.Text ?? string.Empty));
                }
            }
        }

        messages.Add(new UserChatMessage(input));
        return messages;
    }

    /// <summary>
    /// Builds ChatCompletionOptions from model configuration.
    /// </summary>
    private static ChatCompletionOptions BuildChatOptions(ModelConfig model)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = (float)model.Temperature,
            MaxOutputTokenCount = model.MaxTokens
        };

        if (model.TopP.HasValue)
        {
            options.TopP = (float)model.TopP.Value;
        }

        if (model.FrequencyPenalty.HasValue)
        {
            options.FrequencyPenalty = (float)model.FrequencyPenalty.Value;
        }

        if (model.PresencePenalty.HasValue)
        {
            options.PresencePenalty = (float)model.PresencePenalty.Value;
        }

        return options;
    }
}
