using System.Runtime.CompilerServices;
using Ironbees.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Ironbees.AgentFramework;

/// <summary>
/// Adapter for Microsoft Agent Framework (AIAgent).
/// Supports both plain OpenAI and Azure OpenAI via OpenAIClient base class.
/// </summary>
public class MicrosoftAgentFrameworkAdapter : ILLMFrameworkAdapter
{
    private readonly OpenAIClient _client;
    private readonly ILogger<MicrosoftAgentFrameworkAdapter> _logger;

    public MicrosoftAgentFrameworkAdapter(
        OpenAIClient client,
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
        IReadOnlyList<AIChatMessage>? conversationHistory,
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
            // Build conversation input with history
            var fullInput = BuildInputWithHistory(input, conversationHistory);

            // Use AIAgent.RunAsync - returns AgentRunResponse
            var response = await wrapper.AIAgent.RunAsync(fullInput, cancellationToken: cancellationToken);

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
        IReadOnlyList<AIChatMessage>? conversationHistory,
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

        // Build conversation input with history
        var fullInput = BuildInputWithHistory(input, conversationHistory);

        // Microsoft Agent Framework supports streaming via RunStreamingAsync
        await foreach (var update in wrapper.AIAgent.RunStreamingAsync(fullInput, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }

        _logger.LogDebug("Microsoft Agent Framework agent '{AgentName}' streaming completed", agent.Name);
    }

    /// <summary>
    /// Builds input string with conversation history for MAF agents.
    /// MAF's RunAsync takes a single string, so we format history as a conversation transcript.
    /// </summary>
    private static string BuildInputWithHistory(
        string input,
        IReadOnlyList<AIChatMessage>? conversationHistory)
    {
        if (conversationHistory is not { Count: > 0 })
            return input;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Previous conversation:");
        foreach (var msg in conversationHistory)
        {
            var role = msg.Role == ChatRole.User ? "User" : "Assistant";
            sb.AppendLine($"{role}: {msg.Text}");
        }
        sb.AppendLine();
        sb.AppendLine($"Current message: {input}");
        return sb.ToString();
    }
}
