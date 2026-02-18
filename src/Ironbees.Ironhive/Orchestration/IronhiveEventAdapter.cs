// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHiveOrchestrationStreamEvent = IronHive.Abstractions.Agent.Orchestration.OrchestrationStreamEvent;
using IronHiveOrchestrationEventType = IronHive.Abstractions.Agent.Orchestration.OrchestrationEventType;
using IronbeesOrchestrationStreamEvent = Ironbees.Core.Orchestration.OrchestrationStreamEvent;
using IronbeesOrchestratorType = Ironbees.Core.Orchestration.OrchestratorType;
using IronbeesTokenUsageInfo = Ironbees.Core.Orchestration.TokenUsageInfo;
using IronbeesOrchestrationStartedEvent = Ironbees.Core.Orchestration.OrchestrationStartedEvent;
using IronbeesAgentStartedEvent = Ironbees.Core.Orchestration.AgentStartedEvent;
using IronbeesMessageDeltaEvent = Ironbees.Core.Orchestration.MessageDeltaEvent;
using IronbeesAgentCompletedEvent = Ironbees.Core.Orchestration.AgentCompletedEvent;
using IronbeesOrchestrationCompletedEvent = Ironbees.Core.Orchestration.OrchestrationCompletedEvent;
using IronbeesOrchestrationFailedEvent = Ironbees.Core.Orchestration.OrchestrationFailedEvent;
using IronbeesApprovalRequiredEvent = Ironbees.Core.Orchestration.ApprovalRequiredEvent;
using IronbeesHandoffEvent = Ironbees.Core.Orchestration.HandoffEvent;
using IronbeesSpeakerSelectedEvent = Ironbees.Core.Orchestration.SpeakerSelectedEvent;
using IronbeesHumanInputRequiredEvent = Ironbees.Core.Orchestration.HumanInputRequiredEvent;

namespace Ironbees.Ironhive.Orchestration;

/// <summary>
/// Adapts IronHive orchestration stream events to Ironbees orchestration stream events.
/// </summary>
public class IronhiveEventAdapter
{
    private int _agentCount;
    private readonly IronbeesOrchestratorType _orchestratorType;

    public IronhiveEventAdapter(IronbeesOrchestratorType orchestratorType = IronbeesOrchestratorType.Sequential)
    {
        _orchestratorType = orchestratorType;
    }

    /// <summary>
    /// Sets the agent count for orchestration started events.
    /// </summary>
    public void SetAgentCount(int count)
    {
        _agentCount = count;
    }

    /// <summary>
    /// Converts an IronHive orchestration stream event to an Ironbees orchestration stream event.
    /// </summary>
    /// <param name="ironhiveEvent">The IronHive event to convert.</param>
    /// <returns>The converted Ironbees event, or null if the event should be skipped.</returns>
    public IronbeesOrchestrationStreamEvent? Convert(IronHiveOrchestrationStreamEvent ironhiveEvent)
    {
        ArgumentNullException.ThrowIfNull(ironhiveEvent);

        return ironhiveEvent.EventType switch
        {
            IronHiveOrchestrationEventType.Started => ConvertStarted(ironhiveEvent),
            IronHiveOrchestrationEventType.AgentStarted => ConvertAgentStarted(ironhiveEvent),
            IronHiveOrchestrationEventType.MessageDelta => ConvertMessageDelta(ironhiveEvent),
            IronHiveOrchestrationEventType.AgentCompleted => ConvertAgentCompleted(ironhiveEvent),
            IronHiveOrchestrationEventType.AgentFailed => ConvertAgentFailed(ironhiveEvent),
            IronHiveOrchestrationEventType.Completed => ConvertCompleted(ironhiveEvent),
            IronHiveOrchestrationEventType.Failed => ConvertFailed(ironhiveEvent),
            IronHiveOrchestrationEventType.ApprovalRequired => ConvertApprovalRequired(ironhiveEvent),
            IronHiveOrchestrationEventType.Handoff => ConvertHandoff(ironhiveEvent),
            IronHiveOrchestrationEventType.SpeakerSelected => ConvertSpeakerSelected(ironhiveEvent),
            IronHiveOrchestrationEventType.HumanInputRequired => ConvertHumanInputRequired(ironhiveEvent),
            _ => null // Skip unknown event types
        };
    }

    private IronbeesOrchestrationStartedEvent ConvertStarted(IronHiveOrchestrationStreamEvent evt)
    {
        return new IronbeesOrchestrationStartedEvent
        {
            OrchestrationType = _orchestratorType,
            AgentCount = _agentCount
        };
    }

    private static IronbeesAgentStartedEvent? ConvertAgentStarted(IronHiveOrchestrationStreamEvent evt)
    {
        if (string.IsNullOrEmpty(evt.AgentName))
        {
            return null;
        }

        return new IronbeesAgentStartedEvent
        {
            AgentName = evt.AgentName
        };
    }

    private static IronbeesMessageDeltaEvent? ConvertMessageDelta(IronHiveOrchestrationStreamEvent evt)
    {
        if (string.IsNullOrEmpty(evt.AgentName) || evt.StreamingResponse is null)
        {
            return null;
        }

        // Extract text delta from streaming response
        var delta = ExtractDeltaText(evt.StreamingResponse);
        if (string.IsNullOrEmpty(delta))
        {
            return null;
        }

        return new IronbeesMessageDeltaEvent
        {
            AgentName = evt.AgentName,
            Delta = delta
        };
    }

