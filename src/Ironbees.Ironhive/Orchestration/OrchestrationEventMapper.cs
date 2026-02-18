// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.AgentMode.Goals;
using Ironbees.Core.Orchestration;

namespace Ironbees.Ironhive.Orchestration;

/// <summary>
/// Maps IronHive orchestration stream events to Ironbees GoalExecutionEvents.
/// Provides a bridge between IronHive's event system and Ironbees' goal execution model.
/// </summary>
public class OrchestrationEventMapper
{
    /// <summary>
    /// Maps an IronHive orchestration stream event to an Ironbees goal execution event.
    /// </summary>
    /// <param name="streamEvent">The IronHive orchestration event.</param>
    /// <param name="goalId">The goal ID for the execution.</param>
    /// <param name="executionId">The execution ID for this run.</param>
    /// <returns>A mapped GoalExecutionEvent, or null if the event should be skipped.</returns>
    public static GoalExecutionEvent? Map(
        OrchestrationStreamEvent streamEvent,
        string goalId,
        string executionId)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);
        ArgumentNullException.ThrowIfNull(goalId);
        ArgumentNullException.ThrowIfNull(executionId);

        return streamEvent switch
        {
            OrchestrationStartedEvent started => MapStarted(started, goalId, executionId),
            AgentStartedEvent agentStarted => MapAgentStarted(agentStarted, goalId, executionId),
            MessageDeltaEvent messageDelta => MapMessageDelta(messageDelta, goalId, executionId),
            AgentCompletedEvent agentCompleted => MapAgentCompleted(agentCompleted, goalId, executionId),
            ApprovalRequiredEvent approvalRequired => MapApprovalRequired(approvalRequired, goalId, executionId),
            HandoffEvent handoff => MapHandoff(handoff, goalId, executionId),
            SpeakerSelectedEvent speakerSelected => MapSpeakerSelected(speakerSelected, goalId, executionId),
            HumanInputRequiredEvent humanInput => MapHumanInputRequired(humanInput, goalId, executionId),
            OrchestrationCompletedEvent completed => MapCompleted(completed, goalId, executionId),
            OrchestrationFailedEvent failed => MapFailed(failed, goalId, executionId),
            _ => null // Skip unknown event types
        };
    }

    private static GoalExecutionEvent MapStarted(
        OrchestrationStartedEvent started,
        string goalId,
        string executionId)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.GoalLoaded,
            GoalId = goalId,
            ExecutionId = executionId,
            Content = $"Orchestration started: {started.OrchestrationType}",
            Metadata = new Dictionary<string, object>
            {
                ["orchestrationType"] = started.OrchestrationType.ToString(),
                ["agentCount"] = started.AgentCount
            }
        };
    }

    private static GoalExecutionEvent MapAgentStarted(
        AgentStartedEvent agentStarted,
        string goalId,
        string executionId)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.AgentMessage,
            GoalId = goalId,
            ExecutionId = executionId,
            AgentName = agentStarted.AgentName,
            Content = $"Agent '{agentStarted.AgentName}' started",
            Metadata = new Dictionary<string, object>
            {
                ["agentStarted"] = true
            }
        };
    }

    private static GoalExecutionEvent MapMessageDelta(
        MessageDeltaEvent messageDelta,
        string goalId,
        string executionId)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.WorkflowProgress,
            GoalId = goalId,
            ExecutionId = executionId,
            AgentName = messageDelta.AgentName,
            Content = messageDelta.Delta
        };
    }

    private static GoalExecutionEvent MapAgentCompleted(
        AgentCompletedEvent agentCompleted,
        string goalId,
        string executionId)
    {
        var metadata = new Dictionary<string, object>
        {
            ["success"] = agentCompleted.Success
        };

        if (agentCompleted.TokenUsage is not null)
        {
            metadata["inputTokens"] = agentCompleted.TokenUsage.InputTokens;
            metadata["outputTokens"] = agentCompleted.TokenUsage.OutputTokens;
        }

        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.AgentCompleted,
            GoalId = goalId,
            ExecutionId = executionId,
            AgentName = agentCompleted.AgentName,
            Content = agentCompleted.Result,
            Metadata = metadata
        };
    }

    private static GoalExecutionEvent MapApprovalRequired(
        ApprovalRequiredEvent approvalRequired,
        string goalId,
        string executionId)
    {
        var options = new List<HitlOption>
        {
            new() { Id = "approve", Label = "Approve", Description = "Approve and continue execution", IsDefault = true },
            new() { Id = "reject", Label = "Reject", Description = "Reject and stop execution" }
        };

        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.HitlRequested,
            GoalId = goalId,
            ExecutionId = executionId,
            AgentName = approvalRequired.AgentName,
            HitlRequest = new HitlRequestDetails
            {
                RequestId = Guid.NewGuid().ToString(),
                RequestType = HitlRequestType.Approval,
                Reason = approvalRequired.Reason ?? "Agent execution requires approval",
                CheckpointName = approvalRequired.StepName,
                Options = options,
                Context = new Dictionary<string, object>
                {
                    ["agentName"] = approvalRequired.AgentName,
                    ["proposedAction"] = approvalRequired.ProposedAction ?? ""
                }
            }
        };
    }

    private static GoalExecutionEvent MapHandoff(
        HandoffEvent handoff,
        string goalId,
        string executionId)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.WorkflowProgress,
            GoalId = goalId,
            ExecutionId = executionId,
            AgentName = handoff.FromAgent,
            Content = $"Handoff from '{handoff.FromAgent}' to '{handoff.ToAgent}'",
            Metadata = new Dictionary<string, object>
            {
                ["handoff"] = true,
                ["fromAgent"] = handoff.FromAgent,
                ["toAgent"] = handoff.ToAgent,
                ["reason"] = handoff.Reason ?? ""
            }
        };
    }

    private static GoalExecutionEvent MapSpeakerSelected(
        SpeakerSelectedEvent speakerSelected,
        string goalId,
        string executionId)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.WorkflowProgress,
            GoalId = goalId,
            ExecutionId = executionId,
            AgentName = speakerSelected.SelectedAgent,
            Content = $"Speaker selected: '{speakerSelected.SelectedAgent}'",
            Metadata = new Dictionary<string, object>
            {
                ["speakerSelected"] = true,
                ["round"] = speakerSelected.Round,
                ["selectionReason"] = speakerSelected.Reason ?? ""
            }
        };
    }

    private static GoalExecutionEvent MapHumanInputRequired(
        HumanInputRequiredEvent humanInput,
        string goalId,
        string executionId)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.HitlRequested,
            GoalId = goalId,
            ExecutionId = executionId,
            HitlRequest = new HitlRequestDetails
            {
                RequestId = Guid.NewGuid().ToString(),
                RequestType = HitlRequestType.Input,
                Reason = humanInput.Prompt ?? "Human input is required",
                Context = new Dictionary<string, object>
                {
                    ["inputType"] = humanInput.InputType?.ToString() ?? "text"
                }
            }
        };
    }

    private static GoalExecutionEvent MapCompleted(
        OrchestrationCompletedEvent completed,
        string goalId,
        string executionId)
    {
        var metadata = new Dictionary<string, object>
        {
            ["totalAgentsExecuted"] = completed.TotalAgentsExecuted,
            ["durationMs"] = completed.Duration.TotalMilliseconds
        };

        if (completed.TotalTokenUsage is not null)
        {
            metadata["totalInputTokens"] = completed.TotalTokenUsage.InputTokens;
            metadata["totalOutputTokens"] = completed.TotalTokenUsage.OutputTokens;
        }

        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.GoalCompleted,
            GoalId = goalId,
            ExecutionId = executionId,
            Content = completed.FinalResult,
            Metadata = metadata
        };
    }

    private static GoalExecutionEvent MapFailed(
        OrchestrationFailedEvent failed,
        string goalId,
        string executionId)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.GoalFailed,
            GoalId = goalId,
            ExecutionId = executionId,
            AgentName = failed.FailedAgent,
            Error = new GoalExecutionError
            {
                Code = failed.ErrorCode ?? "ORCHESTRATION_FAILED",
                Message = failed.ErrorMessage ?? "Orchestration failed",
                ExceptionType = failed.ExceptionType,
                IsRecoverable = failed.IsRecoverable
            }
        };
    }
}
