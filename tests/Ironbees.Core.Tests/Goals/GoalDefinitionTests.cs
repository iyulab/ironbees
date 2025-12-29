// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Goals;
using Xunit;

namespace Ironbees.Core.Tests.Goals;

public class GoalDefinitionTests
{
    [Fact]
    public void GoalDefinition_WithRequiredFields_CreatesSuccessfully()
    {
        // Arrange & Act
        var goal = new GoalDefinition
        {
            Id = "test-goal",
            Name = "Test Goal",
            Description = "A test goal for unit testing",
            WorkflowTemplate = "goal-loop"
        };

        // Assert
        Assert.Equal("test-goal", goal.Id);
        Assert.Equal("Test Goal", goal.Name);
        Assert.Equal("A test goal for unit testing", goal.Description);
        Assert.Equal("goal-loop", goal.WorkflowTemplate);
        Assert.Equal("1.0", goal.Version);
        Assert.Empty(goal.SuccessCriteria);
        Assert.NotNull(goal.Constraints);
        Assert.NotNull(goal.Checkpoint);
    }

    [Fact]
    public void GoalDefinition_WithSuccessCriteria_SetsCorrectly()
    {
        // Arrange
        var criteria = new List<SuccessCriterion>
        {
            new SuccessCriterion
            {
                Id = "criterion-1",
                Description = "Test criterion",
                Type = SuccessCriterionType.LlmEvaluation,
                Weight = 0.5,
                Required = true
            }
        };

        // Act
        var goal = new GoalDefinition
        {
            Id = "test-goal",
            Name = "Test Goal",
            Description = "A test goal",
            WorkflowTemplate = "goal-loop",
            SuccessCriteria = criteria
        };

        // Assert
        Assert.Single(goal.SuccessCriteria);
        Assert.Equal("criterion-1", goal.SuccessCriteria[0].Id);
        Assert.Equal(SuccessCriterionType.LlmEvaluation, goal.SuccessCriteria[0].Type);
        Assert.Equal(0.5, goal.SuccessCriteria[0].Weight);
    }

    [Fact]
    public void GoalDefinition_WithConstraints_SetsCorrectly()
    {
        // Arrange
        var constraints = new GoalConstraints
        {
            MaxIterations = 5,
            MaxTokens = 10000,
            MaxDuration = TimeSpan.FromMinutes(30),
            AllowedAgents = ["agent1", "agent2"]
        };

        // Act
        var goal = new GoalDefinition
        {
            Id = "test-goal",
            Name = "Test Goal",
            Description = "A test goal",
            WorkflowTemplate = "goal-loop",
            Constraints = constraints
        };

        // Assert
        Assert.Equal(5, goal.Constraints.MaxIterations);
        Assert.Equal(10000, goal.Constraints.MaxTokens);
        Assert.Equal(TimeSpan.FromMinutes(30), goal.Constraints.MaxDuration);
        Assert.Equal(2, goal.Constraints.AllowedAgents.Count);
    }

    [Fact]
    public void GoalConstraints_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var constraints = new GoalConstraints();

        // Assert
        Assert.Equal(10, constraints.MaxIterations);
        Assert.Null(constraints.MaxTokens);
        Assert.Null(constraints.MaxDuration);
        Assert.Empty(constraints.AllowedAgents);
        Assert.Empty(constraints.AllowedTools);
    }

    [Fact]
    public void CheckpointSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new CheckpointSettings();

        // Assert
        Assert.True(settings.Enabled);
        Assert.Null(settings.Interval);
        Assert.True(settings.AfterEachIteration);
        Assert.Equal("checkpoints", settings.CheckpointDirectory);
    }

    [Fact]
    public void SuccessCriterion_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var criterion = new SuccessCriterion
        {
            Id = "test",
            Description = "Test criterion"
        };

        // Assert
        Assert.Equal(SuccessCriterionType.Manual, criterion.Type);
        Assert.Null(criterion.Condition);
        Assert.Equal(1.0, criterion.Weight);
        Assert.True(criterion.Required);
    }

    [Fact]
    public void SuccessCriterion_ConditionType_RequiresCondition()
    {
        // Arrange & Act
        var criterion = new SuccessCriterion
        {
            Id = "conditional",
            Description = "Conditional criterion",
            Type = SuccessCriterionType.Condition,
            Condition = "output.score > 0.8"
        };

        // Assert
        Assert.Equal(SuccessCriterionType.Condition, criterion.Type);
        Assert.Equal("output.score > 0.8", criterion.Condition);
    }
}