    private static IronbeesAgentCompletedEvent? ConvertAgentCompleted(IronHiveOrchestrationStreamEvent evt)
    {
        if (string.IsNullOrEmpty(evt.AgentName))
        {
            return null;
        }

        IronbeesTokenUsageInfo? tokenUsage = null;
        string? result = null;

        if (evt.CompletedResponse is not null)
        {
            result = ExtractMessageText(evt.CompletedResponse.Message);

            if (evt.CompletedResponse.TokenUsage is not null)
            {
                tokenUsage = new IronbeesTokenUsageInfo
                {
                    InputTokens = evt.CompletedResponse.TokenUsage.InputTokens,
                    OutputTokens = evt.CompletedResponse.TokenUsage.OutputTokens
                };
            }
        }

        return new IronbeesAgentCompletedEvent
        {
            AgentName = evt.AgentName,
            Success = true,
            Result = result,
            TokenUsage = tokenUsage
        };
    }

    private static IronbeesAgentCompletedEvent? ConvertAgentFailed(IronHiveOrchestrationStreamEvent evt)
    {
        if (string.IsNullOrEmpty(evt.AgentName))
        {
            return null;
        }

        return new IronbeesAgentCompletedEvent
        {
            AgentName = evt.AgentName,
            Success = false,
            Result = evt.Error
        };
    }

    private static IronbeesOrchestrationCompletedEvent ConvertCompleted(IronHiveOrchestrationStreamEvent evt)
    {
        var result = evt.Result;
        string? finalResult = null;
        IronbeesTokenUsageInfo? tokenUsage = null;
        int agentsExecuted = 0;
        TimeSpan duration = TimeSpan.Zero;

        if (result is not null)
        {
            finalResult = result.FinalOutput is not null
                ? ExtractMessageText(result.FinalOutput)
                : null;

            agentsExecuted = result.Steps?.Count ?? 0;
            duration = result.TotalDuration;

            if (result.TokenUsage is not null)
            {
                tokenUsage = new IronbeesTokenUsageInfo
                {
                    InputTokens = result.TokenUsage.TotalInputTokens,
                    OutputTokens = result.TokenUsage.TotalOutputTokens
                };
            }
        }

        return new IronbeesOrchestrationCompletedEvent
        {
            FinalResult = finalResult,
            TotalAgentsExecuted = agentsExecuted,
            Duration = duration,
            TotalTokenUsage = tokenUsage
        };
    }

    private static IronbeesOrchestrationFailedEvent ConvertFailed(IronHiveOrchestrationStreamEvent evt)
    {
        return new IronbeesOrchestrationFailedEvent
        {
            FailedAgent = evt.AgentName,
            ErrorMessage = evt.Error ?? "Orchestration failed",
            IsRecoverable = false
        };
    }

    private static IronbeesApprovalRequiredEvent? ConvertApprovalRequired(IronHiveOrchestrationStreamEvent evt)
    {
        if (string.IsNullOrEmpty(evt.AgentName))
        {
            return null;
        }

        return new IronbeesApprovalRequiredEvent
        {
            AgentName = evt.AgentName,
            Reason = "Approval required for agent execution"
        };
    }

    private static IronbeesHandoffEvent? ConvertHandoff(IronHiveOrchestrationStreamEvent evt)
    {
        // Handoff events need to track from/to agents
        // IronHive sends HandoffEvent with the target agent in AgentName
        // We need to track the previous agent for the FromAgent field
        if (string.IsNullOrEmpty(evt.AgentName))
        {
            return null;
        }

        return new IronbeesHandoffEvent
        {
            FromAgent = "", // Will be filled by higher level orchestration logic
            ToAgent = evt.AgentName,
            Reason = "Handoff from previous agent"
        };
    }

    private static IronbeesSpeakerSelectedEvent? ConvertSpeakerSelected(IronHiveOrchestrationStreamEvent evt)
    {
        if (string.IsNullOrEmpty(evt.AgentName))
        {
            return null;
        }

        return new IronbeesSpeakerSelectedEvent
        {
            SelectedAgent = evt.AgentName,
            Round = 0, // Round info not available in IronHive event
            Reason = "Speaker selected for group chat"
        };
    }

    private static IronbeesHumanInputRequiredEvent ConvertHumanInputRequired(IronHiveOrchestrationStreamEvent evt)
    {
        return new IronbeesHumanInputRequiredEvent
        {
            Prompt = "Human input is required",
            InputType = "text"
        };
    }

    private static string ExtractDeltaText(StreamingMessageResponse response)
    {
        if (response is StreamingContentDeltaResponse delta
            && delta.Delta is TextDeltaContent textDelta)
        {
            return textDelta.Value;
        }

        return string.Empty;
    }

    private static string? ExtractMessageText(Message? message)
    {
        if (message is null)
        {
            return null;
        }

        ICollection<MessageContent>? content = message switch
        {
            IronHive.Abstractions.Messages.Roles.UserMessage userMsg => userMsg.Content,
            IronHive.Abstractions.Messages.Roles.AssistantMessage assistantMsg => assistantMsg.Content,
            _ => null
        };

        if (content is null or { Count: 0 })
        {
            return null;
        }

        var textParts = content
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return string.Join("", textParts);
    }
}
