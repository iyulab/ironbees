using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure.AI.OpenAI;
using Ironbees.Core;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Ironbees.AgentFramework;

/// <summary>
/// Adapter for Azure OpenAI ChatClient
/// </summary>
public class AgentFrameworkAdapter : ILLMFrameworkAdapter
{
    private readonly AzureOpenAIClient _client;
    private readonly ILogger<AgentFrameworkAdapter> _logger;

    public AgentFrameworkAdapter(
        AzureOpenAIClient client,
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
    public async Task<string> RunAsync(
        IAgent agent,
        string input,
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
            // Build messages with system prompt and user input
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(wrapper.Config.SystemPrompt),
                new UserChatMessage(input)
            };

            // Create chat options
            var options = new ChatCompletionOptions
            {
                Temperature = (float)wrapper.Config.Model.Temperature,
                MaxOutputTokenCount = wrapper.Config.Model.MaxTokens
            };

            if (wrapper.Config.Model.TopP.HasValue)
            {
                options.TopP = (float)wrapper.Config.Model.TopP.Value;
            }

            if (wrapper.Config.Model.FrequencyPenalty.HasValue)
            {
                options.FrequencyPenalty = (float)wrapper.Config.Model.FrequencyPenalty.Value;
            }

            if (wrapper.Config.Model.PresencePenalty.HasValue)
            {
                options.PresencePenalty = (float)wrapper.Config.Model.PresencePenalty.Value;
            }

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
    public async IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
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

        // Build messages with system prompt and user input
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(wrapper.Config.SystemPrompt),
            new UserChatMessage(input)
        };

        // Create chat options
        var options = new ChatCompletionOptions
        {
            Temperature = (float)wrapper.Config.Model.Temperature,
            MaxOutputTokenCount = wrapper.Config.Model.MaxTokens
        };

        if (wrapper.Config.Model.TopP.HasValue)
        {
            options.TopP = (float)wrapper.Config.Model.TopP.Value;
        }

        if (wrapper.Config.Model.FrequencyPenalty.HasValue)
        {
            options.FrequencyPenalty = (float)wrapper.Config.Model.FrequencyPenalty.Value;
        }

        if (wrapper.Config.Model.PresencePenalty.HasValue)
        {
            options.PresencePenalty = (float)wrapper.Config.Model.PresencePenalty.Value;
        }

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
}
