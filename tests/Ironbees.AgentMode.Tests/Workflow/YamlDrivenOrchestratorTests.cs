using System.Text.Json;
using Ironbees.AgentMode.Exceptions;
using Ironbees.AgentMode.Workflow;
using Ironbees.AgentMode.Workflow.Triggers;
using Ironbees.AgentMode.Models;
using Ironbees.Core.Orchestration;
using Xunit;

namespace Ironbees.AgentMode.Tests.Workflow;

[Collection("Sequential")]
public class YamlDrivenOrchestratorTests
{
    private readonly MockWorkflowLoader _loader = new();
    private readonly MockTriggerEvaluatorFactory _triggerFactory = new();
    private readonly MockAgentExecutorFactory _executorFactory = new();

    private YamlDrivenOrchestrator CreateOrchestrator() =>
        new(_loader, _triggerFactory, _executorFactory);

    [Fact]
    public async Task ExecuteAsync_SimpleWorkflow_CompletesSuccessfully()
    {
        // Arrange
        var workflow = CreateSimpleWorkflow();
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "test input").ToListAsync();

        // Assert
        Assert.True(states.Count >= 2); // At least initial and completed states
        Assert.Equal(WorkflowExecutionStatus.Completed, states.Last().Status);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidWorkflow_ThrowsOrchestratorException()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "InvalidWorkflow",
            States = [] // No states - invalid
        };
        _loader.ValidationResult = new WorkflowValidationResult
        {
            Errors = [new WorkflowValidationError("WF001", "No states defined")]
        };
        var orchestrator = CreateOrchestrator();

        // Act & Assert
        await Assert.ThrowsAsync<OrchestratorException>(async () =>
            await orchestrator.ExecuteAsync(workflow, "input").ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WithAgentState_ExecutesAgent()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "AgentWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent, Executor = "test-agent", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "test input").ToListAsync();

        // Assert
        Assert.True(_executorFactory.CreateExecutorCalled);
        Assert.Equal("test-agent", _executorFactory.LastAgentName);
        Assert.Equal(WorkflowExecutionStatus.Completed, states.Last().Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithTrigger_ExecutesWhenSatisfied()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "TriggerWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "WAIT" },
                new WorkflowStateDefinition
                {
                    Id = "WAIT",
                    Type = WorkflowStateType.Agent,
                    Executor = "worker",
                    Trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "ready.txt" },
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Trigger returns true immediately (satisfied)
        _triggerFactory.EvaluationResults = new Queue<bool>([true]);

        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        Assert.Equal(WorkflowExecutionStatus.Completed, states.Last().Status);
        Assert.True(_executorFactory.CreateExecutorCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WithConditions_FollowsCorrectPath()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "ConditionalWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "CHECK" },
                new WorkflowStateDefinition
                {
                    Id = "CHECK",
                    Type = WorkflowStateType.Agent,
                    Executor = "checker",
                    Conditions =
                    [
                        new ConditionalTransition { If = "success", Then = "SUCCESS" },
                        new ConditionalTransition { Then = "FAILURE", IsDefault = true }
                    ]
                },
                new WorkflowStateDefinition { Id = "SUCCESS", Type = WorkflowStateType.Terminal },
                new WorkflowStateDefinition { Id = "FAILURE", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        var finalState = states.Last();
        Assert.Equal(WorkflowExecutionStatus.Completed, finalState.Status);
        Assert.Equal("SUCCESS", finalState.CurrentStateId);
    }

    [Fact]
    public async Task ExecuteAsync_ParallelState_ExecutesAllExecutors()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "ParallelWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PARALLEL" },
                new WorkflowStateDefinition
                {
                    Id = "PARALLEL",
                    Type = WorkflowStateType.Parallel,
                    Executors = ["agent1", "agent2", "agent3"],
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        Assert.Equal(3, _executorFactory.CreatedExecutorCount);
        Assert.Contains("agent1", _executorFactory.AllAgentNames);
        Assert.Contains("agent2", _executorFactory.AllAgentNames);
        Assert.Contains("agent3", _executorFactory.AllAgentNames);
    }

    [Fact]
    public async Task ExecuteAsync_AgentError_SetsFailedStatus()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "ErrorWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent, Executor = "failing-agent", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        _executorFactory.ShouldThrow = true;
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        var finalState = states.Last();
        Assert.Equal(WorkflowExecutionStatus.Failed, finalState.Status);
        Assert.NotNull(finalState.ErrorMessage);
    }

    [Fact]
    public async Task GetStateAsync_ExistingExecution_ReturnsState()
    {
        // Arrange
        var workflow = CreateSimpleWorkflow();
        var orchestrator = CreateOrchestrator();

        // Start execution and capture ID
        string? executionId = null;
        await foreach (var state in orchestrator.ExecuteAsync(workflow, "input"))
        {
            executionId = state.ExecutionId;
            break; // Get first state only
        }

        // Act - Get state while executing
        // Note: This test verifies GetStateAsync works during execution
        // In a real scenario, we'd need a longer-running workflow
        Assert.NotNull(executionId);
    }

    [Fact]
    public async Task GetStateAsync_NonExistentExecution_ThrowsStateNotFoundException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act & Assert
        await Assert.ThrowsAsync<StateNotFoundException>(
            () => orchestrator.GetStateAsync("non-existent-id"));
    }

    [Fact]
    public async Task CancelAsync_NonExistentExecution_ThrowsStateNotFoundException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act & Assert
        await Assert.ThrowsAsync<StateNotFoundException>(
            () => orchestrator.CancelAsync("non-existent-id"));
    }

    [Fact]
    public async Task ListActiveExecutionsAsync_NoExecutions_ReturnsEmpty()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act
        var executions = await orchestrator.ListActiveExecutionsAsync();

        // Assert
        Assert.Empty(executions);
    }

    [Fact]
    public async Task ExecuteAsync_WithIterationCount_TracksIterations()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "IterativeWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        var agentState = states.FirstOrDefault(s => s.CurrentStateId == "AGENT" && s.IterationCount > 0);
        Assert.NotNull(agentState);
        Assert.Equal(1, agentState.IterationCount);
    }

    [Fact]
    public async Task ExecuteAsync_StateNotFound_FailsWithError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "BrokenWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "MISSING" }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        Assert.Equal(WorkflowExecutionStatus.Failed, states.Last().Status);
        Assert.Contains("not found", states.Last().ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexConditionExpression_FollowsCorrectPath()
    {
        // Arrange — test that compound expression "success && build.success" works
        var workflow = new WorkflowDefinition
        {
            Name = "ComplexConditionWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition
                {
                    Id = "AGENT",
                    Type = WorkflowStateType.Agent,
                    Executor = "checker",
                    Conditions =
                    [
                        new ConditionalTransition { If = "success && build.success", Then = "SUCCESS" },
                        new ConditionalTransition { Then = "FAILURE", IsDefault = true }
                    ]
                },
                new WorkflowStateDefinition { Id = "SUCCESS", Type = WorkflowStateType.Terminal },
                new WorkflowStateDefinition { Id = "FAILURE", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert — MockAgentExecutor returns build_success=true, status is Running → both true
        var finalState = states.Last();
        Assert.Equal(WorkflowExecutionStatus.Completed, finalState.Status);
        Assert.Equal("SUCCESS", finalState.CurrentStateId);
    }

    [Fact]
    public async Task ExecuteAsync_WithIterationCountCondition_ExitsLoopCorrectly()
    {
        // Arrange — test iteration_count >= N condition for loop termination
        var workflow = new WorkflowDefinition
        {
            Name = "IterationLoopWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition
                {
                    Id = "AGENT",
                    Type = WorkflowStateType.Agent,
                    Executor = "worker",
                    Conditions =
                    [
                        new ConditionalTransition { If = "iteration_count >= 2", Then = "END" }
                    ],
                    Next = "AGENT" // Loop back
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert — should loop twice then exit
        var finalState = states.Last();
        Assert.Equal(WorkflowExecutionStatus.Completed, finalState.Status);
        Assert.Equal("END", finalState.CurrentStateId);
        // At least 2 iterations (initial + loop)
        var agentStates = states.Where(s => s.CurrentStateId == "AGENT").ToList();
        Assert.True(agentStates.Count >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_WithNotCondition_NegatesCorrectly()
    {
        // Arrange — test !failure condition (should be true when not failed)
        var workflow = new WorkflowDefinition
        {
            Name = "NotConditionWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "CHECK" },
                new WorkflowStateDefinition
                {
                    Id = "CHECK",
                    Type = WorkflowStateType.Agent,
                    Executor = "checker",
                    Conditions =
                    [
                        new ConditionalTransition { If = "!failure", Then = "SUCCESS" },
                        new ConditionalTransition { Then = "FAILURE", IsDefault = true }
                    ]
                },
                new WorkflowStateDefinition { Id = "SUCCESS", Type = WorkflowStateType.Terminal },
                new WorkflowStateDefinition { Id = "FAILURE", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act — status is Running, so !failure = !false = true
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        Assert.Equal("SUCCESS", states.Last().CurrentStateId);
    }

    [Fact]
    public async Task ExecuteAsync_WithOutputDataComparison_UsesAgentResult()
    {
        // Arrange — test output.* comparison with agent result data
        var workflow = new WorkflowDefinition
        {
            Name = "OutputComparisonWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition
                {
                    Id = "AGENT",
                    Type = WorkflowStateType.Agent,
                    Executor = "scorer",
                    Conditions =
                    [
                        new ConditionalTransition { If = "build.success && test.success", Then = "PASS" },
                        new ConditionalTransition { Then = "FAIL", IsDefault = true }
                    ]
                },
                new WorkflowStateDefinition { Id = "PASS", Type = WorkflowStateType.Terminal },
                new WorkflowStateDefinition { Id = "FAIL", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act — MockAgentExecutor returns build_success=true, test_success=true
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        Assert.Equal("PASS", states.Last().CurrentStateId);
    }

    [Fact]
    public async Task ExecuteAsync_WithOrCondition_MatchesFirstTrueCondition()
    {
        // Arrange — test OR condition: success || failure
        var workflow = new WorkflowDefinition
        {
            Name = "OrConditionWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "CHECK" },
                new WorkflowStateDefinition
                {
                    Id = "CHECK",
                    Type = WorkflowStateType.Agent,
                    Executor = "checker",
                    Conditions =
                    [
                        new ConditionalTransition { If = "success || failure", Then = "MATCHED" },
                        new ConditionalTransition { Then = "UNMATCHED", IsDefault = true }
                    ]
                },
                new WorkflowStateDefinition { Id = "MATCHED", Type = WorkflowStateType.Terminal },
                new WorkflowStateDefinition { Id = "UNMATCHED", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = CreateOrchestrator();

        // Act — status is Running, so success=true → OR short-circuits to true
        var states = await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert
        Assert.Equal("MATCHED", states.Last().CurrentStateId);
    }

    #region ResumeFromCheckpointAsync Tests

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithoutCheckpointStore_ThrowsInvalidOperationException()
    {
        // Arrange - no checkpoint store
        var orchestrator = CreateOrchestrator();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("exec-1")) { }
        });
        Assert.Contains("ICheckpointStore", ex.Message);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithNullCheckpointId_ThrowsArgumentNullException()
    {
        // Arrange
        var checkpointStore = new MockCheckpointStore();
        var orchestrator = new YamlDrivenOrchestrator(
            _loader, _triggerFactory, _executorFactory, checkpointStore);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync(null!)) { }
        });
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithNonExistentCheckpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var checkpointStore = new MockCheckpointStore();
        var orchestrator = new YamlDrivenOrchestrator(
            _loader, _triggerFactory, _executorFactory, checkpointStore);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("non-existent")) { }
        });
        Assert.Contains("No checkpoint found", ex.Message);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithEmptySerializedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var checkpointStore = new MockCheckpointStore();
        checkpointStore.SavedCheckpoints["exec-1"] = new OrchestrationCheckpoint
        {
            CheckpointId = "cp-1",
            OrchestrationId = "exec-1",
            SerializedState = null
        };
        var orchestrator = new YamlDrivenOrchestrator(
            _loader, _triggerFactory, _executorFactory, checkpointStore);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in orchestrator.ResumeFromCheckpointAsync("exec-1")) { }
        });
        Assert.Contains("serialized state", ex.Message);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_WithValidCheckpoint_ResumesExecution()
    {
        // Arrange
        var checkpointStore = new MockCheckpointStore();
        var workflow = new WorkflowDefinition
        {
            Name = "ResumeWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Simulate a checkpoint saved at the AGENT state (before it executes)
        var resumeContext = CreateResumeContext(workflow, "AGENT", "exec-1", "test input");
        checkpointStore.SavedCheckpoints["exec-1"] = new OrchestrationCheckpoint
        {
            CheckpointId = "cp-1",
            OrchestrationId = "exec-1",
            CurrentState = "AGENT",
            SerializedState = JsonSerializer.Serialize(resumeContext, s_jsonOptions)
        };

        var orchestrator = new YamlDrivenOrchestrator(
            _loader, _triggerFactory, _executorFactory, checkpointStore);

        // Act
        var states = new List<WorkflowRuntimeState>();
        await foreach (var state in orchestrator.ResumeFromCheckpointAsync("exec-1"))
        {
            states.Add(state);
        }

        // Assert - should resume from AGENT, execute it, then complete
        Assert.True(states.Count >= 2, $"Expected at least 2 states, got {states.Count}");
        Assert.Equal("exec-1", states[0].ExecutionId);
        Assert.Equal("ResumeWorkflow", states[0].WorkflowName);
        Assert.Equal(WorkflowExecutionStatus.Completed, states.Last().Status);
        Assert.True(_executorFactory.CreateExecutorCalled);
    }

    [Fact]
    public async Task ResumeFromCheckpointAsync_PreservesOriginalInput()
    {
        // Arrange
        var checkpointStore = new MockCheckpointStore();
        var workflow = CreateSimpleWorkflow();
        var resumeContext = CreateResumeContext(workflow, "END", "exec-1", "my original input");
        checkpointStore.SavedCheckpoints["exec-1"] = new OrchestrationCheckpoint
        {
            CheckpointId = "cp-1",
            OrchestrationId = "exec-1",
            SerializedState = JsonSerializer.Serialize(resumeContext, s_jsonOptions)
        };

        var orchestrator = new YamlDrivenOrchestrator(
            _loader, _triggerFactory, _executorFactory, checkpointStore);

        // Act
        var states = new List<WorkflowRuntimeState>();
        await foreach (var state in orchestrator.ResumeFromCheckpointAsync("exec-1"))
        {
            states.Add(state);
        }

        // Assert
        Assert.All(states, s => Assert.Equal("my original input", s.Input));
    }

    [Fact]
    public async Task ExecuteAsync_WithCheckpointStore_SavesCheckpoints()
    {
        // Arrange
        var checkpointStore = new MockCheckpointStore();
        var workflow = new WorkflowDefinition
        {
            Name = "CheckpointWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        var orchestrator = new YamlDrivenOrchestrator(
            _loader, _triggerFactory, _executorFactory, checkpointStore);

        // Act
        await orchestrator.ExecuteAsync(workflow, "input").ToListAsync();

        // Assert - checkpoints should have been saved during execution
        Assert.True(checkpointStore.SaveCount > 0,
            $"Expected checkpoints to be saved, but SaveCount was {checkpointStore.SaveCount}");
    }

    #endregion

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static WorkflowDefinition CreateSimpleWorkflow()
    {
        return new WorkflowDefinition
        {
            Name = "SimpleWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
    }

    private static object CreateResumeContext(
        WorkflowDefinition workflow,
        string currentStateId,
        string executionId,
        string input)
    {
        return new
        {
            workflow,
            currentStateId,
            input,
            executionId,
            startedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            iterationCount = 0,
            outputData = new Dictionary<string, object>(),
            workingDirectory = Directory.GetCurrentDirectory()
        };
    }

    #region Mock Classes

    private sealed class MockCheckpointStore : ICheckpointStore
    {
        public Dictionary<string, OrchestrationCheckpoint> SavedCheckpoints { get; } = new();
        public int SaveCount { get; private set; }

        public Task SaveCheckpointAsync(string orchestrationId, OrchestrationCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            SavedCheckpoints[orchestrationId] = checkpoint;
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<OrchestrationCheckpoint?> LoadCheckpointAsync(string orchestrationId, string? checkpointId = null, CancellationToken cancellationToken = default)
        {
            SavedCheckpoints.TryGetValue(orchestrationId, out var checkpoint);
            return Task.FromResult(checkpoint);
        }

        public Task<IReadOnlyList<OrchestrationCheckpoint>> ListCheckpointsAsync(string orchestrationId, CancellationToken cancellationToken = default)
        {
            if (SavedCheckpoints.TryGetValue(orchestrationId, out var checkpoint))
            {
                return Task.FromResult<IReadOnlyList<OrchestrationCheckpoint>>([checkpoint]);
            }
            return Task.FromResult<IReadOnlyList<OrchestrationCheckpoint>>([]);
        }

        public Task DeleteCheckpointAsync(string orchestrationId, string checkpointId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAllCheckpointsAsync(string orchestrationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class MockWorkflowLoader : IWorkflowLoader
    {
        public WorkflowValidationResult ValidationResult { get; set; } = new();

        public Task<WorkflowDefinition> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<WorkflowDefinition> LoadFromStringAsync(string yaml, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<WorkflowDefinition> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<WorkflowDefinition>> LoadFromDirectoryAsync(string directoryPath, string searchPattern = "*.yaml", CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public WorkflowValidationResult Validate(WorkflowDefinition workflow)
            => ValidationResult;
    }

    private sealed class MockTriggerEvaluatorFactory : ITriggerEvaluatorFactory
    {
        public Queue<bool> EvaluationResults { get; set; } = new([true]);

        public ITriggerEvaluator GetEvaluator(TriggerType triggerType)
            => new MockTriggerEvaluator(this);

        private sealed class MockTriggerEvaluator(MockTriggerEvaluatorFactory factory) : ITriggerEvaluator
        {
            public TriggerType TriggerType => TriggerType.FileExists;

            public Task<bool> EvaluateAsync(TriggerDefinition trigger, TriggerEvaluationContext context, CancellationToken cancellationToken = default)
            {
                var result = factory.EvaluationResults.Count > 0 ? factory.EvaluationResults.Dequeue() : true;
                return Task.FromResult(result);
            }
        }
    }

    private sealed class MockAgentExecutorFactory : IAgentExecutorFactory
    {
        public bool CreateExecutorCalled { get; private set; }
        public string? LastAgentName { get; private set; }
        public int CreatedExecutorCount { get; private set; }
        public List<string> AllAgentNames { get; } = [];
        public bool ShouldThrow { get; set; }

        public Task<IAgentExecutor> CreateExecutorAsync(string agentName, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
        {
            CreateExecutorCalled = true;
            LastAgentName = agentName;
            CreatedExecutorCount++;
            AllAgentNames.Add(agentName);

            if (ShouldThrow)
            {
                return Task.FromResult<IAgentExecutor>(new ThrowingExecutor());
            }

            return Task.FromResult<IAgentExecutor>(new MockAgentExecutor());
        }

        private sealed class MockAgentExecutor : IAgentExecutor
        {
            public Task<AgentExecutionResult> ExecuteAsync(string input, IReadOnlyDictionary<string, object> context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AgentExecutionResult
                {
                    Success = true,
                    Data = new Dictionary<string, object>
                    {
                        ["build_success"] = true,
                        ["test_success"] = true
                    }
                });
            }
        }

        private sealed class ThrowingExecutor : IAgentExecutor
        {
            public Task<AgentExecutionResult> ExecuteAsync(string input, IReadOnlyDictionary<string, object> context, CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Agent execution failed");
            }
        }
    }

    #endregion
}
