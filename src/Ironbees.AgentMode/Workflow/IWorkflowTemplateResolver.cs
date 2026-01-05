// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Goals;

namespace Ironbees.AgentMode.Workflow;

/// <summary>
/// Interface for resolving workflow templates with parameter substitution.
/// Templates support placeholders like {{goal.id}}, {{goal.maxIterations}}, etc.
/// </summary>
/// <remarks>
/// Thin Wrapper Philosophy:
/// - Declaration in Ironbees (workflow templates with parameters)
/// - Execution delegated to MAF (resolved WorkflowDefinition)
///
/// Template Format:
/// <code>
/// name: "{{goal.id}}-workflow"
/// version: "{{goal.version}}"
/// settings:
///   defaultMaxIterations: {{goal.constraints.maxIterations}}
/// states:
///   - id: START
///     type: start
///     next: EXECUTE
///   - id: EXECUTE
///     type: agent
///     executor: "{{parameters.executor}}"
///     next: EVALUATE
/// </code>
/// </remarks>
public interface IWorkflowTemplateResolver
{
    /// <summary>
    /// Resolves a workflow template using the provided goal definition.
    /// </summary>
    /// <param name="templateName">Name of the template (without path/extension).</param>
    /// <param name="goal">Goal definition providing parameter values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved workflow definition with parameters substituted.</returns>
    /// <exception cref="WorkflowTemplateNotFoundException">When template is not found.</exception>
    /// <exception cref="WorkflowTemplateResolutionException">When parameter substitution fails.</exception>
    Task<WorkflowDefinition> ResolveAsync(
        string templateName,
        GoalDefinition goal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a workflow template using custom parameters.
    /// </summary>
    /// <param name="templateName">Name of the template (without path/extension).</param>
    /// <param name="parameters">Dictionary of parameter values for substitution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved workflow definition with parameters substituted.</returns>
    Task<WorkflowDefinition> ResolveAsync(
        string templateName,
        IDictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a workflow template exists.
    /// </summary>
    /// <param name="templateName">Name of the template to check.</param>
    /// <returns>True if the template exists.</returns>
    bool TemplateExists(string templateName);

    /// <summary>
    /// Gets all available template names.
    /// </summary>
    /// <returns>List of template names.</returns>
    IReadOnlyList<string> GetAvailableTemplates();

    /// <summary>
    /// Validates a workflow template for correct syntax and parameter references.
    /// </summary>
    /// <param name="templateName">Name of the template to validate.</param>
    /// <returns>Validation result.</returns>
    WorkflowTemplateValidationResult ValidateTemplate(string templateName);
}

/// <summary>
/// Result of workflow template validation.
/// </summary>
public sealed record WorkflowTemplateValidationResult
{
    /// <summary>
    /// Whether the template is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Template name that was validated.
    /// </summary>
    public required string TemplateName { get; init; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// List of parameter placeholders found in the template.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static WorkflowTemplateValidationResult Success(
        string templateName,
        IEnumerable<string> parameters) => new()
    {
        TemplateName = templateName,
        Parameters = parameters.ToList()
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static WorkflowTemplateValidationResult Failure(
        string templateName,
        IEnumerable<string> errors) => new()
    {
        TemplateName = templateName,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Exception thrown when a workflow template is not found.
/// </summary>
public class WorkflowTemplateNotFoundException : Exception
{
    /// <summary>
    /// The template name that was not found.
    /// </summary>
    public string TemplateName { get; }

    /// <summary>
    /// The directories that were searched.
    /// </summary>
    public IReadOnlyList<string> SearchedPaths { get; }

    public WorkflowTemplateNotFoundException(string templateName, IEnumerable<string> searchedPaths)
        : base($"Workflow template '{templateName}' not found. Searched: {string.Join(", ", searchedPaths)}")
    {
        TemplateName = templateName;
        SearchedPaths = searchedPaths.ToList();
    }
}

/// <summary>
/// Exception thrown when workflow template resolution fails.
/// </summary>
public class WorkflowTemplateResolutionException : Exception
{
    /// <summary>
    /// The template name that failed to resolve.
    /// </summary>
    public string TemplateName { get; }

    /// <summary>
    /// List of unresolved parameter placeholders.
    /// </summary>
    public IReadOnlyList<string> UnresolvedParameters { get; }

    public WorkflowTemplateResolutionException(
        string templateName,
        IEnumerable<string> unresolvedParameters)
        : base($"Failed to resolve workflow template '{templateName}'. " +
               $"Unresolved parameters: {string.Join(", ", unresolvedParameters)}")
    {
        TemplateName = templateName;
        UnresolvedParameters = unresolvedParameters.ToList();
    }

    public WorkflowTemplateResolutionException(
        string templateName,
        string message,
        Exception innerException)
        : base($"Failed to resolve workflow template '{templateName}': {message}", innerException)
    {
        TemplateName = templateName;
        UnresolvedParameters = [];
    }
}
