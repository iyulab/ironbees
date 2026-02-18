using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Ironbees.AgentMode.Exceptions;
using Ironbees.AgentMode.Models;
using Ironbees.AgentMode.Workflow.Triggers;
using Ironbees.Core.Orchestration;

namespace Ironbees.AgentMode.Workflow;

/// <summary>
/// Orchestrates workflow execution driven by YAML definitions.
/// This is Ironbees' value-add: translating YAML configuration to
/// MS Agent Framework execution while maintaining filesystem conventions.
/// </summary>
/// <remarks>
/// Design Decision: This orchestrator focuses on:
/// - Loading and validating YAML workflow definitions
/// - Resolving agent references from filesystem conventions
/// - Evaluating custom triggers (file_exists, dir_not_empty)
/// - Delegating actual agent execution to MS Agent Framework
/// - Checkpoint persistence and resumption via ICheckpointStore
/// </remarks>
public sealed class YamlDrivenOrchestrator : IWorkflowOrchestrator<WorkflowRuntimeState>
{
    private readonly IWorkflowLoader _workflowLoader;
    private readonly ITriggerEvaluatorFactory _triggerEvaluatorFactory;
    private readonly IAgentExecutorFactory _agentExecutorFactory;
    private readonly ICheckpointStore? _checkpointStore;

    private readonly ConcurrentDictionary<string, WorkflowExecution> _executions = new();

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public YamlDrivenOrchestrator(
        IWorkflowLoader workflowLoader,
        ITriggerEvaluatorFactory triggerEvaluatorFactory,
        IAgentExecutorFactory agentExecutorFactory,
        ICheckpointStore? checkpointStore = null)
    {
        _workflowLoader = workflowLoader ?? throw new ArgumentNullException(nameof(workflowLoader));
        _triggerEvaluatorFactory = triggerEvaluatorFactory ?? throw new ArgumentNullException(nameof(triggerEvaluatorFactory));
        _agentExecutorFactory = agentExecutorFactory ?? throw new ArgumentNullException(nameof(agentExecutorFactory));
        _checkpointStore = checkpointStore;
    }

