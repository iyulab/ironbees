using System.Collections.Concurrent;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;
using Ironbees.Autonomous.Utilities;
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
    private readonly IFallbackStrategy<TRequest, TResult>? _fallbackStrategy;
    private readonly Func<string, string, TRequest> _requestFactory;
    private readonly ILogger _logger;
    private readonly List<string> _previousOutputs = [];
    private IFinalIterationStrategy<TRequest, TResult>? _finalIterationStrategy;

    // Context management (enabled by default via builder)
    private readonly IAutonomousContextProvider? _contextProvider;
    private readonly IAutonomousMemoryStore? _memoryStore;
    private readonly IContextSaturationMonitor? _saturationMonitor;

    private readonly ConcurrentQueue<TRequest> _taskQueue = new();
    private readonly ConcurrentDictionary<string, ExecutionHistoryEntry> _history = new();
    private readonly List<ExecutionCheckpoint> _checkpoints = [];

    private AutonomousState _state = AutonomousState.Idle;
    private AutonomousConfig _config = new();
    private AutonomousConfig? _defaultConfig;  // Stored from builder
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
    /// <param name="fallbackStrategy">Optional fallback strategy for failed executions</param>
    /// <param name="logger">Optional logger</param>
    /// <param name="contextProvider">Optional context provider (enabled by default via builder)</param>
    /// <param name="memoryStore">Optional memory store (enabled by default via builder)</param>
    /// <param name="saturationMonitor">Optional saturation monitor (enabled by default via builder)</param>
    public AutonomousOrchestrator(
        ITaskExecutor<TRequest, TResult> executor,
        Func<string, string, TRequest> requestFactory,
        IOracleVerifier? oracle = null,
        IHumanInTheLoop? humanInTheLoop = null,
        IFallbackStrategy<TRequest, TResult>? fallbackStrategy = null,
        ILogger? logger = null,
        IAutonomousContextProvider? contextProvider = null,
        IAutonomousMemoryStore? memoryStore = null,
        IContextSaturationMonitor? saturationMonitor = null)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _requestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        _oracle = oracle;
        _humanInTheLoop = humanInTheLoop;
        _fallbackStrategy = fallbackStrategy;
        _logger = logger ?? NullLogger.Instance;
        _contextProvider = contextProvider;
        _memoryStore = memoryStore;
        _saturationMonitor = saturationMonitor;
    }

    /// <summary>
    /// Context provider for managing execution context (enabled by default)
    /// </summary>
    public IAutonomousContextProvider? ContextProvider => _contextProvider;

    /// <summary>
    /// Memory store for persistent memory across iterations (enabled by default)
    /// </summary>
    public IAutonomousMemoryStore? MemoryStore => _memoryStore;

    /// <summary>
    /// Saturation monitor for context window management (enabled by default)
    /// </summary>
    public IContextSaturationMonitor? SaturationMonitor => _saturationMonitor;

    /// <summary>
    /// Set the default configuration (called by builder).
    /// This config is used by StartAsync() when no explicit config is provided.
    /// </summary>
    internal void SetDefaultConfig(AutonomousConfig config)
    {
        _defaultConfig = config;
    }

    /// <summary>
    /// Set the final iteration strategy (called by builder).
    /// This strategy is invoked when the last iteration is reached to enforce completion behavior.
    /// </summary>
    internal void SetFinalIterationStrategy(IFinalIterationStrategy<TRequest, TResult> strategy)
    {
        _finalIterationStrategy = strategy;
    }

    /// <summary>
    /// Get the current active configuration
    /// </summary>
    public AutonomousConfig CurrentConfig => _config;

    /// <summary>
    /// Dump current configuration to a TextWriter for debugging
    /// </summary>
    public void DumpConfiguration(TextWriter writer)
    {
        var config = _state == AutonomousState.Running ? _config : (_defaultConfig ?? _config);
        writer.WriteLine("┌─── Autonomous Configuration ─────────────────────────────");
        writer.WriteLine($"│ MaxIterations: {config.MaxIterations}");
        writer.WriteLine($"│ MaxOracleIterations: {config.MaxOracleIterations}");
        writer.WriteLine($"│ CompletionMode: {config.CompletionMode}");
        writer.WriteLine($"│ EnableOracle: {config.EnableOracle}");
        writer.WriteLine($"│ EnableHumanInTheLoop: {config.EnableHumanInTheLoop}");
        writer.WriteLine($"│ EnableCheckpointing: {config.EnableCheckpointing}");
        writer.WriteLine($"│ EnableContextTracking: {config.EnableContextTracking}");
        writer.WriteLine($"│ EnableReflection: {config.EnableReflection}");
        writer.WriteLine($"│ EnableFallbackStrategy: {config.EnableFallbackStrategy}");
        writer.WriteLine($"│ EnableFinalIterationStrategy: {config.EnableFinalIterationStrategy}");
        writer.WriteLine($"│ AutoContinueOnOracle: {config.AutoContinueOnOracle}");
        writer.WriteLine($"│ AutoContinueOnIncomplete: {config.AutoContinueOnIncomplete}");
        writer.WriteLine($"│ InferCanContinueFromComplete: {config.InferCanContinueFromComplete}");
        writer.WriteLine($"│ AutoContinuePromptTemplate: {config.AutoContinuePromptTemplate}");
        writer.WriteLine($"│ ContinueOnFailure: {config.ContinueOnFailure}");
        writer.WriteLine($"│ MinConfidenceThreshold: {config.MinConfidenceThreshold:P0}");
        writer.WriteLine($"│ HumanReviewConfidenceThreshold: {config.HumanReviewConfidenceThreshold:P0}");
        writer.WriteLine($"│ RetryOnFailureCount: {config.RetryOnFailureCount}");
        writer.WriteLine($"│ RetryDelayMs: {config.RetryDelayMs}");
        writer.WriteLine("└──────────────────────────────────────────────────────────");
    }

    /// <summary>
    /// Start autonomous execution with configuration.
    /// If no config is provided, uses the configuration from the builder (WithSettings/WithXxx methods).
    /// </summary>
    public async Task StartAsync(AutonomousConfig? config = null, CancellationToken cancellationToken = default)
    {
        if (_state == AutonomousState.Running)
        {
            _logger.LogWarning("Autonomous execution already running");
            return;
        }

        // Use provided config, or fall back to builder's stored config, or default
        _config = config ?? _defaultConfig ?? new AutonomousConfig();
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

            // Final iteration strategy: modify request if approaching final iteration
            if (_config.EnableFinalIterationStrategy && _finalIterationStrategy != null)
            {
                var finalContext = CreateFinalIterationContext(request, default);
                if (finalContext.IsInFinalPhase)
                {
                    RaiseEvent(AutonomousEventType.FinalIterationApproaching,
                        $"Approaching final iteration ({finalContext.RemainingIterations} remaining)");

                    var modifiedRequest = await _finalIterationStrategy.BeforeFinalIterationAsync(finalContext);
                    if (modifiedRequest != null)
                    {
                        request = modifiedRequest;
                        RaiseEvent(AutonomousEventType.RequestModified, "Request modified by final iteration strategy");
                    }
                }
            }

            try
            {
                var (goalAchieved, lastVerdict) = await ExecuteTaskWithOracleLoopAsync(request, cancellationToken);

                if (goalAchieved && _config.CompletionMode == CompletionMode.UntilGoalAchieved)
                {
                    _state = AutonomousState.StoppedByGoalAchieved;
                    RaiseEvent(AutonomousEventType.Completed, "Goal achieved");
                    break;
                }

                RaiseEvent(AutonomousEventType.IterationCompleted, $"Iteration {_currentIteration} completed");

                // AutoContinue: Automatically enqueue next iteration if oracle says CanContinue
                // Eliminates need for manual event handling in client code
                // AutoContinueOnIncomplete: Continue even if CanContinue=false when IsComplete=false
                if (_config.AutoContinueOnOracle &&
                    lastVerdict != null &&
                    !lastVerdict.IsComplete &&
                    (lastVerdict.CanContinue || _config.AutoContinueOnIncomplete))
                {
                    var nextPrompt = BuildAutoContinuePrompt(lastVerdict);
                    RaiseEvent(AutonomousEventType.AutoContinuing,
                        $"Auto-continuing with prompt: {nextPrompt[..Math.Min(50, nextPrompt.Length)]}...");
                    EnqueuePrompt(nextPrompt);
                }

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

    private async Task<(bool GoalAchieved, OracleVerdict? LastVerdict)> ExecuteTaskWithOracleLoopAsync(TRequest request, CancellationToken cancellationToken)
    {
        _currentTaskId = request.RequestId;
        _currentOracleIteration = 0;
        var currentPrompt = request.Prompt;
        var goalAchieved = false;
        OracleVerdict? lastVerdict = null;

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

            // === Context Management Integration ===
            // Record output to context provider (for deep research, software automation, etc.)
            if (_contextProvider != null && !string.IsNullOrEmpty(result.Output))
            {
                await _contextProvider.RecordOutputAsync(result.Output, new ContextMetadata
                {
                    OutputType = result.Success ? "execution_result" : "error",
                    Importance = result.Success ? 0.7 : 0.9,
                    IterationNumber = _currentIteration,
                    Tags = [$"iteration_{_currentIteration}", $"oracle_{_currentOracleIteration}"]
                }, cancellationToken);

                // Track token usage (estimate based on output length)
                var estimatedTokens = EstimateTokens(currentPrompt) + EstimateTokens(result.Output);
                _saturationMonitor?.RecordUsage(estimatedTokens, "execution");
            }

            // Update legacy context with output
            if (_config.EnableContextTracking && _executionContext != null)
            {
                _executionContext = _executionContext.WithPreviousOutput(
                    result.Output.Length > 1000 ? result.Output[..1000] + "..." : result.Output);
            }

            // Track output for AutoContinue template
            if (!string.IsNullOrEmpty(result.Output))
            {
                _previousOutputs.Add(result.Output);
                // Keep only recent outputs per config
                while (_previousOutputs.Count > _config.MaxContextOutputs)
                {
                    _previousOutputs.RemoveAt(0);
                }
            }

            // Oracle verification if enabled
            if (_config.EnableOracle && _oracle?.IsConfigured == true)
            {
                RaiseEvent(AutonomousEventType.OracleVerifying, $"Oracle verifying (iteration {_currentOracleIteration})...");

                try
                {
                    // Build context-aware prompt - integrate with ContextProvider if available
                    string contextSummary;
                    if (_contextProvider != null)
                    {
                        var relevantContext = await _contextProvider.GetRelevantContextAsync(
                            request.Prompt, _currentIteration, cancellationToken);
                        contextSummary = relevantContext.Count > 0
                            ? string.Join("\n", relevantContext.Select(c => $"[{c.Type}] {c.Content}"))
                            : "No prior context.";
                    }
                    else if (_config.EnableContextTracking && _executionContext != null)
                    {
                        contextSummary = _executionContext.BuildContextSummary();
                    }
                    else
                    {
                        contextSummary = "No prior context.";
                    }

                    var oraclePrompt = BuildOraclePrompt(request.Prompt, result.Output, contextSummary);

                    var verdict = await _oracle.VerifyAsync(
                        request.Prompt,
                        result.Output,
                        _config.OracleConfig,
                        cancellationToken);

                    lastVerdict = verdict;

                    // Infer CanContinue from IsComplete if configured (for local/smaller LLMs)
                    if (_config.InferCanContinueFromComplete && !verdict.IsComplete && !verdict.CanContinue)
                    {
                        RaiseEvent(AutonomousEventType.OracleVerified,
                            "Inferring CanContinue=true from IsComplete=false (InferCanContinueFromComplete enabled)");
                        lastVerdict = verdict with { CanContinue = true };
                    }

                    // Track oracle token usage
                    _saturationMonitor?.RecordUsage(
                        EstimateTokens(oraclePrompt) + EstimateTokens(verdict.Analysis),
                        "oracle");

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

        return (goalAchieved, lastVerdict);
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

    private string BuildAutoContinuePrompt(OracleVerdict verdict)
    {
        var template = _config.AutoContinuePromptTemplate;

        // Get last output for the template
        var lastOutput = _previousOutputs.Count > 0
            ? _previousOutputs[^1]
            : "";

        return template
            .Replace("{iteration}", (_currentIteration + 1).ToString())
            .Replace("{previous_output}", lastOutput.Length > 200 ? lastOutput[..200] + "..." : lastOutput)
            .Replace("{oracle_analysis}", verdict.Analysis ?? "No analysis");
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

    private FinalIterationContext<TRequest, TResult> CreateFinalIterationContext(TRequest request, TResult? lastResult)
    {
        return new FinalIterationContext<TRequest, TResult>
        {
            CurrentIteration = _currentIteration,
            MaxIterations = _config.MaxIterations,
            OriginalRequest = request,
            LastResult = lastResult,
            PreviousOutputs = _previousOutputs.AsReadOnly(),
            SessionId = _sessionId
        };
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

    private static int EstimateTokens(string? text) => TokenEstimator.EstimateTokens(text);
}
