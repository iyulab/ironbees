namespace Ironbees.Core.Pipeline;

/// <summary>
/// Builder for creating agent pipelines with fluent API
/// </summary>
public class AgentPipelineBuilder
{
    private readonly string _name;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly List<PipelineStep> _steps = new();
    private readonly List<ParallelPipelineStep> _parallelSteps = new();

    public AgentPipelineBuilder(string name, IAgentOrchestrator orchestrator)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <summary>
    /// Add an agent to the pipeline
    /// </summary>
    /// <param name="agentName">Name of the agent</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder AddAgent(string agentName)
    {
        _steps.Add(new PipelineStep
        {
            AgentName = agentName
        });

        return this;
    }

    /// <summary>
    /// Add an agent with configuration
    /// </summary>
    /// <param name="agentName">Name of the agent</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder AddAgent(string agentName, Action<PipelineStepBuilder> configure)
    {
        var step = new PipelineStep { AgentName = agentName };
        var stepBuilder = new PipelineStepBuilder(step);
        configure(stepBuilder);

        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Add a conditional agent that only executes if condition is true
    /// </summary>
    /// <param name="agentName">Name of the agent</param>
    /// <param name="condition">Condition to check</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder AddAgentIf(string agentName, Func<PipelineContext, bool> condition)
    {
        _steps.Add(new PipelineStep
        {
            AgentName = agentName,
            Condition = condition
        });

        return this;
    }

    /// <summary>
    /// Add multiple agents in sequence
    /// </summary>
    /// <param name="agentNames">Names of agents to add</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder AddAgents(params string[] agentNames)
    {
        foreach (var agentName in agentNames)
        {
            AddAgent(agentName);
        }

        return this;
    }

    /// <summary>
    /// Add an input transformer to the last added agent
    /// </summary>
    /// <param name="transformer">Input transformer function</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder TransformInput(Func<PipelineContext, string> transformer)
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Cannot add transformer without adding an agent first");
        }

        _steps[^1].InputTransformer = transformer;
        return this;
    }

    /// <summary>
    /// Add an output transformer to the last added agent
    /// </summary>
    /// <param name="transformer">Output transformer function</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder TransformOutput(Func<PipelineContext, string, string> transformer)
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Cannot add transformer without adding an agent first");
        }

        _steps[^1].OutputTransformer = transformer;
        return this;
    }

    /// <summary>
    /// Configure error handling for the last added agent
    /// </summary>
    /// <param name="continueOnError">Whether to continue pipeline on error</param>
    /// <param name="maxRetries">Maximum retry attempts</param>
    /// <param name="retryDelay">Delay between retries</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder WithErrorHandling(
        bool continueOnError = false,
        int maxRetries = 0,
        TimeSpan? retryDelay = null)
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Cannot configure error handling without adding an agent first");
        }

        _steps[^1].ContinueOnError = continueOnError;
        _steps[^1].MaxRetries = maxRetries;
        _steps[^1].RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);

        return this;
    }

    /// <summary>
    /// Set timeout for the last added agent
    /// </summary>
    /// <param name="timeout">Timeout duration</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder WithTimeout(TimeSpan timeout)
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Cannot set timeout without adding an agent first");
        }

        _steps[^1].Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Add multiple agents to execute in parallel
    /// </summary>
    /// <param name="agentNames">Names of agents to execute in parallel</param>
    /// <param name="executionOptions">Parallel execution options</param>
    /// <param name="collaborationStrategy">Strategy for aggregating results</param>
    /// <param name="collaborationOptions">Collaboration strategy options</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder AddParallelAgents(
        string[] agentNames,
        ParallelExecutionOptions? executionOptions = null,
        ICollaborationStrategy? collaborationStrategy = null,
        CollaborationOptions? collaborationOptions = null)
    {
        if (agentNames == null || agentNames.Length == 0)
        {
            throw new ArgumentException("At least one agent name is required for parallel execution", nameof(agentNames));
        }

        var parallelStep = new ParallelPipelineStep
        {
            AgentNames = agentNames.ToList(),
            ExecutionOptions = executionOptions ?? new ParallelExecutionOptions(),
            CollaborationStrategy = collaborationStrategy,
            CollaborationOptions = collaborationOptions ?? new CollaborationOptions()
        };

        _parallelSteps.Add(parallelStep);
        return this;
    }

    /// <summary>
    /// Add multiple agents to execute in parallel with configuration
    /// </summary>
    /// <param name="agentNames">Names of agents to execute in parallel</param>
    /// <param name="configure">Configuration action for parallel step</param>
    /// <returns>Builder for chaining</returns>
    public AgentPipelineBuilder AddParallelAgents(
        string[] agentNames,
        Action<ParallelPipelineStepBuilder> configure)
    {
        if (agentNames == null || agentNames.Length == 0)
        {
            throw new ArgumentException("At least one agent name is required for parallel execution", nameof(agentNames));
        }

        var parallelStep = new ParallelPipelineStep
        {
            AgentNames = agentNames.ToList()
        };

        var stepBuilder = new ParallelPipelineStepBuilder(parallelStep);
        configure(stepBuilder);

        _parallelSteps.Add(parallelStep);
        return this;
    }

    /// <summary>
    /// Build the pipeline
    /// </summary>
    /// <returns>Configured pipeline</returns>
    public IAgentPipeline Build()
    {
        if (_steps.Count == 0 && _parallelSteps.Count == 0)
        {
            throw new InvalidOperationException("Pipeline must have at least one step");
        }

        var pipeline = new AgentPipeline(_name, _orchestrator);

        foreach (var step in _steps)
        {
            pipeline.AddStep(step);
        }

        foreach (var parallelStep in _parallelSteps)
        {
            pipeline.AddParallelStep(parallelStep);
        }

        return pipeline;
    }
}

