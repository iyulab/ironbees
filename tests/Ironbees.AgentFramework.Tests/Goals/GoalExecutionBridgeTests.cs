// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.AgentFramework.Goals;
using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Goals;
using Ironbees.AgentMode.Workflow;
using Ironbees.Core.Goals;
using Microsoft.Agents.AI;
using NSubstitute;
using Xunit;

namespace Ironbees.AgentFramework.Tests.Goals;

public class GoalExecutionBridgeTests
{
    private readonly IGoalLoader _mockGoalLoader;
    private readonly IWorkflowTemplateResolver _mockTemplateResolver;
    private readonly IMafWorkflowExecutor _mockWorkflowExecutor;
    private readonly ICheckpointStore _mockCheckpointStore;
    private readonly Func<string, CancellationToken, Task<AIAgent>> _mockAgentResolver;

    public GoalExecutionBridgeTests()
    {
        _mockGoalLoader = Substitute.For<IGoalLoader>();
        _mockTemplateResolver = Substitute.For<IWorkflowTemplateResolver>();
        _mockWorkflowExecutor = Substitute.For<IMafWorkflowExecutor>();
        _mockCheckpointStore = Substitute.For<ICheckpointStore>();
        _mockAgentResolver = (name, ct) => Task.FromResult<AIAgent>(null!);
    }

    private GoalExecutionBridge CreateBridge()
    {
        return new GoalExecutionBridge(
            _mockGoalLoader,
            _mockTemplateResolver,
            _mockWorkflowExecutor,
            _mockCheckpointStore,
            _mockAgentResolver);
    }

    private static GoalDefinition CreateTestGoal(string id = "test-goal") => new()
    {
        Id = id,
        Name = "Test Goal",
        Description = "A test goal for unit testing",
        WorkflowTemplate = "test-template",
        Constraints = new GoalConstraints
        {
            MaxIterations = 5,
            MaxTokens = 10000
        },
        Checkpoint = new CheckpointSettings
        {
            Enabled = true,
            AfterEachIteration = true
        },
        Parameters = new Dictionary<string, object>
        {
            ["executor"] = "test-agent"
        }
    };

    private static WorkflowDefinition CreateTestWorkflowDefinition() => new()
    {
        Name = "test-workflow",
        Version = "1.0",
        States =
        [
            new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
            new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
        ]
    };

