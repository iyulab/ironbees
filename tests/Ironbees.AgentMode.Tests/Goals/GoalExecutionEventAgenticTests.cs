// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.AgentMode.Goals;
using Xunit;

namespace Ironbees.AgentMode.Tests.Goals;

public class GoalExecutionEventAgenticTests
{
    #region HitlRequestType Tests

    [Theory]
    [InlineData(HitlRequestType.Approval)]
    [InlineData(HitlRequestType.Decision)]
    [InlineData(HitlRequestType.Input)]
    [InlineData(HitlRequestType.Review)]
    [InlineData(HitlRequestType.Uncertainty)]
    [InlineData(HitlRequestType.Exception)]
    public void HitlRequestType_AllValues_AreValid(HitlRequestType requestType)
    {
        // Assert
        Assert.True(Enum.IsDefined(requestType));
    }

    #endregion

    #region HitlOption Tests

    [Fact]
    public void HitlOption_WithRequiredFields_CreatesSuccessfully()
    {
        // Arrange & Act
        var option = new HitlOption
        {
            Id = "continue",
            Label = "Continue Processing"
        };

        // Assert
        Assert.Equal("continue", option.Id);
        Assert.Equal("Continue Processing", option.Label);
        Assert.Null(option.Description);
        Assert.False(option.IsDefault);
        Assert.Null(option.Data);
    }

    [Fact]
    public void HitlOption_WithAllFields_SetsCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["action"] = "skip",
            ["count"] = 100
        };

        // Act
        var option = new HitlOption
        {
            Id = "skip-sampling",
            Label = "Skip to Approval",
            Description = "Skip remaining sampling and proceed to final approval",
            IsDefault = true,
            Data = data
        };

        // Assert
        Assert.Equal("skip-sampling", option.Id);
        Assert.Equal("Skip to Approval", option.Label);
        Assert.Equal("Skip remaining sampling and proceed to final approval", option.Description);
        Assert.True(option.IsDefault);
        Assert.NotNull(option.Data);
        Assert.Equal(2, option.Data.Count);
        Assert.Equal("skip", option.Data["action"]);
    }

    #endregion

    #region HitlRequestDetails Tests

    [Fact]
    public void HitlRequestDetails_WithRequiredFields_CreatesSuccessfully()
    {
        // Arrange & Act
        var details = new HitlRequestDetails
        {
            RequestId = "hitl-001",
            RequestType = HitlRequestType.Approval,
            Reason = "Rules have stabilized. Ready for batch processing."
        };

        // Assert
        Assert.Equal("hitl-001", details.RequestId);
        Assert.Equal(HitlRequestType.Approval, details.RequestType);
        Assert.Equal("Rules have stabilized. Ready for batch processing.", details.Reason);
        Assert.Null(details.CheckpointName);
        Assert.Null(details.Context);
        Assert.Null(details.Options);
        Assert.True(details.RequestedAt <= DateTimeOffset.UtcNow);
        Assert.Null(details.ExpiresAt);
    }

    [Fact]
    public void HitlRequestDetails_WithFullConfiguration_SetsCorrectly()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            ["confidence"] = 0.95,
            ["samplesProcessed"] = 500
        };

        var options = new List<HitlOption>
        {
            new() { Id = "continue", Label = "Continue", IsDefault = true },
            new() { Id = "stop", Label = "Stop Processing" }
        };

        var requestedAt = DateTimeOffset.UtcNow;
        var expiresAt = requestedAt.AddHours(1);

        // Act
        var details = new HitlRequestDetails
        {
            RequestId = "hitl-002",
            RequestType = HitlRequestType.Decision,
            Reason = "Low confidence detected",
            CheckpointName = "after-initial-sample",
            Context = context,
            Options = options,
            RequestedAt = requestedAt,
            ExpiresAt = expiresAt
        };

        // Assert
        Assert.Equal("hitl-002", details.RequestId);
        Assert.Equal(HitlRequestType.Decision, details.RequestType);
        Assert.Equal("Low confidence detected", details.Reason);
        Assert.Equal("after-initial-sample", details.CheckpointName);
        Assert.NotNull(details.Context);
        Assert.Equal(0.95, details.Context["confidence"]);
        Assert.NotNull(details.Options);
        Assert.Equal(2, details.Options.Count);
        Assert.Equal(requestedAt, details.RequestedAt);
        Assert.Equal(expiresAt, details.ExpiresAt);
    }

    [Theory]
    [InlineData(HitlRequestType.Approval)]
    [InlineData(HitlRequestType.Decision)]
    [InlineData(HitlRequestType.Input)]
    [InlineData(HitlRequestType.Review)]
    [InlineData(HitlRequestType.Uncertainty)]
    [InlineData(HitlRequestType.Exception)]
    public void HitlRequestDetails_AllRequestTypes_AreValid(HitlRequestType requestType)
    {
        // Arrange & Act
        var details = new HitlRequestDetails
        {
            RequestId = $"hitl-{requestType}",
            RequestType = requestType,
            Reason = $"Request type: {requestType}"
        };

        // Assert
        Assert.Equal(requestType, details.RequestType);
    }

    #endregion

    #region GoalExecutionEvent Agentic Properties Tests

    [Fact]
    public void GoalExecutionEvent_WithHitlRequest_SetsCorrectly()
    {
        // Arrange
        var hitlRequest = new HitlRequestDetails
        {
            RequestId = "hitl-001",
            RequestType = HitlRequestType.Approval,
            Reason = "Ready for batch processing"
        };

        // Act
        var evt = new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.HitlRequested,
            GoalId = "test-goal",
            ExecutionId = "exec-001",
            HitlRequest = hitlRequest
        };

        // Assert
        Assert.Equal(GoalExecutionEventType.HitlRequested, evt.Type);
        Assert.NotNull(evt.HitlRequest);
        Assert.Equal("hitl-001", evt.HitlRequest.RequestId);
        Assert.Equal(HitlRequestType.Approval, evt.HitlRequest.RequestType);
    }

    #endregion

    #region GoalExecutionError Tests (Agentic Context)

    [Fact]
    public void GoalExecutionError_FromException_CreatesCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Sampling failed due to data corruption");

        // Act
        var error = GoalExecutionError.FromException(exception, isRecoverable: true);

        // Assert
        Assert.Equal("InvalidOperationException", error.Code);
        Assert.Equal("Sampling failed due to data corruption", error.Message);
        Assert.Equal("System.InvalidOperationException", error.ExceptionType);
        Assert.True(error.IsRecoverable);
    }

    [Fact]
    public void GoalExecutionError_NonRecoverable_SetsCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Simulated OOM error: Dataset too large");

        // Act
        var error = GoalExecutionError.FromException(exception, isRecoverable: false);

        // Assert
        Assert.False(error.IsRecoverable);
        Assert.Equal("InvalidOperationException", error.Code);
    }

    #endregion
}
