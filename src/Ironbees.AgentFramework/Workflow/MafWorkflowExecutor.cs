using Ironbees.AgentMode.Core.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace Ironbees.AgentFramework.Workflow;

/// <summary>
/// Executes MAF workflows converted from Ironbees YAML workflow definitions.
/// This class bridges Ironbees' file-based workflow definitions with MAF's workflow execution engine,
/// adhering to the Thin Wrapper philosophy by delegating execution to MAF.
/// </summary>
public sealed class MafWorkflowExecutor : IMafWorkflowExecutor
{
    private readonly IWorkflowConverter _converter;
    private readonly ILogger<MafWorkflowExecutor>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MafWorkflowExecutor"/> class.
    /// </summary>
    /// <param name="converter">The workflow converter for YAML to MAF conversion.</param>
    /// <param name="logger">Optional logger for execution diagnostics.</param>
    public MafWorkflowExecutor(
        IWorkflowConverter converter,
        ILogger<MafWorkflowExecutor>? logger = null)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkflowExecutionEvent> ExecuteAsync(
        WorkflowDefinition definition,
        string input,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(agentResolver);

        _logger?.LogInformation("Starting workflow execution for '{WorkflowName}'", definition.Name);

        yield return new WorkflowExecutionEvent
        {
            Type = WorkflowExecutionEventType.WorkflowStarted,
            Content = $"Starting workflow: {definition.Name}",
            Metadata = new Dictionary<string, object>
            {
                ["workflowName"] = definition.Name,
                ["stateCount"] = definition.States.Count
            }
        };

        // Convert YAML workflow to MAF workflow
        Microsoft.Agents.AI.Workflows.Workflow? mafWorkflow = null;
        Exception? conversionException = null;

        try
        {
            mafWorkflow = await _converter.ConvertAsync(definition, agentResolver, cancellationToken);
            _logger?.LogDebug("Workflow '{WorkflowName}' converted successfully", definition.Name);
        }
        catch (Exception ex)
        {
            conversionException = ex;
            _logger?.LogError(ex, "Failed to convert workflow '{WorkflowName}'", definition.Name);
        }

        // Handle conversion error outside catch block (CS1631 fix)
        if (conversionException != null)
        {
            yield return new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = $"Workflow conversion failed: {conversionException.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = conversionException.GetType().Name,
                    ["message"] = conversionException.Message
                }
            };
            yield break;
        }

        // Execute the MAF workflow
        await foreach (var evt in ExecuteWorkflowAsync(mafWorkflow!, input, cancellationToken))
        {
            yield return evt;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkflowExecutionEvent> ExecuteWorkflowAsync(
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        _logger?.LogDebug("Executing MAF workflow with input length: {InputLength}", input.Length);

        // Use a channel to safely yield values from async operations
        // This pattern avoids CS1626 (cannot yield in try block with catch)
        var channel = Channel.CreateUnbounded<WorkflowExecutionEvent>();

        // Start the execution task
        var executionTask = ExecuteWorkflowCoreAsync(workflow, input, channel.Writer, cancellationToken);

        // Yield events as they arrive
        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        // Await the execution task to propagate any unhandled exceptions
        await executionTask;
    }

    /// <summary>
    /// Core execution logic that writes events to a channel.
    /// </summary>
    private async Task ExecuteWorkflowCoreAsync(
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        string input,
        ChannelWriter<WorkflowExecutionEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            // Execute using MAF InProcessExecution with streaming
            await using var streamingRun = await InProcessExecution.StreamAsync(
                workflow,
                input,
                cancellationToken: cancellationToken);

            // Process workflow events from the streaming run
            await foreach (var evt in streamingRun.WatchStreamAsync().WithCancellation(cancellationToken))
            {
                var executionEvent = MapWorkflowEvent(evt);
                if (executionEvent != null)
                {
                    await writer.WriteAsync(executionEvent, cancellationToken);
                }
            }

            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.WorkflowCompleted,
                Content = "Workflow execution completed successfully"
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogInformation("Workflow execution was cancelled");
            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = "Workflow execution was cancelled",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = nameof(OperationCanceledException),
                    ["message"] = "Workflow execution was cancelled"
                }
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during workflow execution");
            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = $"Workflow execution failed: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = ex.GetType().Name,
                    ["message"] = ex.Message
                }
            }, CancellationToken.None);
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// Maps MAF WorkflowEvent to Ironbees WorkflowExecutionEvent.
    /// </summary>
    private WorkflowExecutionEvent? MapWorkflowEvent(WorkflowEvent evt)
    {
        return evt switch
        {
            AgentRunUpdateEvent update => new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.AgentMessage,
                AgentName = update.ExecutorId,
                Content = update.Data?.ToString(),
                Metadata = new Dictionary<string, object>
                {
                    ["eventType"] = "AgentRunUpdate",
                    ["executorId"] = update.ExecutorId ?? "unknown"
                }
            },

            ExecutorCompletedEvent completed => new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.AgentCompleted,
                AgentName = completed.ExecutorId,
                Content = completed.Data?.ToString(),
                Metadata = new Dictionary<string, object>
                {
                    ["eventType"] = "ExecutorCompleted",
                    ["executorId"] = completed.ExecutorId ?? "unknown"
                }
            },

            SuperStepCompletedEvent superStep => new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.SuperStepCompleted,
                Checkpoint = superStep.CompletionInfo?.Checkpoint,
                Metadata = new Dictionary<string, object>
                {
                    ["eventType"] = "SuperStepCompleted",
                    ["hasCheckpoint"] = superStep.CompletionInfo?.Checkpoint != null
                }
            },

            _ => null // Ignore other event types
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkflowExecutionEvent> ExecuteWithCheckpointingAsync(
        WorkflowDefinition definition,
        string input,
        string executionId,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        ICheckpointStore checkpointStore,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(agentResolver);
        ArgumentNullException.ThrowIfNull(checkpointStore);

        _logger?.LogInformation(
            "Starting checkpointed workflow execution for '{WorkflowName}' with execution ID '{ExecutionId}'",
            definition.Name, executionId);

        yield return new WorkflowExecutionEvent
        {
            Type = WorkflowExecutionEventType.WorkflowStarted,
            Content = $"Starting checkpointed workflow: {definition.Name}",
            Metadata = new Dictionary<string, object>
            {
                ["workflowName"] = definition.Name,
                ["executionId"] = executionId,
                ["stateCount"] = definition.States.Count,
                ["checkpointingEnabled"] = true
            }
        };

        // Convert YAML workflow to MAF workflow
        Microsoft.Agents.AI.Workflows.Workflow? mafWorkflow = null;
        Exception? conversionException = null;

        try
        {
            mafWorkflow = await _converter.ConvertAsync(definition, agentResolver, cancellationToken);
            _logger?.LogDebug("Workflow '{WorkflowName}' converted successfully", definition.Name);
        }
        catch (Exception ex)
        {
            conversionException = ex;
            _logger?.LogError(ex, "Failed to convert workflow '{WorkflowName}'", definition.Name);
        }

        if (conversionException != null)
        {
            yield return new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = $"Workflow conversion failed: {conversionException.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = conversionException.GetType().Name,
                    ["message"] = conversionException.Message
                }
            };
            yield break;
        }

        // Execute with checkpointing
        var channel = Channel.CreateUnbounded<WorkflowExecutionEvent>();
        var executionTask = ExecuteWithCheckpointingCoreAsync(
            mafWorkflow!, input, executionId, definition.Name,
            checkpointStore, channel.Writer, cancellationToken);

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        await executionTask;
    }

    /// <summary>
    /// Core execution logic with checkpointing that writes events to a channel.
    /// </summary>
    private async Task ExecuteWithCheckpointingCoreAsync(
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        string input,
        string executionId,
        string workflowName,
        ICheckpointStore checkpointStore,
        ChannelWriter<WorkflowExecutionEvent> writer,
        CancellationToken cancellationToken)
    {
        var executionStartedAt = DateTimeOffset.UtcNow;

        try
        {
            // Execute using MAF InProcessExecution with checkpointing enabled
            var checkpointManager = CheckpointManager.Default;
            await using var checkpointedRun = await InProcessExecution.StreamAsync(
                workflow,
                input,
                checkpointManager,
                cancellationToken: cancellationToken);

            // Process workflow events from the streaming run
            await foreach (var evt in checkpointedRun.Run.WatchStreamAsync().WithCancellation(cancellationToken))
            {
                var executionEvent = MapWorkflowEvent(evt);
                if (executionEvent != null)
                {
                    await writer.WriteAsync(executionEvent, cancellationToken);
                }

                // Save checkpoint when super-step completes
                if (evt is SuperStepCompletedEvent superStepEvent && superStepEvent.CompletionInfo?.Checkpoint != null)
                {
                    var checkpointInfo = superStepEvent.CompletionInfo.Checkpoint;
                    var checkpointData = new CheckpointData
                    {
                        CheckpointId = $"cp-{Guid.NewGuid():N}",
                        ExecutionId = executionId,
                        WorkflowName = workflowName,
                        MafCheckpointJson = SerializeCheckpoint(checkpointInfo),
                        Input = input,
                        ExecutionStartedAt = executionStartedAt
                    };

                    await checkpointStore.SaveAsync(checkpointData, cancellationToken);
                    _logger?.LogDebug(
                        "Saved checkpoint '{CheckpointId}' for execution '{ExecutionId}'",
                        checkpointData.CheckpointId, executionId);
                }
            }

            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.WorkflowCompleted,
                Content = "Checkpointed workflow execution completed successfully",
                Metadata = new Dictionary<string, object>
                {
                    ["executionId"] = executionId
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogInformation("Checkpointed workflow execution was cancelled for '{ExecutionId}'", executionId);
            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = "Workflow execution was cancelled",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = nameof(OperationCanceledException),
                    ["message"] = "Workflow execution was cancelled",
                    ["executionId"] = executionId
                }
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during checkpointed workflow execution for '{ExecutionId}'", executionId);
            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = $"Workflow execution failed: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = ex.GetType().Name,
                    ["message"] = ex.Message,
                    ["executionId"] = executionId
                }
            }, CancellationToken.None);
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkflowExecutionEvent> ResumeFromCheckpointAsync(
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        CheckpointData checkpoint,
        ICheckpointStore checkpointStore,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(checkpointStore);

        if (string.IsNullOrWhiteSpace(checkpoint.MafCheckpointJson))
        {
            throw new ArgumentException(
                "Checkpoint does not contain MAF checkpoint data (MafCheckpointJson is null or empty).",
                nameof(checkpoint));
        }

        _logger?.LogInformation(
            "Resuming workflow '{WorkflowName}' from checkpoint '{CheckpointId}' for execution '{ExecutionId}'",
            checkpoint.WorkflowName, checkpoint.CheckpointId, checkpoint.ExecutionId);

        yield return new WorkflowExecutionEvent
        {
            Type = WorkflowExecutionEventType.WorkflowStarted,
            Content = $"Resuming workflow from checkpoint: {checkpoint.CheckpointId}",
            Metadata = new Dictionary<string, object>
            {
                ["workflowName"] = checkpoint.WorkflowName,
                ["executionId"] = checkpoint.ExecutionId,
                ["checkpointId"] = checkpoint.CheckpointId,
                ["resuming"] = true
            }
        };

        var channel = Channel.CreateUnbounded<WorkflowExecutionEvent>();
        var executionTask = ResumeFromCheckpointCoreAsync(
            workflow, checkpoint, checkpointStore, channel.Writer, cancellationToken);

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        await executionTask;
    }

    /// <summary>
    /// Core resume logic that writes events to a channel.
    /// </summary>
    private async Task ResumeFromCheckpointCoreAsync(
        Microsoft.Agents.AI.Workflows.Workflow workflow,
        CheckpointData checkpoint,
        ICheckpointStore checkpointStore,
        ChannelWriter<WorkflowExecutionEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize the MAF checkpoint
            var mafCheckpoint = DeserializeCheckpoint(checkpoint.MafCheckpointJson!);
            if (mafCheckpoint == null)
            {
                throw new InvalidOperationException("Failed to deserialize MAF checkpoint data.");
            }

            // Resume using MAF InProcessExecution
            var checkpointManager = CheckpointManager.Default;
            var resumedRun = await InProcessExecution.ResumeStreamAsync(
                workflow,
                mafCheckpoint,
                checkpointManager,
                cancellationToken: cancellationToken);

            // Process workflow events
            await foreach (var evt in resumedRun.Run.WatchStreamAsync().WithCancellation(cancellationToken))
            {
                var executionEvent = MapWorkflowEvent(evt);
                if (executionEvent != null)
                {
                    await writer.WriteAsync(executionEvent, cancellationToken);
                }

                // Save new checkpoints during resumed execution
                if (evt is SuperStepCompletedEvent superStepEvent && superStepEvent.CompletionInfo?.Checkpoint != null)
                {
                    var checkpointInfo = superStepEvent.CompletionInfo.Checkpoint;
                    var newCheckpointData = new CheckpointData
                    {
                        CheckpointId = $"cp-{Guid.NewGuid():N}",
                        ExecutionId = checkpoint.ExecutionId,
                        WorkflowName = checkpoint.WorkflowName,
                        MafCheckpointJson = SerializeCheckpoint(checkpointInfo),
                        Input = checkpoint.Input,
                        ExecutionStartedAt = checkpoint.ExecutionStartedAt
                    };

                    await checkpointStore.SaveAsync(newCheckpointData, cancellationToken);
                    _logger?.LogDebug(
                        "Saved checkpoint '{CheckpointId}' during resumed execution",
                        newCheckpointData.CheckpointId);
                }
            }

            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.WorkflowCompleted,
                Content = "Resumed workflow execution completed successfully",
                Metadata = new Dictionary<string, object>
                {
                    ["executionId"] = checkpoint.ExecutionId,
                    ["resumedFromCheckpoint"] = checkpoint.CheckpointId
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogInformation(
                "Resumed workflow execution was cancelled for '{ExecutionId}'",
                checkpoint.ExecutionId);
            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = "Resumed workflow execution was cancelled",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = nameof(OperationCanceledException),
                    ["message"] = "Workflow execution was cancelled",
                    ["executionId"] = checkpoint.ExecutionId
                }
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error during resumed workflow execution for '{ExecutionId}'",
                checkpoint.ExecutionId);
            await writer.WriteAsync(new WorkflowExecutionEvent
            {
                Type = WorkflowExecutionEventType.Error,
                Content = $"Resumed workflow execution failed: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["exception"] = ex.GetType().Name,
                    ["message"] = ex.Message,
                    ["executionId"] = checkpoint.ExecutionId
                }
            }, CancellationToken.None);
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// Serializes a MAF CheckpointInfo to JSON.
    /// </summary>
    private static string SerializeCheckpoint(CheckpointInfo checkpointInfo)
    {
        return JsonSerializer.Serialize(checkpointInfo, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    /// <summary>
    /// Deserializes a MAF CheckpointInfo from JSON.
    /// </summary>
    private static CheckpointInfo? DeserializeCheckpoint(string json)
    {
        return JsonSerializer.Deserialize<CheckpointInfo>(json);
    }
}
