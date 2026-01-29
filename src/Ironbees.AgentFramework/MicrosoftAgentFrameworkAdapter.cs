using System.Runtime.CompilerServices;
using Azure.AI.OpenAI;
using Ironbees.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace Ironbees.AgentFramework;

/// <summary>
/// Adapter for Microsoft Agent Framework (AIAgent)
/// </summary>
public class MicrosoftAgentFrameworkAdapter : ILLMFrameworkAdapter
{
    private readonly AzureOpenAIClient _client;
    private readonly ILogger<MicrosoftAgentFrameworkAdapter> _logger;

    public MicrosoftAgentFrameworkAdapter(
        AzureOpenAIClient client,
        ILogger<MicrosoftAgentFrameworkAdapter> logger)
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

        _logger.LogInformation("Creating Microsoft Agent Framework agent '{AgentName}' with model '{Model}'",
            config.Name,
            config.Model.Deployment);

        try
        {
            // Get ChatClient for the specified deployment and create AIAgent
            // Using ChatClientAgent constructor directly for compatibility with MAF v1.0.0-preview.260128.1
            var chatClient = _client
                .GetChatClient(config.Model.Deployment)
                .AsIChatClient();

            var aiAgent = new ChatClientAgent(
                chatClient,
                instructions: config.SystemPrompt,
                name: config.Name);

            _logger.LogInformation("Successfully created Microsoft Agent Framework agent '{AgentName}'", config.Name);

            return Task.FromResult<IAgent>(new MicrosoftAgentWrapper(aiAgent, config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Microsoft Agent Framework agent '{AgentName}'", config.Name);
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

        if (agent is not MicrosoftAgentWrapper wrapper)
        {
            throw new ArgumentException("Agent must be created by MicrosoftAgentFrameworkAdapter", nameof(agent));
        }

        _logger.LogDebug("Running Microsoft Agent Framework agent '{AgentName}' with input length {InputLength}",
            agent.Name,
            input.Length);

        try
        {
            // Use AIAgent.RunAsync - returns AgentRunResponse
            var response = await wrapper.AIAgent.RunAsync(input, cancellationToken: cancellationToken);

            _logger.LogDebug("Microsoft Agent Framework agent '{AgentName}' completed successfully", agent.Name);

            // Extract text from AgentRunResponse
            return response.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Microsoft Agent Framework agent '{AgentName}'", agent.Name);
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

        if (agent is not MicrosoftAgentWrapper wrapper)
        {
            throw new ArgumentException("Agent must be created by MicrosoftAgentFrameworkAdapter", nameof(agent));
        }

        _logger.LogDebug("Streaming Microsoft Agent Framework agent '{AgentName}' with input length {InputLength}",
            agent.Name,
            input.Length);

        // Microsoft Agent Framework supports streaming via RunStreamingAsync
        // Iterate through the streaming updates
        await foreach (var update in wrapper.AIAgent.RunStreamingAsync(input, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }

        _logger.LogDebug("Microsoft Agent Framework agent '{AgentName}' streaming completed", agent.Name);
    }
}
