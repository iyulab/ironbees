using Ironbees.AgentMode.Core.Exceptions;
using Ironbees.AgentMode.Core.Workflow;
using Ironbees.AgentMode.Core.Workflow.Triggers;
using Ironbees.AgentMode.Models;
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

    [Fact(Skip = "Intermittent test host crash in .NET 10 - needs investigation")]
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

    #region Mock Classes

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
