using System.Runtime.CompilerServices;
using Ironbees.AgentMode.Goals;
using Ironbees.Core;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHiveAgentParametersConfig = IronHive.Abstractions.Agent.AgentParametersConfig;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive;

/// <summary>
/// Adapter that bridges Ironbees to IronHive for agent execution
/// </summary>
public partial class IronhiveAdapter : ILLMFrameworkAdapter
{
    private readonly IHiveService _hiveService;
    private readonly IIronhiveOrchestratorFactory _orchestratorFactory;
    private readonly OrchestrationEventMapper _eventMapper;
    private readonly ILogger<IronhiveAdapter> _logger;

    public IronhiveAdapter(
        IHiveService hiveService,
        IIronhiveOrchestratorFactory orchestratorFactory,
        OrchestrationEventMapper eventMapper,
        ILogger<IronhiveAdapter> logger)
    {
        _hiveService = hiveService ?? throw new ArgumentNullException(nameof(hiveService));
        _orchestratorFactory = orchestratorFactory ?? throw new ArgumentNullException(nameof(orchestratorFactory));
        _eventMapper = eventMapper ?? throw new ArgumentNullException(nameof(eventMapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogCreatingIronHiveAgent(_logger, config.Name, config.Model.Provider, config.Model.Deployment);
        }

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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogRunningIronHiveAgent(_logger, agent.Name, input.Length);
        }

        var response = await ironhiveAgent.InvokeAsync(messages, cancellationToken);

        if (response.TokenUsage is not null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogAgentUsage(_logger, agent.Name, response.Message.Model ?? "unknown",
                    response.TokenUsage.InputTokens, response.TokenUsage.OutputTokens);
            }
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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogStreamingIronHiveAgent(_logger, agent.Name, input.Length);
        }

