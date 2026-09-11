using System.Runtime.CompilerServices;
using Ironbees.Core;
using Ironbees.Core.Streaming;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace Ironbees.AgentFramework;

/// <summary>
/// Adapter for OpenAI-compatible ChatClient (supports both plain OpenAI and Azure OpenAI)
/// </summary>
public partial class AgentFrameworkAdapter : ILLMFrameworkAdapter
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

        var deployment = config.Model.Deployment;
        if (string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException(
                $"Agent '{config.Name}' has no model deployment resolved. Provide ProcessOptions.ModelOverride, " +
                "configure a default model, or set 'model.deployment' in agent.yaml.");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogCreatingAgent(_logger, config.Name, deployment);
        }

        try
        {
            // Get ChatClient for the specified deployment
            var chatClient = _client.GetChatClient(deployment);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogAgentCreated(_logger, config.Name);
            }

            return Task.FromResult<IAgent>(new AgentWrapper(chatClient, config));
        }
        catch (Exception ex)
        {
            LogFailedToCreateAgent(_logger, ex, config.Name);
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
    public Task<string> RunAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory,
        CancellationToken cancellationToken = default)
    {
        return RunCoreAsync(agent, input, conversationHistory, runOptions: null, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Honours <see cref="AgentRunOptions.MaxTokens"/>. Refuses <see cref="AgentRunOptions.Suggestions"/>,
    /// <see cref="AgentRunOptions.ThinkingEffort"/> and <see cref="AgentRunOptions.Tools"/> with
    /// <see cref="NotSupportedException"/>: this adapter runs a single Chat Completions call with no
    /// tool-execution loop, no reasoning channel to surface, and no suggestion pass.
    /// </remarks>
    public async Task<AgentRunResult> RunStructuredAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnsupported(options);
        var text = await RunCoreAsync(agent, input, conversationHistory, options, cancellationToken).ConfigureAwait(false);
        return new AgentRunResult { Text = text };
    }

    private async Task<string> RunCoreAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory,
        AgentRunOptions? runOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (agent is not AgentWrapper wrapper)
        {
            throw new ArgumentException("Agent must be created by AgentFrameworkAdapter", nameof(agent));
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogRunningAgent(_logger, agent.Name, input.Length);
        }

        try
        {
            // Build messages with system prompt, optional history, and user input
            var messages = BuildMessages(wrapper.Config.SystemPrompt, input, conversationHistory);

            // Create chat options
            var options = BuildChatOptions(wrapper.Config.Model, runOptions);

            // Call chat completion
            var response = await wrapper.ChatClient.CompleteChatAsync(messages, options, cancellationToken);

            var content = response.Value.Content[0].Text;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogAgentCompleted(_logger, agent.Name);
            }

            return content;
        }
        catch (Exception ex)
        {
            LogErrorRunningAgent(_logger, ex, agent.Name);
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
    public IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory,
        CancellationToken cancellationToken = default)
    {
        return StreamCoreAsync(agent, input, conversationHistory, runOptions: null, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Same options contract as <see cref="RunStructuredAsync"/>; a refused option throws at call time,
    /// before the stream is enumerated.
    /// </remarks>
    public IAsyncEnumerable<StreamChunk> StreamStructuredAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnsupported(options);
        return StreamStructuredCoreAsync(agent, input, conversationHistory, options, cancellationToken);
    }

    private async IAsyncEnumerable<StreamChunk> StreamStructuredCoreAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory,
        AgentRunOptions? runOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var text in StreamCoreAsync(agent, input, conversationHistory, runOptions, cancellationToken).ConfigureAwait(false))
        {
            yield return new TextChunk(text);
        }

        yield return new CompletionChunk();
    }

    private async IAsyncEnumerable<string> StreamCoreAsync(
        IAgent agent,
        string input,
        IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? conversationHistory,
        AgentRunOptions? runOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (agent is not AgentWrapper wrapper)
        {
            throw new ArgumentException("Agent must be created by AgentFrameworkAdapter", nameof(agent));
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogStreamingAgent(_logger, agent.Name, input.Length);
        }

        // Build messages with system prompt, optional history, and user input
        var messages = BuildMessages(wrapper.Config.SystemPrompt, input, conversationHistory);

        // Create chat options
        var options = BuildChatOptions(wrapper.Config.Model, runOptions);

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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogAgentStreamingCompleted(_logger, agent.Name);
        }
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating agent '{AgentName}' with model '{Model}'")]
    private static partial void LogCreatingAgent(ILogger logger, string agentName, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully created agent '{AgentName}'")]
    private static partial void LogAgentCreated(ILogger logger, string agentName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create agent '{AgentName}'")]
    private static partial void LogFailedToCreateAgent(ILogger logger, Exception exception, string agentName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running agent '{AgentName}' with input length {InputLength}")]
    private static partial void LogRunningAgent(ILogger logger, string agentName, int inputLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agent '{AgentName}' completed successfully")]
    private static partial void LogAgentCompleted(ILogger logger, string agentName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error running agent '{AgentName}'")]
    private static partial void LogErrorRunningAgent(ILogger logger, Exception exception, string agentName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Streaming agent '{AgentName}' with input length {InputLength}")]
    private static partial void LogStreamingAgent(ILogger logger, string agentName, int inputLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agent '{AgentName}' streaming completed")]
    private static partial void LogAgentStreamingCompleted(ILogger logger, string agentName);

    /// <summary>
    /// Builds ChatCompletionOptions from the agent's model configuration and this call's options.
    /// The one place both halves turn options into a request, so the two cannot disagree about it.
    /// </summary>
    private static ChatCompletionOptions BuildChatOptions(ModelConfig model, AgentRunOptions? runOptions)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = (float)model.Temperature,
            MaxOutputTokenCount = runOptions?.MaxTokens ?? model.MaxTokens
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

    /// <summary>
    /// Refuses the per-invoke options this adapter cannot apply. Each is refused rather than dropped:
    /// an option a caller set and the adapter ignored would read as applied.
    /// </summary>
    private void ThrowIfUnsupported(AgentRunOptions? options)
    {
        if (options?.Suggestions is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support structured suggestions (AgentRunOptions.Suggestions).");
        }

        if (options?.ThinkingEffort is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support per-invoke thinking effort (AgentRunOptions.ThinkingEffort).");
        }

        if (options?.Tools is not null)
        {
            throw new NotSupportedException(
                $"{GetType().Name} does not support per-invoke tools (AgentRunOptions.Tools).");
        }
    }
}
