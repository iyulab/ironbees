using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Workflow;
using Ironbees.AgentMode.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// Unit tests for MafDrivenOrchestrator.
/// Tests verify that the orchestrator correctly translates MAF execution events
/// to Ironbees WorkflowRuntimeState, and supports checkpoint resumption.
/// </summary>
public class MafDrivenOrchestratorTests
{
    private readonly IWorkflowLoader _mockWorkflowLoader;
    private readonly IMafWorkflowExecutor _mockMafExecutor;
    private readonly IWorkflowConverter _mockWorkflowConverter;
    private readonly ICheckpointStore _mockCheckpointStore;
    private readonly ILogger<MafDrivenOrchestrator> _mockLogger;
    private readonly Func<string, CancellationToken, Task<AIAgent>> _agentResolver;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MafDrivenOrchestratorTests()
    {
        _mockWorkflowLoader = Substitute.For<IWorkflowLoader>();
        _mockMafExecutor = Substitute.For<IMafWorkflowExecutor>();
        _mockWorkflowConverter = Substitute.For<IWorkflowConverter>();
        _mockCheckpointStore = Substitute.For<ICheckpointStore>();
        _mockLogger = Substitute.For<ILogger<MafDrivenOrchestrator>>();
        _agentResolver = (name, _) => Task.FromResult<AIAgent>(null!);
    }