        await foreach (var chunk in ironhiveAgent.InvokeStreamingAsync(messages, cancellationToken))
        {
            if (chunk is StreamingContentDeltaResponse delta
                && delta.Delta is TextDeltaContent textDelta)
            {
                yield return textDelta.Value;
            }
            else if (chunk is StreamingMessageDoneResponse done && done.TokenUsage is not null)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    LogAgentStreamingUsage(_logger, agent.Name, done.Model,
                        done.TokenUsage.InputTokens, done.TokenUsage.OutputTokens);
                }
            }
            else if (chunk is StreamingMessageErrorResponse error)
            {
                LogIronHiveStreamingError(_logger, error.Code, error.Message);
                yield return $"[Error {error.Code}]: {error.Message}";
                yield break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating IronHive agent: {AgentName} with provider {Provider}, model {Model}")]
    private static partial void LogCreatingIronHiveAgent(ILogger logger, string agentName, string provider, string model);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running IronHive agent {AgentName} with input length {InputLength}")]
    private static partial void LogRunningIronHiveAgent(ILogger logger, string agentName, int inputLength);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent {AgentName} ({Model}) usage: {Input} input, {Output} output tokens")]
    private static partial void LogAgentUsage(ILogger logger, string agentName, string model, int input, int output);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Streaming IronHive agent {AgentName} with input length {InputLength}")]
    private static partial void LogStreamingIronHiveAgent(ILogger logger, string agentName, int inputLength);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent {AgentName} ({Model}) streaming usage: {Input} input, {Output} output tokens")]
    private static partial void LogAgentStreamingUsage(ILogger logger, string agentName, string? model, int input, int output);

    [LoggerMessage(Level = LogLevel.Error, Message = "IronHive streaming error: Code={Code}, Message={ErrorMessage}")]
    private static partial void LogIronHiveStreamingError(ILogger logger, int code, string? errorMessage);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating orchestrator type {OrchestratorType} with {AgentCount} agents")]
    private static partial void LogCreatingOrchestrator(ILogger logger, Core.Orchestration.OrchestratorType orchestratorType, int agentCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting orchestration for goal {GoalId}, execution {ExecutionId}")]
    private static partial void LogStartingOrchestration(ILogger logger, string goalId, string executionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting orchestration with approval handler for goal {GoalId}, execution {ExecutionId}")]
    private static partial void LogStartingOrchestrationWithApproval(ILogger logger, string goalId, string executionId);

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

    private static List<Message> CreateMessages(
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

    /// <summary>
    /// Creates an orchestrator for multi-agent coordination.
    /// </summary>
    /// <param name="settings">Orchestration settings defining the pattern and configuration.</param>
    /// <param name="agentConfigs">Agent configurations to include in orchestration.</param>
    /// <param name="handoffMap">Optional handoff target map for handoff orchestration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A configured orchestrator.</returns>
    public async Task<IMultiAgentOrchestrator> CreateOrchestratorAsync(
        OrchestratorSettings settings,
        IReadOnlyList<AgentConfig> agentConfigs,
        IReadOnlyDictionary<string, IReadOnlyList<HandoffTargetDefinition>>? handoffMap = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(agentConfigs);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogCreatingOrchestrator(_logger, settings.Type, agentConfigs.Count);
        }

        // Create Ironbees agents from configs
        var agents = new List<IAgent>();
        foreach (var config in agentConfigs)
        {
            var agent = await CreateAgentAsync(config, cancellationToken);
            agents.Add(agent);
        }

        return _orchestratorFactory.CreateOrchestrator(settings, agents, handoffMap);
    }

    /// <summary>
    /// Runs an orchestration and streams goal execution events.
    /// </summary>
    /// <param name="orchestrator">The orchestrator to run.</param>
    /// <param name="input">The input message to start orchestration.</param>
    /// <param name="goalId">The goal ID for event tracking.</param>
    /// <param name="executionId">The execution ID for this run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of goal execution events.</returns>
    public async IAsyncEnumerable<GoalExecutionEvent> RunOrchestrationAsync(
        IMultiAgentOrchestrator orchestrator,
        string input,
        string goalId,
        string executionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(goalId);
        ArgumentNullException.ThrowIfNull(executionId);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogStartingOrchestration(_logger, goalId, executionId);
        }

        await foreach (var streamEvent in orchestrator.RunStreamingAsync(input, cancellationToken))
        {
            var mappedEvent = OrchestrationEventMapper.Map(streamEvent, goalId, executionId);
            if (mappedEvent is not null)
            {
                yield return mappedEvent;
            }
        }
    }

    /// <summary>
    /// Runs orchestration with approval callback for HITL patterns.
    /// </summary>
    /// <param name="orchestrator">The orchestrator to run.</param>
    /// <param name="input">The input message to start orchestration.</param>
    /// <param name="goalId">The goal ID for event tracking.</param>
    /// <param name="executionId">The execution ID for this run.</param>
    /// <param name="approvalHandler">Callback for handling approval requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of goal execution events.</returns>
    public async IAsyncEnumerable<GoalExecutionEvent> RunOrchestrationWithApprovalAsync(
        IMultiAgentOrchestrator orchestrator,
        string input,
        string goalId,
        string executionId,
        Func<HitlRequestDetails, Task<bool>> approvalHandler,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(goalId);
        ArgumentNullException.ThrowIfNull(executionId);
        ArgumentNullException.ThrowIfNull(approvalHandler);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogStartingOrchestrationWithApproval(_logger, goalId, executionId);
        }

        await foreach (var streamEvent in orchestrator.RunStreamingAsync(input, cancellationToken))
        {
            var mappedEvent = OrchestrationEventMapper.Map(streamEvent, goalId, executionId);
            if (mappedEvent is null)
            {
                continue;
            }

            // Handle HITL approval requests
            if (mappedEvent.Type == GoalExecutionEventType.HitlRequested && mappedEvent.HitlRequest is not null)
            {
                yield return mappedEvent;

                var approved = await approvalHandler(mappedEvent.HitlRequest);

                yield return new GoalExecutionEvent
                {
                    Type = GoalExecutionEventType.HitlResponseReceived,
                    GoalId = goalId,
                    ExecutionId = executionId,
                    Content = approved ? "Approved" : "Rejected",
                    Metadata = new Dictionary<string, object>
                    {
                        ["requestId"] = mappedEvent.HitlRequest.RequestId,
                        ["approved"] = approved
                    }
                };

                if (!approved)
                {
                    yield return new GoalExecutionEvent
                    {
                        Type = GoalExecutionEventType.GoalCancelled,
                        GoalId = goalId,
                        ExecutionId = executionId,
                        Content = "Orchestration cancelled due to rejected approval"
                    };
                    yield break;
                }
            }
            else
            {
                yield return mappedEvent;
            }
        }
    }
}
