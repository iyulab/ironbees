using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Configuration;
using Ironbees.Autonomous.Context;
using Ironbees.Autonomous.Models;
using Microsoft.Extensions.Logging;

namespace Ironbees.Autonomous;

/// <summary>
/// Fluent builder for AutonomousOrchestrator configuration.
/// Simplifies setup by providing a declarative API for common patterns.
/// </summary>
/// <typeparam name="TRequest">Task request type</typeparam>
/// <typeparam name="TResult">Task result type</typeparam>
public class AutonomousOrchestratorBuilder<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    private ITaskExecutor<TRequest, TResult>? _executor;
    private Func<string, string, TRequest>? _requestFactory;
    private IOracleVerifier? _oracle;
    private IHumanInTheLoop? _humanInTheLoop;
    private IFallbackStrategy<TRequest, TResult>? _fallbackStrategy;
    private IFinalIterationStrategy<TRequest, TResult>? _finalIterationStrategy;
    private ILogger? _logger;
    private AutonomousConfig _config = new();
    private OrchestratorSettings? _settings;

    // Context management interfaces (enabled by default)
    private IAutonomousContextProvider? _contextProvider;
    private IAutonomousMemoryStore? _memoryStore;
    private IContextSaturationMonitor? _saturationMonitor;
    private AutonomousContextOptions _contextOptions = new() { Enabled = true };
    private bool _contextDisabled = false;

    /// <summary>
    /// Set the task executor (required)
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithExecutor(
        ITaskExecutor<TRequest, TResult> executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        return this;
    }

    /// <summary>
    /// Set the request factory (required)
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithRequestFactory(
        Func<string, string, TRequest> requestFactory)
    {
        _requestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        return this;
    }

    /// <summary>
    /// Add oracle verification
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithOracle(IOracleVerifier oracle)
    {
        _oracle = oracle;
        _config = _config with { EnableOracle = true };
        return this;
    }

    /// <summary>
    /// Add human-in-the-loop oversight
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithHumanInTheLoop(
        IHumanInTheLoop humanInTheLoop,
        params HumanInterventionPoint[] requiredApprovalPoints)
    {
        _humanInTheLoop = humanInTheLoop;
        _config = _config with
        {
            EnableHumanInTheLoop = true,
            RequiredApprovalPoints = requiredApprovalPoints.ToList()
        };
        return this;
    }

    /// <summary>
    /// Add fallback strategy for failed executions
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithFallbackStrategy(
        IFallbackStrategy<TRequest, TResult> fallbackStrategy)
    {
        _fallbackStrategy = fallbackStrategy;
        _config = _config with { EnableFallbackStrategy = true };
        return this;
    }

    /// <summary>
    /// Add final iteration strategy for enforcing completion on last iteration.
    /// Use this to ensure the task produces a complete result when max iterations is reached.
    /// Example: In 20 Questions, force a guess on question 20.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithFinalIterationStrategy(
        IFinalIterationStrategy<TRequest, TResult> strategy)
    {
        _finalIterationStrategy = strategy;
        _config = _config with { EnableFinalIterationStrategy = true };
        return this;
    }

    /// <summary>
    /// Add final iteration strategy using a lambda for simple enforcement.
    /// The function receives the context and should return a modified result for completion.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithFinalIterationEnforcement(
        Func<FinalIterationContext<TRequest, TResult>, TResult> completionEnforcer,
        string? warningMessage = null)
    {
        _finalIterationStrategy = new PromptEnforcementFinalIterationStrategy<TRequest, TResult>(
            warningMessage ?? "⚠️ This is your final iteration. You MUST provide a complete answer now.",
            null,
            completionEnforcer);
        _config = _config with { EnableFinalIterationStrategy = true };
        return this;
    }

    /// <summary>
    /// Add logger
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Configure maximum iterations
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithMaxIterations(int maxIterations)
    {
        _config = _config with { MaxIterations = maxIterations };
        return this;
    }

    /// <summary>
    /// Configure maximum oracle iterations per task
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithMaxOracleIterations(int maxOracleIterations)
    {
        _config = _config with { MaxOracleIterations = maxOracleIterations };
        return this;
    }

    /// <summary>
    /// Enable auto-continue when oracle returns CanContinue=true.
    /// Eliminates need for manual event handling to continue iterative workflows.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithAutoContinue(
        string? promptTemplate = null)
    {
        _config = _config with
        {
            AutoContinueOnOracle = true,
            AutoContinuePromptTemplate = promptTemplate ?? _config.AutoContinuePromptTemplate
        };
        return this;
    }

    /// <summary>
    /// Enable auto-continue even when CanContinue=false but IsComplete=false.
    /// Useful for LLMs that don't reliably set CanContinue correctly.
    /// Must be used with WithAutoContinue().
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithAutoContinueOnIncomplete()
    {
        _config = _config with { AutoContinueOnIncomplete = true };
        return this;
    }

    /// <summary>
    /// Automatically infer CanContinue from IsComplete in oracle verdict.
    /// When enabled, CanContinue is set to !IsComplete after parsing verdict.
    /// Useful for smaller/local LLMs (GPUStack, Ollama) that don't reliably
    /// follow the CanContinue guideline in their JSON responses.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithInferCanContinueFromComplete()
    {
        _config = _config with { InferCanContinueFromComplete = true };
        return this;
    }

    /// <summary>
    /// Configure retry behavior for failed executions
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithRetry(
        int retryCount,
        int delayMs = 1000)
    {
        _config = _config with
        {
            RetryOnFailureCount = retryCount,
            RetryDelayMs = delayMs
        };
        return this;
    }

    /// <summary>
    /// Set completion mode
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithCompletionMode(CompletionMode mode)
    {
        _config = _config with { CompletionMode = mode };
        return this;
    }

    /// <summary>
    /// Enable checkpointing for recovery
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithCheckpointing(bool enabled = true)
    {
        _config = _config with { EnableCheckpointing = enabled };
        return this;
    }

    /// <summary>
    /// Enable context tracking (state passing between iterations)
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithContextTracking(
        int maxLearnings = 10,
        int maxOutputs = 5)
    {
        _config = _config with
        {
            EnableContextTracking = true,
            MaxContextLearnings = maxLearnings,
            MaxContextOutputs = maxOutputs
        };
        return this;
    }

    /// <summary>
    /// Enable reflection mode in oracle (Reflexion pattern)
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithReflection(bool enabled = true)
    {
        _config = _config with { EnableReflection = enabled };
        return this;
    }

    /// <summary>
    /// Set confidence threshold for goal completion
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithConfidenceThreshold(
        double minConfidence = 0.7,
        double humanReviewThreshold = 0.5)
    {
        _config = _config with
        {
            MinConfidenceThreshold = minConfidence,
            HumanReviewConfidenceThreshold = humanReviewThreshold
        };
        return this;
    }

    /// <summary>
    /// Continue processing queue on task failure
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> ContinueOnFailure(bool enabled = true)
    {
        _config = _config with { ContinueOnFailure = enabled };
        return this;
    }

    /// <summary>
    /// Apply custom configuration
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> Configure(
        Action<AutonomousConfig> configure)
    {
        configure(_config);
        return this;
    }

    // ========================================================================
    // Context Management Integration
    // ========================================================================

    /// <summary>
    /// Add context provider for iteration context management.
    /// External systems like Memory Indexer can implement IAutonomousContextProvider.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithContextProvider(
        IAutonomousContextProvider contextProvider)
    {
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _contextOptions.Enabled = true;
        return this;
    }

    /// <summary>
    /// Add memory store for persistent memory across iterations.
    /// External systems like Memory Indexer can implement IAutonomousMemoryStore.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithMemoryStore(
        IAutonomousMemoryStore memoryStore)
    {
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        return this;
    }

    /// <summary>
    /// Add saturation monitor for context window management.
    /// Enables integration with tiered memory systems for automatic eviction.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithSaturationMonitor(
        IContextSaturationMonitor saturationMonitor)
    {
        _saturationMonitor = saturationMonitor ?? throw new ArgumentNullException(nameof(saturationMonitor));
        return this;
    }

    /// <summary>
    /// Configure context management options.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithContextOptions(
        Action<AutonomousContextOptions> configure)
    {
        configure(_contextOptions);
        return this;
    }

    /// <summary>
    /// Enable default in-memory context management using separate components.
    /// For simpler setup, use WithDefaultContext() instead.
    /// For production, use WithContextProvider() with external memory system.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithInMemoryContext(
        int maxContextItems = 50,
        int maxMemories = 1000)
    {
        _contextProvider = new InMemoryContextProvider(maxContextItems);
        _memoryStore = new InMemoryMemoryStore(maxMemories);
        _saturationMonitor = new InMemorySaturationMonitor(_contextOptions.Saturation);
        _contextOptions.Enabled = true;
        return this;
    }

    /// <summary>
    /// Enable default context management with all-in-one manager.
    /// This is the simplest way to add context management without external dependencies.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithDefaultContext(
        Action<AutonomousContextOptions>? configure = null)
    {
        configure?.Invoke(_contextOptions);
        var manager = new DefaultContextManager(_contextOptions);
        _contextProvider = manager;
        _memoryStore = manager;
        _saturationMonitor = manager;
        _contextOptions.Enabled = true;
        return this;
    }

    /// <summary>
    /// Enable default context management with pre-configured manager.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithDefaultContext(
        DefaultContextManager manager)
    {
        _contextProvider = manager ?? throw new ArgumentNullException(nameof(manager));
        _memoryStore = manager;
        _saturationMonitor = manager;
        _contextOptions.Enabled = true;
        _contextDisabled = false;
        return this;
    }

    /// <summary>
    /// Disable context management entirely.
    /// Use this when you don't need context tracking or want to handle it externally.
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithoutContext()
    {
        _contextDisabled = true;
        _contextProvider = null;
        _memoryStore = null;
        _saturationMonitor = null;
        _contextOptions.Enabled = false;
        return this;
    }

    /// <summary>
    /// Get the configured context provider (or null if not configured).
    /// </summary>
    public IAutonomousContextProvider? GetContextProvider() => _contextProvider;

    /// <summary>
    /// Get the configured memory store (or null if not configured).
    /// </summary>
    public IAutonomousMemoryStore? GetMemoryStore() => _memoryStore;

    /// <summary>
    /// Get the configured saturation monitor (or null if not configured).
    /// </summary>
    public IContextSaturationMonitor? GetSaturationMonitor() => _saturationMonitor;

    // ========================================================================

    /// <summary>
    /// Build the orchestrator with stored configuration.
    /// The configuration from WithSettings() or builder methods is automatically applied.
    /// Context management is enabled by default using DefaultContextManager.
    /// </summary>
    public AutonomousOrchestrator<TRequest, TResult> Build()
    {
        if (_executor == null)
            throw new InvalidOperationException("Executor is required. Call WithExecutor().");

        if (_requestFactory == null)
            throw new InvalidOperationException("Request factory is required. Call WithRequestFactory().");

        // Auto-create DefaultContextManager if context is enabled but no provider specified
        if (!_contextDisabled && _contextProvider == null)
        {
            var manager = new DefaultContextManager(_contextOptions);
            _contextProvider = manager;
            _memoryStore = manager;
            _saturationMonitor = manager;
        }

        var orchestrator = new AutonomousOrchestrator<TRequest, TResult>(
            _executor,
            _requestFactory,
            _oracle,
            _humanInTheLoop,
            _fallbackStrategy,
            _logger,
            _contextProvider,
            _memoryStore,
            _saturationMonitor);

        // Store configuration in orchestrator for use by StartAsync()
        orchestrator.SetDefaultConfig(_config);

        // Set final iteration strategy if configured
        if (_finalIterationStrategy != null)
        {
            orchestrator.SetFinalIterationStrategy(_finalIterationStrategy);
        }

        return orchestrator;
    }

    /// <summary>
    /// Build and start the orchestrator with an initial prompt
    /// </summary>
    public async Task<AutonomousOrchestrator<TRequest, TResult>> BuildAndStartAsync(
        string initialPrompt,
        CancellationToken cancellationToken = default)
    {
        var orchestrator = Build();
        orchestrator.EnqueuePrompt(initialPrompt);
        await orchestrator.StartAsync(_config, cancellationToken);
        return orchestrator;
    }

    /// <summary>
    /// Get the current configuration
    /// </summary>
    public AutonomousConfig GetConfig() => _config;

    /// <summary>
    /// Get the loaded settings (if loaded from YAML)
    /// </summary>
    public OrchestratorSettings? GetSettings() => _settings;

    /// <summary>
    /// Load configuration from OrchestratorSettings (typically loaded from YAML)
    /// </summary>
    public AutonomousOrchestratorBuilder<TRequest, TResult> WithSettings(OrchestratorSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _config = settings.ToAutonomousConfig();
        return this;
    }

    /// <summary>
    /// Load configuration from a YAML file
    /// </summary>
    public async Task<AutonomousOrchestratorBuilder<TRequest, TResult>> WithSettingsFileAsync(
        string yamlFilePath,
        CancellationToken cancellationToken = default)
    {
        var loader = new SettingsLoader();
        var settings = await loader.LoadFromFileAsync(yamlFilePath, cancellationToken);
        return WithSettings(settings);
    }

    /// <summary>
    /// Load configuration from environment variables with optional YAML file
    /// </summary>
    public async Task<AutonomousOrchestratorBuilder<TRequest, TResult>> WithEnvironmentAsync(
        string? yamlFilePath = null,
        CancellationToken cancellationToken = default)
    {
        var loader = new SettingsLoader();
        var settings = await loader.LoadWithEnvironmentAsync(yamlFilePath, cancellationToken);
        return WithSettings(settings);
    }
}