    public async IAsyncEnumerable<WorkflowRuntimeState> ExecuteAsync(
        WorkflowDefinition workflow,
        string input,
        WorkflowExecutionContext? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Validate workflow
        var validation = _workflowLoader.Validate(workflow);
        if (!validation.IsValid)
        {
            throw new OrchestratorException(
                $"Invalid workflow: {string.Join(", ", validation.Errors.Select(e => e.Message))}",
                null,
                workflow.Name);
        }

        // Initialize execution
        var execution = new WorkflowExecution
        {
            ExecutionId = Guid.NewGuid().ToString(),
            Workflow = workflow,
            Input = input,
            Context = context ?? new WorkflowExecutionContext
            {
                WorkingDirectory = Directory.GetCurrentDirectory()
            },
            StartedAt = DateTimeOffset.UtcNow
        };

        _executions[execution.ExecutionId] = execution;

        // Create initial state
        var state = new WorkflowRuntimeState
        {
            ExecutionId = execution.ExecutionId,
            WorkflowName = workflow.Name,
            CurrentStateId = FindStartState(workflow)?.Id ?? workflow.States.First().Id,
            Status = WorkflowExecutionStatus.Running,
            Input = input,
            StartedAt = execution.StartedAt,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        yield return state;

        // Execute state machine and yield all subsequent states
        await foreach (var nextState in RunStateMachineAsync(
            workflow, execution, state, cancellationToken))
        {
            yield return nextState;
        }
    }

    /// <summary>
    /// Resumes a workflow execution from a previously saved checkpoint.
    /// The <paramref name="checkpointId"/> is treated as the execution ID;
    /// the latest checkpoint for that execution is loaded.
    /// </summary>
    public async IAsyncEnumerable<WorkflowRuntimeState> ResumeFromCheckpointAsync(
        string checkpointId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);

        if (_checkpointStore == null)
        {
            throw new InvalidOperationException(
                "Cannot resume from checkpoint: no ICheckpointStore was configured. " +
                "Provide an ICheckpointStore in the constructor to enable checkpoint resumption.");
        }

        // Load the latest checkpoint for this execution
        var checkpoint = await _checkpointStore.LoadCheckpointAsync(
            checkpointId, null, cancellationToken);
        if (checkpoint == null)
        {
            throw new InvalidOperationException(
                $"No checkpoint found for execution '{checkpointId}'.");
        }

        if (string.IsNullOrWhiteSpace(checkpoint.SerializedState))
        {
            throw new InvalidOperationException(
                $"Checkpoint for execution '{checkpointId}' does not contain serialized state.");
        }

        // Deserialize the resume context
        var resumeContext = JsonSerializer.Deserialize<WorkflowResumeContext>(
            checkpoint.SerializedState, s_jsonOptions);
        if (resumeContext?.Workflow == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize checkpoint state for execution '{checkpointId}'.");
        }

        var workflow = resumeContext.Workflow;

        // Reconstruct execution
        var execution = new WorkflowExecution
        {
            ExecutionId = resumeContext.ExecutionId,
            Workflow = workflow,
            Input = resumeContext.Input,
            Context = new WorkflowExecutionContext
            {
                WorkingDirectory = resumeContext.WorkingDirectory ?? Directory.GetCurrentDirectory()
            },
            StartedAt = resumeContext.StartedAt
        };

        _executions[execution.ExecutionId] = execution;

        // Reconstruct state from checkpoint
        var state = new WorkflowRuntimeState
        {
            ExecutionId = resumeContext.ExecutionId,
            WorkflowName = workflow.Name,
            CurrentStateId = resumeContext.CurrentStateId,
            Status = WorkflowExecutionStatus.Running,
            Input = resumeContext.Input,
            StartedAt = resumeContext.StartedAt,
            IterationCount = resumeContext.IterationCount,
            OutputData = resumeContext.OutputData ?? new Dictionary<string, object>(),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        yield return state;

        // Continue state machine from checkpointed state
        await foreach (var nextState in RunStateMachineAsync(
            workflow, execution, state, cancellationToken))
        {
            yield return nextState;
        }
    }

    public Task ApproveAsync(string executionId, ApprovalDecision decision)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
        {
            throw new StateNotFoundException(executionId);
        }

        if (execution.CurrentState?.Status != WorkflowExecutionStatus.WaitingForApproval)
        {
            throw new InvalidStateException(
                executionId,
                execution.CurrentState?.CurrentStateId ?? "unknown",
                "Execution is not waiting for approval");
        }

        execution.ApprovalDecision = decision;
        execution.ApprovalGate?.TrySetResult(decision);

        return Task.CompletedTask;
    }

    public Task CancelAsync(string executionId)
    {
        if (!_executions.TryRemove(executionId, out var execution))
        {
            throw new StateNotFoundException(executionId);
        }

        execution.CancellationSource?.Cancel();
        execution.ApprovalGate?.TrySetCanceled();

        return Task.CompletedTask;
    }

