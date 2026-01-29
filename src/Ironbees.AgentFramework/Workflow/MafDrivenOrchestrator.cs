using Ironbees.AgentMode.Workflow;
using Ironbees.AgentMode.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Ironbees.AgentFramework.Workflow;

/// <summary>
/// Orchestrates workflow execution using Microsoft Agent Framework.
/// This class bridges Ironbees' YAML-based workflow definitions with MAF's execution engine,
/// providing a complete Thin Wrapper implementation that delegates execution to MAF.
/// </summary>
/// <remarks>
/// Design Decision: This orchestrator focuses on:
/// - Loading YAML workflow definitions (via IWorkflowLoader)
/// - Converting to MAF workflows (via IWorkflowConverter)
/// - Delegating execution to MAF (via IMafWorkflowExecutor)
/// - Translating MAF events back to Ironbees WorkflowRuntimeState
///
/// This class provides MAF-backed execution while maintaining compatibility with
/// the existing IWorkflowOrchestrator interface.
/// </remarks>
public sealed class MafDrivenOrchestrator : IWorkflowOrchestrator<WorkflowRuntimeState>
{
    private readonly IWorkflowLoader _workflowLoader;
    private readonly IMafWorkflowExecutor _mafExecutor;
    private readonly Func<string, CancellationToken, Task<AIAgent>> _agentResolver;
    private readonly ILogger<MafDrivenOrchestrator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MafDrivenOrchestrator"/> class.
    /// </summary>
    /// <param name="workflowLoader">The workflow loader for YAML definitions.</param>
    /// <param name="mafExecutor">The MAF workflow executor.</param>
    /// <param name="agentResolver">Function to resolve agent names to AIAgent instances.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public MafDrivenOrchestrator(
        IWorkflowLoader workflowLoader,
        IMafWorkflowExecutor mafExecutor,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        ILogger<MafDrivenOrchestrator>? logger = null)
    {
        _workflowLoader = workflowLoader ?? throw new ArgumentNullException(nameof(workflowLoader));
        _mafExecutor = mafExecutor ?? throw new ArgumentNullException(nameof(mafExecutor));
        _agentResolver = agentResolver ?? throw new ArgumentNullException(nameof(agentResolver));
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkflowRuntimeState> ExecuteAsync(
        WorkflowDefinition workflow,
        string input,
        WorkflowExecutionContext? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        _logger?.LogInformation("Starting MAF-driven workflow execution for '{WorkflowName}'", workflow.Name);

        // Validate workflow using standard loader validation
        var validation = _workflowLoader.Validate(workflow);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join(", ", validation.Errors.Select(e => e.Message));
            _logger?.LogError("Workflow validation failed: {Errors}", errorMessage);

            yield return new WorkflowRuntimeState
            {
                ExecutionId = Guid.NewGuid().ToString(),
                WorkflowName = workflow.Name,
                CurrentStateId = "VALIDATION_ERROR",
                Status = WorkflowExecutionStatus.Failed,
                ErrorMessage = $"Invalid workflow: {errorMessage}",
                Input = input,
                StartedAt = DateTimeOffset.UtcNow,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            yield break;
        }

        var executionId = Guid.NewGuid().ToString();
        var startTime = DateTimeOffset.UtcNow;
        string? currentAgentName = null;

        // Execute via MAF and translate events to WorkflowRuntimeState
        await foreach (var mafEvent in _mafExecutor.ExecuteAsync(
            workflow, input, _agentResolver, cancellationToken))
        {
            var state = TranslateToRuntimeState(mafEvent, executionId, workflow.Name, input, startTime, ref currentAgentName);
            if (state != null)
            {
                yield return state;
            }
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<WorkflowRuntimeState> ResumeFromCheckpointAsync(
        string checkpointId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement MAF checkpoint resumption in Phase 4-B
        _logger?.LogWarning("ResumeFromCheckpointAsync not yet implemented for MAF orchestrator");
        throw new NotImplementedException(
            "MAF checkpoint resumption will be implemented in Phase 4-B. " +
            "Use YamlDrivenOrchestrator for non-checkpointed execution.");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// MAF handles human-in-the-loop through its own workflow patterns.
    /// Use CancellationToken for execution control.
    /// </exception>
    public Task ApproveAsync(string executionId, ApprovalDecision decision)
    {
        throw new NotSupportedException(
            $"MafDrivenOrchestrator does not support ApproveAsync. " +
            $"MAF handles approvals through its own workflow patterns. " +
            $"ExecutionId: {executionId}");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// MAF cancellation is handled through CancellationToken passed to ExecuteAsync.
    /// </exception>
    public Task CancelAsync(string executionId)
    {
        throw new NotSupportedException(
            $"MafDrivenOrchestrator does not support CancelAsync. " +
            $"Use CancellationToken passed to ExecuteAsync for cancellation. " +
            $"ExecutionId: {executionId}");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// MAF execution state is emitted through the async enumerable returned by ExecuteAsync.
    /// </exception>
    public Task<WorkflowRuntimeState> GetStateAsync(string executionId)
    {
        throw new NotSupportedException(
            $"MafDrivenOrchestrator does not support GetStateAsync. " +
            $"State is emitted through ExecuteAsync async enumerable. " +
            $"ExecutionId: {executionId}");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// MAF manages its own execution tracking internally.
    /// </exception>
    public Task<IReadOnlyList<WorkflowExecutionSummary>> ListActiveExecutionsAsync()
    {
        throw new NotSupportedException(
            "MafDrivenOrchestrator does not support ListActiveExecutionsAsync. " +
            "MAF manages its own execution tracking internally.");
    }

    /// <summary>
    /// Translates a MAF WorkflowExecutionEvent to Ironbees WorkflowRuntimeState.
    /// </summary>
    private WorkflowRuntimeState? TranslateToRuntimeState(
        WorkflowExecutionEvent mafEvent,
        string executionId,
        string workflowName,
        string input,
        DateTimeOffset startTime,
        ref string? currentAgentName)
    {
        // Update current agent if provided
        if (!string.IsNullOrEmpty(mafEvent.AgentName))
        {
            currentAgentName = mafEvent.AgentName;
        }

        return mafEvent.Type switch
        {
            WorkflowExecutionEventType.WorkflowStarted => new WorkflowRuntimeState
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                CurrentStateId = "START",
                Status = WorkflowExecutionStatus.Running,
                Input = input,
                StartedAt = startTime,
                LastUpdatedAt = mafEvent.Timestamp
            },

            WorkflowExecutionEventType.AgentStarted => new WorkflowRuntimeState
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                CurrentStateId = mafEvent.AgentName ?? currentAgentName ?? "AGENT",
                Status = WorkflowExecutionStatus.Running,
                Input = input,
                StartedAt = startTime,
                LastUpdatedAt = mafEvent.Timestamp
            },

            WorkflowExecutionEventType.AgentMessage => new WorkflowRuntimeState
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                CurrentStateId = mafEvent.AgentName ?? currentAgentName ?? "AGENT",
                Status = WorkflowExecutionStatus.Running,
                Input = input,
                OutputData = CreateOutputData("message", mafEvent.Content),
                StartedAt = startTime,
                LastUpdatedAt = mafEvent.Timestamp
            },

            WorkflowExecutionEventType.AgentCompleted => new WorkflowRuntimeState
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                CurrentStateId = mafEvent.AgentName ?? currentAgentName ?? "AGENT",
                Status = WorkflowExecutionStatus.Running,
                Input = input,
                OutputData = CreateOutputData("result", mafEvent.Content),
                StartedAt = startTime,
                LastUpdatedAt = mafEvent.Timestamp
            },

            WorkflowExecutionEventType.SuperStepCompleted => new WorkflowRuntimeState
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                CurrentStateId = currentAgentName ?? "CHECKPOINT",
                Status = WorkflowExecutionStatus.Running,
                Input = input,
                OutputData = CreateCheckpointData(mafEvent.Checkpoint),
                StartedAt = startTime,
                LastUpdatedAt = mafEvent.Timestamp
            },

            WorkflowExecutionEventType.WorkflowCompleted => new WorkflowRuntimeState
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                CurrentStateId = "END",
                Status = WorkflowExecutionStatus.Completed,
                Input = input,
                OutputData = CreateOutputData("finalResult", mafEvent.Content),
                StartedAt = startTime,
                CompletedAt = mafEvent.Timestamp,
                LastUpdatedAt = mafEvent.Timestamp
            },

            WorkflowExecutionEventType.Error => new WorkflowRuntimeState
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                CurrentStateId = currentAgentName ?? "ERROR",
                Status = WorkflowExecutionStatus.Failed,
                Input = input,
                ErrorMessage = mafEvent.Content,
                StartedAt = startTime,
                LastUpdatedAt = mafEvent.Timestamp
            },

            _ => null
        };
    }

    /// <summary>
    /// Creates OutputData dictionary with a single key-value pair.
    /// </summary>
    private static IReadOnlyDictionary<string, object> CreateOutputData(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new Dictionary<string, object>();
        }
        return new Dictionary<string, object> { [key] = value };
    }

    /// <summary>
    /// Creates OutputData dictionary containing checkpoint information.
    /// </summary>
    private static IReadOnlyDictionary<string, object> CreateCheckpointData(object? checkpoint)
    {
        if (checkpoint == null)
        {
            return new Dictionary<string, object>();
        }
        return new Dictionary<string, object> { ["checkpoint"] = checkpoint };
    }
}