    [Fact]
    public void Constructor_WithNullGoalLoader_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GoalExecutionBridge(
            null!,
            _mockTemplateResolver,
            _mockWorkflowExecutor,
            _mockCheckpointStore,
            _mockAgentResolver));
    }

    [Fact]
    public void Constructor_WithNullTemplateResolver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GoalExecutionBridge(
            _mockGoalLoader,
            null!,
            _mockWorkflowExecutor,
            _mockCheckpointStore,
            _mockAgentResolver));
    }

    [Fact]
    public void Constructor_WithNullWorkflowExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GoalExecutionBridge(
            _mockGoalLoader,
            _mockTemplateResolver,
            null!,
            _mockCheckpointStore,
            _mockAgentResolver));
    }

    [Fact]
    public void Constructor_WithNullCheckpointStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GoalExecutionBridge(
            _mockGoalLoader,
            _mockTemplateResolver,
            _mockWorkflowExecutor,
            null!,
            _mockAgentResolver));
    }

    [Fact]
    public void Constructor_WithNullAgentResolver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GoalExecutionBridge(
            _mockGoalLoader,
            _mockTemplateResolver,
            _mockWorkflowExecutor,
            _mockCheckpointStore,
            null!));
    }

    [Fact]
    public async Task ExecuteGoalAsync_WithNullGoalId_ThrowsArgumentException()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act & Assert
        // ArgumentNullException is a subclass of ArgumentException
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in bridge.ExecuteGoalAsync((string)null!, "input"))
            {
            }
        });
    }

    [Fact]
    public async Task ExecuteGoalAsync_WithEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in bridge.ExecuteGoalAsync("goal-id", ""))
            {
            }
        });
    }

    [Fact]
    public async Task ExecuteGoalAsync_GoalNotFound_YieldsErrorEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        _mockGoalLoader.GetGoalByIdAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((GoalDefinition?)null);

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ExecuteGoalAsync("non-existent", "input"))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(GoalExecutionEventType.GoalFailed, events[0].Type);
        Assert.Contains("not found", events[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteGoalAsync_WithGoalDefinition_YieldsGoalLoadedEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal() with
        {
            Checkpoint = new CheckpointSettings { Enabled = false }
        };
        var workflow = CreateTestWorkflowDefinition();

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns(workflow);

        _mockWorkflowExecutor.ExecuteAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<WorkflowExecutionEvent>());

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ExecuteGoalAsync(goal, "test input"))
        {
            events.Add(evt);
        }

        // Assert
        Assert.True(events.Count >= 2);
        Assert.Equal(GoalExecutionEventType.GoalLoaded, events[0].Type);
        Assert.Equal(goal.Id, events[0].GoalId);
        Assert.Contains(goal.Name, events[0].Content);
    }

    [Fact]
    public async Task ExecuteGoalAsync_WithGoalDefinition_YieldsWorkflowResolvedEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal() with
        {
            Checkpoint = new CheckpointSettings { Enabled = false }
        };
        var workflow = CreateTestWorkflowDefinition();

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns(workflow);

        _mockWorkflowExecutor.ExecuteAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<WorkflowExecutionEvent>());

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ExecuteGoalAsync(goal, "test input"))
        {
            events.Add(evt);
        }

        // Assert
        var resolvedEvent = events.FirstOrDefault(e => e.Type == GoalExecutionEventType.WorkflowResolved);
        Assert.NotNull(resolvedEvent);
        Assert.Contains(workflow.Name, resolvedEvent.Content);
    }

    [Fact]
    public async Task ExecuteGoalAsync_TemplateNotFound_YieldsErrorEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal();

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns<WorkflowDefinition>(x => throw new WorkflowTemplateNotFoundException("test-template", ["path1"]));

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ExecuteGoalAsync(goal, "test input"))
        {
            events.Add(evt);
        }

        // Assert
        var errorEvent = events.FirstOrDefault(e => e.Type == GoalExecutionEventType.GoalFailed);
        Assert.NotNull(errorEvent);
        Assert.NotNull(errorEvent.Error);
        Assert.Equal("TEMPLATE_NOT_FOUND", errorEvent.Error.Code);
    }

    [Fact]
    public async Task ExecuteGoalAsync_TemplateResolutionFailed_YieldsErrorEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal();

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns<WorkflowDefinition>(x => throw new WorkflowTemplateResolutionException("test-template", ["missing.param"]));

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ExecuteGoalAsync(goal, "test input"))
        {
            events.Add(evt);
        }

        // Assert
        var errorEvent = events.FirstOrDefault(e => e.Type == GoalExecutionEventType.GoalFailed);
        Assert.NotNull(errorEvent);
        Assert.NotNull(errorEvent.Error);
        Assert.Equal("TEMPLATE_RESOLUTION_FAILED", errorEvent.Error.Code);
    }

    [Fact]
    public async Task ExecuteGoalAsync_WithCheckpointingDisabled_UsesExecuteAsync()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal() with
        {
            Checkpoint = new CheckpointSettings { Enabled = false }
        };
        var workflow = CreateTestWorkflowDefinition();

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns(workflow);

        _mockWorkflowExecutor.ExecuteAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<WorkflowExecutionEvent>());

        // Act
        await foreach (var _ in bridge.ExecuteGoalAsync(goal, "test input"))
        {
        }

        // Assert
        _mockWorkflowExecutor.Received(1).ExecuteAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteGoalAsync_WithCheckpointingEnabled_UsesExecuteWithCheckpointingAsync()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal() with
        {
            Checkpoint = new CheckpointSettings { Enabled = true }
        };
        var workflow = CreateTestWorkflowDefinition();

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns(workflow);

        _mockWorkflowExecutor.ExecuteWithCheckpointingAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<ICheckpointStore>(),
            Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<WorkflowExecutionEvent>());

        // Act
        await foreach (var _ in bridge.ExecuteGoalAsync(goal, "test input"))
        {
        }

        // Assert
        _mockWorkflowExecutor.Received(1).ExecuteWithCheckpointingAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<ICheckpointStore>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteGoalAsync_WorkflowCompletes_YieldsGoalCompletedEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal() with
        {
            Checkpoint = new CheckpointSettings { Enabled = false }
        };
        var workflow = CreateTestWorkflowDefinition();

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns(workflow);

        var workflowEvents = new List<WorkflowExecutionEvent>
        {
            new() { Type = WorkflowExecutionEventType.WorkflowStarted, Content = "Started" },
            new() { Type = WorkflowExecutionEventType.WorkflowCompleted, Content = "Completed" }
        };

        _mockWorkflowExecutor.ExecuteAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>())
            .Returns(workflowEvents.ToAsyncEnumerable());

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ExecuteGoalAsync(goal, "test input"))
        {
            events.Add(evt);
        }

        // Assert
        var completedEvents = events.Where(e => e.Type == GoalExecutionEventType.GoalCompleted).ToList();
        Assert.True(completedEvents.Count >= 1);
    }

    [Fact]
    public async Task GetExecutionStatusAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act
        var status = await bridge.GetExecutionStatusAsync("non-existent");

        // Assert
        Assert.Null(status);
    }

    [Fact]
    public async Task CancelExecutionAsync_NotFound_ReturnsFalse()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act
        var result = await bridge.CancelExecutionAsync("non-existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetExecutionResultAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var bridge = CreateBridge();

        // Act
        var result = await bridge.GetExecutionResultAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCheckpointsAsync_ReturnsCheckpointsFromStore()
    {
        // Arrange
        var bridge = CreateBridge();
        var checkpoints = new List<CheckpointData>
        {
            new()
            {
                CheckpointId = "cp-1",
                ExecutionId = "exec-1",
                WorkflowName = "test-goal",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                CheckpointId = "cp-2",
                ExecutionId = "exec-1",
                WorkflowName = "test-goal",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1)
            }
        };

        _mockCheckpointStore.GetAllForExecutionAsync(
            "exec-1",
            Arg.Any<CancellationToken>())
            .Returns(checkpoints);

        // Act
        var result = await bridge.GetCheckpointsAsync("exec-1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("cp-1", result[0].CheckpointId);
        Assert.Equal("cp-2", result[1].CheckpointId);
    }

    [Fact]
    public async Task ExecuteGoalAsync_WithCustomExecutionId_UsesProvidedId()
    {
        // Arrange
        var bridge = CreateBridge();
        var goal = CreateTestGoal() with
        {
            Checkpoint = new CheckpointSettings { Enabled = false }
        };
        var workflow = CreateTestWorkflowDefinition();
        var customExecutionId = "custom-exec-123";

        _mockTemplateResolver.ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<GoalDefinition>(),
            Arg.Any<CancellationToken>())
            .Returns(workflow);

        _mockWorkflowExecutor.ExecuteAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<string>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<WorkflowExecutionEvent>());

        var options = new GoalExecutionOptions { ExecutionId = customExecutionId };

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ExecuteGoalAsync(goal, "test input", options))
        {
            events.Add(evt);
        }

        // Assert
        Assert.All(events, e => Assert.Equal(customExecutionId, e.ExecutionId));
    }

    [Fact]
    public async Task ResumeGoalAsync_CheckpointNotFound_YieldsErrorEvent()
    {
        // Arrange
        var bridge = CreateBridge();
        _mockCheckpointStore.GetLatestForExecutionAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns((CheckpointData?)null);

        // Act
        var events = new List<GoalExecutionEvent>();
        await foreach (var evt in bridge.ResumeGoalAsync("exec-1"))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Single(events);
        Assert.Equal(GoalExecutionEventType.GoalFailed, events[0].Type);
        Assert.Contains("checkpoint", events[0].Content, StringComparison.OrdinalIgnoreCase);
    }
}
