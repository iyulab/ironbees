// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using IronHive.Abstractions.Agent.Orchestration;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHiveOrchestrationEventType = IronHive.Abstractions.Agent.Orchestration.OrchestrationEventType;
using IronbeesOrchestrationStreamEvent = Ironbees.Core.Orchestration.OrchestrationStreamEvent;
using IronbeesOrchestrationResult = Ironbees.Core.Orchestration.OrchestrationResult;
using IronbeesTokenUsageInfo = Ironbees.Core.Orchestration.TokenUsageInfo;
using IronbeesOrchestratorType = Ironbees.Core.Orchestration.OrchestratorType;
using IronbeesAgentCompletedEvent = Ironbees.Core.Orchestration.AgentCompletedEvent;
using IronbeesOrchestrationCompletedEvent = Ironbees.Core.Orchestration.OrchestrationCompletedEvent;
using IronbeesOrchestrationFailedEvent = Ironbees.Core.Orchestration.OrchestrationFailedEvent;
using IronbeesHandoffEvent = Ironbees.Core.Orchestration.HandoffEvent;
using IronbeesIMultiAgentOrchestrator = Ironbees.Core.Orchestration.IMultiAgentOrchestrator;

namespace Ironbees.Ironhive.Orchestration;

/// <summary>
/// Wraps an IronHive IAgentOrchestrator to satisfy the Ironbees IMultiAgentOrchestrator interface.
/// Provides the bridge between Ironbees declarative orchestration and IronHive runtime execution.
/// </summary>
internal sealed class IronhiveOrchestratorWrapper : IronbeesIMultiAgentOrchestrator
{
    private readonly IAgentOrchestrator _ironhiveOrchestrator;
    private readonly IronhiveEventAdapter _eventAdapter;
    private readonly IronbeesOrchestratorType _orchestratorType;

    /// <summary>
    /// Gets the underlying IronHive orchestrator.
    /// </summary>
    internal IAgentOrchestrator IronhiveOrchestrator => _ironhiveOrchestrator;

    public IronhiveOrchestratorWrapper(
        IAgentOrchestrator ironhiveOrchestrator,
        IronhiveEventAdapter eventAdapter,
        IronbeesOrchestratorType orchestratorType)
    {
        _ironhiveOrchestrator = ironhiveOrchestrator ?? throw new ArgumentNullException(nameof(ironhiveOrchestrator));
        _eventAdapter = eventAdapter ?? throw new ArgumentNullException(nameof(eventAdapter));
        _orchestratorType = orchestratorType;

        // Configure event adapter with orchestrator metadata
        _eventAdapter.SetAgentCount(_ironhiveOrchestrator.Agents.Count);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<IronbeesOrchestrationStreamEvent> RunStreamingAsync(
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new[]
        {
            new UserMessage
            {
                Content = [new TextMessageContent { Value = input }]
            }
        };

        string? previousAgent = null;

        await foreach (var evt in _ironhiveOrchestrator.ExecuteStreamingAsync(messages, cancellationToken))
        {
            var converted = _eventAdapter.Convert(evt);

            // Handle handoff events specially to track from/to agents
            if (evt.EventType == IronHiveOrchestrationEventType.Handoff && converted is IronbeesHandoffEvent handoff)
            {
                // Update FromAgent with the previously executing agent
                yield return handoff with { FromAgent = previousAgent ?? "unknown" };
                previousAgent = handoff.ToAgent;
            }
            else if (converted is not null)
            {
                // Track agent transitions for handoff context
                if (evt.EventType == IronHiveOrchestrationEventType.AgentStarted && !string.IsNullOrEmpty(evt.AgentName))
                {
                    previousAgent = evt.AgentName;
                }

                yield return converted;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IronbeesOrchestrationResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var totalTokens = new IronbeesTokenUsageInfo();
        var agentCount = 0;
        string? lastResult = null;
        string? errorMessage = null;
        var success = true;

        try
        {
            await foreach (var evt in RunStreamingAsync(input, cancellationToken))
            {
                switch (evt)
                {
                    case IronbeesAgentCompletedEvent completed:
                        agentCount++;
                        lastResult = completed.Result;
                        if (completed.TokenUsage is not null)
                        {
                            totalTokens = new IronbeesTokenUsageInfo
                            {
                                InputTokens = totalTokens.InputTokens + completed.TokenUsage.InputTokens,
                                OutputTokens = totalTokens.OutputTokens + completed.TokenUsage.OutputTokens
                            };
                        }
                        break;

                    case IronbeesOrchestrationFailedEvent failed:
                        success = false;
                        errorMessage = failed.ErrorMessage;
                        break;

                    case IronbeesOrchestrationCompletedEvent completed:
                        lastResult = completed.FinalResult;
                        if (completed.TotalTokenUsage is not null)
                        {
                            totalTokens = completed.TotalTokenUsage;
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
        }

        stopwatch.Stop();

        return new IronbeesOrchestrationResult
        {
            Success = success,
            FinalOutput = lastResult,
            TotalAgentsExecuted = agentCount,
            Duration = stopwatch.Elapsed,
            TotalTokenUsage = totalTokens,
            ErrorMessage = errorMessage
        };
    }
}