/// <summary>
/// Builder for configuring individual pipeline steps
/// </summary>
public class PipelineStepBuilder
{
    private readonly PipelineStep _step;

    internal PipelineStepBuilder(PipelineStep step)
    {
        _step = step;
    }

    /// <summary>
    /// Set input transformer for this step
    /// </summary>
    public PipelineStepBuilder WithInputTransformer(Func<PipelineContext, string> transformer)
    {
        _step.InputTransformer = transformer;
        return this;
    }

    /// <summary>
    /// Set output transformer for this step
    /// </summary>
    public PipelineStepBuilder WithOutputTransformer(Func<PipelineContext, string, string> transformer)
    {
        _step.OutputTransformer = transformer;
        return this;
    }

    /// <summary>
    /// Set condition for this step
    /// </summary>
    public PipelineStepBuilder WithCondition(Func<PipelineContext, bool> condition)
    {
        _step.Condition = condition;
        return this;
    }

    /// <summary>
    /// Configure error handling for this step
    /// </summary>
    public PipelineStepBuilder WithErrorHandling(
        bool continueOnError = false,
        int maxRetries = 0,
        TimeSpan? retryDelay = null)
    {
        _step.ContinueOnError = continueOnError;
        _step.MaxRetries = maxRetries;
        _step.RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        return this;
    }

    /// <summary>
    /// Set timeout for this step
    /// </summary>
    public PipelineStepBuilder WithTimeout(TimeSpan timeout)
    {
        _step.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Add metadata to this step
    /// </summary>
    public PipelineStepBuilder WithMetadata(string key, object value)
    {
        _step.Metadata[key] = value;
        return this;
    }
}

/// <summary>
/// Builder for configuring parallel pipeline steps
/// </summary>
public class ParallelPipelineStepBuilder
{
    private readonly ParallelPipelineStep _step;

    internal ParallelPipelineStepBuilder(ParallelPipelineStep step)
    {
        _step = step;
    }

    /// <summary>
    /// Configure parallel execution options
    /// </summary>
    public ParallelPipelineStepBuilder WithExecutionOptions(Action<ParallelExecutionOptions> configure)
    {
        configure(_step.ExecutionOptions);
        return this;
    }

