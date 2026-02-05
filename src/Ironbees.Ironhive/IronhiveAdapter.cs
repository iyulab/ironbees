using System.Runtime.CompilerServices;
using Ironbees.Core;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHiveAgentConfig = IronHive.Abstractions.Agent.AgentConfig;
using IronHiveAgentParametersConfig = IronHive.Abstractions.Agent.AgentParametersConfig;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive;

/// <summary>
/// Adapter that bridges Ironbees to IronHive for agent execution
/// </summary>
public class IronhiveAdapter : ILLMFrameworkAdapter
{
    private readonly IHiveService _hiveService;
    private readonly ILogger<IronhiveAdapter> _logger;

    public IronhiveAdapter(IHiveService hiveService, ILogger<IronhiveAdapter> logger)
    {
        _hiveService = hiveService ?? throw new ArgumentNullException(nameof(hiveService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        _logger.LogDebug("Creating IronHive agent: {AgentName} with provider {Provider}, model {Model}",
            config.Name, config.Model.Provider, config.Model.Deployment);

        var ironhiveAgent = _hiveService.CreateAgent(cfg =>
        {
            cfg.Name = config.Name;
            cfg.Description = config.Description;
            cfg.Provider = config.Model.Provider;
            cfg.Model = config.Model.Deployment;
            cfg.Instructions = config.SystemPrompt;

            if (config.Model.MaxTokens != 4000 ||
                config.Model.Temperature != 0.7 ||
                config.Model.TopP.HasValue)
            {
                cfg.Parameters = new IronHiveAgentParametersConfig();

                if (config.Model.MaxTokens != 4000)
                    cfg.Parameters.MaxTokens = config.Model.MaxTokens;
                if (config.Model.Temperature != 0.7)
                    cfg.Parameters.Temperature = (float)config.Model.Temperature;
                if (config.Model.TopP.HasValue)
                    cfg.Parameters.TopP = (float)config.Model.TopP.Value;
            }
        });
        var wrapper = new IronhiveAgentWrapper(ironhiveAgent, config);

        return Task.FromResult<IAgent>(wrapper);
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
        IReadOnlyList<ChatMessage>? conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var ironhiveAgent = GetIronhiveAgent(agent);
        var messages = CreateMessages(input, conversationHistory);

        _logger.LogDebug("Running IronHive agent {AgentName} with input length {InputLength}",
            agent.Name, input.Length);

        var response = await ironhiveAgent.InvokeAsync(messages, cancellationToken);

        if (response.TokenUsage is not null)
        {
            _logger.LogInformation(
                "Agent {AgentName} ({Model}) usage: {Input} input, {Output} output tokens",
                agent.Name, response.Message.Model ?? "unknown",
                response.TokenUsage.InputTokens, response.TokenUsage.OutputTokens);
        }

        return ExtractText(response);
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
        IReadOnlyList<ChatMessage>? conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ironhiveAgent = GetIronhiveAgent(agent);
        var messages = CreateMessages(input, conversationHistory);

        _logger.LogDebug("Streaming IronHive agent {AgentName} with input length {InputLength}",
            agent.Name, input.Length);

        await foreach (var chunk in ironhiveAgent.InvokeStreamingAsync(messages, cancellationToken))
        {
            if (chunk is StreamingContentDeltaResponse delta
                && delta.Delta is TextDeltaContent textDelta)
            {
                yield return textDelta.Value;
            }
            else if (chunk is StreamingMessageDoneResponse done && done.TokenUsage is not null)
            {
                _logger.LogInformation(
                    "Agent {AgentName} ({Model}) streaming usage: {Input} input, {Output} output tokens",
                    agent.Name, done.Model,
                    done.TokenUsage.InputTokens, done.TokenUsage.OutputTokens);
            }
            else if (chunk is StreamingMessageErrorResponse error)
            {
                _logger.LogError("IronHive streaming error: Code={Code}, Message={Message}",
                    error.Code, error.Message);
                yield return $"[Error {error.Code}]: {error.Message}";
                yield break;
            }
        }
    }

    private static IronHiveAgent GetIronhiveAgent(IAgent agent)
    {
        if (agent is IronhiveAgentWrapper wrapper)
        {
            return wrapper.IronhiveAgent;
        }

        throw new InvalidOperationException(
            $"Agent '{agent.Name}' is not an IronHive agent. " +
            $"Expected IronhiveAgentWrapper but got {agent.GetType().Name}.");
    }

    private static IEnumerable<Message> CreateMessages(
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory)
    {
        var messages = new List<Message>();

        if (conversationHistory is { Count: > 0 })
        {
            foreach (var historyMsg in conversationHistory)
            {
                if (historyMsg.Role == ChatRole.User)
                {
                    messages.Add(new UserMessage
                    {
                        Content = [new TextMessageContent { Value = historyMsg.Text ?? "" }]
                    });
                }
                else if (historyMsg.Role == ChatRole.Assistant)
                {
                    messages.Add(new AssistantMessage
                    {
                        Content = [new TextMessageContent { Value = historyMsg.Text ?? "" }]
                    });
                }
            }
        }

        messages.Add(new UserMessage
        {
            Content = [new TextMessageContent { Value = input }]
        });

        return messages;
    }

    private static string ExtractText(MessageResponse response)
    {
        var textParts = response.Message.Content
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return string.Join("", textParts);
    }

}
