// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.AgentMode.Core.Goals;
using Xunit;

namespace Ironbees.AgentMode.Tests.Goals;

public class GoalExecutionEventAgenticTests
{
    #region GoalExecutionEventType Tests

    [Theory]
    [InlineData(GoalExecutionEventType.HitlRequested)]
    [InlineData(GoalExecutionEventType.HitlResponseReceived)]
    [InlineData(GoalExecutionEventType.ConfidenceUpdated)]
    [InlineData(GoalExecutionEventType.SamplingProgress)]
    [InlineData(GoalExecutionEventType.PatternDiscovered)]
    [InlineData(GoalExecutionEventType.RulesStabilized)]
    public void GoalExecutionEventType_AgenticEvents_AreValid(GoalExecutionEventType eventType)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(GoalExecutionEventType), eventType));
    }

    [Fact]
    public void GoalExecutionEventType_ContainsAllExpectedAgenticTypes()
    {
        // Arrange
        var agenticTypes = new[]
        {
            GoalExecutionEventType.HitlRequested,
            GoalExecutionEventType.HitlResponseReceived,
            GoalExecutionEventType.ConfidenceUpdated,
            GoalExecutionEventType.SamplingProgress,
            GoalExecutionEventType.PatternDiscovered,
            GoalExecutionEventType.RulesStabilized
        };

        // Act & Assert
        foreach (var type in agenticTypes)
        {
            Assert.True(Enum.IsDefined(typeof(GoalExecutionEventType), type),
                $"{type} should be defined in GoalExecutionEventType");
        }
    }

    #endregion

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
        Assert.True(Enum.IsDefined(typeof(HitlRequestType), requestType));
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

    #region ConfidenceInfo Tests

    [Fact]
    public void ConfidenceInfo_WithRequiredFields_CreatesSuccessfully()
    {
        // Arrange & Act
        var info = new ConfidenceInfo
        {
            CurrentConfidence = 0.85
        };

        // Assert
        Assert.Equal(0.85, info.CurrentConfidence);
        Assert.Equal(0, info.TargetThreshold);
        Assert.Equal(0, info.SamplesProcessed);
        Assert.Equal(0, info.StableIterations);
        Assert.False(info.IsStable);
        Assert.Null(info.ConfidenceDelta);
        Assert.Equal(0, info.PatternsDiscovered);
    }

    [Fact]
    public void ConfidenceInfo_WithFullConfiguration_SetsCorrectly()
    {
        // Arrange & Act
        var info = new ConfidenceInfo
        {
            CurrentConfidence = 0.98,
            TargetThreshold = 0.95,
            SamplesProcessed = 500,
            StableIterations = 3,
            IsStable = true,
            ConfidenceDelta = 0.02,
            PatternsDiscovered = 15
        };

        // Assert
        Assert.Equal(0.98, info.CurrentConfidence);
        Assert.Equal(0.95, info.TargetThreshold);
        Assert.Equal(500, info.SamplesProcessed);
        Assert.Equal(3, info.StableIterations);
        Assert.True(info.IsStable);
        Assert.Equal(0.02, info.ConfidenceDelta);
        Assert.Equal(15, info.PatternsDiscovered);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.95)]
    [InlineData(1.0)]
    public void ConfidenceInfo_ConfidenceRange_AcceptsValidValues(double confidence)
    {
        // Arrange & Act
        var info = new ConfidenceInfo { CurrentConfidence = confidence };

        // Assert
        Assert.Equal(confidence, info.CurrentConfidence);
    }

    [Fact]
    public void ConfidenceInfo_NegativeConfidenceDelta_IsValid()
    {
        // Arrange & Act - Confidence can decrease
        var info = new ConfidenceInfo
        {
            CurrentConfidence = 0.80,
            ConfidenceDelta = -0.05
        };

        // Assert
        Assert.Equal(-0.05, info.ConfidenceDelta);
    }

    #endregion

    #region SamplingProgressInfo Tests

    [Fact]
    public void SamplingProgressInfo_WithRequiredFields_CreatesSuccessfully()
    {
        // Arrange & Act
        var progress = new SamplingProgressInfo
        {
            CurrentBatch = 1,
            SamplesInBatch = 100,
            TotalProcessed = 100
        };

        // Assert
        Assert.Equal(1, progress.CurrentBatch);
        Assert.Equal(100, progress.SamplesInBatch);
        Assert.Equal(100, progress.TotalProcessed);
        Assert.Null(progress.TotalDatasetSize);
        Assert.Null(progress.ProcessingPercentage);
        Assert.Null(progress.DiscoveredPatterns);
        Assert.Equal(0, progress.ExceptionsInBatch);
        Assert.Null(progress.ErrorRate);
    }

    [Fact]
    public void SamplingProgressInfo_WithFullConfiguration_SetsCorrectly()
    {
        // Arrange
        var patterns = new List<string> { "pattern-1", "pattern-2", "pattern-3" };

        // Act
        var progress = new SamplingProgressInfo
        {
            CurrentBatch = 3,
            SamplesInBatch = 500,
            TotalProcessed = 1500,
            TotalDatasetSize = 10000,
            DiscoveredPatterns = patterns,
            ExceptionsInBatch = 5,
            ErrorRate = 0.003
        };

        // Assert
        Assert.Equal(3, progress.CurrentBatch);
        Assert.Equal(500, progress.SamplesInBatch);
        Assert.Equal(1500, progress.TotalProcessed);
        Assert.Equal(10000, progress.TotalDatasetSize);
        Assert.NotNull(progress.DiscoveredPatterns);
        Assert.Equal(3, progress.DiscoveredPatterns.Count);
        Assert.Equal(5, progress.ExceptionsInBatch);
        Assert.Equal(0.003, progress.ErrorRate);
    }

    [Fact]
    public void SamplingProgressInfo_ProcessingPercentage_CalculatesCorrectly()
    {
        // Arrange & Act
        var progress = new SamplingProgressInfo
        {
            CurrentBatch = 2,
            SamplesInBatch = 500,
            TotalProcessed = 1000,
            TotalDatasetSize = 5000
        };

        // Assert
        Assert.NotNull(progress.ProcessingPercentage);
        Assert.Equal(20.0, progress.ProcessingPercentage.Value, precision: 1);
    }

    [Fact]
    public void SamplingProgressInfo_ProcessingPercentage_ReturnsNullWhenNoTotalSize()
    {
        // Arrange & Act
        var progress = new SamplingProgressInfo
        {
            CurrentBatch = 2,
            SamplesInBatch = 500,
            TotalProcessed = 1000,
            TotalDatasetSize = null
        };

        // Assert
        Assert.Null(progress.ProcessingPercentage);
    }

    [Fact]
    public void SamplingProgressInfo_ProcessingPercentage_ReturnsNullWhenZeroTotalSize()
    {
        // Arrange & Act
        var progress = new SamplingProgressInfo
        {
            CurrentBatch = 2,
            SamplesInBatch = 500,
            TotalProcessed = 1000,
            TotalDatasetSize = 0
        };

        // Assert
        Assert.Null(progress.ProcessingPercentage);
    }

    [Fact]
    public void SamplingProgressInfo_ProgressiveScenario_TracksGrowingBatches()
    {
        // Arrange - Simulate progressive sampling: 100 → 500 → 2500
        var batch1 = new SamplingProgressInfo
        {
            CurrentBatch = 1,
            SamplesInBatch = 100,
            TotalProcessed = 100,
            TotalDatasetSize = 10000
        };

        var batch2 = new SamplingProgressInfo
        {
            CurrentBatch = 2,
            SamplesInBatch = 500,
            TotalProcessed = 600,
            TotalDatasetSize = 10000
        };

        var batch3 = new SamplingProgressInfo
        {
            CurrentBatch = 3,
            SamplesInBatch = 2500,
            TotalProcessed = 3100,
            TotalDatasetSize = 10000
        };

        // Assert
        Assert.Equal(1.0, batch1.ProcessingPercentage!.Value, precision: 1);
        Assert.Equal(6.0, batch2.ProcessingPercentage!.Value, precision: 1);
        Assert.Equal(31.0, batch3.ProcessingPercentage!.Value, precision: 1);
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

    [Fact]
    public void GoalExecutionEvent_WithConfidence_SetsCorrectly()
    {
        // Arrange
        var confidence = new ConfidenceInfo
        {
            CurrentConfidence = 0.95,
            TargetThreshold = 0.98,
            IsStable = false
        };

        // Act
        var evt = new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.ConfidenceUpdated,
            GoalId = "test-goal",
            ExecutionId = "exec-001",
            Confidence = confidence
        };

        // Assert
        Assert.Equal(GoalExecutionEventType.ConfidenceUpdated, evt.Type);
        Assert.NotNull(evt.Confidence);
        Assert.Equal(0.95, evt.Confidence.CurrentConfidence);
        Assert.Equal(0.98, evt.Confidence.TargetThreshold);
        Assert.False(evt.Confidence.IsStable);
    }

    [Fact]
    public void GoalExecutionEvent_WithSampling_SetsCorrectly()
    {
        // Arrange
        var sampling = new SamplingProgressInfo
        {
            CurrentBatch = 2,
            SamplesInBatch = 500,
            TotalProcessed = 600,
            TotalDatasetSize = 10000
        };

        // Act
        var evt = new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.SamplingProgress,
            GoalId = "test-goal",
            ExecutionId = "exec-001",
            Sampling = sampling
        };

        // Assert
        Assert.Equal(GoalExecutionEventType.SamplingProgress, evt.Type);
        Assert.NotNull(evt.Sampling);
        Assert.Equal(2, evt.Sampling.CurrentBatch);
        Assert.Equal(500, evt.Sampling.SamplesInBatch);
        Assert.Equal(6.0, evt.Sampling.ProcessingPercentage!.Value, precision: 1);
    }

    [Fact]
    public void GoalExecutionEvent_PatternDiscovered_SetsContent()
    {
        // Arrange & Act
        var evt = new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.PatternDiscovered,
            GoalId = "test-goal",
            ExecutionId = "exec-001",
            Content = "Discovered new date format pattern: MM/DD/YYYY"
        };

        // Assert
        Assert.Equal(GoalExecutionEventType.PatternDiscovered, evt.Type);
        Assert.Contains("date format pattern", evt.Content);
    }

    [Fact]
    public void GoalExecutionEvent_RulesStabilized_SetsMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["stabilityWindow"] = 3,
            ["patternsCount"] = 15,
            ["confidence"] = 0.98
        };

        // Act
        var evt = new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.RulesStabilized,
            GoalId = "test-goal",
            ExecutionId = "exec-001",
            Content = "Rules have stabilized after 3 consecutive iterations",
            Metadata = metadata
        };

        // Assert
        Assert.Equal(GoalExecutionEventType.RulesStabilized, evt.Type);
        Assert.NotNull(evt.Metadata);
        Assert.Equal(3, evt.Metadata["stabilityWindow"]);
        Assert.Equal(15, evt.Metadata["patternsCount"]);
    }

    [Fact]
    public void GoalExecutionEvent_FullAgenticScenario_ConfiguresCorrectly()
    {
        // Arrange - Simulate a complete agentic workflow event stream
        var events = new List<GoalExecutionEvent>
        {
            // Initial sampling
            new()
            {
                Type = GoalExecutionEventType.SamplingProgress,
                GoalId = "preproc-001",
                ExecutionId = "exec-001",
                IterationNumber = 1,
                Sampling = new SamplingProgressInfo
                {
                    CurrentBatch = 1,
                    SamplesInBatch = 100,
                    TotalProcessed = 100,
                    TotalDatasetSize = 10000
                }
            },
            // Confidence update
            new()
            {
                Type = GoalExecutionEventType.ConfidenceUpdated,
                GoalId = "preproc-001",
                ExecutionId = "exec-001",
                IterationNumber = 1,
                Confidence = new ConfidenceInfo
                {
                    CurrentConfidence = 0.75,
                    TargetThreshold = 0.98,
                    SamplesProcessed = 100,
                    IsStable = false
                }
            },
            // HITL checkpoint
            new()
            {
                Type = GoalExecutionEventType.HitlRequested,
                GoalId = "preproc-001",
                ExecutionId = "exec-001",
                IterationNumber = 1,
                HitlRequest = new HitlRequestDetails
                {
                    RequestId = "hitl-001",
                    RequestType = HitlRequestType.Review,
                    Reason = "Initial sample analysis complete",
                    CheckpointName = "after-initial-sample",
                    Options =
                    [
                        new HitlOption { Id = "continue", Label = "Continue", IsDefault = true },
                        new HitlOption { Id = "modify", Label = "Modify Rules" }
                    ]
                }
            }
        };

        // Assert
        Assert.Equal(3, events.Count);
        Assert.All(events, e => Assert.Equal("preproc-001", e.GoalId));
        Assert.All(events, e => Assert.Equal("exec-001", e.ExecutionId));

        var samplingEvent = events[0];
        Assert.NotNull(samplingEvent.Sampling);
        Assert.Equal(1.0, samplingEvent.Sampling.ProcessingPercentage!.Value, precision: 1);

        var confidenceEvent = events[1];
        Assert.NotNull(confidenceEvent.Confidence);
        Assert.Equal(0.75, confidenceEvent.Confidence.CurrentConfidence);

        var hitlEvent = events[2];
        Assert.NotNull(hitlEvent.HitlRequest);
        Assert.Equal(2, hitlEvent.HitlRequest.Options!.Count);
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
        var exception = new OutOfMemoryException("Dataset too large");

        // Act
        var error = GoalExecutionError.FromException(exception, isRecoverable: false);

        // Assert
        Assert.False(error.IsRecoverable);
        Assert.Equal("OutOfMemoryException", error.Code);
    }

    #endregion
}