    public Task<WorkflowRuntimeState> GetStateAsync(string executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
        {
            throw new StateNotFoundException(executionId);
        }

        return Task.FromResult(execution.CurrentState ?? new WorkflowRuntimeState
        {
            ExecutionId = executionId,
            WorkflowName = execution.Workflow.Name,
            CurrentStateId = "unknown",
            Status = WorkflowExecutionStatus.Running,
            Input = execution.Input,
            StartedAt = execution.StartedAt,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<IReadOnlyList<WorkflowExecutionSummary>> ListActiveExecutionsAsync()
    {
        var summaries = _executions.Values
            .Select(e => new WorkflowExecutionSummary
            {
                ExecutionId = e.ExecutionId,
                WorkflowName = e.Workflow.Name,
                CurrentState = e.CurrentState?.CurrentStateId ?? "unknown",
                Status = e.CurrentState?.Status ?? WorkflowExecutionStatus.Running,
                StartedAt = e.StartedAt,
                LastUpdatedAt = e.CurrentState?.LastUpdatedAt ?? e.StartedAt
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowExecutionSummary>>(summaries);
    }

    #region State Machine

    /// <summary>
    /// Core state machine loop shared by ExecuteAsync and ResumeFromCheckpointAsync.
    /// Executes workflow states, saves checkpoints, and handles terminal transitions.
    /// </summary>
    private async IAsyncEnumerable<WorkflowRuntimeState> RunStateMachineAsync(
        WorkflowDefinition workflow,
        WorkflowExecution execution,
        WorkflowRuntimeState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!IsTerminalState(workflow, state.CurrentStateId)
            && state.Status == WorkflowExecutionStatus.Running
            && !cancellationToken.IsCancellationRequested)
        {
            var currentStateDef = workflow.States.FirstOrDefault(s => s.Id == state.CurrentStateId);
            if (currentStateDef == null)
            {
                state = state with
                {
                    Status = WorkflowExecutionStatus.Failed,
                    ErrorMessage = $"State not found: {state.CurrentStateId}",
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };
                yield return state;
                break;
            }

            // Evaluate trigger if present
            if (currentStateDef.Trigger != null)
            {
                var triggerSatisfied = await EvaluateTriggerAsync(
                    currentStateDef.Trigger,
                    execution.Context,
                    cancellationToken);

                if (!triggerSatisfied)
                {
                    state = state with
                    {
                        Status = WorkflowExecutionStatus.WaitingForTrigger,
                        LastUpdatedAt = DateTimeOffset.UtcNow
                    };
                    yield return state;

                    // Wait and retry trigger evaluation
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }
            }

            // Execute state based on type
            WorkflowRuntimeState? newState = null;
            try
            {
                newState = currentStateDef.Type switch
                {
                    WorkflowStateType.Start => await ExecuteStartStateAsync(state, currentStateDef, cancellationToken),
                    WorkflowStateType.Agent => await ExecuteAgentStateAsync(state, currentStateDef, execution, cancellationToken),
                    WorkflowStateType.Parallel => await ExecuteParallelStateAsync(state, currentStateDef, execution, cancellationToken),
                    WorkflowStateType.HumanGate => await ExecuteHumanGateAsync(state, currentStateDef, execution, cancellationToken),
                    WorkflowStateType.Escalation => await ExecuteEscalationAsync(state, currentStateDef, cancellationToken),
                    WorkflowStateType.Terminal => state with { Status = WorkflowExecutionStatus.Completed },
                    _ => throw new OrchestratorException($"Unknown state type: {currentStateDef.Type}", execution.ExecutionId, currentStateDef.Id)
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                newState = state with
                {
                    Status = WorkflowExecutionStatus.Failed,
                    ErrorMessage = ex.Message,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (newState != null)
            {
                state = newState;
                execution.CurrentState = state;
                yield return state;

                // Save checkpoint after each state transition (when store available)
                if (_checkpointStore != null && state.Status == WorkflowExecutionStatus.Running)
                {
                    await SaveCheckpointAsync(execution, state, workflow, cancellationToken);
                }
            }

            // Determine next state
            if (state.Status == WorkflowExecutionStatus.Running)
            {
                var nextStateId = DetermineNextState(currentStateDef, state);
                if (nextStateId != null)
                {
                    state = state with
                    {
                        CurrentStateId = nextStateId,
                        LastUpdatedAt = DateTimeOffset.UtcNow
                    };
                }
            }
        }

        // Mark as completed if reached terminal
        if (IsTerminalState(workflow, state.CurrentStateId) && state.Status == WorkflowExecutionStatus.Running)
        {
            state = state with
            {
                Status = WorkflowExecutionStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            yield return state;
        }

        // Cleanup
        _executions.TryRemove(execution.ExecutionId, out _);
    }

    #endregion

    #region State Execution Methods

    private static Task<WorkflowRuntimeState> ExecuteStartStateAsync(
        WorkflowRuntimeState state,
        WorkflowStateDefinition stateDef,
        CancellationToken cancellationToken)
    {
        // Start state just transitions to next
        return Task.FromResult(state with
        {
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task<WorkflowRuntimeState> ExecuteAgentStateAsync(
        WorkflowRuntimeState state,
        WorkflowStateDefinition stateDef,
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stateDef.Executor))
        {
            throw new OrchestratorException(
                $"Agent state '{stateDef.Id}' requires an executor.",
                execution.ExecutionId,
                stateDef.Id);
        }

        // Create agent executor from factory
        var executor = await _agentExecutorFactory.CreateExecutorAsync(
            stateDef.Executor,
            execution.Context,
            cancellationToken);

        // Execute agent with current context
        var result = await executor.ExecuteAsync(
            state.Input,
            state.OutputData,
            cancellationToken);

        // Update state with result
        var newOutputData = state.OutputData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        foreach (var (key, value) in result.Data)
        {
            newOutputData[key] = value;
        }

        return state with
        {
            OutputData = newOutputData,
            IterationCount = state.IterationCount + 1,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<WorkflowRuntimeState> ExecuteParallelStateAsync(
        WorkflowRuntimeState state,
        WorkflowStateDefinition stateDef,
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        if (stateDef.Executors.Count == 0)
        {
            throw new OrchestratorException(
                $"Parallel state '{stateDef.Id}' requires executors.",
                execution.ExecutionId,
                stateDef.Id);
        }

        // Execute all agents in parallel
        var tasks = stateDef.Executors.Select(async executorName =>
        {
            var executor = await _agentExecutorFactory.CreateExecutorAsync(
                executorName,
                execution.Context,
                cancellationToken);

            return await executor.ExecuteAsync(
                state.Input,
                state.OutputData,
                cancellationToken);
        });

        var results = await Task.WhenAll(tasks);

        // Merge results
        var newOutputData = state.OutputData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        foreach (var result in results)
        {
            foreach (var (key, value) in result.Data)
            {
                newOutputData[key] = value;
            }
        }

        return state with
        {
            OutputData = newOutputData,
            IterationCount = state.IterationCount + 1,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<WorkflowRuntimeState> ExecuteHumanGateAsync(
        WorkflowRuntimeState state,
        WorkflowStateDefinition stateDef,
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        var settings = stateDef.HumanGate ?? new HumanGateSettings();

        // Update state to waiting
        state = state with
        {
            Status = WorkflowExecutionStatus.WaitingForApproval,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        // Set up approval gate
        execution.ApprovalGate = new TaskCompletionSource<ApprovalDecision>();

        // Wait for approval with timeout
        using var timeoutCts = new CancellationTokenSource(settings.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        try
        {
            var decision = await execution.ApprovalGate.Task.WaitAsync(linkedCts.Token);

            if (decision.Approved)
            {
                return state with
                {
                    Status = WorkflowExecutionStatus.Running,
                    CurrentStateId = settings.OnApprove ?? stateDef.Next ?? state.CurrentStateId,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };
            }
            else
            {
                // Add feedback to output data if provided
                var newOutputData = state.OutputData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                if (!string.IsNullOrWhiteSpace(decision.Feedback))
                {
                    newOutputData["approval_feedback"] = decision.Feedback;
                }

                return state with
                {
                    Status = WorkflowExecutionStatus.Running,
                    CurrentStateId = settings.OnReject ?? state.CurrentStateId,
                    OutputData = newOutputData,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout - escalate or fail
            return state with
            {
                Status = WorkflowExecutionStatus.Failed,
                ErrorMessage = "Approval timeout exceeded",
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private static Task<WorkflowRuntimeState> ExecuteEscalationAsync(
        WorkflowRuntimeState state,
        WorkflowStateDefinition stateDef,
        CancellationToken cancellationToken)
    {
        // Log escalation - actual notification would be implemented via middleware
        return Task.FromResult(state with
        {
            Status = WorkflowExecutionStatus.Failed,
            ErrorMessage = $"Escalation triggered at state '{stateDef.Id}'",
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    #endregion

    #region Checkpoint Methods

    /// <summary>
    /// Saves a checkpoint for the current execution state.
    /// </summary>
    private async Task SaveCheckpointAsync(
        WorkflowExecution execution,
        WorkflowRuntimeState state,
        WorkflowDefinition workflow,
        CancellationToken cancellationToken)
    {
        var resumeContext = new WorkflowResumeContext
        {
            Workflow = workflow,
            CurrentStateId = state.CurrentStateId,
            Input = state.Input,
            ExecutionId = execution.ExecutionId,
            StartedAt = state.StartedAt,
            IterationCount = state.IterationCount,
            OutputData = state.OutputData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            WorkingDirectory = execution.Context.WorkingDirectory
        };

        var checkpoint = new OrchestrationCheckpoint
        {
            CheckpointId = Guid.NewGuid().ToString(),
            OrchestrationId = execution.ExecutionId,
            CurrentState = state.CurrentStateId,
            CurrentAgent = null,
            SerializedState = JsonSerializer.Serialize(resumeContext, s_jsonOptions)
        };

        await _checkpointStore!.SaveCheckpointAsync(
            execution.ExecutionId, checkpoint, cancellationToken);
    }

    #endregion

    #region Helper Methods

    private async Task<bool> EvaluateTriggerAsync(
        TriggerDefinition trigger,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var evaluator = _triggerEvaluatorFactory.GetEvaluator(trigger.Type);
        var evalContext = new TriggerEvaluationContext
        {
            WorkingDirectory = context.WorkingDirectory ?? Directory.GetCurrentDirectory()
        };

        return await evaluator.EvaluateAsync(trigger, evalContext, cancellationToken);
    }

    private static WorkflowStateDefinition? FindStartState(WorkflowDefinition workflow)
    {
        return workflow.States.FirstOrDefault(s => s.Type == WorkflowStateType.Start)
            ?? workflow.States.FirstOrDefault();
    }

    private static bool IsTerminalState(WorkflowDefinition workflow, string stateId)
    {
        var state = workflow.States.FirstOrDefault(s => s.Id == stateId);
        return state?.Type == WorkflowStateType.Terminal ||
               stateId.Equals("END", StringComparison.OrdinalIgnoreCase);
    }

    private static string? DetermineNextState(WorkflowStateDefinition currentState, WorkflowRuntimeState runtimeState)
    {
        // Check conditions first
        if (currentState.Conditions.Count > 0)
        {
            foreach (var condition in currentState.Conditions)
            {
                if (condition.IsDefault)
                {
                    continue; // Default handled last
                }

                if (EvaluateCondition(condition.If, runtimeState))
                {
                    return condition.Then;
                }
            }

            // Check for default condition
            var defaultCondition = currentState.Conditions.FirstOrDefault(c => c.IsDefault);
            if (defaultCondition != null)
            {
                return defaultCondition.Then;
            }
        }

        // Simple next state
        return currentState.Next;
    }

    private static bool EvaluateCondition(string? condition, WorkflowRuntimeState state) =>
        WorkflowExpressionEvaluator.Evaluate(condition, state);

    #endregion

    #region Internal Classes

    private sealed class WorkflowExecution
    {
        public required string ExecutionId { get; init; }
        public required WorkflowDefinition Workflow { get; init; }
        public required string Input { get; init; }
        public required WorkflowExecutionContext Context { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public WorkflowRuntimeState? CurrentState { get; set; }
        public TaskCompletionSource<ApprovalDecision>? ApprovalGate { get; set; }
        public ApprovalDecision? ApprovalDecision { get; set; }
        public CancellationTokenSource? CancellationSource { get; set; }
    }

    /// <summary>
    /// Serializable context for checkpoint resumption.
    /// Contains everything needed to reconstruct and resume a workflow execution.
    /// </summary>
    private sealed class WorkflowResumeContext
    {
        public required WorkflowDefinition Workflow { get; set; }
        public required string CurrentStateId { get; set; }
        public required string Input { get; set; }
        public required string ExecutionId { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public int IterationCount { get; set; }
        public Dictionary<string, object>? OutputData { get; set; }
        public string? WorkingDirectory { get; set; }
    }

    #endregion
}

/// <summary>
/// Runtime state of a workflow execution.
/// </summary>
public sealed record WorkflowRuntimeState
{
    public required string ExecutionId { get; init; }
    public required string WorkflowName { get; init; }
    public required string CurrentStateId { get; init; }
    public required WorkflowExecutionStatus Status { get; init; }
    public required string Input { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset LastUpdatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public int IterationCount { get; init; }
    public IReadOnlyDictionary<string, object> OutputData { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Factory for creating agent executors.
/// </summary>
public interface IAgentExecutorFactory
{
    /// <summary>
    /// Creates an executor for the specified agent.
    /// </summary>
    Task<IAgentExecutor> CreateExecutorAsync(
        string agentName,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes a single agent in a workflow.
/// </summary>
public interface IAgentExecutor
{
    /// <summary>
    /// Executes the agent with given input.
    /// </summary>
    Task<AgentExecutionResult> ExecuteAsync(
        string input,
        IReadOnlyDictionary<string, object> context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of agent execution.
/// </summary>
public sealed record AgentExecutionResult
{
    public bool Success { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, object> Data { get; init; } = new Dictionary<string, object>();
}
