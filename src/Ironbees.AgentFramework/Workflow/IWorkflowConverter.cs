using Ironbees.AgentMode.Workflow;
using Microsoft.Agents.AI;

namespace Ironbees.AgentFramework.Workflow;

/// <summary>
/// Converts Ironbees YAML-based workflow definitions to Microsoft Agent Framework workflows.
/// This adapter enables Ironbees' file-system conventions while delegating orchestration to MAF.
/// </summary>
/// <remarks>
/// <para>
/// The converter follows Ironbees' Thin Wrapper philosophy:
/// - YAML parsing and agent loading remain in Ironbees (core differentiation)
/// - Workflow execution is delegated to MAF (avoid reimplementation)
/// </para>
/// <para>
/// Supported state types:
/// - Start: Entry point (maps to MAF workflow start)
/// - Agent: Single agent execution (maps to ChatClientAgent)
/// - Parallel: Concurrent agent execution (maps to MAF concurrent workflow)
/// - HumanGate: Approval gates (maps to MAF human-in-the-loop patterns)
/// - Terminal: Workflow completion (maps to MAF workflow end)
/// </para>
/// </remarks>
public interface IWorkflowConverter
{
    /// <summary>
    /// Converts an Ironbees workflow definition to a MAF workflow.
    /// </summary>
    /// <param name="definition">The YAML-parsed workflow definition.</param>
    /// <param name="agentResolver">
    /// Function to resolve agent names to actual AIAgent instances.
    /// This allows Ironbees to handle agent loading via file-system conventions.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A MAF Workflow object ready for execution.</returns>
    /// <exception cref="WorkflowConversionException">
    /// Thrown when the workflow definition cannot be converted due to validation errors
    /// or unsupported features.
    /// </exception>
    Task<Microsoft.Agents.AI.Workflows.Workflow> ConvertAsync(
        WorkflowDefinition definition,
        Func<string, CancellationToken, Task<AIAgent>> agentResolver,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a workflow definition can be converted to MAF workflow.
    /// </summary>
    /// <param name="definition">The workflow definition to validate.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    WorkflowConversionValidation Validate(WorkflowDefinition definition);
}

/// <summary>
/// Result of workflow conversion validation.
/// </summary>
public sealed class WorkflowConversionValidation
{
    /// <summary>
    /// Gets whether the workflow can be converted.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors that prevent conversion.
    /// </summary>
    public IReadOnlyList<WorkflowConversionError> Errors { get; init; } = [];

    /// <summary>
    /// Gets the list of warnings that don't prevent conversion but may indicate issues.
    /// </summary>
    public IReadOnlyList<WorkflowConversionWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Gets features in the workflow that are not yet supported by the converter.
    /// </summary>
    public IReadOnlyList<string> UnsupportedFeatures { get; init; } = [];

    /// <summary>
    /// Creates a valid result with optional warnings.
    /// </summary>
    public static WorkflowConversionValidation Valid(
        IReadOnlyList<WorkflowConversionWarning>? warnings = null) =>
        new()
        {
            Errors = [],
            Warnings = warnings ?? []
        };

    /// <summary>
    /// Creates an invalid result with errors.
    /// </summary>
    public static WorkflowConversionValidation Invalid(
        params WorkflowConversionError[] errors) =>
        new()
        {
            Errors = errors
        };
}

/// <summary>
/// A validation error that prevents workflow conversion.
/// </summary>
/// <param name="Code">Error code for programmatic handling.</param>
/// <param name="Message">Human-readable error description.</param>
/// <param name="StateId">The state ID where the error occurred, if applicable.</param>
public sealed record WorkflowConversionError(
    string Code,
    string Message,
    string? StateId = null);

/// <summary>
/// A validation warning that doesn't prevent conversion but indicates potential issues.
/// </summary>
/// <param name="Code">Warning code for programmatic handling.</param>
/// <param name="Message">Human-readable warning description.</param>
/// <param name="StateId">The state ID where the warning applies, if applicable.</param>
public sealed record WorkflowConversionWarning(
    string Code,
    string Message,
    string? StateId = null);