    /// <summary>
    /// Set the collaboration strategy for aggregating results
    /// </summary>
    public ParallelPipelineStepBuilder WithCollaborationStrategy(
        ICollaborationStrategy strategy,
        Action<CollaborationOptions>? configureOptions = null)
    {
        _step.CollaborationStrategy = strategy;
        configureOptions?.Invoke(_step.CollaborationOptions);
        return this;
    }

    /// <summary>
    /// Use voting strategy for result aggregation
    /// </summary>
    public ParallelPipelineStepBuilder WithVoting(Action<CollaborationOptions>? configureOptions = null)
    {
        _step.CollaborationStrategy = new CollaborationStrategies.VotingStrategy(_step.CollaborationOptions);
        configureOptions?.Invoke(_step.CollaborationOptions);
        return this;
    }

    /// <summary>
    /// Use best-of-N strategy for result aggregation
    /// </summary>
    public ParallelPipelineStepBuilder WithBestOfN(
        Func<PipelineStepResult, double> scoringFunction,
        Action<CollaborationOptions>? configureOptions = null)
    {
        _step.CollaborationStrategy = new CollaborationStrategies.BestOfNStrategy(
            scoringFunction,
            _step.CollaborationOptions);
        configureOptions?.Invoke(_step.CollaborationOptions);
        return this;
    }

    /// <summary>
    /// Use ensemble strategy for result aggregation
    /// </summary>
    public ParallelPipelineStepBuilder WithEnsemble(
        Func<IReadOnlyList<PipelineStepResult>, Task<string>> combinerFunction,
        Action<CollaborationOptions>? configureOptions = null)
    {
        _step.CollaborationStrategy = new CollaborationStrategies.EnsembleStrategy(
            combinerFunction,
            _step.CollaborationOptions);
        configureOptions?.Invoke(_step.CollaborationOptions);
        return this;
    }

    /// <summary>
    /// Use first-success strategy for result aggregation
    /// </summary>
    public ParallelPipelineStepBuilder WithFirstSuccess(
        Func<PipelineStepResult, bool>? validationFunction = null,
        Action<CollaborationOptions>? configureOptions = null)
    {
        _step.CollaborationStrategy = new CollaborationStrategies.FirstSuccessStrategy(
            validationFunction,
            _step.CollaborationOptions);
        configureOptions?.Invoke(_step.CollaborationOptions);
        return this;
    }

    /// <summary>
    /// Set input transformer for parallel step
    /// </summary>
    public ParallelPipelineStepBuilder WithInputTransformer(Func<PipelineContext, string> transformer)
    {
        _step.InputTransformer = transformer;
        return this;
    }

    /// <summary>
    /// Set output transformer for parallel step
    /// </summary>
    public ParallelPipelineStepBuilder WithOutputTransformer(Func<PipelineContext, string, string> transformer)
    {
        _step.OutputTransformer = transformer;
        return this;
    }

    /// <summary>
    /// Set condition for parallel step
    /// </summary>
    public ParallelPipelineStepBuilder WithCondition(Func<PipelineContext, bool> condition)
    {
        _step.Condition = condition;
        return this;
    }

    /// <summary>
    /// Add metadata to parallel step
    /// </summary>
    public ParallelPipelineStepBuilder WithMetadata(string key, object value)
    {
        _step.Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Set maximum degree of parallelism
    /// </summary>
    public ParallelPipelineStepBuilder WithMaxDegreeOfParallelism(int maxDegree)
    {
        _step.ExecutionOptions.MaxDegreeOfParallelism = maxDegree;
        return this;
    }

    /// <summary>
    /// Set overall timeout for parallel execution
    /// </summary>
    public ParallelPipelineStepBuilder WithTimeout(TimeSpan timeout)
    {
        _step.ExecutionOptions.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Set per-agent timeout
    /// </summary>
    public ParallelPipelineStepBuilder WithPerAgentTimeout(TimeSpan timeout)
    {
        _step.ExecutionOptions.PerAgentTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Set failure policy
    /// </summary>
    public ParallelPipelineStepBuilder WithFailurePolicy(ParallelFailurePolicy policy)
    {
        _step.ExecutionOptions.FailurePolicy = policy;
        return this;
    }
}
