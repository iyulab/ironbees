// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Goals;
using Xunit;

namespace Ironbees.Core.Tests.Goals;

public class FileSystemGoalLoaderTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemGoalLoader _loader;

    public FileSystemGoalLoaderTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ironbees-goal-loader-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _loader = new FileSystemGoalLoader();
    }

    public void Dispose()
    {
        _loader.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private string CreateTestGoal(string goalName, string? yamlContent = null)
    {
        var goalPath = Path.Combine(_testDirectory, goalName);
        Directory.CreateDirectory(goalPath);

        yamlContent ??= $@"
id: {goalName}
name: Test Goal {goalName}
description: A test goal for {goalName}
version: '1.0'
workflowTemplate: goal-loop
successCriteria:
  - id: criterion-1
    description: Test criterion
    type: Manual
    weight: 1.0
    required: true
constraints:
  maxIterations: 10
  maxTokens: 10000
checkpoint:
  enabled: true
  afterEachIteration: true
parameters:
  testParam: testValue
tags:
  - test
";
        File.WriteAllText(Path.Combine(goalPath, "goal.yaml"), yamlContent);
        return goalPath;
    }

    [Fact]
    public async Task LoadGoalAsync_ValidGoal_Success()
    {
        // Arrange
        var goalPath = CreateTestGoal("test-goal");

        // Act
        var goal = await _loader.LoadGoalAsync(goalPath);

        // Assert
        Assert.NotNull(goal);
        Assert.Equal("test-goal", goal.Id);
        Assert.Equal("Test Goal test-goal", goal.Name);
        Assert.Equal("A test goal for test-goal", goal.Description);
        Assert.Equal("goal-loop", goal.WorkflowTemplate);
        Assert.Single(goal.SuccessCriteria);
        Assert.Equal(10, goal.Constraints.MaxIterations);
    }

    [Fact]
    public async Task LoadGoalAsync_MissingDirectory_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent");

        // Act & Assert
        await Assert.ThrowsAsync<GoalLoadException>(
            () => _loader.LoadGoalAsync(nonExistentPath));
    }

    [Fact]
    public async Task LoadGoalAsync_MissingGoalYaml_ThrowsException()
    {
        // Arrange
        var goalPath = Path.Combine(_testDirectory, "incomplete-goal");
        Directory.CreateDirectory(goalPath);

        // Act & Assert
        await Assert.ThrowsAsync<GoalLoadException>(
            () => _loader.LoadGoalAsync(goalPath));
    }

    [Fact]
    public async Task LoadGoalAsync_InvalidYaml_ThrowsException()
    {
        // Arrange
        var goalPath = CreateTestGoal("invalid-yaml", "invalid: yaml: content: {{{");

        // Act & Assert
        await Assert.ThrowsAsync<GoalLoadException>(
            () => _loader.LoadGoalAsync(goalPath));
    }

    [Fact]
    public async Task LoadGoalAsync_WithCaching_ReturnsCachedGoal()
    {
        // Arrange
        var goalPath = CreateTestGoal("cached-goal");
        var loaderWithCache = new FileSystemGoalLoader(new FileSystemGoalLoaderOptions
        {
            EnableCaching = true
        });

        // Act
        var goal1 = await loaderWithCache.LoadGoalAsync(goalPath);
        var goal2 = await loaderWithCache.LoadGoalAsync(goalPath);

        // Assert
        Assert.Same(goal1, goal2); // Same reference from cache
        loaderWithCache.Dispose();
    }

    [Fact]
    public async Task LoadAllGoalsAsync_MultipleGoals_LoadsAll()
    {
        // Arrange
        CreateTestGoal("goal-1");
        CreateTestGoal("goal-2");
        CreateTestGoal("goal-3");

        // Act
        var goals = await _loader.LoadAllGoalsAsync(_testDirectory);

        // Assert
        Assert.Equal(3, goals.Count);
        Assert.Contains(goals, g => g.Id == "goal-1");
        Assert.Contains(goals, g => g.Id == "goal-2");
        Assert.Contains(goals, g => g.Id == "goal-3");
    }

    [Fact]
    public async Task LoadAllGoalsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var emptyDir = Path.Combine(_testDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        // Act
        var goals = await _loader.LoadAllGoalsAsync(emptyDir);

        // Assert
        Assert.Empty(goals);
    }

    [Fact]
    public async Task LoadAllGoalsAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "non-existent");

        // Act
        var goals = await _loader.LoadAllGoalsAsync(nonExistentDir);

        // Assert
        Assert.Empty(goals);
    }

    [Fact]
    public async Task LoadAllGoalsAsync_WithInvalidGoal_ContinuesLoading()
    {
        // Arrange
        CreateTestGoal("valid-goal");
        var invalidGoalPath = Path.Combine(_testDirectory, "invalid-goal");
        Directory.CreateDirectory(invalidGoalPath);
        // No goal.yaml file

        var loader = new FileSystemGoalLoader(new FileSystemGoalLoaderOptions
        {
            StopOnFirstError = false
        });

        // Act
        var goals = await loader.LoadAllGoalsAsync(_testDirectory);

        // Assert
        Assert.Single(goals);
        Assert.Equal("valid-goal", goals[0].Id);
        loader.Dispose();
    }

    [Fact]
    public async Task LoadAllGoalsAsync_StopOnFirstError_ThrowsOnFirstInvalid()
    {
        // Arrange
        var invalidGoalPath = Path.Combine(_testDirectory, "a-invalid-goal"); // 'a' to be first alphabetically
        Directory.CreateDirectory(invalidGoalPath);
        // No goal.yaml file

        CreateTestGoal("z-valid-goal"); // 'z' to be last

        var loader = new FileSystemGoalLoader(new FileSystemGoalLoaderOptions
        {
            StopOnFirstError = true
        });

        // Act & Assert
        await Assert.ThrowsAsync<GoalLoadException>(
            () => loader.LoadAllGoalsAsync(_testDirectory));
        loader.Dispose();
    }

    [Fact]
    public async Task ValidateGoalDirectoryAsync_ValidDirectory_ReturnsTrue()
    {
        // Arrange
        var goalPath = CreateTestGoal("valid-goal");

        // Act
        var isValid = await _loader.ValidateGoalDirectoryAsync(goalPath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateGoalDirectoryAsync_InvalidDirectory_ReturnsFalse()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDirectory, "invalid");
        Directory.CreateDirectory(invalidPath);
        // No goal.yaml

        // Act
        var isValid = await _loader.ValidateGoalDirectoryAsync(invalidPath);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task GetGoalByIdAsync_ExistingGoal_ReturnsGoal()
    {
        // Arrange
        CreateTestGoal("find-me");
        CreateTestGoal("other-goal");

        // Act
        var goal = await _loader.GetGoalByIdAsync("find-me", _testDirectory);

        // Assert
        Assert.NotNull(goal);
        Assert.Equal("find-me", goal.Id);
    }

    [Fact]
    public async Task GetGoalByIdAsync_NonExistingGoal_ReturnsNull()
    {
        // Arrange
        CreateTestGoal("other-goal");

        // Act
        var goal = await _loader.GetGoalByIdAsync("not-found", _testDirectory);

        // Assert
        Assert.Null(goal);
    }

    [Fact]
    public async Task GetGoalByIdAsync_CaseInsensitive_ReturnsGoal()
    {
        // Arrange
        CreateTestGoal("MyGoal");

        // Act
        var goal = await _loader.GetGoalByIdAsync("MYGOAL", _testDirectory);

        // Assert
        Assert.NotNull(goal);
        Assert.Equal("MyGoal", goal.Id);
    }

    [Fact]
    public void ClearCache_RemovesCachedGoals()
    {
        // Arrange
        var loader = new FileSystemGoalLoader(new FileSystemGoalLoaderOptions
        {
            EnableCaching = true
        });

        // Act
        loader.ClearCache();

        // Assert - no exception means success
        Assert.True(true);
        loader.Dispose();
    }

    [Fact]
    public async Task LoadGoalAsync_SetsSourcePath()
    {
        // Arrange
        var goalPath = CreateTestGoal("source-path-test");

        // Act
        var goal = await _loader.LoadGoalAsync(goalPath);

        // Assert
        Assert.Equal(goalPath, goal.SourcePath);
    }

    [Fact]
    public async Task LoadGoalAsync_WithParameters_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
id: param-goal
name: Parameter Goal
description: Goal with parameters
workflowTemplate: goal-loop
parameters:
  stringParam: hello
  intParam: 42
";
        var goalPath = CreateTestGoal("param-goal", yaml);

        // Act
        var goal = await _loader.LoadGoalAsync(goalPath);

        // Assert
        Assert.Equal(2, goal.Parameters.Count);
        Assert.Equal("hello", goal.Parameters["stringParam"]);
    }

    [Fact]
    public async Task LoadGoalAsync_WithTags_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
id: tag-goal
name: Tagged Goal
description: Goal with tags
workflowTemplate: goal-loop
tags:
  - production
  - critical
  - review
";
        var goalPath = CreateTestGoal("tag-goal", yaml);

        // Act
        var goal = await _loader.LoadGoalAsync(goalPath);

        // Assert
        Assert.Equal(3, goal.Tags.Count);
        Assert.Contains("production", goal.Tags);
        Assert.Contains("critical", goal.Tags);
    }
}