/// <summary>
/// Static factory for creating orchestrator builders
/// </summary>
public static class AutonomousOrchestrator
{
    /// <summary>
    /// Create a new orchestrator builder
    /// </summary>
    public static AutonomousOrchestratorBuilder<TRequest, TResult> Create<TRequest, TResult>()
        where TRequest : ITaskRequest
        where TResult : ITaskResult
    {
        return new AutonomousOrchestratorBuilder<TRequest, TResult>();
    }

    /// <summary>
    /// Create a builder and load settings from a YAML file
    /// </summary>
    public static async Task<AutonomousOrchestratorBuilder<TRequest, TResult>> FromSettingsFileAsync<TRequest, TResult>(
        string yamlFilePath,
        CancellationToken cancellationToken = default)
        where TRequest : ITaskRequest
        where TResult : ITaskResult
    {
        var builder = new AutonomousOrchestratorBuilder<TRequest, TResult>();
        await builder.WithSettingsFileAsync(yamlFilePath, cancellationToken);
        return builder;
    }

    /// <summary>
    /// Create a builder and load settings from environment with optional YAML file
    /// </summary>
    public static async Task<AutonomousOrchestratorBuilder<TRequest, TResult>> FromEnvironmentAsync<TRequest, TResult>(
        string? yamlFilePath = null,
        CancellationToken cancellationToken = default)
        where TRequest : ITaskRequest
        where TResult : ITaskResult
    {
        var builder = new AutonomousOrchestratorBuilder<TRequest, TResult>();
        await builder.WithEnvironmentAsync(yamlFilePath, cancellationToken);
        return builder;
    }
}
