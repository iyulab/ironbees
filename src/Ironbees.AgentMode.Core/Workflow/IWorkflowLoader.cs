using Ironbees.AgentMode.Core.Exceptions;

namespace Ironbees.AgentMode.Core.Workflow;

/// <summary>
/// Interface for loading workflow definitions from various sources.
/// Follows the Thin Wrapper philosophy - focuses on loading configuration,
/// not implementing execution logic.
/// </summary>
public interface IWorkflowLoader
{
    /// <summary>
    /// Loads a workflow definition from a YAML file path.
    /// </summary>
    /// <param name="filePath">Path to the workflow YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed workflow definition.</returns>
    /// <exception cref="FileNotFoundException">When the file doesn't exist.</exception>
    /// <exception cref="WorkflowParseException">When YAML parsing fails.</exception>
    Task<WorkflowDefinition> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a workflow definition from YAML content string.
    /// </summary>
    /// <param name="yamlContent">YAML content string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed workflow definition.</returns>
    /// <exception cref="WorkflowParseException">When YAML parsing fails.</exception>
    Task<WorkflowDefinition> LoadFromStringAsync(
        string yamlContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a workflow definition from a stream.
    /// </summary>
    /// <param name="stream">Stream containing YAML content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed workflow definition.</returns>
    /// <exception cref="WorkflowParseException">When YAML parsing fails.</exception>
    Task<WorkflowDefinition> LoadFromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers and loads all workflows from a directory.
    /// </summary>
    /// <param name="directoryPath">Directory containing workflow YAML files.</param>
    /// <param name="searchPattern">File search pattern (default: "*.yaml").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of loaded workflow definitions.</returns>
    Task<IReadOnlyList<WorkflowDefinition>> LoadFromDirectoryAsync(
        string directoryPath,
        string searchPattern = "*.yaml",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a workflow definition for correctness.
    /// </summary>
    /// <param name="workflow">Workflow definition to validate.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    WorkflowValidationResult Validate(WorkflowDefinition workflow);
}

/// <summary>
/// Result of workflow validation.
/// </summary>
public sealed record WorkflowValidationResult
{
    /// <summary>
    /// Whether the workflow is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public IReadOnlyList<WorkflowValidationError> Errors { get; init; } = [];

    /// <summary>
    /// List of validation warnings (non-blocking).
    /// </summary>
    public IReadOnlyList<WorkflowValidationWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static WorkflowValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static WorkflowValidationResult Failed(params WorkflowValidationError[] errors) =>
        new() { Errors = errors };
}

/// <summary>
/// Represents a validation error.
/// </summary>
public sealed record WorkflowValidationError(
    string Code,
    string Message,
    string? Location = null);

/// <summary>
/// Represents a validation warning.
/// </summary>
public sealed record WorkflowValidationWarning(
    string Code,
    string Message,
    string? Location = null);
