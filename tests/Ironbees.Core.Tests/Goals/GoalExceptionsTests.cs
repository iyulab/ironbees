// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Goals;
using Xunit;

namespace Ironbees.Core.Tests.Goals;

public class GoalExceptionsTests
{
    [Fact]
    public void GoalLoadException_WithMessage_SetsMessage()
    {
        // Arrange & Act
        var exception = new GoalLoadException("Test message");

        // Assert
        Assert.Equal("Test message", exception.Message);
        Assert.Null(exception.GoalPath);
        Assert.Null(exception.GoalId);
    }

    [Fact]
    public void GoalLoadException_WithPath_SetsPath()
    {
        // Arrange & Act
        var exception = new GoalLoadException("Test message", "/path/to/goal");

        // Assert
        Assert.Equal("Test message", exception.Message);
        Assert.Equal("/path/to/goal", exception.GoalPath);
    }

    [Fact]
    public void GoalLoadException_WithInnerException_SetsInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("Inner error");

        // Act
        var exception = new GoalLoadException("Test message", "/path/to/goal", inner);

        // Assert
        Assert.Equal("Test message", exception.Message);
        Assert.Equal("/path/to/goal", exception.GoalPath);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void GoalLoadException_WithGoalId_SetsGoalId()
    {
        // Arrange & Act
        var exception = new GoalLoadException("Test message", "/path/to/goal", "goal-123");

        // Assert
        Assert.Equal("/path/to/goal", exception.GoalPath);
        Assert.Equal("goal-123", exception.GoalId);
    }

    [Fact]
    public void GoalValidationException_WithErrors_SetsErrors()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var exception = new GoalValidationException("Validation failed", errors);

        // Assert
        Assert.Equal("Validation failed", exception.Message);
        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains("Error 1", exception.Errors);
        Assert.Contains("Error 2", exception.Errors);
    }

    [Fact]
    public void GoalValidationException_WithPath_SetsPath()
    {
        // Arrange
        var errors = new[] { "Error 1" };

        // Act
        var exception = new GoalValidationException("Validation failed", "/path/to/goal", errors);

        // Assert
        Assert.Equal("/path/to/goal", exception.GoalPath);
        Assert.Single(exception.Errors);
    }

    [Fact]
    public void GoalNotFoundException_WithMessage_SetsMessage()
    {
        // Arrange & Act
        var exception = new GoalNotFoundException("Goal not found");

        // Assert
        Assert.Equal("Goal not found", exception.Message);
        Assert.Null(exception.GoalId);
        Assert.Null(exception.SearchPath);
    }

    [Fact]
    public void GoalNotFoundException_WithGoalId_SetsGoalId()
    {
        // Arrange & Act
        var exception = new GoalNotFoundException("Goal not found", "goal-123");

        // Assert
        Assert.Equal("goal-123", exception.GoalId);
    }

    [Fact]
    public void GoalNotFoundException_WithSearchPath_SetsSearchPath()
    {
        // Arrange & Act
        var exception = new GoalNotFoundException("Goal not found", "goal-123", "/search/path");

        // Assert
        Assert.Equal("goal-123", exception.GoalId);
        Assert.Equal("/search/path", exception.SearchPath);
    }
}
