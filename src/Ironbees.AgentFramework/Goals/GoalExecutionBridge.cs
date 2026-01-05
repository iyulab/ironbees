// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Goals;
using Ironbees.AgentMode.Workflow;
using Ironbees.Core.Goals;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Ironbees.AgentFramework.Goals;

/// <summary>
/// Bridges goal definitions to MAF workflow execution.
/// Implements the "Thin Wrapper" philosophy: declaration in Ironbees, execution delegated to MAF.
/// </summary>
public sealed class GoalExecutionBridge : IGoalExecutionBridge
{
    private readonly IGoalLoader _goalLoader;
    private readonly IWorkflowTemplateResolver _templateResolver;
    private readonly IMafWorkflowExecutor _workflowExecutor;
    private readonly ICheckpointStore _checkpointStore;
    private readonly Func<string, CancellationToken, Task<AIAgent>> _agentResolver;
    private readonly ILogger<GoalExecutionBridge>? _logger;

    private readonly ConcurrentDictionary<string, ExecutionContext> _activeExecutions = new();
    private readonly ConcurrentDictionary<string, GoalExecutionResult> _completedExecutions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GoalExecutionBridge"/> class.
    /// </summary>
    /// <param name="goalLoader">The goal loader for loading goal definitions.</param>
    /// <param name="templateResolver">The template resolver for resolving workflow templates.</param>
    /// <param name="workflowExecutor">The workflow executor for executing MAF workflows.</param>
    /// <param name="checkpointStore">The checkpoint store for persistence.</param>
    /// <param name="agentResolver">The agent resolver function.</param>
    /// <param name="logger">Optional logger.</param>
    public GoalExecutionBridge(
        IGoalLoader goalLoader,
        IWorkflowTemplateResolver templateResolver,
        IMafWorkflowExecutor workflowExecutor,
        ICheckpointStore checkpointStore,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        ILogger<GoalExecutionBridge>? logger = null)
    {
        _goalLoader = goalLoader ?? throw new ArgumentNullException(nameof(goalLoader));
        _templateResolver = templateResolver ?? throw new ArgumentNullException(nameof(templateResolver));
        _workflowExecutor = workflowExecutor ?? throw new ArgumentNullException(nameof(workflowExecutor));
        _checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
        _agentResolver = agentResolver ?? throw new ArgumentNullException(nameof(agentResolver));
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<GoalExecutionEvent> ExecuteGoalAsync(
        string goalId,
        string input,
        GoalExecutionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        options ??= GoalExecutionOptions.Default;
        var executionId = options.ExecutionId ?? $"goal-exec-{Guid.NewGuid():N}";

        _logger?.LogInformation("Starting goal execution for '{GoalId}' with execution ID '{ExecutionId}'",
            goalId, executionId);

        // Load the goal
        GoalDefinition? goal = null;
        GoalExecutionEvent? loadError = null;

        try
        {
            goal = await _goalLoader.GetGoalByIdAsync(goalId, cancellationToken: cancellationToken);
            if (goal == null)
            {
                loadError = CreateErrorEvent(goalId, executionId, "GOAL_NOT_FOUND",
                    $"Goal '{goalId}' not found in the configured goals directory.");
            }
        }
        catch (GoalNotFoundException ex)
        {
            _logger?.LogWarning(ex, "Goal '{GoalId}' not found", goalId);
            loadError = CreateErrorEvent(goalId, executionId, "GOAL_NOT_FOUND", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load goal '{GoalId}'", goalId);
            loadError = CreateErrorEvent(goalId, executionId, "GOAL_LOAD_FAILED", ex);
        }

        // Handle error outside catch block (CS1631 fix)
        if (loadError != null)
        {
            yield return loadError;
            yield break;
        }

        // Delegate to the goal definition overload
        await foreach (var evt in ExecuteGoalAsync(goal!, input, options with { ExecutionId = executionId }, cancellationToken))
        {
            yield return evt;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<GoalExecutionEvent> ExecuteGoalAsync(
        GoalDefinition goal,
        string input,
        GoalExecutionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        options ??= GoalExecutionOptions.Default;
        var executionId = options.ExecutionId ?? $"goal-exec-{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow;

        // Create execution context
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var context = new ExecutionContext
        {
            GoalId = goal.Id,
            ExecutionId = executionId,
            Status = GoalExecutionStatus.Loading,
            StartedAt = startedAt,
            CancellationTokenSource = cts
        };

        if (!_activeExecutions.TryAdd(executionId, context))
        {
            yield return CreateErrorEvent(goal.Id, executionId, "EXECUTION_EXISTS",
                $"Execution with ID '{executionId}' already exists.");
            yield break;
        }

        try
        {
            // Yield GoalLoaded event
            yield return new GoalExecutionEvent
            {
                Type = GoalExecutionEventType.GoalLoaded,
                GoalId = goal.Id,
                ExecutionId = executionId,
                Content = $"Goal '{goal.Name}' loaded successfully",
                Metadata = new Dictionary<string, object>
                {
                    ["goalName"] = goal.Name,
                    ["workflowTemplate"] = goal.WorkflowTemplate,
                    ["maxIterations"] = goal.Constraints.MaxIterations
                }
            };

            // Resolve workflow template
            context.Status = GoalExecutionStatus.ResolvingWorkflow;
            WorkflowDefinition? workflowDefinition = null;
            GoalExecutionEvent? templateError = null;

            try
            {
                // Merge options parameters with goal parameters
                var parameters = MergeParameters(goal, options);
                workflowDefinition = await _templateResolver.ResolveAsync(
                    goal.WorkflowTemplate,
                    goal,
                    cancellationToken);

                _logger?.LogDebug("Resolved workflow template '{Template}' for goal '{GoalId}'",
                    goal.WorkflowTemplate, goal.Id);
            }
            catch (WorkflowTemplateNotFoundException ex)
            {
                _logger?.LogError(ex, "Workflow template '{Template}' not found for goal '{GoalId}'",
                    goal.WorkflowTemplate, goal.Id);
                templateError = CreateErrorEvent(goal.Id, executionId, "TEMPLATE_NOT_FOUND", ex);
            }
            catch (WorkflowTemplateResolutionException ex)
            {
                _logger?.LogError(ex, "Failed to resolve workflow template for goal '{GoalId}'", goal.Id);
                templateError = CreateErrorEvent(goal.Id, executionId, "TEMPLATE_RESOLUTION_FAILED", ex);
            }

            // Handle error outside catch block (CS1631 fix)
            if (templateError != null)
            {
                yield return templateError;
                yield break;
            }

            yield return new GoalExecutionEvent
            {
                Type = GoalExecutionEventType.WorkflowResolved,
                GoalId = goal.Id,
                ExecutionId = executionId,
                Content = $"Workflow '{workflowDefinition!.Name}' resolved with {workflowDefinition.States.Count} states",
                Metadata = new Dictionary<string, object>
                {
                    ["workflowName"] = workflowDefinition.Name,
                    ["stateCount"] = workflowDefinition.States.Count
                }
            };

            // Execute workflow
            context.Status = GoalExecutionStatus.Running;
            var enableCheckpointing = options.EnableCheckpointing ?? goal.Checkpoint.Enabled;
            var iterationCount = 0;

            if (enableCheckpointing)
            {
                await foreach (var evt in ExecuteWithCheckpointingAsync(
                    goal, workflowDefinition, input, executionId, options, context, cts.Token))
                {
                    if (evt.Type == GoalExecutionEventType.IterationCompleted)
                    {
                        iterationCount++;
                    }
                    yield return evt;
                }
            }
            else
            {
                await foreach (var evt in ExecuteWithoutCheckpointingAsync(
                    goal, workflowDefinition, input, executionId, options, context, cts.Token))
                {
                    if (evt.Type == GoalExecutionEventType.IterationCompleted)
                    {
                        iterationCount++;
                    }
                    yield return evt;
                }
            }

            // Determine final status
            var completedAt = DateTimeOffset.UtcNow;
            var finalStatus = context.Status switch
            {
                GoalExecutionStatus.Cancelled => GoalExecutionStatus.Cancelled,
                GoalExecutionStatus.Failed => GoalExecutionStatus.Failed,
                _ => GoalExecutionStatus.Completed
            };

            // Store result
            var result = new GoalExecutionResult
            {
                GoalId = goal.Id,
                ExecutionId = executionId,
                IsSuccess = finalStatus == GoalExecutionStatus.Completed,
                Status = finalStatus,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                TotalIterations = iterationCount,
                LastCheckpointId = context.LastCheckpointId
            };

            _completedExecutions.TryAdd(executionId, result);

            yield return new GoalExecutionEvent
            {
                Type = finalStatus == GoalExecutionStatus.Completed
                    ? GoalExecutionEventType.GoalCompleted
                    : GoalExecutionEventType.GoalFailed,
                GoalId = goal.Id,
                ExecutionId = executionId,
                Content = finalStatus == GoalExecutionStatus.Completed
                    ? "Goal completed successfully"
                    : "Goal execution failed",
                IterationNumber = iterationCount,
                Metadata = new Dictionary<string, object>
                {
                    ["duration"] = (completedAt - startedAt).TotalMilliseconds,
                    ["iterations"] = iterationCount,
                    ["status"] = finalStatus.ToString()
                }
            };
        }
        finally
        {
            _activeExecutions.TryRemove(executionId, out _);
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<GoalExecutionEvent> ResumeGoalAsync(
        string executionId,
        string? checkpointId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        _logger?.LogInformation("Resuming goal execution '{ExecutionId}' from checkpoint '{CheckpointId}'",
            executionId, checkpointId ?? "latest");

        // Get checkpoint
        CheckpointData? checkpoint;
        if (checkpointId != null)
        {
            checkpoint = await _checkpointStore.GetAsync(checkpointId, cancellationToken);
        }
        else
        {
            checkpoint = await _checkpointStore.GetLatestForExecutionAsync(executionId, cancellationToken);
        }

        if (checkpoint == null)
        {
            yield return CreateErrorEvent("unknown", executionId, "CHECKPOINT_NOT_FOUND",
                $"No checkpoint found for execution '{executionId}'");
            yield break;
        }

        // Load the goal
        var goal = await _goalLoader.GetGoalByIdAsync(checkpoint.WorkflowName, cancellationToken: cancellationToken);
        if (goal == null)
        {
            // Try to load by workflow name match
            _logger?.LogWarning("Could not find goal for checkpoint workflow '{WorkflowName}'",
                checkpoint.WorkflowName);
            yield return CreateErrorEvent("unknown", executionId, "GOAL_NOT_FOUND",
                $"Could not find goal for checkpoint workflow '{checkpoint.WorkflowName}'");
            yield break;
        }

        yield return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.GoalResuming,
            GoalId = goal.Id,
            ExecutionId = executionId,
            CheckpointId = checkpoint.CheckpointId,
            Content = $"Resuming goal from checkpoint '{checkpoint.CheckpointId}'",
            Metadata = new Dictionary<string, object>
            {
                ["checkpointCreatedAt"] = checkpoint.CreatedAt,
                ["originalInput"] = checkpoint.Input ?? ""
            }
        };

        // Resolve template and get MAF workflow
        var workflowDefinition = await _templateResolver.ResolveAsync(goal.WorkflowTemplate, goal, cancellationToken);
        var mafWorkflow = await ConvertToMafWorkflowAsync(workflowDefinition, cancellationToken);

        // Resume from checkpoint
        await foreach (var workflowEvent in _workflowExecutor.ResumeFromCheckpointAsync(
            mafWorkflow, checkpoint, _checkpointStore, cancellationToken))
        {
            yield return MapWorkflowEvent(workflowEvent, goal.Id, executionId);
        }
    }

    /// <inheritdoc />
    public Task<GoalExecutionStatus?> GetExecutionStatusAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        if (_activeExecutions.TryGetValue(executionId, out var context))
        {
            return Task.FromResult<GoalExecutionStatus?>(context.Status);
        }

        if (_completedExecutions.TryGetValue(executionId, out var result))
        {
            return Task.FromResult<GoalExecutionStatus?>(result.Status);
        }

        return Task.FromResult<GoalExecutionStatus?>(null);
    }

    /// <inheritdoc />
    public Task<bool> CancelExecutionAsync(
        string executionId,
        bool saveCheckpoint = true,
        CancellationToken cancellationToken = default)
    {
        if (!_activeExecutions.TryGetValue(executionId, out var context))
        {
            return Task.FromResult(false);
        }

        context.Status = GoalExecutionStatus.Cancelled;
        context.CancellationTokenSource.Cancel();
        _logger?.LogInformation("Cancelled goal execution '{ExecutionId}'", executionId);

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<GoalExecutionResult?> GetExecutionResultAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        _completedExecutions.TryGetValue(executionId, out var result);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GoalCheckpointInfo>> GetCheckpointsAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var checkpoints = await _checkpointStore.GetAllForExecutionAsync(executionId, cancellationToken);

        return checkpoints.Select(cp => new GoalCheckpointInfo
        {
            CheckpointId = cp.CheckpointId,
            ExecutionId = cp.ExecutionId,
            GoalId = cp.WorkflowName,
            CreatedAt = cp.CreatedAt,
            StateId = cp.CurrentStateId
        }).ToList();
    }

    private async IAsyncEnumerable<GoalExecutionEvent> ExecuteWithCheckpointingAsync(
        GoalDefinition goal,
        WorkflowDefinition workflowDefinition,
        string input,
        string executionId,
        GoalExecutionOptions options,
        ExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var workflowEvent in _workflowExecutor.ExecuteWithCheckpointingAsync(
            workflowDefinition, input, executionId, _agentResolver, _checkpointStore, cancellationToken))
        {
            var goalEvent = MapWorkflowEvent(workflowEvent, goal.Id, executionId);

            // Track checkpoint IDs
            if (goalEvent.Type == GoalExecutionEventType.CheckpointSaved && goalEvent.CheckpointId != null)
            {
                context.LastCheckpointId = goalEvent.CheckpointId;
            }

            // Update status on errors
            if (goalEvent.Type == GoalExecutionEventType.GoalFailed)
            {
                context.Status = GoalExecutionStatus.Failed;
            }

            yield return goalEvent;
        }
    }

    private async IAsyncEnumerable<GoalExecutionEvent> ExecuteWithoutCheckpointingAsync(
        GoalDefinition goal,
        WorkflowDefinition workflowDefinition,
        string input,
        string executionId,
        GoalExecutionOptions options,
        ExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var workflowEvent in _workflowExecutor.ExecuteAsync(
            workflowDefinition, input, _agentResolver, cancellationToken))
        {
            var goalEvent = MapWorkflowEvent(workflowEvent, goal.Id, executionId);

            // Update status on errors
            if (goalEvent.Type == GoalExecutionEventType.GoalFailed)
            {
                context.Status = GoalExecutionStatus.Failed;
            }

            yield return goalEvent;
        }
    }

    private GoalExecutionEvent MapWorkflowEvent(WorkflowExecutionEvent workflowEvent, string goalId, string executionId)
    {
        var eventType = workflowEvent.Type switch
        {
            WorkflowExecutionEventType.WorkflowStarted => GoalExecutionEventType.WorkflowProgress,
            WorkflowExecutionEventType.AgentStarted => GoalExecutionEventType.WorkflowProgress,
            WorkflowExecutionEventType.AgentMessage => GoalExecutionEventType.AgentMessage,
            WorkflowExecutionEventType.AgentCompleted => GoalExecutionEventType.AgentCompleted,
            WorkflowExecutionEventType.SuperStepCompleted => GoalExecutionEventType.CheckpointSaved,
            WorkflowExecutionEventType.WorkflowCompleted => GoalExecutionEventType.GoalCompleted,
            WorkflowExecutionEventType.Error => GoalExecutionEventType.GoalFailed,
            _ => GoalExecutionEventType.WorkflowProgress
        };

        string? checkpointId = null;
        if (workflowEvent.Type == WorkflowExecutionEventType.SuperStepCompleted &&
            workflowEvent.Metadata?.TryGetValue("checkpointId", out var cpId) == true)
        {
            checkpointId = cpId?.ToString();
        }

        return new GoalExecutionEvent
        {
            Type = eventType,
            GoalId = goalId,
            ExecutionId = executionId,
            Timestamp = workflowEvent.Timestamp,
            Content = workflowEvent.Content,
            AgentName = workflowEvent.AgentName,
            CheckpointId = checkpointId,
            Metadata = workflowEvent.Metadata,
            Error = workflowEvent.Type == WorkflowExecutionEventType.Error
                ? new GoalExecutionError
                {
                    Code = "WORKFLOW_ERROR",
                    Message = workflowEvent.Content ?? "Unknown error",
                    IsRecoverable = true
                }
                : null
        };
    }

    private static GoalExecutionEvent CreateErrorEvent(string goalId, string executionId, string code, string message)
    {
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.GoalFailed,
            GoalId = goalId,
            ExecutionId = executionId,
            Content = message,
            Error = new GoalExecutionError
            {
                Code = code,
                Message = message,
                IsRecoverable = false
            }
        };
    }

    private static GoalExecutionEvent CreateErrorEvent(string goalId, string executionId, string code, Exception ex)
    {
        var error = GoalExecutionError.FromException(ex);
        return new GoalExecutionEvent
        {
            Type = GoalExecutionEventType.GoalFailed,
            GoalId = goalId,
            ExecutionId = executionId,
            Content = ex.Message,
            Error = error with { Code = code }
        };
    }

    private static IDictionary<string, object> MergeParameters(GoalDefinition goal, GoalExecutionOptions options)
    {
        var parameters = new Dictionary<string, object>(goal.Parameters);

        if (options.Parameters != null)
        {
            foreach (var (key, value) in options.Parameters)
            {
                parameters[key] = value;
            }
        }

        return parameters;
    }

    private async Task<Microsoft.Agents.AI.Workflows.Workflow> ConvertToMafWorkflowAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        // The MafWorkflowExecutor internally handles conversion via IWorkflowConverter
        // For resume, we need to get a pre-converted workflow
        // This is a simplification - in a full implementation, we'd use IWorkflowConverter directly
        var converter = new MafWorkflowConverter();
        return await converter.ConvertAsync(definition, _agentResolver, cancellationToken);
    }

    private sealed class ExecutionContext
    {
        public required string GoalId { get; init; }
        public required string ExecutionId { get; init; }
        public GoalExecutionStatus Status { get; set; }
        public required DateTimeOffset StartedAt { get; init; }
        public required CancellationTokenSource CancellationTokenSource { get; init; }
        public string? LastCheckpointId { get; set; }
    }
}
