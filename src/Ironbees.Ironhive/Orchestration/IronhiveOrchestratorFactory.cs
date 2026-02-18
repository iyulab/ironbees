// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using IronHive.Abstractions.Agent.Orchestration;
using IronHive.Core.Agent.Orchestration;
using Microsoft.Extensions.Logging;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;
using IronHiveHandoffTarget = IronHive.Abstractions.Agent.Orchestration.HandoffTarget;
using IronHiveOrchestratorOptions = IronHive.Abstractions.Agent.Orchestration.OrchestratorOptions;
using IronbeesIAgent = Ironbees.Core.IAgent;
using IronbeesIMultiAgentOrchestrator = Ironbees.Core.Orchestration.IMultiAgentOrchestrator;
using IronbeesOrchestratorSettings = Ironbees.Core.Orchestration.OrchestratorSettings;
using IronbeesOrchestratorType = Ironbees.Core.Orchestration.OrchestratorType;
using IronbeesHandoffTargetDefinition = Ironbees.Core.Orchestration.HandoffTargetDefinition;

namespace Ironbees.Ironhive.Orchestration;

/// <summary>
/// Factory implementation for creating orchestrators from Ironbees settings.
/// Maps Ironbees declarative orchestration configuration to IronHive runtime orchestrators.
/// </summary>
public partial class IronhiveOrchestratorFactory : IIronhiveOrchestratorFactory
{
    private readonly ILogger<IronhiveOrchestratorFactory> _logger;
    private readonly IronhiveMiddlewareFactory? _middlewareFactory;

    public IronhiveOrchestratorFactory(
        ILogger<IronhiveOrchestratorFactory> logger,
        IronhiveMiddlewareFactory? middlewareFactory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _middlewareFactory = middlewareFactory;
    }

    /// <inheritdoc />
    public IronbeesIMultiAgentOrchestrator CreateOrchestrator(
        IronbeesOrchestratorSettings settings,
        IReadOnlyList<IronbeesIAgent> agents,
        IReadOnlyDictionary<string, IReadOnlyList<IronbeesHandoffTargetDefinition>>? handoffMap = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(agents);

        if (agents.Count == 0)
        {
            throw new ArgumentException("At least one agent is required for orchestration.", nameof(agents));
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogCreatingOrchestrator(_logger, settings.Type, agents.Count);
        }

        // Extract IronHive agents from wrappers
        var ironhiveAgents = ExtractIronhiveAgents(agents);

        // Build base orchestrator options
        var baseOptions = BuildBaseOptions(settings);

        // Apply middleware if configured
        if (settings.Middleware is not null && _middlewareFactory is not null)
        {
            baseOptions.AgentMiddlewares = _middlewareFactory.Create(settings.Middleware);
        }

        return settings.Type switch
        {
            IronbeesOrchestratorType.Sequential => CreateSequentialOrchestrator(settings, ironhiveAgents, baseOptions),
            IronbeesOrchestratorType.Parallel => CreateParallelOrchestrator(settings, ironhiveAgents, baseOptions),
            IronbeesOrchestratorType.HubSpoke => CreateHubSpokeOrchestrator(settings, ironhiveAgents, baseOptions),
            IronbeesOrchestratorType.Handoff => CreateHandoffOrchestrator(settings, ironhiveAgents, handoffMap, baseOptions),
            IronbeesOrchestratorType.GroupChat => CreateGroupChatOrchestrator(settings, ironhiveAgents, baseOptions),
            IronbeesOrchestratorType.Graph => CreateGraphOrchestrator(settings, ironhiveAgents, baseOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Type, "Unknown orchestrator type")
        };
    }

    private static List<IronHiveAgent> ExtractIronhiveAgents(IReadOnlyList<IronbeesIAgent> agents)
    {
        var ironhiveAgents = new List<IronHiveAgent>();

        foreach (var agent in agents)
        {
            if (agent is IronhiveAgentWrapper wrapper)
            {
                ironhiveAgents.Add(wrapper.IronhiveAgent);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Agent '{agent.Name}' is not an IronHive agent. " +
                    $"Expected IronhiveAgentWrapper but got {agent.GetType().Name}.");
            }
        }

