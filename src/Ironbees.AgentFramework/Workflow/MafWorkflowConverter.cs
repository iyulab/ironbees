using Ironbees.AgentMode.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Ironbees.AgentFramework.Workflow;

/// <summary>
/// Converts Ironbees YAML-based workflow definitions to Microsoft Agent Framework workflows.
/// </summary>
/// <remarks>
/// <para>
/// This implementation follows Ironbees' Thin Wrapper philosophy:
/// - YAML parsing and agent loading remain in Ironbees (core differentiation)
/// - Workflow execution is delegated to MAF (avoid reimplementation)
/// </para>
/// <para>
/// Current implementation focuses on sequential and parallel workflows.
/// Advanced features (HumanGate, conditional transitions) will be added incrementally.
/// </para>
/// </remarks>
public sealed partial class MafWorkflowConverter : IWorkflowConverter
{
    private readonly ILogger<MafWorkflowConverter>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MafWorkflowConverter"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public MafWorkflowConverter(ILogger<MafWorkflowConverter>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Microsoft.Agents.AI.Workflows.Workflow> ConvertAsync(
        WorkflowDefinition definition,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(agentResolver);

        // Validate first
        var validation = Validate(definition);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => $"{e.Code}: {e.Message}"));
            throw new WorkflowConversionException($"Workflow validation failed: {errors}");
        }

        if (_logger is not null && _logger.IsEnabled(LogLevel.Information))
        {
            LogConvertingWorkflow(_logger, definition.Name, definition.States.Count);
        }

        // Determine workflow pattern based on state types
        var pattern = DetermineWorkflowPattern(definition);

        return pattern switch
        {
            WorkflowPattern.Sequential => await BuildSequentialWorkflowAsync(
                definition, agentResolver, cancellationToken),
            WorkflowPattern.Parallel => await BuildParallelWorkflowAsync(
                definition, agentResolver, cancellationToken),
            WorkflowPattern.Mixed => await BuildMixedWorkflowAsync(
                definition, agentResolver, cancellationToken),
            _ => throw new WorkflowConversionException($"Unsupported workflow pattern: {pattern}")
        };
    }

    /// <inheritdoc />
    public WorkflowConversionValidation Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<WorkflowConversionError>();
        var warnings = new List<WorkflowConversionWarning>();
        var unsupportedFeatures = new List<string>();

        // Validate basic structure
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add(new WorkflowConversionError("WFC001", "Workflow name is required"));
        }

        if (definition.States.Count == 0)
        {
            errors.Add(new WorkflowConversionError("WFC002", "Workflow must have at least one state"));
        }

        // Validate states
        var startStates = definition.States.Where(s => s.Type == WorkflowStateType.Start).ToList();
        var terminalStates = definition.States.Where(s => s.Type == WorkflowStateType.Terminal).ToList();

        if (startStates.Count == 0)
        {
            errors.Add(new WorkflowConversionError("WFC003", "Workflow must have a Start state"));
        }
        else if (startStates.Count > 1)
        {
            errors.Add(new WorkflowConversionError("WFC004", "Workflow cannot have multiple Start states"));
        }

        if (terminalStates.Count == 0)
        {
            errors.Add(new WorkflowConversionError("WFC005", "Workflow must have at least one Terminal state"));
        }

        // Validate state references
        var stateIds = definition.States.Select(s => s.Id).ToHashSet();
        foreach (var state in definition.States)
        {
            // Validate Next reference
            if (!string.IsNullOrEmpty(state.Next) && !stateIds.Contains(state.Next))
            {
                errors.Add(new WorkflowConversionError(
                    "WFC006",
                    $"State '{state.Id}' references non-existent state '{state.Next}'",
                    state.Id));
            }

            // Validate Agent state has executor
            if (state.Type == WorkflowStateType.Agent && string.IsNullOrEmpty(state.Executor))
            {
                errors.Add(new WorkflowConversionError(
                    "WFC007",
                    $"Agent state '{state.Id}' must specify an executor",
                    state.Id));
            }

            // Validate Parallel state has executors
            if (state.Type == WorkflowStateType.Parallel && state.Executors.Count == 0)
            {
                errors.Add(new WorkflowConversionError(
                    "WFC008",
                    $"Parallel state '{state.Id}' must specify at least one executor",
                    state.Id));
            }

            // Check for unsupported features
            if (state.Type == WorkflowStateType.HumanGate)
            {
                unsupportedFeatures.Add($"HumanGate state '{state.Id}' - MAF human-in-the-loop integration pending");
            }

            if (state.Type == WorkflowStateType.Escalation)
            {
                unsupportedFeatures.Add($"Escalation state '{state.Id}' - not yet implemented");
            }

            if (state.Conditions.Count > 0)
            {
                warnings.Add(new WorkflowConversionWarning(
                    "WFC100",
                    $"State '{state.Id}' has conditional transitions - simplified to linear flow",
                    state.Id));
            }

            if (state.Trigger != null)
            {
                warnings.Add(new WorkflowConversionWarning(
                    "WFC101",
                    $"State '{state.Id}' has trigger - triggers handled by Ironbees, not MAF",
                    state.Id));
            }
        }

        return new WorkflowConversionValidation
        {
            Errors = errors,
            Warnings = warnings,
            UnsupportedFeatures = unsupportedFeatures
        };
    }

    private static WorkflowPattern DetermineWorkflowPattern(WorkflowDefinition definition)
    {
        var hasParallelStates = definition.States.Any(s => s.Type == WorkflowStateType.Parallel);
        var hasSequentialStates = definition.States.Any(s => s.Type == WorkflowStateType.Agent);

        return (hasParallelStates, hasSequentialStates) switch
        {
            (false, true) => WorkflowPattern.Sequential,
            (true, false) => WorkflowPattern.Parallel,
            (true, true) => WorkflowPattern.Mixed,
            _ => WorkflowPattern.Sequential // Default to sequential for simple workflows
        };
    }

    private async Task<Microsoft.Agents.AI.Workflows.Workflow> BuildSequentialWorkflowAsync(
        WorkflowDefinition definition,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        CancellationToken cancellationToken)
    {
        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingSequentialWorkflow(_logger, definition.Name);
        }

        // Extract agent states in execution order
        var agentStates = GetOrderedAgentStates(definition);
        var agents = new List<AIAgent>();

        foreach (var state in agentStates)
        {
            if (!string.IsNullOrEmpty(state.Executor))
            {
                var agent = await agentResolver(state.Executor, cancellationToken);
                agents.Add(agent);
                if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
                {
                    LogResolvedAgent(_logger, state.Executor, state.Id);
                }
            }
        }

        if (agents.Count == 0)
        {
            throw new WorkflowConversionException(
                "No agents found in workflow. Sequential workflow requires at least one agent.");
        }

        // Build sequential workflow using MAF
        return AgentWorkflowBuilder.BuildSequential([.. agents]);
    }

    private async Task<Microsoft.Agents.AI.Workflows.Workflow> BuildParallelWorkflowAsync(
        WorkflowDefinition definition,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        CancellationToken cancellationToken)
    {
        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingParallelWorkflow(_logger, definition.Name);
        }

        var parallelState = definition.States.First(s => s.Type == WorkflowStateType.Parallel);
        var agents = new List<AIAgent>();

        foreach (var executor in parallelState.Executors)
        {
            var agent = await agentResolver(executor, cancellationToken);
            agents.Add(agent);
            if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
            {
                LogResolvedParallelAgent(_logger, executor);
            }
        }

        if (agents.Count == 0)
        {
            throw new WorkflowConversionException(
                "No agents found in parallel state. Parallel workflow requires at least one agent.");
        }

        // Build concurrent workflow using MAF
        return AgentWorkflowBuilder.BuildConcurrent([.. agents]);
    }

    private async Task<Microsoft.Agents.AI.Workflows.Workflow> BuildMixedWorkflowAsync(
        WorkflowDefinition definition,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        CancellationToken cancellationToken)
    {
        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            LogBuildingMixedWorkflow(_logger, definition.Name);
        }

        // For mixed workflows with both sequential and parallel states,
        // we flatten to sequential execution for now.
        // MAF's WorkflowBuilder API is limited in preview - full graph support pending.

        if (_logger is not null) { LogMixedWorkflowFlattened(_logger, definition.Name); }

        var orderedStates = GetOrderedStates(definition);
        var allAgents = new List<AIAgent>();

        foreach (var state in orderedStates)
        {
            switch (state.Type)
            {
                case WorkflowStateType.Agent:
                    if (!string.IsNullOrEmpty(state.Executor))
                    {
                        var agent = await agentResolver(state.Executor, cancellationToken);
                        allAgents.Add(agent);
                        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
                        {
                            LogAddedAgentFromState(_logger, state.Executor, state.Id);
                        }
                    }
                    break;

                case WorkflowStateType.Parallel:
                    // Add all parallel executors sequentially for now
                    foreach (var executor in state.Executors)
                    {
                        var pAgent = await agentResolver(executor, cancellationToken);
                        allAgents.Add(pAgent);
                        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
                        {
                            LogAddedParallelAgentFlattened(_logger, executor, state.Id);
                        }
                    }
                    break;

                case WorkflowStateType.Start:
                case WorkflowStateType.Terminal:
                    // These are control states, no agents to add
                    break;

                default:
                    if (_logger is not null) { LogUnsupportedStateType(_logger, state.Type); }
                    break;
            }
        }

        if (allAgents.Count == 0)
        {
            throw new WorkflowConversionException(
                "No agents found in mixed workflow. Workflow requires at least one agent.");
        }

        // Build as sequential workflow using MAF
        return AgentWorkflowBuilder.BuildSequential([.. allAgents]);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Converting workflow '{WorkflowName}' with {StateCount} states")]
    private static partial void LogConvertingWorkflow(ILogger logger, string workflowName, int stateCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building sequential workflow for '{WorkflowName}'")]
    private static partial void LogBuildingSequentialWorkflow(ILogger logger, string workflowName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved agent '{Executor}' for state '{StateId}'")]
    private static partial void LogResolvedAgent(ILogger logger, string executor, string stateId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building parallel workflow for '{WorkflowName}'")]
    private static partial void LogBuildingParallelWorkflow(ILogger logger, string workflowName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved parallel agent '{Executor}'")]
    private static partial void LogResolvedParallelAgent(ILogger logger, string executor);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building mixed workflow for '{WorkflowName}'")]
    private static partial void LogBuildingMixedWorkflow(ILogger logger, string workflowName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mixed workflow '{WorkflowName}' contains both sequential and parallel states. Flattening to sequential execution. For true parallelism, use separate Parallel states.")]
    private static partial void LogMixedWorkflowFlattened(ILogger logger, string workflowName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added agent '{Executor}' from state '{StateId}'")]
    private static partial void LogAddedAgentFromState(ILogger logger, string executor, string stateId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added parallel agent '{Executor}' from state '{StateId}' (flattened)")]
    private static partial void LogAddedParallelAgentFlattened(ILogger logger, string executor, string stateId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "State type '{StateType}' not supported in mixed workflow, skipping")]
    private static partial void LogUnsupportedStateType(ILogger logger, WorkflowStateType stateType);

    private static List<WorkflowStateDefinition> GetOrderedAgentStates(WorkflowDefinition definition)
    {
        var result = new List<WorkflowStateDefinition>();
        var visited = new HashSet<string>();

        // Find start state
        var currentState = definition.States.FirstOrDefault(s => s.Type == WorkflowStateType.Start);
        if (currentState == null) return result;

        // Follow the chain
        while (currentState != null)
        {
            if (visited.Contains(currentState.Id))
            {
                break; // Prevent infinite loops
            }
            visited.Add(currentState.Id);

            if (currentState.Type == WorkflowStateType.Agent)
            {
                result.Add(currentState);
            }

            if (currentState.Type == WorkflowStateType.Terminal || string.IsNullOrEmpty(currentState.Next))
            {
                break;
            }

            currentState = definition.States.FirstOrDefault(s => s.Id == currentState.Next);
        }

        return result;
    }

    private static List<WorkflowStateDefinition> GetOrderedStates(WorkflowDefinition definition)
    {
        var result = new List<WorkflowStateDefinition>();
        var visited = new HashSet<string>();

        // Find start state
        var currentState = definition.States.FirstOrDefault(s => s.Type == WorkflowStateType.Start);

        // Follow the chain
        while (currentState != null)
        {
            if (visited.Contains(currentState.Id))
            {
                break; // Prevent infinite loops
            }
            visited.Add(currentState.Id);

            result.Add(currentState);

            if (currentState.Type == WorkflowStateType.Terminal || string.IsNullOrEmpty(currentState.Next))
            {
                break;
            }

            currentState = definition.States.FirstOrDefault(s => s.Id == currentState.Next);
        }

        return result;
    }

    private enum WorkflowPattern
    {
        Sequential,
        Parallel,
        Mixed
    }
}

/// <summary>
/// Exception thrown when workflow conversion fails.
/// </summary>
public sealed class WorkflowConversionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowConversionException"/> class.
    /// </summary>
    public WorkflowConversionException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowConversionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WorkflowConversionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowConversionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public WorkflowConversionException(string message, Exception innerException)
        : base(message, innerException) { }
}
