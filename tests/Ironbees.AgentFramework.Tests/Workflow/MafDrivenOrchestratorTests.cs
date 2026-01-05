using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Workflow;
using Ironbees.AgentMode.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// Unit tests for MafDrivenOrchestrator.
/// Tests verify that the orchestrator correctly translates MAF execution events
/// to Ironbees WorkflowRuntimeState.
/// </summary>
public class MafDrivenOrchestratorTests
{
    private readonly Mock<IWorkflowLoader> _mockWorkflowLoader;
    private readonly Mock<IMafWorkflowExecutor> _mockMafExecutor;
    private readonly Mock<ILogger<MafDrivenOrchestrator>> _mockLogger;
    private readonly Func<string, CancellationToken, Task<AIAgent>> _agentResolver;

    public MafDrivenOrchestratorTests()
    {
        _mockWorkflowLoader = new Mock<IWorkflowLoader>();
        _mockMafExecutor = new Mock<IMafWorkflowExecutor>();
        _mockLogger = new Mock<ILogger<MafDrivenOrchestrator>>();
        _agentResolver = (name, _) => Task.FromResult<AIAgent>(null!);
    }

    private MafDrivenOrchestrator CreateOrchestrator()
    {
        return new MafDrivenOrchestrator(
            _mockWorkflowLoader.Object,
            _mockMafExecutor.Object,
            _agentResolver,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        var orchestrator = CreateOrchestrator();

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Constructor_WithNullWorkflowLoader_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MafDrivenOrchestrator(
            null!,
            _mockMafExecutor.Object,
            _agentResolver,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullMafExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MafDrivenOrchestrator(
            _mockWorkflowLoader.Object,
            null!,
            _agentResolver,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullAgentResolver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MafDrivenOrchestrator(
            _mockWorkflowLoader.Object,
            _mockMafExecutor.Object,
            null!,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Succeeds()
    {
        // Act - logger is optional
        var orchestrator = new MafDrivenOrchestrator(
            _mockWorkflowLoader.Object,
            _mockMafExecutor.Object,
            _agentResolver,
            null);

        // Assert
        Assert.NotNull(orchestrator);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithNullWorkflow_ThrowsArgumentNullException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in orchestrator.ExecuteAsync(null!, "input")) { }
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in orchestrator.ExecuteAsync(workflow, null!)) { }
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in orchestrator.ExecuteAsync(workflow, "")) { }
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithValidationFailure_YieldsFailedState()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        _mockWorkflowLoader.Setup(l => l.Validate(workflow))
            .Returns(new WorkflowValidationResult
            {
                // IsValid is computed from Errors.Count == 0
                Errors = [new WorkflowValidationError("TEST001", "Validation failed", "TestState")]
            });

        // Act
        var states = new List<WorkflowRuntimeState>();
        await foreach (var state in orchestrator.ExecuteAsync(workflow, "test input"))
        {
            states.Add(state);
        }

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Failed, states[0].Status);
        Assert.Equal("VALIDATION_ERROR", states[0].CurrentStateId);
        Assert.Contains("Validation failed", states[0].ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithWorkflowStartedEvent_YieldsRunningState()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.WorkflowStarted,
                Content = "Starting workflow"
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Running, states[0].Status);
        Assert.Equal("START", states[0].CurrentStateId);
    }

    [Fact]
    public async Task ExecuteAsync_WithAgentStartedEvent_YieldsRunningStateWithAgentName()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.AgentStarted,
                AgentName = "test-agent"
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Running, states[0].Status);
        Assert.Equal("test-agent", states[0].CurrentStateId);
    }

    [Fact]
    public async Task ExecuteAsync_WithAgentMessageEvent_YieldsStateWithOutputData()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.AgentMessage,
                AgentName = "test-agent",
                Content = "Agent message content"
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Running, states[0].Status);
        Assert.NotNull(states[0].OutputData);
        Assert.True(states[0].OutputData.ContainsKey("message"));
        Assert.Equal("Agent message content", states[0].OutputData["message"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithAgentCompletedEvent_YieldsStateWithResult()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.AgentCompleted,
                AgentName = "test-agent",
                Content = "Agent result"
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Running, states[0].Status);
        Assert.NotNull(states[0].OutputData);
        Assert.True(states[0].OutputData.ContainsKey("result"));
        Assert.Equal("Agent result", states[0].OutputData["result"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuperStepCompletedEvent_YieldsStateWithCheckpoint()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();
        var checkpointData = new { id = "checkpoint-1" };

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.SuperStepCompleted,
                Checkpoint = checkpointData
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Running, states[0].Status);
        Assert.NotNull(states[0].OutputData);
        Assert.True(states[0].OutputData.ContainsKey("checkpoint"));
    }

    [Fact]
    public async Task ExecuteAsync_WithWorkflowCompletedEvent_YieldsCompletedState()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.WorkflowCompleted,
                Content = "Final result"
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Completed, states[0].Status);
        Assert.Equal("END", states[0].CurrentStateId);
        Assert.NotNull(states[0].CompletedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithErrorEvent_YieldsFailedState()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = "Error occurred"
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Single(states);
        Assert.Equal(WorkflowExecutionStatus.Failed, states[0].Status);
        Assert.Equal("Error occurred", states[0].ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleEvents_YieldsMultipleStates()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.WorkflowStarted,
                Content = "Starting"
            },
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.AgentStarted,
                AgentName = "agent1"
            },
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.AgentCompleted,
                AgentName = "agent1",
                Content = "Done"
            },
            new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.WorkflowCompleted,
                Content = "Final"
            }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Equal(4, states.Count);
        Assert.Equal(WorkflowExecutionStatus.Running, states[0].Status);
        Assert.Equal(WorkflowExecutionStatus.Running, states[1].Status);
        Assert.Equal(WorkflowExecutionStatus.Running, states[2].Status);
        Assert.Equal(WorkflowExecutionStatus.Completed, states[3].Status);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExecutionIdAcrossStates()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent { Type = WorkflowExecutionEventType.WorkflowStarted },
            new WorkflowExecutionEvent { Type = WorkflowExecutionEventType.AgentStarted, AgentName = "agent1" },
            new WorkflowExecutionEvent { Type = WorkflowExecutionEventType.WorkflowCompleted }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.Equal(3, states.Count);
        var executionId = states[0].ExecutionId;
        Assert.All(states, s => Assert.Equal(executionId, s.ExecutionId));
    }

    [Fact]
    public async Task ExecuteAsync_PreservesWorkflowNameAcrossStates()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflowWithName("TestWorkflow");

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent { Type = WorkflowExecutionEventType.WorkflowStarted },
            new WorkflowExecutionEvent { Type = WorkflowExecutionEventType.WorkflowCompleted }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, "test input");

        // Assert
        Assert.All(states, s => Assert.Equal("TestWorkflow", s.WorkflowName));
    }

    [Fact]
    public async Task ExecuteAsync_PreservesInputAcrossStates()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var workflow = CreateSimpleWorkflow();
        var input = "Test input value";

        SetupValidWorkflow(workflow);
        SetupMafExecutorReturnsEvents(workflow, [
            new WorkflowExecutionEvent { Type = WorkflowExecutionEventType.WorkflowStarted },
            new WorkflowExecutionEvent { Type = WorkflowExecutionEventType.WorkflowCompleted }
        ]);

        // Act
        var states = await CollectStatesAsync(orchestrator, workflow, input);

        // Assert
        Assert.All(states, s => Assert.Equal(input, s.Input));
    }

    #endregion

    #region ResumeFromCheckpointAsync Tests

    [Fact]
    public async Task ResumeFromCheckpointAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("checkpoint-1")) { }
        });
    }

    #endregion

    #region ApproveAsync Tests

    [Fact]
    public async Task ApproveAsync_CompletesWithoutException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var decision = new ApprovalDecision { Approved = true };

        // Act & Assert - should not throw
        await orchestrator.ApproveAsync("execution-1", decision);
    }

    #endregion

    #region CancelAsync Tests

    [Fact]
    public async Task CancelAsync_CompletesWithoutException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act & Assert - should not throw
        await orchestrator.CancelAsync("execution-1");
    }

    #endregion

    #region GetStateAsync Tests

    [Fact]
    public async Task GetStateAsync_ReturnsDefaultState()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var executionId = "execution-1";

        // Act
        var state = await orchestrator.GetStateAsync(executionId);

        // Assert
        Assert.NotNull(state);
        Assert.Equal(executionId, state.ExecutionId);
        Assert.Equal(WorkflowExecutionStatus.Running, state.Status);
        Assert.Equal("UNAVAILABLE", state.CurrentStateId);
    }

    #endregion

    #region ListActiveExecutionsAsync Tests

    [Fact]
    public async Task ListActiveExecutionsAsync_ReturnsEmptyList()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act
        var executions = await orchestrator.ListActiveExecutionsAsync();

        // Assert
        Assert.NotNull(executions);
        Assert.Empty(executions);
    }

    #endregion

    #region Helper Methods

    private static WorkflowDefinition CreateSimpleWorkflow()
    {
        return CreateSimpleWorkflowWithName("TestWorkflow");
    }

    private static WorkflowDefinition CreateSimpleWorkflowWithName(string name)
    {
        return new WorkflowDefinition
        {
            Name = name,
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
    }

    private void SetupValidWorkflow(WorkflowDefinition workflow)
    {
        // IsValid is computed from Errors.Count == 0, so empty Errors means valid
        _mockWorkflowLoader.Setup(l => l.Validate(workflow))
            .Returns(new WorkflowValidationResult { Errors = [] });
    }

    private void SetupMafExecutorReturnsEvents(WorkflowDefinition workflow, List<WorkflowExecutionEvent> events)
    {
        _mockMafExecutor
            .Setup(e => e.ExecuteAsync(
                workflow,
                It.IsAny<string>(),
                It.IsAny<Func<string, CancellationToken, Task<AIAgent>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(events.ToAsyncEnumerable());
    }

    private static async Task<List<WorkflowRuntimeState>> CollectStatesAsync(
        MafDrivenOrchestrator orchestrator,
        WorkflowDefinition workflow,
        string input)
    {
        var states = new List<WorkflowRuntimeState>();
        await foreach (var state in orchestrator.ExecuteAsync(workflow, input))
        {
            states.Add(state);
        }
        return states;
    }

    #endregion
}
