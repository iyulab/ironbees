using System.Collections.Concurrent;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ironbees.Autonomous;

/// <summary>
/// Core orchestrator for autonomous goal-based task execution with oracle verification,
/// human-in-the-loop oversight, execution context tracking, and reflection capabilities.
/// </summary>
/// <typeparam name="TRequest">Task request type implementing ITaskRequest</typeparam>
/// <typeparam name="TResult">Task result type implementing ITaskResult</typeparam>
public class AutonomousOrchestrator<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    private readonly ITaskExecutor<TRequest, TResult> _executor;
    private readonly IOracleVerifier? _oracle;
    private readonly IHumanInTheLoop? _humanInTheLoop;
    private readonly Func<string, string, TRequest> _requestFactory;
    private readonly ILogger _logger;

    private readonly ConcurrentQueue<TRequest> _taskQueue = new();
    private readonly ConcurrentDictionary<string, ExecutionHistoryEntry> _history = new();
    private readonly List<ExecutionCheckpoint> _checkpoints = [];

    private AutonomousState _state = AutonomousState.Idle;
    private AutonomousConfig _config = new();
    private string _sessionId = string.Empty;
    private int _currentIteration;
    private int _currentOracleIteration;
    private string? _currentTaskId;
    private string? _lastError;
    private CancellationTokenSource? _cts;
    private AutonomousExecutionContext? _executionContext;

    /// <summary>
    /// Event raised when autonomous execution state changes or produces output
    /// </summary>
    public event Action<AutonomousEvent>? OnEvent;

    /// <summary>
    /// Current execution state
    /// </summary>
    public AutonomousState State => _state;

    /// <summary>
    /// Current session ID
    /// </summary>
    public string SessionId => _sessionId;

    /// <summary>
    /// Current execution context
    /// </summary>
    public AutonomousExecutionContext? ExecutionContext => _executionContext;

    /// <summary>
    /// Current status snapshot
    /// </summary>
    public AutonomousStatus Status => new()
    {
        State = _state,
        SessionId = _sessionId,
        QueuedTaskCount = _taskQueue.Count,
        CurrentIteration = _currentIteration,
        MaxIterations = _config.MaxIterations,
        OracleEnabled = _config.EnableOracle && _oracle?.IsConfigured == true,
        CurrentOracleIteration = _currentOracleIteration,
        MaxOracleIterations = _config.MaxOracleIterations,
        CompletionMode = _config.CompletionMode,
        CheckpointCount = _checkpoints.Count,
        CheckpointingEnabled = _config.EnableCheckpointing,
        HistoryEntryCount = _history.Count,
        CurrentTaskId = _currentTaskId,
        LastError = _lastError
    };

    /// <summary>
    /// Create orchestrator with executor and optional components
    /// </summary>
    /// <param name="executor">Task executor implementation</param>
    /// <param name="requestFactory">Factory to create requests from (requestId, prompt)</param>
    /// <param name="oracle">Optional oracle verifier for goal checking</param>
    /// <param name="humanInTheLoop">Optional human-in-the-loop handler</param>
    /// <param name="logger">Optional logger</param>
    public AutonomousOrchestrator(
        ITaskExecutor<TRequest, TResult> executor,
        Func<string, string, TRequest> requestFactory,
        IOracleVerifier? oracle = null,
        IHumanInTheLoop? humanInTheLoop = null,
        ILogger? logger = null)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _requestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        _oracle = oracle;
        _humanInTheLoop = humanInTheLoop;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Start autonomous execution with configuration
    /// </summary>
    public async Task StartAsync(AutonomousConfig? config = null, CancellationToken cancellationToken = default)
    {
        if (_state == AutonomousState.Running)
        {
            _logger.LogWarning("Autonomous execution already running");
            return;
        }

        _config = config ?? new AutonomousConfig();
        _sessionId = Guid.NewGuid().ToString("N")[..8];
        _currentIteration = 0;
        _currentOracleIteration = 0;
        _lastError = null;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Initialize execution context
        if (_config.EnableContextTracking)
        {
            _executionContext = AutonomousExecutionContext.Initial(_sessionId, "Autonomous execution session");
        }

        _state = AutonomousState.Running;
        RaiseEvent(AutonomousEventType.Started, "Autonomous execution started");

        try
        {
            await ExecuteLoopAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            _state = AutonomousState.StoppedByUser;
            RaiseEvent(AutonomousEventType.Stopped, "Execution stopped by user");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _state = AutonomousState.StoppedByError;
            RaiseEvent(AutonomousEventType.Error, $"Execution stopped by error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Start from a checkpoint (restore and continue)
    /// </summary>
    public async Task StartFromCheckpointAsync(
        string checkpointId,
        AutonomousConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var checkpoint = _checkpoints.FirstOrDefault(c => c.Id == checkpointId)
            ?? throw new ArgumentException($"Checkpoint {checkpointId} not found", nameof(checkpointId));

        // Request human approval if HITL enabled
        if (_config.EnableHumanInTheLoop &&
            _config.RequiredApprovalPoints.Contains(HumanInterventionPoint.BeforeCheckpointRestore) &&
            _humanInTheLoop?.IsAvailable == true)
        {
            var approval = await RequestApprovalAsync(
                HumanInterventionPoint.BeforeCheckpointRestore,
                $"Restore from checkpoint at iteration {checkpoint.IterationNumber}?",
                cancellationToken);

            if (approval.Decision == ApprovalDecision.Rejected)
            {
                RaiseEvent(AutonomousEventType.Stopped, "Checkpoint restore rejected by human");
                return;
            }
        }

        await RestoreFromCheckpointAsync(checkpoint, cancellationToken);
        await StartAsync(config ?? checkpoint.ConfigSnapshot, cancellationToken);
    }

    /// <summary>
    /// Restore state from a checkpoint
    /// </summary>
    public Task RestoreFromCheckpointAsync(ExecutionCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        if (_state == AutonomousState.Running)
        {
            throw new InvalidOperationException("Cannot restore while running");
        }

        _sessionId = checkpoint.SessionId;
        _currentIteration = checkpoint.IterationNumber;
        _config = checkpoint.ConfigSnapshot ?? new AutonomousConfig();

        // Restore queue
        while (_taskQueue.TryDequeue(out _)) { }
        foreach (var item in checkpoint.QueueSnapshot)
        {
            if (item is TRequest request)
            {
                _taskQueue.Enqueue(request);
            }
        }

        // Restore history
        _history.Clear();
        foreach (var entry in checkpoint.HistorySnapshot)
        {
            _history[entry.Id] = entry;
        }

        // Restore context from history
        if (_config.EnableContextTracking)
        {
            _executionContext = RebuildContextFromHistory(checkpoint.HistorySnapshot);
        }

        RaiseEvent(AutonomousEventType.CheckpointRestored,
            $"Restored from checkpoint {checkpoint.Id} at iteration {checkpoint.IterationNumber}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueue a task for execution
    /// </summary>
    public void EnqueueTask(TRequest request)
    {
        _taskQueue.Enqueue(request);
        RaiseEvent(AutonomousEventType.TaskEnqueued, $"Task enqueued: {request.RequestId}");
    }

    /// <summary>
    /// Enqueue a prompt for execution (creates request automatically)
    /// </summary>
    public void EnqueuePrompt(string prompt)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var request = _requestFactory(requestId, prompt);

        // Update execution context with original goal
        if (_config.EnableContextTracking && _executionContext != null)
        {
            _executionContext = _executionContext with { OriginalGoal = prompt };
        }

        EnqueueTask(request);
    }

    /// <summary>
    /// Pause execution
    /// </summary>
    public void Pause()
    {
        if (_state == AutonomousState.Running)
        {
            _state = AutonomousState.Paused;
            RaiseEvent(AutonomousEventType.Paused, "Execution paused");
        }
    }

    /// <summary>
    /// Resume execution
    /// </summary>
    public void Resume()
    {
        if (_state == AutonomousState.Paused)
        {
            _state = AutonomousState.Running;
            RaiseEvent(AutonomousEventType.Resumed, "Execution resumed");
        }
    }

    /// <summary>
    /// Stop execution
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Clear task queue
    /// </summary>
    public void ClearQueue()
    {
        while (_taskQueue.TryDequeue(out _)) { }
        RaiseEvent(AutonomousEventType.QueueCleared, "Task queue cleared");
    }

    /// <summary>
    /// Get execution history
    /// </summary>
    public IReadOnlyList<ExecutionHistoryEntry> GetHistory() =>
        _history.Values.OrderBy(h => h.StartedAt).ToList();

    /// <summary>
    /// Get checkpoints
    /// </summary>
    public IReadOnlyList<ExecutionCheckpoint> GetCheckpoints() => _checkpoints.AsReadOnly();

    /// <summary>
    /// Inject a checkpoint (for testing or external persistence)
    /// </summary>
    public void InjectCheckpoint(ExecutionCheckpoint checkpoint)
    {
        _checkpoints.Add(checkpoint);
    }

    private async Task ExecuteLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait while paused
            while (_state == AutonomousState.Paused && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }

            // Check queue
            if (!_taskQueue.TryDequeue(out var request))
            {
                if (_config.CompletionMode == CompletionMode.UntilQueueEmpty)
                {
                    _state = AutonomousState.Completed;
                    RaiseEvent(AutonomousEventType.Completed, "Queue empty, execution completed");
                    break;
                }

                RaiseEvent(AutonomousEventType.QueueEmpty, "Waiting for tasks...");
                await Task.Delay(500, cancellationToken);
                continue;
            }

            // Check iteration limit
            if (_currentIteration >= _config.MaxIterations)
            {
                _state = AutonomousState.StoppedByMaxIterations;
                RaiseEvent(AutonomousEventType.MaxIterationsReached, $"Max iterations ({_config.MaxIterations}) reached");
                break;
            }

            // Request approval before task start if required
            if (await ShouldRequestApprovalAsync(HumanInterventionPoint.BeforeTaskStart, cancellationToken))
            {
                var approval = await RequestApprovalAsync(
                    HumanInterventionPoint.BeforeTaskStart,
                    $"Start task: {request.Prompt[..Math.Min(100, request.Prompt.Length)]}...",
                    cancellationToken);

                if (approval.Decision == ApprovalDecision.Rejected)
                {
                    RaiseEvent(AutonomousEventType.Stopped, "Task start rejected by human");
                    _state = AutonomousState.StoppedByUser;
                    break;
                }

                if (approval.Decision == ApprovalDecision.ModifyAndApprove && approval.ModifiedAction != null)
                {
                    request = _requestFactory(request.RequestId, approval.ModifiedAction);
                }
            }

            // Execute task with oracle loop
            _currentIteration++;
            RaiseEvent(AutonomousEventType.IterationStarted, $"Starting iteration {_currentIteration}");

            // Update context
            if (_config.EnableContextTracking && _executionContext != null)
            {
                _executionContext = _executionContext.WithNextIteration(_currentIteration, 0);
                RaiseEvent(AutonomousEventType.ContextUpdated, "Context updated for new iteration");
            }

            try
            {
                var completed = await ExecuteTaskWithOracleLoopAsync(request, cancellationToken);

                if (completed && _config.CompletionMode == CompletionMode.UntilGoalAchieved)
                {
                    _state = AutonomousState.StoppedByGoalAchieved;
                    RaiseEvent(AutonomousEventType.Completed, "Goal achieved");
                    break;
                }

                RaiseEvent(AutonomousEventType.IterationCompleted, $"Iteration {_currentIteration} completed");

                // Request feedback after task completion if enabled
                if (_config.EnableHumanInTheLoop && _config.RequestFeedbackOnComplete)
                {
                    await RequestFeedbackAsync(request, cancellationToken);
                }
            }
            catch (Exception ex) when (_config.ContinueOnFailure)
            {
                _lastError = ex.Message;
                RaiseEvent(AutonomousEventType.TaskFailed, $"Task failed (continuing): {ex.Message}");

                // Record error in context
                if (_config.EnableContextTracking && _executionContext != null)
                {
                    _executionContext = _executionContext.WithErrorResolution(new ErrorResolution
                    {
                        IterationNumber = _currentIteration,
                        ErrorSummary = ex.Message,
                        Category = ErrorCategory.Runtime,
                        ResolutionApplied = "ContinueOnFailure enabled - skipping task",
                        WasSuccessful = false
                    });
                }

                // Request approval after failure if required
                if (await ShouldRequestApprovalAsync(HumanInterventionPoint.TaskFailed, cancellationToken))
                {
                    var approval = await RequestApprovalAsync(
                        HumanInterventionPoint.TaskFailed,
                        $"Task failed: {ex.Message}. Continue?",
                        cancellationToken);

                    if (approval.Decision == ApprovalDecision.Rejected)
                    {
                        _state = AutonomousState.StoppedByUser;
                        break;
                    }
                }
            }

            // Create checkpoint if enabled
            if (_config.EnableCheckpointing)
            {
                CreateCheckpoint();
            }
        }
    }

    private async Task<bool> ExecuteTaskWithOracleLoopAsync(TRequest request, CancellationToken cancellationToken)
    {
        _currentTaskId = request.RequestId;
        _currentOracleIteration = 0;
        var currentPrompt = request.Prompt;
        var goalAchieved = false;

        RaiseEvent(AutonomousEventType.TaskStarted, $"Task started: {request.RequestId}", request.RequestId);

        while (_currentOracleIteration < _config.MaxOracleIterations && !cancellationToken.IsCancellationRequested)
        {
            _currentOracleIteration++;

            // Update context for oracle iteration
            if (_config.EnableContextTracking && _executionContext != null)
            {
                _executionContext = _executionContext.WithNextIteration(_currentIteration, _currentOracleIteration);
            }

            // Create history entry
            var historyEntry = new ExecutionHistoryEntry
            {
                SessionId = _sessionId,
                IterationNumber = _currentIteration,
                ExecutionPrompt = currentPrompt
            };

            // Execute task
            var currentRequest = _currentOracleIteration == 1
                ? request
                : _requestFactory(request.RequestId + $"_{_currentOracleIteration}", currentPrompt);

            var result = await _executor.ExecuteAsync(
                currentRequest,
                output => RaiseEvent(AutonomousEventType.TaskOutput, output.Content, request.RequestId),
                cancellationToken);

            // Update history
            historyEntry = historyEntry with
            {
                ExecutionOutput = result.Output,
                Success = result.Success,
                ErrorMessage = result.ErrorOutput,
                CompletedAt = DateTimeOffset.UtcNow
            };

            // Update context with output
            if (_config.EnableContextTracking && _executionContext != null)
            {
                _executionContext = _executionContext.WithPreviousOutput(
                    result.Output.Length > 1000 ? result.Output[..1000] + "..." : result.Output);
            }

            // Oracle verification if enabled
            if (_config.EnableOracle && _oracle?.IsConfigured == true)
            {
                RaiseEvent(AutonomousEventType.OracleVerifying, $"Oracle verifying (iteration {_currentOracleIteration})...");

                try
                {
                    // Build context-aware prompt if context tracking enabled
                    var contextSummary = _config.EnableContextTracking && _executionContext != null
                        ? _executionContext.BuildContextSummary()
                        : "No prior context.";

                    var oraclePrompt = BuildOraclePrompt(request.Prompt, result.Output, contextSummary);

                    var verdict = await _oracle.VerifyAsync(
                        request.Prompt,
                        result.Output,
                        _config.OracleConfig,
                        cancellationToken);

                    historyEntry = historyEntry with
                    {
                        OraclePrompt = oraclePrompt,
                        OracleVerdict = verdict
                    };

                    RaiseEvent(AutonomousEventType.OracleVerified,
                        $"Oracle verdict: complete={verdict.IsComplete}, confidence={verdict.Confidence:P0}",
                        request.RequestId,
                        verdict,
                        _currentOracleIteration);

                    // Capture reflection if available
                    if (_config.EnableReflection && verdict.Reflection != null && _executionContext != null)
                    {
                        _executionContext = _executionContext
                            .WithLearning(verdict.Reflection.ToLearning(_currentIteration))
                            .WithReflection(verdict.Reflection.ToInsight());

                        RaiseEvent(AutonomousEventType.ReflectionCaptured,
                            $"Reflection: {verdict.Reflection.LessonsLearned ?? "captured"}");
                    }

                    // Check if human review needed due to low confidence
                    if (verdict.Confidence < _config.HumanReviewConfidenceThreshold)
                    {
                        if (await ShouldRequestApprovalAsync(HumanInterventionPoint.OracleUncertain, cancellationToken))
                        {
                            var approval = await RequestApprovalAsync(
                                HumanInterventionPoint.OracleUncertain,
                                $"Oracle uncertain (confidence: {verdict.Confidence:P0}). Analysis: {verdict.Analysis}",
                                cancellationToken);

                            if (approval.Feedback != null && _executionContext != null)
                            {
                                _executionContext = _executionContext.WithHumanFeedback(approval.Feedback);
                            }

                            if (approval.Decision == ApprovalDecision.Rejected)
                            {
                                break;
                            }

                            if (approval.ModifiedAction != null)
                            {
                                currentPrompt = approval.ModifiedAction;
                                continue;
                            }
                        }
                    }

                    if (verdict.IsComplete && verdict.Confidence >= _config.MinConfidenceThreshold)
                    {
                        goalAchieved = true;
                        RaiseEvent(AutonomousEventType.OracleComplete, "Goal achieved with sufficient confidence");
                        break;
                    }

                    if (!verdict.CanContinue)
                    {
                        RaiseEvent(AutonomousEventType.OracleComplete, "Oracle indicates cannot continue");
                        break;
                    }

                    // Prepare next iteration with suggested prompt
                    if (!string.IsNullOrWhiteSpace(verdict.NextPromptSuggestion))
                    {
                        currentPrompt = verdict.NextPromptSuggestion;
                        RaiseEvent(AutonomousEventType.OracleRetrying,
                            $"Retrying with refined prompt: {currentPrompt[..Math.Min(100, currentPrompt.Length)]}...");
                    }
                    else
                    {
                        break; // No suggestion means we're done
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Oracle verification failed");
                    RaiseEvent(AutonomousEventType.OracleError, $"Oracle error: {ex.Message}");
                    break;
                }
            }
            else
            {
                // No oracle - single execution
                break;
            }

            // Store history
            _history[historyEntry.Id] = historyEntry;
            RaiseEvent(AutonomousEventType.HistoryEntryAdded, $"History entry added: {historyEntry.Id}",
                historyEntry: historyEntry);
        }

        RaiseEvent(AutonomousEventType.TaskCompleted, $"Task completed: {request.RequestId}", request.RequestId);
        _currentTaskId = null;

        return goalAchieved;
    }

    private string BuildOraclePrompt(string originalPrompt, string executionOutput, string context)
    {
        var template = _config.EnableReflection && _config.OracleConfig.EnableReflection
            ? _config.OracleConfig.ReflectionUserPromptTemplate
            : _config.OracleConfig.UserPromptTemplate;

        return template
            .Replace("{original_prompt}", originalPrompt)
            .Replace("{execution_output}", executionOutput)
            .Replace("{context}", context);
    }

    private async Task<bool> ShouldRequestApprovalAsync(HumanInterventionPoint point, CancellationToken cancellationToken)
    {
        if (!_config.EnableHumanInTheLoop)
            return false;

        if (!_config.RequiredApprovalPoints.Contains(point))
            return false;

        if (_humanInTheLoop?.IsAvailable != true)
            return false;

        return true;
    }

    private async Task<HumanApproval> RequestApprovalAsync(
        HumanInterventionPoint point,
        string summary,
        CancellationToken cancellationToken)
    {
        if (_humanInTheLoop == null)
        {
            return HumanApproval.AutoApprove(Guid.NewGuid().ToString("N")[..8]);
        }

        var request = new HumanApprovalRequest
        {
            InterventionPoint = point,
            Summary = summary,
            TaskId = _currentTaskId
        };

        RaiseEvent(AutonomousEventType.HumanApprovalRequested, $"Approval requested: {summary}");

        try
        {
            var approval = await _humanInTheLoop.RequestApprovalAsync(request, cancellationToken);
            RaiseEvent(AutonomousEventType.HumanApprovalReceived, $"Approval received: {approval.Decision}");
            return approval;
        }
        catch (OperationCanceledException) when (_config.AutoApproveOnTimeout)
        {
            RaiseEvent(AutonomousEventType.HumanApprovalTimeout, "Approval timeout - auto-approving");
            return HumanApproval.Timeout(request.RequestId);
        }
    }

    private async Task RequestFeedbackAsync(TRequest request, CancellationToken cancellationToken)
    {
        if (_humanInTheLoop?.IsAvailable != true)
            return;

        var lastHistoryEntry = _history.Values
            .OrderByDescending(h => h.CompletedAt)
            .FirstOrDefault();

        if (lastHistoryEntry == null)
            return;

        var feedbackRequest = new HumanFeedbackRequest
        {
            FeedbackType = FeedbackType.QualityAssessment,
            OriginalPrompt = request.Prompt,
            ExecutionOutput = lastHistoryEntry.ExecutionOutput ?? "",
            TaskId = request.RequestId,
            OracleAnalysis = lastHistoryEntry.OracleVerdict?.Analysis
        };

        RaiseEvent(AutonomousEventType.HumanFeedbackRequested, "Feedback requested");

        try
        {
            var feedback = await _humanInTheLoop.RequestFeedbackAsync(feedbackRequest, cancellationToken);
            RaiseEvent(AutonomousEventType.HumanFeedbackReceived,
                $"Feedback received: satisfactory={feedback.IsSatisfactory}");

            // Update context with human feedback
            if (_config.EnableContextTracking && _executionContext != null && feedback.Comments != null)
            {
                _executionContext = _executionContext.WithHumanFeedback(feedback.Comments);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get human feedback");
        }
    }

    private AutonomousExecutionContext RebuildContextFromHistory(IReadOnlyList<ExecutionHistoryEntry> history)
    {
        var context = AutonomousExecutionContext.Initial(_sessionId, "Restored session");

        foreach (var entry in history.OrderBy(h => h.StartedAt))
        {
            if (!string.IsNullOrEmpty(entry.ExecutionOutput))
            {
                context = context.WithPreviousOutput(
                    entry.ExecutionOutput.Length > 500
                        ? entry.ExecutionOutput[..500] + "..."
                        : entry.ExecutionOutput);
            }

            if (entry.OracleVerdict?.Reflection != null)
            {
                context = context
                    .WithLearning(entry.OracleVerdict.Reflection.ToLearning(entry.IterationNumber))
                    .WithReflection(entry.OracleVerdict.Reflection.ToInsight());
            }
        }

        return context;
    }

    private void CreateCheckpoint()
    {
        var checkpoint = new ExecutionCheckpoint
        {
            SessionId = _sessionId,
            IterationNumber = _currentIteration,
            QueueSnapshot = _taskQueue.Cast<object>().ToList(),
            HistorySnapshot = _history.Values.ToList(),
            ConfigSnapshot = _config
        };

        _checkpoints.Add(checkpoint);
        RaiseEvent(AutonomousEventType.CheckpointCreated, $"Checkpoint created at iteration {_currentIteration}");
    }

    private void RaiseEvent(
        AutonomousEventType type,
        string? message = null,
        string? taskId = null,
        OracleVerdict? verdict = null,
        int? oracleIteration = null,
        ExecutionHistoryEntry? historyEntry = null)
    {
        var evt = new AutonomousEvent
        {
            Type = type,
            Message = message,
            CurrentTaskId = taskId,
            OracleVerdict = verdict,
            OracleIteration = oracleIteration,
            HistoryEntry = historyEntry,
            State = _state
        };

        OnEvent?.Invoke(evt);
        _logger.LogDebug("Event: {Type} - {Message}", type, message);
    }
}
