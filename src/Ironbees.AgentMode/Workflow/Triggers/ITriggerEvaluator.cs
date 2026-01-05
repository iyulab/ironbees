namespace Ironbees.AgentMode.Workflow.Triggers;

/// <summary>
/// Interface for evaluating workflow triggers.
/// Triggers determine when a workflow state can be activated.
/// </summary>
public interface ITriggerEvaluator
{
    /// <summary>
    /// Gets the trigger type this evaluator handles.
    /// </summary>
    TriggerType TriggerType { get; }

    /// <summary>
    /// Evaluates whether the trigger condition is met.
    /// </summary>
    /// <param name="trigger">Trigger definition to evaluate.</param>
    /// <param name="context">Execution context with working directory info.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if trigger condition is satisfied.</returns>
    Task<bool> EvaluateAsync(
        TriggerDefinition trigger,
        TriggerEvaluationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for trigger evaluation.
/// </summary>
public sealed record TriggerEvaluationContext
{
    /// <summary>
    /// Working directory for file-based triggers.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Current workflow state data for expression-based triggers.
    /// </summary>
    public IReadOnlyDictionary<string, object> StateData { get; init; } =
        new Dictionary<string, object>();
}

/// <summary>
/// Factory for creating trigger evaluators.
/// </summary>
public interface ITriggerEvaluatorFactory
{
    /// <summary>
    /// Gets an evaluator for the specified trigger type.
    /// </summary>
    /// <param name="triggerType">Type of trigger.</param>
    /// <returns>Appropriate trigger evaluator.</returns>
    /// <exception cref="NotSupportedException">When trigger type is not supported.</exception>
    ITriggerEvaluator GetEvaluator(TriggerType triggerType);
}