        return ironhiveAgents;
    }

    private static IronHiveOrchestratorOptions BuildBaseOptions(IronbeesOrchestratorSettings settings)
    {
        return new IronHiveOrchestratorOptions
        {
            Timeout = settings.Timeout,
            AgentTimeout = settings.AgentTimeout,
            StopOnAgentFailure = settings.StopOnAgentFailure
        };
    }

    private IronhiveOrchestratorWrapper CreateSequentialOrchestrator(
        IronbeesOrchestratorSettings settings,
        List<IronHiveAgent> ironhiveAgents,
        IronHiveOrchestratorOptions baseOptions)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingSequentialOrchestrator(_logger);
        }

        var orchestrator = new SequentialOrchestrator(new SequentialOrchestratorOptions
        {
            Name = "ironbees-sequential",
            Timeout = baseOptions.Timeout,
            AgentTimeout = baseOptions.AgentTimeout,
            StopOnAgentFailure = baseOptions.StopOnAgentFailure,
            AgentMiddlewares = baseOptions.AgentMiddlewares,
            PassOutputAsInput = true,
            AccumulateHistory = false
        });

        orchestrator.AddAgents(ironhiveAgents);

        var eventAdapter = new IronhiveEventAdapter(IronbeesOrchestratorType.Sequential);
        return new IronhiveOrchestratorWrapper(orchestrator, eventAdapter, IronbeesOrchestratorType.Sequential);
    }

    private IronhiveOrchestratorWrapper CreateParallelOrchestrator(
        IronbeesOrchestratorSettings settings,
        List<IronHiveAgent> ironhiveAgents,
        IronHiveOrchestratorOptions baseOptions)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingParallelOrchestrator(_logger);
        }

        var orchestrator = new ParallelOrchestrator(new ParallelOrchestratorOptions
        {
            Name = "ironbees-parallel",
            Timeout = baseOptions.Timeout,
            AgentTimeout = baseOptions.AgentTimeout,
            StopOnAgentFailure = baseOptions.StopOnAgentFailure,
            AgentMiddlewares = baseOptions.AgentMiddlewares,
            ResultAggregation = ParallelResultAggregation.All
        });

        orchestrator.AddAgents(ironhiveAgents);

        var eventAdapter = new IronhiveEventAdapter(IronbeesOrchestratorType.Parallel);
        return new IronhiveOrchestratorWrapper(orchestrator, eventAdapter, IronbeesOrchestratorType.Parallel);
    }

    private IronhiveOrchestratorWrapper CreateHubSpokeOrchestrator(
        IronbeesOrchestratorSettings settings,
        List<IronHiveAgent> ironhiveAgents,
        IronHiveOrchestratorOptions baseOptions)
    {
        if (string.IsNullOrEmpty(settings.HubAgent))
        {
            throw new ArgumentException(
                "HubAgent must be specified for HubSpoke orchestration.", nameof(settings));
        }

        var hubAgent = ironhiveAgents.FirstOrDefault(a => a.Name == settings.HubAgent)
            ?? throw new ArgumentException(
                $"Hub agent '{settings.HubAgent}' not found in provided agents.", nameof(settings));

        var spokeAgents = ironhiveAgents.Where(a => a.Name != settings.HubAgent).ToList();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingHubSpokeOrchestrator(_logger, settings.HubAgent, spokeAgents.Count);
        }

        var orchestrator = new HubSpokeOrchestrator(new HubSpokeOrchestratorOptions
        {
            Name = "ironbees-hubspoke",
            Timeout = baseOptions.Timeout,
            AgentTimeout = baseOptions.AgentTimeout,
            StopOnAgentFailure = baseOptions.StopOnAgentFailure,
            AgentMiddlewares = baseOptions.AgentMiddlewares,
            MaxRounds = settings.MaxRounds
        });

        orchestrator.SetHubAgent(hubAgent);
        foreach (var spoke in spokeAgents)
        {
            orchestrator.AddSpokeAgent(spoke);
        }

        var eventAdapter = new IronhiveEventAdapter(IronbeesOrchestratorType.HubSpoke);
        return new IronhiveOrchestratorWrapper(orchestrator, eventAdapter, IronbeesOrchestratorType.HubSpoke);
    }

    private IronhiveOrchestratorWrapper CreateHandoffOrchestrator(
        IronbeesOrchestratorSettings settings,
        List<IronHiveAgent> ironhiveAgents,
        IReadOnlyDictionary<string, IReadOnlyList<IronbeesHandoffTargetDefinition>>? handoffMap,
        IronHiveOrchestratorOptions baseOptions)
    {
        if (string.IsNullOrEmpty(settings.InitialAgent))
        {
            throw new ArgumentException(
                "InitialAgent must be specified for Handoff orchestration.", nameof(settings));
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingHandoffOrchestrator(_logger, settings.InitialAgent, settings.MaxTransitions);
        }

        var builder = new HandoffOrchestratorBuilder()
            .SetName("ironbees-handoff")
            .SetInitialAgent(settings.InitialAgent)
            .SetMaxTransitions(settings.MaxTransitions)
            .SetTimeout(settings.Timeout)
            .SetAgentTimeout(settings.AgentTimeout);

        // Add agents with their handoff targets
        foreach (var agent in ironhiveAgents)
        {
            var targets = GetHandoffTargets(agent.Name, handoffMap);
            builder.AddAgent(agent, targets);
        }

        var orchestrator = builder.Build();

        var eventAdapter = new IronhiveEventAdapter(IronbeesOrchestratorType.Handoff);
        return new IronhiveOrchestratorWrapper(orchestrator, eventAdapter, IronbeesOrchestratorType.Handoff);
    }

    private IronhiveOrchestratorWrapper CreateGroupChatOrchestrator(
        IronbeesOrchestratorSettings settings,
        List<IronHiveAgent> ironhiveAgents,
        IronHiveOrchestratorOptions baseOptions)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingGroupChatOrchestrator(_logger, ironhiveAgents.Count, settings.MaxRounds);
        }

        var builder = new GroupChatOrchestratorBuilder()
            .SetName("ironbees-groupchat")
            .SetMaxRounds(settings.MaxRounds)
            .SetTimeout(settings.Timeout)
            .SetAgentTimeout(settings.AgentTimeout);

        // Add all agents
        foreach (var agent in ironhiveAgents)
        {
            builder.AddAgent(agent);
        }

        // Configure speaker selection strategy
        builder = ConfigureSpeakerSelection(builder, settings);

        // Configure termination condition
        if (!string.IsNullOrEmpty(settings.TerminationCondition))
        {
            builder.TerminateOnKeyword(settings.TerminationCondition);
        }
        else
        {
            builder.TerminateAfterRounds(settings.MaxRounds);
        }

        var orchestrator = builder.Build();

        var eventAdapter = new IronhiveEventAdapter(IronbeesOrchestratorType.GroupChat);
        return new IronhiveOrchestratorWrapper(orchestrator, eventAdapter, IronbeesOrchestratorType.GroupChat);
    }

    private IronhiveOrchestratorWrapper CreateGraphOrchestrator(
        IronbeesOrchestratorSettings settings,
        List<IronHiveAgent> ironhiveAgents,
        IronHiveOrchestratorOptions baseOptions)
    {
        if (settings.Graph is null)
        {
            throw new ArgumentException(
                "Graph settings must be specified for Graph orchestration.", nameof(settings));
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingGraphOrchestrator(_logger, settings.Graph.Nodes.Count, settings.Graph.Edges.Count);
        }

        var builder = new GraphOrchestratorBuilder()
            .WithOptions(new GraphOrchestratorOptions
            {
                Name = "ironbees-graph",
                Timeout = baseOptions.Timeout,
                AgentTimeout = baseOptions.AgentTimeout,
                StopOnAgentFailure = baseOptions.StopOnAgentFailure,
                AgentMiddlewares = baseOptions.AgentMiddlewares
            });

        // Create a lookup for agents by name
        var agentLookup = ironhiveAgents.ToDictionary(a => a.Name, a => a);

        // Add nodes
        foreach (var node in settings.Graph.Nodes)
        {
            if (!agentLookup.TryGetValue(node.Agent, out var agent))
            {
                throw new ArgumentException(
                    $"Agent '{node.Agent}' referenced in graph node '{node.Id}' not found.",
                    nameof(settings));
            }

            builder.AddNode(node.Id, agent);
        }

        // Add edges
        foreach (var edge in settings.Graph.Edges)
        {
            // If condition is specified, create a condition function
            if (!string.IsNullOrEmpty(edge.Condition))
            {
                // For now, support simple keyword-based conditions
                var keyword = edge.Condition;
                builder.AddEdge(edge.From, edge.To, result =>
                {
                    var text = result.Response?.Message?.Content?
                        .OfType<IronHive.Abstractions.Messages.Content.TextMessageContent>()
                        .Select(c => c.Value)
                        .FirstOrDefault();

                    return text?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false;
                });
            }
            else
            {
                builder.AddEdge(edge.From, edge.To);
            }
        }

        // Set start and output nodes
        builder.SetStartNode(settings.Graph.StartNode);
        builder.SetOutputNode(settings.Graph.OutputNode);

        var orchestrator = builder.Build();

        var eventAdapter = new IronhiveEventAdapter(IronbeesOrchestratorType.Graph);
        return new IronhiveOrchestratorWrapper(orchestrator, eventAdapter, IronbeesOrchestratorType.Graph);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating {OrchestratorType} orchestrator with {AgentCount} agents")]
    private static partial void LogCreatingOrchestrator(ILogger logger, IronbeesOrchestratorType orchestratorType, int agentCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building Sequential orchestrator")]
    private static partial void LogBuildingSequentialOrchestrator(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building Parallel orchestrator")]
    private static partial void LogBuildingParallelOrchestrator(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building HubSpoke orchestrator with hub '{HubAgent}' and {SpokeCount} spokes")]
    private static partial void LogBuildingHubSpokeOrchestrator(ILogger logger, string hubAgent, int spokeCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building Handoff orchestrator with initial agent '{InitialAgent}', max transitions: {MaxTransitions}")]
    private static partial void LogBuildingHandoffOrchestrator(ILogger logger, string? initialAgent, int maxTransitions);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building GroupChat orchestrator with {AgentCount} agents, max rounds: {MaxRounds}")]
    private static partial void LogBuildingGroupChatOrchestrator(ILogger logger, int agentCount, int maxRounds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building Graph orchestrator with {NodeCount} nodes and {EdgeCount} edges")]
    private static partial void LogBuildingGraphOrchestrator(ILogger logger, int nodeCount, int edgeCount);

    private static IronHiveHandoffTarget[] GetHandoffTargets(
        string agentName,
        IReadOnlyDictionary<string, IReadOnlyList<IronbeesHandoffTargetDefinition>>? handoffMap)
    {
        if (handoffMap is null || !handoffMap.TryGetValue(agentName, out var targets))
        {
            return [];
        }

        return targets
            .Select(t => new IronHiveHandoffTarget
            {
                AgentName = t.AgentName,
                Description = t.Description
            })
            .ToArray();
    }

    private static GroupChatOrchestratorBuilder ConfigureSpeakerSelection(
        GroupChatOrchestratorBuilder builder,
        IronbeesOrchestratorSettings settings)
    {
        return settings.SpeakerSelectionStrategy?.ToLowerInvariant() switch
        {
            "round_robin" or "roundrobin" => builder.WithRoundRobin(),
            "random" => builder.WithRandom(),
            // For LLM-based selection, user would need to provide a manager agent
            // Default to round-robin for now
            _ => builder.WithRoundRobin()
        };
    }
}
