using Ironbees.AgentMode.Workflow;
using Ironbees.AgentMode.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
public sealed partial class MafDrivenOrchestrator : IWorkflowOrchestrator<WorkflowRuntimeState>
{
    private readonly IWorkflowLoader _workflowLoader;
    private readonly IMafWorkflowExecutor _mafExecutor;
    private readonly IWorkflowConverter _workflowConverter;
    private readonly Func<string, CancellationToken, Task<AIAgent>> _agentResolver;
    private readonly ICheckpointStore? _checkpointStore;
    private readonly ILogger<MafDrivenOrchestrator>? _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="MafDrivenOrchestrator"/> class.
    /// </summary>
    /// <param name="workflowLoader">The workflow loader for YAML definitions.</param>
    /// <param name="mafExecutor">The MAF workflow executor.</param>
    /// <param name="workflowConverter">The workflow converter for YAML to MAF conversion.</param>
    /// <param name="agentResolver">Function to resolve agent names to AIAgent instances.</param>
    /// <param name="checkpointStore">Optional checkpoint store for persistence and resumption.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public MafDrivenOrchestrator(
        IWorkflowLoader workflowLoader,
        IMafWorkflowExecutor mafExecutor,
        IWorkflowConverter workflowConverter,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        ICheckpointStore? checkpointStore = null,
        ILogger<MafDrivenOrchestrator>? logger = null)
    {
        _workflowLoader = workflowLoader ?? throw new ArgumentNullException(nameof(workflowLoader));
        _mafExecutor = mafExecutor ?? throw new ArgumentNullException(nameof(mafExecutor));
        _workflowConverter = workflowConverter ?? throw new ArgumentNullException(nameof(workflowConverter));
        _agentResolver = agentResolver ?? throw new ArgumentNullException(nameof(agentResolver));
        _checkpointStore = checkpointStore;
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

        if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
        {
            LogStartingMafWorkflowExecution(_logger, workflow.Name);
        }

        // Validate workflow using standard loader validation
        var validation = _workflowLoader.Validate(workflow);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join(", ", validation.Errors.Select(e => e.Message));
            if (_logger is not null) { LogWorkflowValidationFailed(_logger, errorMessage); }

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

        // Choose execution path based on checkpoint store availability
        IAsyncEnumerable<WorkflowExecutionEvent> eventStream;
        if (_checkpointStore != null)
        {
            if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
            {
                LogExecutingWithCheckpointing(_logger, workflow.Name, executionId);
            }
            eventStream = _mafExecutor.ExecuteWithCheckpointingAsync(
                workflow, input, executionId, _agentResolver, _checkpointStore, cancellationToken);
        }
        else
        {
            eventStream = _mafExecutor.ExecuteAsync(
                workflow, input, _agentResolver, cancellationToken);
        }

        // Serialize workflow definition for checkpoint context (enables future resumption)
        var workflowContextJson = _checkpointStore != null
            ? SerializeWorkflowContext(workflow)
            : null;

        // Execute via MAF and translate events to WorkflowRuntimeState
        await foreach (var mafEvent in eventStream.WithCancellation(cancellationToken))
        {
            // When checkpoint store is available and a super-step completes,
            // update the checkpoint with the serialized workflow definition
            if (_checkpointStore != null &&
                mafEvent.Type == WorkflowExecutionEventType.SuperStepCompleted &&
                workflowContextJson != null)
            {
                await EnrichCheckpointWithWorkflowContextAsync(
                    executionId, workflowContextJson, cancellationToken);
            }

            var state = TranslateToRuntimeState(
                mafEvent, executionId, workflow.Name, input, startTime, ref currentAgentName);
            if (state != null)
            {
                yield return state;
            }
        }
    }

    /// <inheritdoc />
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

        if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
        {
            LogResumingFromCheckpoint(_logger, checkpointId);
        }

        // Load the checkpoint
        var checkpoint = await _checkpointStore.GetAsync(checkpointId, cancellationToken);
        if (checkpoint == null)
        {
            throw new InvalidOperationException(
                $"Checkpoint '{checkpointId}' not found in the checkpoint store.");
        }

        if (string.IsNullOrWhiteSpace(checkpoint.MafCheckpointJson))
        {
            throw new InvalidOperationException(
                $"Checkpoint '{checkpointId}' does not contain MAF checkpoint data. " +
                "Only checkpoints created during MAF execution can be resumed.");
        }

        // Reconstruct the workflow definition from checkpoint context
        WorkflowDefinition workflowDefinition;
        if (!string.IsNullOrWhiteSpace(checkpoint.ContextJson))
        {
            workflowDefinition = DeserializeWorkflowContext(checkpoint.ContextJson);
        }
        else
        {
            throw new InvalidOperationException(
                $"Checkpoint '{checkpointId}' does not contain workflow definition context. " +
                "Ensure the workflow was executed with checkpoint support enabled.");
        }

        // Convert the workflow definition to MAF format
        var mafWorkflow = await _workflowConverter.ConvertAsync(
            workflowDefinition, _agentResolver, cancellationToken);

        var executionId = checkpoint.ExecutionId;
        var startTime = checkpoint.ExecutionStartedAt ?? DateTimeOffset.UtcNow;
        var input = checkpoint.Input ?? string.Empty;
        string? currentAgentName = checkpoint.CurrentStateId;

        if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
        {
            LogResumedWorkflow(_logger, checkpoint.WorkflowName, executionId, checkpointId);
        }

        // Resume via MAF executor and translate events
        await foreach (var mafEvent in _mafExecutor.ResumeFromCheckpointAsync(
            mafWorkflow, checkpoint, _checkpointStore, cancellationToken))
        {
            var state = TranslateToRuntimeState(
                mafEvent, executionId, checkpoint.WorkflowName, input, startTime, ref currentAgentName);
            if (state != null)
            {
                yield return state;
            }
        }
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
    /// Enriches the latest checkpoint for an execution with serialized workflow context.
    /// This enables future resumption by storing the workflow definition alongside MAF checkpoint data.
    /// </summary>
    private async Task EnrichCheckpointWithWorkflowContextAsync(
        string executionId,
        string workflowContextJson,
        CancellationToken cancellationToken)
    {
        var latestCheckpoint = await _checkpointStore!.GetLatestForExecutionAsync(
            executionId, cancellationToken);

        if (latestCheckpoint != null && string.IsNullOrEmpty(latestCheckpoint.ContextJson))
        {
            var enriched = latestCheckpoint with { ContextJson = workflowContextJson };
            await _checkpointStore.SaveAsync(enriched, cancellationToken);
        }
    }

    /// <summary>
    /// Serializes a WorkflowDefinition to JSON for storage in checkpoint context.
    /// </summary>
    private static string SerializeWorkflowContext(WorkflowDefinition workflow)
    {
        return JsonSerializer.Serialize(workflow, s_jsonOptions);
    }

    /// <summary>
    /// Deserializes a WorkflowDefinition from JSON stored in checkpoint context.
    /// </summary>
    private static WorkflowDefinition DeserializeWorkflowContext(string contextJson)
    {
        return JsonSerializer.Deserialize<WorkflowDefinition>(contextJson, s_jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize workflow definition from checkpoint context.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting MAF-driven workflow execution for '{WorkflowName}'")]
    private static partial void LogStartingMafWorkflowExecution(ILogger logger, string workflowName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Workflow validation failed: {Errors}")]
    private static partial void LogWorkflowValidationFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing workflow '{WorkflowName}' with checkpointing enabled (executionId: {ExecutionId})")]
    private static partial void LogExecutingWithCheckpointing(ILogger logger, string workflowName, string executionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resuming workflow from checkpoint '{CheckpointId}'")]
    private static partial void LogResumingFromCheckpoint(ILogger logger, string checkpointId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resumed workflow '{WorkflowName}' execution '{ExecutionId}' from checkpoint '{CheckpointId}'")]
    private static partial void LogResumedWorkflow(ILogger logger, string workflowName, string executionId, string checkpointId);

    /// <summary>
    /// Translates a MAF WorkflowExecutionEvent to Ironbees WorkflowRuntimeState.
    /// </summary>
    private static WorkflowRuntimeState? TranslateToRuntimeState(
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
    private static Dictionary<string, object> CreateOutputData(string key, string? value)
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
    private static Dictionary<string, object> CreateCheckpointData(object? checkpoint)
    {
        if (checkpoint == null)
        {
            return new Dictionary<string, object>();
        }
        return new Dictionary<string, object> { ["checkpoint"] = checkpoint };
    }
}
