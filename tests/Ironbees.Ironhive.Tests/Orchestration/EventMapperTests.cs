// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.AgentMode.Goals;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Orchestration;

namespace Ironbees.Ironhive.Tests.Orchestration;

public class EventMapperTests
{
    private readonly OrchestrationEventMapper _mapper;
    private const string GoalId = "test-goal";
    private const string ExecutionId = "exec-123";

    public EventMapperTests()
    {
        _mapper = new OrchestrationEventMapper();
    }

    [Fact]
    public void Map_NullEvent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _mapper.Map(null!, GoalId, ExecutionId));
    }

    [Fact]
    public void Map_NullGoalId_ThrowsArgumentNullException()
    {
        var evt = new OrchestrationStartedEvent
        {
            OrchestrationType = OrchestratorType.Sequential,
            AgentCount = 2
        };

        Assert.Throws<ArgumentNullException>(() =>
            _mapper.Map(evt, null!, ExecutionId));
    }

    [Fact]
    public void Map_NullExecutionId_ThrowsArgumentNullException()
    {
        var evt = new OrchestrationStartedEvent
        {
            OrchestrationType = OrchestratorType.Sequential,
            AgentCount = 2
        };

        Assert.Throws<ArgumentNullException>(() =>
            _mapper.Map(evt, GoalId, null!));
    }

    [Fact]
    public void Map_OrchestrationStartedEvent_ReturnsGoalLoaded()
    {
        // Arrange
        var evt = new OrchestrationStartedEvent
        {
            OrchestrationType = OrchestratorType.Sequential,
            AgentCount = 3
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.GoalLoaded, result.Type);
        Assert.Equal(GoalId, result.GoalId);
        Assert.Equal(ExecutionId, result.ExecutionId);
        Assert.NotNull(result.Metadata);
        Assert.Equal("Sequential", result.Metadata["orchestrationType"]);
        Assert.Equal(3, result.Metadata["agentCount"]);
    }

    [Fact]
    public void Map_AgentStartedEvent_ReturnsAgentMessage()
    {
        // Arrange
        var evt = new AgentStartedEvent { AgentName = "test-agent" };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.AgentMessage, result.Type);
        Assert.Equal("test-agent", result.AgentName);
        Assert.Contains("test-agent", result.Content!);
    }

    [Fact]
    public void Map_MessageDeltaEvent_ReturnsWorkflowProgress()
    {
        // Arrange
        var evt = new MessageDeltaEvent
        {
            AgentName = "agent1",
            Delta = "Hello world"
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.WorkflowProgress, result.Type);
        Assert.Equal("agent1", result.AgentName);
        Assert.Equal("Hello world", result.Content);
    }

    [Fact]
    public void Map_AgentCompletedEvent_ReturnsAgentCompleted()
    {
        // Arrange
        var evt = new AgentCompletedEvent
        {
            AgentName = "analyzer",
            Success = true,
            Result = "Analysis complete",
            TokenUsage = new TokenUsageInfo { InputTokens = 100, OutputTokens = 50 }
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.AgentCompleted, result.Type);
        Assert.Equal("analyzer", result.AgentName);
        Assert.Equal("Analysis complete", result.Content);
        Assert.NotNull(result.Metadata);
        Assert.True((bool)result.Metadata["success"]);
        Assert.Equal(100, result.Metadata["inputTokens"]);
        Assert.Equal(50, result.Metadata["outputTokens"]);
    }

    [Fact]
    public void Map_ApprovalRequiredEvent_ReturnsHitlRequested()
    {
        // Arrange
        var evt = new ApprovalRequiredEvent
        {
            AgentName = "sensitive-agent",
            Reason = "Sensitive operation requires approval",
            StepName = "delete-step",
            ProposedAction = "Delete all files"
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.HitlRequested, result.Type);
        Assert.Equal("sensitive-agent", result.AgentName);
        Assert.NotNull(result.HitlRequest);
        Assert.Equal(HitlRequestType.Approval, result.HitlRequest.RequestType);
        Assert.Equal("delete-step", result.HitlRequest.CheckpointName);
        Assert.NotNull(result.HitlRequest.Options);
        Assert.Equal(2, result.HitlRequest.Options.Count);
    }

    [Fact]
    public void Map_HandoffEvent_ReturnsWorkflowProgressWithHandoffMetadata()
    {
        // Arrange
        var evt = new HandoffEvent
        {
            FromAgent = "agent-a",
            ToAgent = "agent-b",
            Reason = "Task requires specialized handling"
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.WorkflowProgress, result.Type);
        Assert.Equal("agent-a", result.AgentName);
        Assert.Contains("agent-a", result.Content!);
        Assert.Contains("agent-b", result.Content);
        Assert.NotNull(result.Metadata);
        Assert.True((bool)result.Metadata["handoff"]);
        Assert.Equal("agent-a", result.Metadata["fromAgent"]);
        Assert.Equal("agent-b", result.Metadata["toAgent"]);
    }

    [Fact]
    public void Map_SpeakerSelectedEvent_ReturnsWorkflowProgress()
    {
        // Arrange
        var evt = new SpeakerSelectedEvent
        {
            SelectedAgent = "expert",
            Round = 3,
            Reason = "Expertise match"
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.WorkflowProgress, result.Type);
        Assert.Equal("expert", result.AgentName);
        Assert.NotNull(result.Metadata);
        Assert.True((bool)result.Metadata["speakerSelected"]);
        Assert.Equal(3, result.Metadata["round"]);
    }

    [Fact]
    public void Map_HumanInputRequiredEvent_ReturnsHitlRequested()
    {
        // Arrange
        var evt = new HumanInputRequiredEvent
        {
            Prompt = "Please provide clarification",
            InputType = "text"
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.HitlRequested, result.Type);
        Assert.NotNull(result.HitlRequest);
        Assert.Equal(HitlRequestType.Input, result.HitlRequest.RequestType);
        Assert.Equal("Please provide clarification", result.HitlRequest.Reason);
    }

    [Fact]
    public void Map_OrchestrationCompletedEvent_ReturnsGoalCompleted()
    {
        // Arrange
        var evt = new OrchestrationCompletedEvent
        {
            FinalResult = "All agents completed successfully",
            TotalAgentsExecuted = 5,
            Duration = TimeSpan.FromMinutes(2),
            TotalTokenUsage = new TokenUsageInfo { InputTokens = 500, OutputTokens = 300 }
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.GoalCompleted, result.Type);
        Assert.Equal("All agents completed successfully", result.Content);
        Assert.NotNull(result.Metadata);
        Assert.Equal(5, result.Metadata["totalAgentsExecuted"]);
        Assert.Equal(500, result.Metadata["totalInputTokens"]);
        Assert.Equal(300, result.Metadata["totalOutputTokens"]);
    }

    [Fact]
    public void Map_OrchestrationFailedEvent_ReturnsGoalFailed()
    {
        // Arrange
        var evt = new OrchestrationFailedEvent
        {
            FailedAgent = "buggy-agent",
            ErrorCode = "TIMEOUT",
            ErrorMessage = "Agent execution timed out",
            ExceptionType = "TimeoutException",
            IsRecoverable = true
        };

        // Act
        var result = _mapper.Map(evt, GoalId, ExecutionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GoalExecutionEventType.GoalFailed, result.Type);
        Assert.Equal("buggy-agent", result.AgentName);
        Assert.NotNull(result.Error);
        Assert.Equal("TIMEOUT", result.Error.Code);
        Assert.Equal("Agent execution timed out", result.Error.Message);
        Assert.Equal("TimeoutException", result.Error.ExceptionType);
        Assert.True(result.Error.IsRecoverable);
    }
}