    private MafDrivenOrchestrator CreateOrchestrator(ICheckpointStore? checkpointStore = null)
    {
        return new MafDrivenOrchestrator(
            _mockWorkflowLoader,
            _mockMafExecutor,
            _mockWorkflowConverter,
            _agentResolver,
            checkpointStore,
            _mockLogger);
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
    public void Constructor_WithCheckpointStore_Succeeds()
    {
        // Act
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Constructor_WithNullWorkflowLoader_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MafDrivenOrchestrator(
            null!,
            _mockMafExecutor,
            _mockWorkflowConverter,
            _agentResolver,
            logger: _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullMafExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MafDrivenOrchestrator(
            _mockWorkflowLoader,
            null!,
            _mockWorkflowConverter,
            _agentResolver,
            logger: _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullWorkflowConverter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MafDrivenOrchestrator(
            _mockWorkflowLoader,
            _mockMafExecutor,
            null!,
            _agentResolver,
            logger: _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullAgentResolver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MafDrivenOrchestrator(
            _mockWorkflowLoader,
            _mockMafExecutor,
            _mockWorkflowConverter,
            null!,
            logger: _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullLogger_Succeeds()
    {
        // Act - logger is optional
        var orchestrator = new MafDrivenOrchestrator(
            _mockWorkflowLoader,
            _mockMafExecutor,
            _mockWorkflowConverter,
            _agentResolver,
            logger: null);

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

        _mockWorkflowLoader.Validate(workflow)
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
    public async Task ResumeFromCheckpointAsync_WithoutCheckpointStore_ThrowsInvalidOperationException()
    {
        // Arrange - no checkpoint store
        var orchestrator = CreateOrchestrator(checkpointStore: null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("checkpoint-1")) { }
        });
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithNullCheckpointId_ThrowsArgumentException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync(null!)) { }
        });
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithEmptyCheckpointId_ThrowsArgumentException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("")) { }
        });
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithNonExistentCheckpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);
        _mockCheckpointStore.GetAsync("checkpoint-1", Arg.Any<CancellationToken>())
            .Returns((CheckpointData?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("checkpoint-1")) { }
        });
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithMissingMafData_ThrowsInvalidOperationException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);
        var checkpoint = CreateCheckpointData(mafCheckpointJson: null);
        _mockCheckpointStore.GetAsync("cp-1", Arg.Any<CancellationToken>())
            .Returns(checkpoint);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("cp-1")) { }
        });
        Assert.Contains("MAF checkpoint data", ex.Message);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithMissingContextJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);
        var checkpoint = CreateCheckpointData(
            mafCheckpointJson: "{}",
            contextJson: null);
        _mockCheckpointStore.GetAsync("cp-1", Arg.Any<CancellationToken>())
            .Returns(checkpoint);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("cp-1")) { }
        });
        Assert.Contains("workflow definition context", ex.Message);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithValidCheckpoint_DelegatesToExecutor()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);
        var workflow = CreateSimpleWorkflow();
        var workflowContextJson = JsonSerializer.Serialize(workflow, s_jsonOptions);
        var mafWorkflow = CreateMockMafWorkflow();

        var checkpoint = CreateCheckpointData(
            mafCheckpointJson: "{\"state\":\"test\"}",
            contextJson: workflowContextJson,
            executionId: "exec-1",
            workflowName: "TestWorkflow",
            input: "original input");

        _mockCheckpointStore.GetAsync("cp-1", Arg.Any<CancellationToken>())
            .Returns(checkpoint);

        _mockWorkflowConverter.ConvertAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>())
            .Returns(mafWorkflow);

        _mockMafExecutor.ResumeFromCheckpointAsync(
            mafWorkflow,
            checkpoint,
            _mockCheckpointStore,
            Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowExecutionEvent>
            {
                new() { Type = WorkflowExecutionEventType.AgentStarted, AgentName = "resumed-agent" },
                new() { Type = WorkflowExecutionEventType.WorkflowCompleted, Content = "Resumed done" }
            }.ToAsyncEnumerable());

        // Act
        var states = new List<WorkflowRuntimeState>();
        await foreach (var state in orchestrator.ResumeFromCheckpointAsync("cp-1"))
        {
            states.Add(state);
        }

        // Assert
        Assert.Equal(2, states.Count);
        Assert.Equal("exec-1", states[0].ExecutionId);
        Assert.Equal("TestWorkflow", states[0].WorkflowName);
        Assert.Equal(WorkflowExecutionStatus.Running, states[0].Status);
        Assert.Equal("resumed-agent", states[0].CurrentStateId);
        Assert.Equal(WorkflowExecutionStatus.Completed, states[1].Status);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_PreservesOriginalInput()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_mockCheckpointStore);
        var workflow = CreateSimpleWorkflow();
        var workflowContextJson = JsonSerializer.Serialize(workflow, s_jsonOptions);
        var mafWorkflow = CreateMockMafWorkflow();

        var checkpoint = CreateCheckpointData(
            mafCheckpointJson: "{}",
            contextJson: workflowContextJson,
            input: "my original input");

        _mockCheckpointStore.GetAsync("cp-1", Arg.Any<CancellationToken>())
            .Returns(checkpoint);

        _mockWorkflowConverter.ConvertAsync(
            Arg.Any<WorkflowDefinition>(),
            Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
            Arg.Any<CancellationToken>())
            .Returns(mafWorkflow);

        _mockMafExecutor.ResumeFromCheckpointAsync(
            mafWorkflow, checkpoint, _mockCheckpointStore, Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowExecutionEvent>
            {
                new() { Type = WorkflowExecutionEventType.WorkflowCompleted, Content = "Done" }
            }.ToAsyncEnumerable());

        // Act
        var states = new List<WorkflowRuntimeState>();
        await foreach (var state in orchestrator.ResumeFromCheckpointAsync("cp-1"))
        {
            states.Add(state);
        }

        // Assert
        Assert.Single(states);
        Assert.Equal("my original input", states[0].Input);
    }

    #endregion

    #region ApproveAsync Tests

    [Fact]
    public async Task ApproveAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var decision = new ApprovalDecision { Approved = true };

        // Act & Assert - should throw NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            orchestrator.ApproveAsync("execution-1", decision));
    }

    #endregion

    #region CancelAsync Tests

    [Fact]
    public async Task CancelAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act & Assert - should throw NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            orchestrator.CancelAsync("execution-1"));
    }

    #endregion

    #region GetStateAsync Tests

    [Fact]
    public async Task GetStateAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var executionId = "execution-1";

        // Act & Assert - should throw NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            orchestrator.GetStateAsync(executionId));
    }

    #endregion

    #region ListActiveExecutionsAsync Tests

    [Fact]
    public async Task ListActiveExecutionsAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act & Assert - should throw NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            orchestrator.ListActiveExecutionsAsync());
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

    private static CheckpointData CreateCheckpointData(
        string? mafCheckpointJson = "{}",
        string? contextJson = null,
        string executionId = "exec-1",
        string workflowName = "TestWorkflow",
        string? input = "test input",
        string? currentStateId = null)
    {
        return new CheckpointData
        {
            CheckpointId = "cp-1",
            ExecutionId = executionId,
            WorkflowName = workflowName,
            CurrentStateId = currentStateId,
            MafCheckpointJson = mafCheckpointJson,
            Input = input,
            ContextJson = contextJson,
            CreatedAt = DateTimeOffset.UtcNow,
            ExecutionStartedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
    }

    private void SetupValidWorkflow(WorkflowDefinition workflow)
    {
        // IsValid is computed from Errors.Count == 0, so empty Errors means valid
        _mockWorkflowLoader.Validate(workflow)
            .Returns(new WorkflowValidationResult { Errors = [] });
    }

    private void SetupMafExecutorReturnsEvents(WorkflowDefinition workflow, List<WorkflowExecutionEvent> events)
    {
        _mockMafExecutor
            .ExecuteAsync(
                workflow,
                Arg.Any<string>(),
                Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
                Arg.Any<CancellationToken>())
            .Returns(events.ToAsyncEnumerable());
    }

    private static Microsoft.Agents.AI.Workflows.Workflow CreateMockMafWorkflow()
    {
        var mockChatClient = Substitute.For<IChatClient>();
        var agent = new ChatClientAgent(
            mockChatClient,
            instructions: "Test agent",
            name: "test");
        return Microsoft.Agents.AI.Workflows.AgentWorkflowBuilder.BuildSequential(agent);
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
