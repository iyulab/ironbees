using System.Runtime.CompilerServices;
using Ironbees.AgentMode.Goals;
using Ironbees.Core;
using Ironbees.Core.Orchestration;
using Ironbees.Core.Streaming;
using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Tools;
using IronHive.Core.Microsoft;
using IronHive.Core.Tools;
using IronHiveAgentParametersConfig = IronHive.Abstractions.Agent.AgentParametersConfig;
using IronHiveInvokeOptions = IronHive.Abstractions.Agent.AgentInvokeOptions;
using IronHiveSuggestionMode = IronHive.Abstractions.Messages.SuggestionMode;
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
    private readonly IToolCollection? _toolPool;

    public IronhiveAdapter(
        IHiveService hiveService,
        IIronhiveOrchestratorFactory orchestratorFactory,
        OrchestrationEventMapper eventMapper,
        ILogger<IronhiveAdapter> logger,
        IronhiveOptions? options = null)
    {
        _hiveService = hiveService ?? throw new ArgumentNullException(nameof(hiveService));
        _orchestratorFactory = orchestratorFactory ?? throw new ArgumentNullException(nameof(orchestratorFactory));
        _eventMapper = eventMapper ?? throw new ArgumentNullException(nameof(eventMapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolPool = options?.Tools;
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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogCreatingIronHiveAgent(_logger, config.Name, config.Model.Provider, deployment);
        }

        // FrequencyPenalty/PresencePenalty 는 이 경로에서 전달할 수 없다 — IronHive 의
        // AgentParametersConfig/MessageRequest 에 받을 곳이 없다. 조용히 버리면 소비자는 값이 적용된
        // 줄 알기에, 최소한 관측 가능하게 만든다. 지원 범위 표는 ModelConfig 의 XML doc 참조.
        if (config.Model.FrequencyPenalty.HasValue || config.Model.PresencePenalty.HasValue)
        {
            LogUnsupportedPenaltyParameters(_logger, config.Name);
        }

        var ironhiveAgent = _hiveService.CreateAgentFrom(cfg =>
        {
            cfg.Name = config.Name;
            cfg.Description = config.Description;
            cfg.Provider = config.Model.Provider;
            cfg.Model = deployment;
            cfg.Instructions = config.SystemPrompt;

            // MaxTokens/Temperature 는 무조건 전달한다. 예전에는 ModelConfig 의 기본값(4000 / 0.7)과
            // 비교해 "다르면 설정된 것"으로 추론했는데, 그러면 사용자가 그 값을 **명시적으로** 지정한
            // 경우까지 미설정으로 간주해 조용히 버렸다. ModelConfig 는 두 필드가 non-nullable 이라
            // "미설정"을 표현할 수단 자체가 없으므로, 무조건 전달해도 잃는 표현력이 없고 대신
            // ModelConfig 가 문서화한 기본값이 실제로 적용된다.
            cfg.Parameters = new IronHiveAgentParametersConfig
            {
                MaxTokens = config.Model.MaxTokens,
                Temperature = (float)config.Model.Temperature,
                // TopP 는 double? 이라 미설정을 정직하게 표현할 수 있다 — 있을 때만 전달.
                TopP = config.Model.TopP.HasValue ? (float)config.Model.TopP.Value : null,
            };
        });

        ironhiveAgent.Tools = ResolveTools(config.Name, config.Tools);

        var wrapper = new IronhiveAgentWrapper(ironhiveAgent, config);

        return Task.FromResult<IAgent>(wrapper);
    }

    /// <summary>
    /// Resolves an agent's declared tool names against the registered tool pool
    /// (<see cref="IronhiveOptions.Tools"/>). Fails loud on an unresolvable name rather than
    /// silently running the agent without that tool — matching this adapter's existing
    /// unsupported-option convention (see <see cref="MapInvokeOptions"/>'s remarks).
    /// </summary>
    private IToolCollection? ResolveTools(string agentName, List<string>? toolNames)
    {
        if (toolNames is not { Count: > 0 })
        {
            return null;
        }

        if (_toolPool is null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' declares tools ({string.Join(", ", toolNames)}) but no tool " +
                "pool is registered. Set IronhiveOptions.Tools when calling AddIronbeesIronhive.");
        }

        var missing = toolNames.Where(name => !_toolPool.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' declares unknown tool(s): {string.Join(", ", missing)}. " +
                $"Available in the registered pool: {string.Join(", ", _toolPool.Keys)}.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogResolvedAgentTools(_logger, agentName, toolNames.Count);
        }

        return _toolPool.FilterBy(toolNames);
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
    /// <remarks>Text projection of <see cref="RunStructuredAsync"/>.</remarks>
    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var result = await RunStructuredAsync(agent, input, conversationHistory, options: null, cancellationToken);
        return result.Text;
    }

    /// <inheritdoc />
    public async Task<AgentRunResult> RunStructuredAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var ironhiveAgent = GetIronhiveAgent(agent);
        var messages = CreateMessages(input, conversationHistory);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogRunningIronHiveAgent(_logger, agent.Name, input.Length);
        }

        var response = await ironhiveAgent.InvokeAsync(messages, MapInvokeOptions(options), cancellationToken);

        if (response.TokenUsage is not null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogAgentUsage(_logger, agent.Name, response.Model ?? "unknown",
                    response.TokenUsage.InputTokens, response.TokenUsage.OutputTokens);
            }
        }

        return new AgentRunResult
        {
            Text = ExtractText(response),
            Suggestions = MapSuggestions(response.Suggestions),
        };
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
    /// <remarks>Text projection of <see cref="StreamStructuredAsync"/>.</remarks>
    public async IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in StreamStructuredAsync(agent, input, conversationHistory, options: null, cancellationToken))
        {
            if (chunk is TextChunk text)
            {
                yield return text.Content;
            }
            else if (chunk is ErrorChunk error)
            {
                yield return $"[Error {error.ErrorCode}]: {error.Error}";
                yield break;
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamChunk> StreamStructuredAsync(
        IAgent agent,
        string input,
        IReadOnlyList<ChatMessage>? conversationHistory = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ironhiveAgent = GetIronhiveAgent(agent);
        var messages = CreateMessages(input, conversationHistory);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogStreamingIronHiveAgent(_logger, agent.Name, input.Length);
        }

        await foreach (var chunk in ironhiveAgent.InvokeStreamingAsync(messages, MapInvokeOptions(options), cancellationToken))
        {
            if (chunk is StreamingContentDeltaResponse delta)
            {
                if (delta.Delta is TextDeltaContent textDelta)
                {
                    yield return new TextChunk(textDelta.Value);
                }
                else if (delta.Delta is ThinkingDeltaContent thinkingDelta)
                {
                    // Reasoning models (extended thinking) emit ThinkingDeltaContent;
                    // map to Ironbees' own ThinkingChunk vocabulary so structured
                    // stream consumers receive reasoning alongside text.
                    yield return new ThinkingChunk(thinkingDelta.Data);
                }
            }
            else if (chunk is StreamingContentAddedResponse { Content: ToolMessageContent added })
            {
                // Arguments are still streaming in at this point (via ToolDeltaContent on
                // subsequent delta chunks, not surfaced as their own chunk type today) - report
                // the call starting, not its input.
                yield return new ToolCallStartChunk(added.Name, added.Id);
            }
            else if (chunk is StreamingContentCompletedResponse { Content: ToolMessageContent { Output: { } output } completed })
            {
                // IronHive 0.23.0+: ToolOutput carries MessageContent blocks, not a string. The chunk's
                // Result/Error stay text (the contract this adapter has always exposed), so flatten the
                // same way IronHive's own text-only tool-result wires do: join text blocks, describe the rest.
                var outputText = FlattenToolOutput(output);
                yield return new ToolCallCompleteChunk(
                    completed.Name,
                    completed.Id,
                    output.IsSuccess,
                    output.IsSuccess ? outputText : null,
                    output.IsSuccess ? null : outputText);
            }
            else if (chunk is StreamingMessageDoneResponse done)
            {
                if (done.TokenUsage is not null)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        LogAgentStreamingUsage(_logger, agent.Name, done.Model,
                            done.TokenUsage.InputTokens, done.TokenUsage.OutputTokens);
                    }

                    yield return new UsageChunk(done.TokenUsage.InputTokens, done.TokenUsage.OutputTokens);
                }

                var suggestions = MapSuggestions(done.Suggestions);
                if (suggestions is not null)
                {
                    yield return new SuggestionsChunk(suggestions);
                }
            }
            else if (chunk is StreamingMessageErrorResponse error)
            {
                LogIronHiveStreamingError(_logger, error.Code, error.Message);
                yield return new ErrorChunk(error.Message ?? "", IsFatal: true, ErrorCode: error.Code);
                yield break;
            }
        }

        yield return new CompletionChunk();
    }

    /// <summary>
    /// Maps neutral per-invoke options to IronHive <see cref="IronHiveInvokeOptions"/>.
    /// Null options (or all-null fields) map to null so agent defaults apply.
    /// </summary>
    /// <summary>
    /// Flattens a <see cref="ToolOutput"/>'s content blocks to a single string: text blocks are joined
    /// with newlines, any non-text block is replaced by a short placeholder naming what was omitted.
    /// </summary>
    internal static string FlattenToolOutput(ToolOutput output)
    {
        if (output.Content.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", output.Content.Select(c => c switch
        {
            TextMessageContent text => text.Value ?? string.Empty,
            _ => $"[{c.GetType().Name} omitted — non-text tool output is not carried on this stream]"
        }));
    }

    private static IronHiveInvokeOptions? MapInvokeOptions(AgentRunOptions? options)
    {
        if (options is null
            || (options.Suggestions is null && options.ThinkingEffort is null && options.Tools is null
                && options.MaxTokens is null))
        {
            return null;
        }

        var mapped = new IronHiveInvokeOptions
        {
            ThinkingEffort = MapThinkingEffort(options.ThinkingEffort),
            MaxTokens = options.MaxTokens,
        };

        if (options.Suggestions is { } request)
        {
            mapped.Suggestions = new SuggestionOptions
            {
                Mode = request.Mode == Core.SuggestionMode.Always
                    ? IronHiveSuggestionMode.Always
                    : IronHiveSuggestionMode.Auto,
                MaxCount = request.MaxCount,
                MinItems = request.MinItems,
                MaxItems = request.MaxItems,
            };
        }

        if (options.Tools is { Count: > 0 } tools)
        {
            mapped.Tools = new ToolCollection(tools.Select(t => (ITool)new AIToolAdapter(t)));
        }

        return mapped;
    }

    /// <summary>
    /// Maps the neutral <see cref="Core.ThinkingEffort"/> to IronHive
    /// <see cref="MessageThinkingEffort"/>. Null (option not set) stays null so the
    /// framework default applies; <see cref="Core.ThinkingEffort.None"/> maps to the
    /// explicit-off value.
    /// </summary>
    private static MessageThinkingEffort? MapThinkingEffort(Core.ThinkingEffort? effort) => effort switch
    {
        null => null,
        Core.ThinkingEffort.None => MessageThinkingEffort.None,
        Core.ThinkingEffort.Minimal => MessageThinkingEffort.Minimal,
        Core.ThinkingEffort.Low => MessageThinkingEffort.Low,
        Core.ThinkingEffort.Medium => MessageThinkingEffort.Medium,
        Core.ThinkingEffort.High => MessageThinkingEffort.High,
        Core.ThinkingEffort.XHigh => MessageThinkingEffort.XHigh,
        _ => MessageThinkingEffort.Medium,
    };

    /// <summary>
    /// Maps IronHive suggestions to the neutral <see cref="AgentSuggestion"/> model.
    /// Empty or null lists map to null.
    /// </summary>
    private static List<AgentSuggestion>? MapSuggestions(List<Suggestion>? suggestions)
    {
        if (suggestions is not { Count: > 0 })
        {
            return null;
        }

        return suggestions
            .Select(s => new AgentSuggestion(s.Question, s.Items))
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating IronHive agent: {AgentName} with provider {Provider}, model {Model}")]
    private static partial void LogCreatingIronHiveAgent(ILogger logger, string agentName, string provider, string model);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agent {AgentName}: resolved {ToolCount} tool(s) from the registered pool")]
    private static partial void LogResolvedAgentTools(ILogger logger, string agentName, int toolCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent {AgentName}: frequencyPenalty/presencePenalty are configured but the IronHive backend cannot apply them - the agent parameter contract has no field for either. The values are ignored. Use the Agent Framework backend if these parameters are required.")]
    private static partial void LogUnsupportedPenaltyParameters(ILogger logger, string agentName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running IronHive agent {AgentName} with input length {InputLength}")]
    private static partial void LogRunningIronHiveAgent(ILogger logger, string agentName, int inputLength);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent {AgentName} ({Model}) usage: {Input} input, {Output} output tokens")]
    private static partial void LogAgentUsage(ILogger logger, string agentName, string model, int input, int output);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Streaming IronHive agent {AgentName} with input length {InputLength}")]
    private static partial void LogStreamingIronHiveAgent(ILogger logger, string agentName, int inputLength);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent {AgentName} ({Model}) streaming usage: {Input} input, {Output} output tokens")]
    private static partial void LogAgentStreamingUsage(ILogger logger, string agentName, string? model, int input, int output);

    [LoggerMessage(Level = LogLevel.Error, Message = "IronHive streaming error: Code={Code}, Message={ErrorMessage}")]
    private static partial void LogIronHiveStreamingError(ILogger logger, string? code, string? errorMessage);

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
                    messages.Add(Message.User(new TextMessageContent { Value = historyMsg.Text ?? "" }));
                }
                else if (historyMsg.Role == ChatRole.Assistant)
                {
                    messages.Add(Message.Assistant(new TextMessageContent { Value = historyMsg.Text ?? "" }));
                }
            }
        }

        messages.Add(Message.User(new TextMessageContent { Value = input }));

        return messages;
    }

    private static string ExtractText(MessageResponse response)
    {
        var textParts = response.Message?.Content
            .OfType<TextMessageContent>()
            .Select(c => c.Value) ?? [];

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
