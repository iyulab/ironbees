// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Goals;
using Xunit;

namespace Ironbees.Core.Tests.Goals;

public class AgenticSettingsTests
{
    #region SamplingSettings Tests

    [Fact]
    public void SamplingSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new SamplingSettings();

        // Assert
        Assert.Equal(SamplingStrategy.Progressive, settings.Strategy);
        Assert.Equal(100, settings.InitialBatchSize);
        Assert.Equal(5.0, settings.GrowthFactor);
        Assert.Null(settings.MaxSamples);
        Assert.Equal(50, settings.MinSamplesForConfidence);
    }

    [Fact]
    public void SamplingSettings_WithCustomValues_SetsCorrectly()
    {
        // Arrange & Act
        var settings = new SamplingSettings
        {
            Strategy = SamplingStrategy.Stratified,
            InitialBatchSize = 200,
            GrowthFactor = 3.0,
            MaxSamples = 10000,
            MinSamplesForConfidence = 100
        };

        // Assert
        Assert.Equal(SamplingStrategy.Stratified, settings.Strategy);
        Assert.Equal(200, settings.InitialBatchSize);
        Assert.Equal(3.0, settings.GrowthFactor);
        Assert.Equal(10000, settings.MaxSamples);
        Assert.Equal(100, settings.MinSamplesForConfidence);
    }

    [Theory]
    [InlineData(SamplingStrategy.Progressive)]
    [InlineData(SamplingStrategy.Random)]
    [InlineData(SamplingStrategy.Stratified)]
    [InlineData(SamplingStrategy.Sequential)]
    public void SamplingStrategy_AllValues_AreValid(SamplingStrategy strategy)
    {
        // Arrange & Act
        var settings = new SamplingSettings { Strategy = strategy };

        // Assert
        Assert.Equal(strategy, settings.Strategy);
    }

    #endregion

    #region ConfidenceSettings Tests

    [Fact]
    public void ConfidenceSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new ConfidenceSettings();

        // Assert
        Assert.Equal(0.95, settings.Threshold);
        Assert.Equal(3, settings.StabilityWindow);
        Assert.Null(settings.MinConfidenceForHitl);
        Assert.True(settings.TrackHistory);
    }

    [Fact]
    public void ConfidenceSettings_WithCustomValues_SetsCorrectly()
    {
        // Arrange & Act
        var settings = new ConfidenceSettings
        {
            Threshold = 0.98,
            StabilityWindow = 5,
            MinConfidenceForHitl = 0.7,
            TrackHistory = false
        };

        // Assert
        Assert.Equal(0.98, settings.Threshold);
        Assert.Equal(5, settings.StabilityWindow);
        Assert.Equal(0.7, settings.MinConfidenceForHitl);
        Assert.False(settings.TrackHistory);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.95)]
    [InlineData(1.0)]
    public void ConfidenceSettings_ThresholdRange_AcceptsValidValues(double threshold)
    {
        // Arrange & Act
        var settings = new ConfidenceSettings { Threshold = threshold };

        // Assert
        Assert.Equal(threshold, settings.Threshold);
    }

    #endregion

    #region HitlSettings Tests

    [Fact]
    public void HitlSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new HitlSettings();

        // Assert
        Assert.Equal(HitlPolicy.OnUncertainty, settings.Policy);
        Assert.Equal(0.7, settings.UncertaintyThreshold);
        Assert.Empty(settings.Checkpoints);
        Assert.Null(settings.ResponseTimeout);
        Assert.Equal(HitlTimeoutAction.Pause, settings.TimeoutAction);
    }

    [Fact]
    public void HitlSettings_WithCustomValues_SetsCorrectly()
    {
        // Arrange & Act
        var settings = new HitlSettings
        {
            Policy = HitlPolicy.Always,
            UncertaintyThreshold = 0.5,
            Checkpoints = ["after-sampling", "before-apply"],
            ResponseTimeout = TimeSpan.FromHours(1),
            TimeoutAction = HitlTimeoutAction.ContinueWithDefault
        };

        // Assert
        Assert.Equal(HitlPolicy.Always, settings.Policy);
        Assert.Equal(0.5, settings.UncertaintyThreshold);
        Assert.Equal(2, settings.Checkpoints.Count);
        Assert.Contains("after-sampling", settings.Checkpoints);
        Assert.Contains("before-apply", settings.Checkpoints);
        Assert.Equal(TimeSpan.FromHours(1), settings.ResponseTimeout);
        Assert.Equal(HitlTimeoutAction.ContinueWithDefault, settings.TimeoutAction);
    }

    [Theory]
    [InlineData(HitlPolicy.Always)]
    [InlineData(HitlPolicy.OnUncertainty)]
    [InlineData(HitlPolicy.OnThreshold)]
    [InlineData(HitlPolicy.OnException)]
    [InlineData(HitlPolicy.Never)]
    public void HitlPolicy_AllValues_AreValid(HitlPolicy policy)
    {
        // Arrange & Act
        var settings = new HitlSettings { Policy = policy };

        // Assert
        Assert.Equal(policy, settings.Policy);
    }

    [Theory]
    [InlineData(HitlTimeoutAction.Pause)]
    [InlineData(HitlTimeoutAction.ContinueWithDefault)]
    [InlineData(HitlTimeoutAction.Cancel)]
    [InlineData(HitlTimeoutAction.Skip)]
    public void HitlTimeoutAction_AllValues_AreValid(HitlTimeoutAction action)
    {
        // Arrange & Act
        var settings = new HitlSettings { TimeoutAction = action };

        // Assert
        Assert.Equal(action, settings.TimeoutAction);
    }

    #endregion

    #region AgenticSettings Tests

    [Fact]
    public void AgenticSettings_WithAllNull_CreatesSuccessfully()
    {
        // Arrange & Act
        var settings = new AgenticSettings();

        // Assert
        Assert.Null(settings.Sampling);
        Assert.Null(settings.Confidence);
        Assert.Null(settings.Hitl);
    }

    [Fact]
    public void AgenticSettings_WithSamplingOnly_SetsCorrectly()
    {
        // Arrange
        var sampling = new SamplingSettings
        {
            Strategy = SamplingStrategy.Progressive,
            InitialBatchSize = 100
        };

        // Act
        var settings = new AgenticSettings { Sampling = sampling };

        // Assert
        Assert.NotNull(settings.Sampling);
        Assert.Equal(SamplingStrategy.Progressive, settings.Sampling.Strategy);
        Assert.Null(settings.Confidence);
        Assert.Null(settings.Hitl);
    }

    [Fact]
    public void AgenticSettings_WithConfidenceOnly_SetsCorrectly()
    {
        // Arrange
        var confidence = new ConfidenceSettings
        {
            Threshold = 0.98,
            StabilityWindow = 3
        };

        // Act
        var settings = new AgenticSettings { Confidence = confidence };

        // Assert
        Assert.Null(settings.Sampling);
        Assert.NotNull(settings.Confidence);
        Assert.Equal(0.98, settings.Confidence.Threshold);
        Assert.Null(settings.Hitl);
    }

    [Fact]
    public void AgenticSettings_WithHitlOnly_SetsCorrectly()
    {
        // Arrange
        var hitl = new HitlSettings
        {
            Policy = HitlPolicy.OnUncertainty,
            UncertaintyThreshold = 0.7
        };

        // Act
        var settings = new AgenticSettings { Hitl = hitl };

        // Assert
        Assert.Null(settings.Sampling);
        Assert.Null(settings.Confidence);
        Assert.NotNull(settings.Hitl);
        Assert.Equal(HitlPolicy.OnUncertainty, settings.Hitl.Policy);
    }

    [Fact]
    public void AgenticSettings_FullConfiguration_SetsAllCorrectly()
    {
        // Arrange
        var sampling = new SamplingSettings
        {
            Strategy = SamplingStrategy.Progressive,
            InitialBatchSize = 100,
            GrowthFactor = 5.0,
            MaxSamples = 5000,
            MinSamplesForConfidence = 50
        };

        var confidence = new ConfidenceSettings
        {
            Threshold = 0.98,
            StabilityWindow = 3,
            MinConfidenceForHitl = 0.7,
            TrackHistory = true
        };

        var hitl = new HitlSettings
        {
            Policy = HitlPolicy.OnUncertainty,
            UncertaintyThreshold = 0.7,
            Checkpoints = ["after-initial-sample", "before-batch-apply", "on-exception"],
            ResponseTimeout = TimeSpan.FromHours(1),
            TimeoutAction = HitlTimeoutAction.Pause
        };

        // Act
        var settings = new AgenticSettings
        {
            Sampling = sampling,
            Confidence = confidence,
            Hitl = hitl
        };

        // Assert
        Assert.NotNull(settings.Sampling);
        Assert.NotNull(settings.Confidence);
        Assert.NotNull(settings.Hitl);

        Assert.Equal(SamplingStrategy.Progressive, settings.Sampling.Strategy);
        Assert.Equal(100, settings.Sampling.InitialBatchSize);
        Assert.Equal(5.0, settings.Sampling.GrowthFactor);
        Assert.Equal(5000, settings.Sampling.MaxSamples);

        Assert.Equal(0.98, settings.Confidence.Threshold);
        Assert.Equal(3, settings.Confidence.StabilityWindow);
        Assert.Equal(0.7, settings.Confidence.MinConfidenceForHitl);

        Assert.Equal(HitlPolicy.OnUncertainty, settings.Hitl.Policy);
        Assert.Equal(3, settings.Hitl.Checkpoints.Count);
        Assert.Equal(TimeSpan.FromHours(1), settings.Hitl.ResponseTimeout);
    }

    #endregion

    #region GoalDefinition Integration Tests

    [Fact]
    public void GoalDefinition_WithAgenticSettings_SetsCorrectly()
    {
        // Arrange
        var agentic = new AgenticSettings
        {
            Sampling = new SamplingSettings { Strategy = SamplingStrategy.Progressive },
            Confidence = new ConfidenceSettings { Threshold = 0.95 },
            Hitl = new HitlSettings { Policy = HitlPolicy.OnUncertainty }
        };

        // Act
        var goal = new GoalDefinition
        {
            Id = "agentic-goal",
            Name = "Agentic Goal",
            Description = "A goal with agentic patterns",
            WorkflowTemplate = "agentic-loop",
            Agentic = agentic
        };

        // Assert
        Assert.NotNull(goal.Agentic);
        Assert.NotNull(goal.Agentic.Sampling);
        Assert.NotNull(goal.Agentic.Confidence);
        Assert.NotNull(goal.Agentic.Hitl);
        Assert.Equal(SamplingStrategy.Progressive, goal.Agentic.Sampling.Strategy);
        Assert.Equal(0.95, goal.Agentic.Confidence.Threshold);
        Assert.Equal(HitlPolicy.OnUncertainty, goal.Agentic.Hitl.Policy);
    }

    [Fact]
    public void GoalDefinition_WithoutAgenticSettings_IsNull()
    {
        // Arrange & Act
        var goal = new GoalDefinition
        {
            Id = "simple-goal",
            Name = "Simple Goal",
            Description = "A goal without agentic patterns",
            WorkflowTemplate = "goal-loop"
        };

        // Assert
        Assert.Null(goal.Agentic);
    }

    [Fact]
    public void GoalDefinition_IncrementalPreprocessingScenario_ConfiguresCorrectly()
    {
        // Arrange & Act - Full incremental preprocessing configuration as in ROADMAP.md
        var goal = new GoalDefinition
        {
            Id = "incremental-preprocessing",
            Name = "Incremental Data Preprocessing",
            Description = "Progressively sample and analyze data to discover preprocessing rules",
            Version = "1.0",
            WorkflowTemplate = "agentic-loop",
            Agentic = new AgenticSettings
            {
                Sampling = new SamplingSettings
                {
                    Strategy = SamplingStrategy.Progressive,
                    InitialBatchSize = 100,
                    GrowthFactor = 5.0,
                    MaxSamples = 5000,
                    MinSamplesForConfidence = 50
                },
                Confidence = new ConfidenceSettings
                {
                    Threshold = 0.98,
                    StabilityWindow = 3,
                    MinConfidenceForHitl = 0.7,
                    TrackHistory = true
                },
                Hitl = new HitlSettings
                {
                    Policy = HitlPolicy.OnUncertainty,
                    UncertaintyThreshold = 0.7,
                    Checkpoints = ["after-initial-sample", "before-batch-apply", "on-exception"],
                    ResponseTimeout = TimeSpan.FromHours(1),
                    TimeoutAction = HitlTimeoutAction.Pause
                }
            },
            Constraints = new GoalConstraints
            {
                MaxIterations = 20,
                MaxTokens = 100000,
                AllowedAgents = ["data-sampler", "pattern-analyzer", "batch-processor", "report-generator"]
            },
            SuccessCriteria =
            [
                new SuccessCriterion
                {
                    Id = "rules-stable",
                    Description = "Preprocessing rules have stabilized",
                    Type = SuccessCriterionType.Condition,
                    Condition = "confidence >= 0.98 && stabilityWindow >= 3",
                    Required = true,
                    Weight = 0.6
                },
                new SuccessCriterion
                {
                    Id = "error-rate-acceptable",
                    Description = "Error rate is within acceptable bounds",
                    Type = SuccessCriterionType.Condition,
                    Condition = "errorRate <= 0.01",
                    Required = true,
                    Weight = 0.4
                }
            ],
            Checkpoint = new CheckpointSettings
            {
                Enabled = true,
                AfterEachIteration = true,
                CheckpointDirectory = "checkpoints"
            },
            Tags = ["data-preprocessing", "agentic-pattern", "progressive-sampling", "hitl"]
        };

        // Assert - Verify full configuration
        Assert.Equal("incremental-preprocessing", goal.Id);
        Assert.Equal("agentic-loop", goal.WorkflowTemplate);

        // Agentic settings
        Assert.NotNull(goal.Agentic);
        Assert.Equal(SamplingStrategy.Progressive, goal.Agentic.Sampling!.Strategy);
        Assert.Equal(100, goal.Agentic.Sampling.InitialBatchSize);
        Assert.Equal(5.0, goal.Agentic.Sampling.GrowthFactor);
        Assert.Equal(5000, goal.Agentic.Sampling.MaxSamples);

        Assert.Equal(0.98, goal.Agentic.Confidence!.Threshold);
        Assert.Equal(3, goal.Agentic.Confidence.StabilityWindow);

        Assert.Equal(HitlPolicy.OnUncertainty, goal.Agentic.Hitl!.Policy);
        Assert.Equal(0.7, goal.Agentic.Hitl.UncertaintyThreshold);
        Assert.Equal(3, goal.Agentic.Hitl.Checkpoints.Count);

        // Constraints
        Assert.Equal(20, goal.Constraints.MaxIterations);
        Assert.Equal(100000, goal.Constraints.MaxTokens);
        Assert.Equal(4, goal.Constraints.AllowedAgents.Count);

        // Success criteria
        Assert.Equal(2, goal.SuccessCriteria.Count);
        Assert.Equal(0.6, goal.SuccessCriteria[0].Weight);
        Assert.Equal(0.4, goal.SuccessCriteria[1].Weight);

        // Tags
        Assert.Equal(4, goal.Tags.Count);
        Assert.Contains("hitl", goal.Tags);
    }

    #endregion
}
