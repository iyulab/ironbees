// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Goals;
using Xunit;

namespace Ironbees.Core.Tests.Goals;

public class GoalValidatorTests : IDisposable
{
    private readonly string _testDirectory;

    public GoalValidatorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ironbees-goal-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private GoalDefinition CreateValidGoal() => new()
    {
        Id = "test-goal",
        Name = "Test Goal",
        Description = "A valid test goal",
        WorkflowTemplate = "goal-loop",
        SourcePath = _testDirectory
    };

    [Fact]
    public void Validate_ValidGoal_ReturnsSuccess()
    {
        // Arrange
        var goal = CreateValidGoal();

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings); // Warning about no success criteria
    }

    [Fact]
    public void Validate_MissingId_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with { Id = "" };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id"));
    }

    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with { Name = "" };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
    }

    [Fact]
    public void Validate_MissingDescription_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with { Description = "" };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("description"));
    }

    [Fact]
    public void Validate_MissingWorkflowTemplate_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with { WorkflowTemplate = "" };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("workflowTemplate"));
    }

    [Fact]
    public void Validate_InvalidMaxIterations_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with
        {
            Constraints = new GoalConstraints { MaxIterations = 0 }
        };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maxIterations"));
    }

    [Fact]
    public void Validate_InvalidMaxTokens_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with
        {
            Constraints = new GoalConstraints { MaxTokens = 0 }
        };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maxTokens"));
    }

    [Fact]
    public void Validate_SuccessCriterionMissingId_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with
        {
            SuccessCriteria =
            [
                new SuccessCriterion { Id = "", Description = "Test" }
            ]
        };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("criterion") && e.Contains("id"));
    }

    [Fact]
    public void Validate_SuccessCriterionMissingDescription_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with
        {
            SuccessCriteria =
            [
                new SuccessCriterion { Id = "test", Description = "" }
            ]
        };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("requires a description"));
    }

    [Fact]
    public void Validate_SuccessCriterionInvalidWeight_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with
        {
            SuccessCriteria =
            [
                new SuccessCriterion { Id = "test", Description = "Test", Weight = 1.5 }
            ]
        };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("weight"));
    }

    [Fact]
    public void Validate_ConditionTypeWithoutCondition_ReturnsError()
    {
        // Arrange
        var goal = CreateValidGoal() with
        {
            SuccessCriteria =
            [
                new SuccessCriterion
                {
                    Id = "test",
                    Description = "Test",
                    Type = SuccessCriterionType.Condition,
                    Condition = null
                }
            ]
        };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("condition expression"));
    }

    [Fact]
    public void Validate_HighMaxIterations_ReturnsWarning()
    {
        // Arrange
        var goal = CreateValidGoal() with
        {
            Constraints = new GoalConstraints { MaxIterations = 150 }
        };

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("maxIterations"));
    }

    [Fact]
    public void Validate_NoSuccessCriteria_ReturnsWarning()
    {
        // Arrange
        var goal = CreateValidGoal();

        // Act
        var result = GoalValidator.Validate(goal);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("No success criteria"));
    }

    [Fact]
    public void ValidateDirectory_NonExistentPath_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent");

        // Act
        var result = GoalValidator.ValidateDirectory(nonExistentPath);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not found"));
    }

    [Fact]
    public void ValidateDirectory_MissingGoalYaml_ReturnsFailure()
    {
        // Arrange
        var goalPath = Path.Combine(_testDirectory, "incomplete-goal");
        Directory.CreateDirectory(goalPath);

        // Act
        var result = GoalValidator.ValidateDirectory(goalPath);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("goal.yaml"));
    }

    [Fact]
    public void ValidateDirectory_ValidDirectory_ReturnsSuccess()
    {
        // Arrange
        var goalPath = Path.Combine(_testDirectory, "valid-goal");
        Directory.CreateDirectory(goalPath);
        File.WriteAllText(Path.Combine(goalPath, "goal.yaml"), "id: test\nname: Test");

        // Act
        var result = GoalValidator.ValidateDirectory(goalPath);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidationResult_Success_CreatesCorrectResult()
    {
        // Act
        var result = GoalValidationResult.Success("/path/to/goal", "goal-id");

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal("/path/to/goal", result.GoalPath);
        Assert.Equal("goal-id", result.GoalId);
    }

    [Fact]
    public void ValidationResult_Failure_CreatesCorrectResult()
    {
        // Act
        var result = GoalValidationResult.Failure("/path/to/goal", ["Error 1", "Error 2"]);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(result.Warnings);
        Assert.Equal("/path/to/goal", result.GoalPath);
    }
}
