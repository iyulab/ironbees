// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Ironbees.Core.Guardrails;

/// <summary>
/// Orchestrates multiple guardrails for input and output validation.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline executes guardrails in registration order and stops on first violation
/// (fail-fast behavior) unless configured otherwise.
/// </para>
/// </remarks>
public sealed class GuardrailPipeline
{
    private readonly IReadOnlyList<IContentGuardrail> _inputGuardrails;
    private readonly IReadOnlyList<IContentGuardrail> _outputGuardrails;
    private readonly ILogger<GuardrailPipeline>? _logger;
    private readonly GuardrailPipelineOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardrailPipeline"/> class.
    /// </summary>
    /// <param name="inputGuardrails">Guardrails to apply to input content.</param>
    /// <param name="outputGuardrails">Guardrails to apply to output content.</param>
    /// <param name="options">Pipeline configuration options.</param>
    /// <param name="logger">Optional logger.</param>
    public GuardrailPipeline(
        IEnumerable<IContentGuardrail>? inputGuardrails = null,
        IEnumerable<IContentGuardrail>? outputGuardrails = null,
        GuardrailPipelineOptions? options = null,
        ILogger<GuardrailPipeline>? logger = null)
    {
        _inputGuardrails = inputGuardrails?.ToList() ?? [];
        _outputGuardrails = outputGuardrails?.ToList() ?? [];
        _options = options ?? new GuardrailPipelineOptions();
        _logger = logger;
    }

    /// <summary>
    /// Gets the number of input guardrails in the pipeline.
    /// </summary>
    public int InputGuardrailCount => _inputGuardrails.Count;

    /// <summary>
    /// Gets the number of output guardrails in the pipeline.
    /// </summary>
    public int OutputGuardrailCount => _outputGuardrails.Count;

    /// <summary>
    /// Validates input content against all registered input guardrails.
    /// </summary>
    /// <param name="input">The input content to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A combined result of all guardrail validations.</returns>
    /// <exception cref="GuardrailViolationException">
    /// Thrown when <see cref="GuardrailPipelineOptions.ThrowOnViolation"/> is true and a violation occurs.
    /// </exception>
    public async Task<GuardrailPipelineResult> ValidateInputAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(input, _inputGuardrails, "input", cancellationToken);
    }

    /// <summary>
    /// Validates output content against all registered output guardrails.
    /// </summary>
    /// <param name="output">The output content to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A combined result of all guardrail validations.</returns>
    /// <exception cref="GuardrailViolationException">
    /// Thrown when <see cref="GuardrailPipelineOptions.ThrowOnViolation"/> is true and a violation occurs.
    /// </exception>
    public async Task<GuardrailPipelineResult> ValidateOutputAsync(
        string output,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(output, _outputGuardrails, "output", cancellationToken);
    }

    private async Task<GuardrailPipelineResult> ValidateAsync(
        string content,
        IReadOnlyList<IContentGuardrail> guardrails,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (guardrails.Count == 0)
        {
            _logger?.LogDebug("No {ContentType} guardrails configured, skipping validation", contentType);
            return GuardrailPipelineResult.Empty();
        }

        var results = new List<GuardrailResult>();
        var allViolations = new List<GuardrailViolation>();

        _logger?.LogDebug(
            "Validating {ContentType} against {GuardrailCount} guardrails",
            contentType,
            guardrails.Count);

        foreach (var guardrail in guardrails)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = contentType == "input"
                    ? await guardrail.ValidateInputAsync(content, cancellationToken)
                    : await guardrail.ValidateOutputAsync(content, cancellationToken);

                results.Add(result);

                if (!result.IsAllowed)
                {
                    allViolations.AddRange(result.Violations);

                    _logger?.LogWarning(
                        "Guardrail '{GuardrailName}' blocked {ContentType}: {Reason}",
                        guardrail.Name,
                        contentType,
                        result.Reason);

                    if (_options.ThrowOnViolation)
                    {
                        throw new GuardrailViolationException(result);
                    }

                    if (_options.FailFast)
                    {
                        break;
                    }
                }
                else
                {
                    _logger?.LogDebug(
                        "Guardrail '{GuardrailName}' passed for {ContentType}",
                        guardrail.Name,
                        contentType);
                }
            }
            catch (GuardrailViolationException)
            {
                throw;
            }
            catch (Exception ex) when (!_options.ThrowOnGuardrailError)
            {
                _logger?.LogError(
                    ex,
                    "Guardrail '{GuardrailName}' threw an exception during {ContentType} validation",
                    guardrail.Name,
                    contentType);

                // Add a violation for the error
                var errorResult = GuardrailResult.Blocked(
                    guardrail.Name,
                    $"Guardrail error: {ex.Message}",
                    GuardrailViolation.Create("GuardrailError", ex.Message));

                results.Add(errorResult);
                allViolations.AddRange(errorResult.Violations);

                if (_options.FailFast)
                {
                    break;
                }
            }
        }

        return new GuardrailPipelineResult
        {
            IsAllowed = results.All(r => r.IsAllowed),
            Results = results,
            AllViolations = allViolations,
            GuardrailsExecuted = results.Count
        };
    }
}

/// <summary>
/// Configuration options for the guardrail pipeline.
/// </summary>
public sealed class GuardrailPipelineOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to stop on the first violation.
    /// Default is true.
    /// </summary>
    public bool FailFast { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception on violation.
    /// Default is false (returns result instead).
    /// </summary>
    public bool ThrowOnViolation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to throw when a guardrail itself throws an error.
    /// Default is false (logs error and continues).
    /// </summary>
    public bool ThrowOnGuardrailError { get; set; }
}

/// <summary>
/// Represents the combined result of a guardrail pipeline execution.
/// </summary>
public sealed class GuardrailPipelineResult
{
    /// <summary>
    /// Gets a value indicating whether all guardrails allowed the content.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the individual results from each guardrail.
    /// </summary>
    public IReadOnlyList<GuardrailResult> Results { get; init; } = [];

    /// <summary>
    /// Gets all violations from all guardrails.
    /// </summary>
    public IReadOnlyList<GuardrailViolation> AllViolations { get; init; } = [];

    /// <summary>
    /// Gets the number of guardrails that were executed.
    /// </summary>
    public int GuardrailsExecuted { get; init; }

    /// <summary>
    /// Creates an empty result indicating no guardrails were executed.
    /// </summary>
    /// <returns>An empty allowed result.</returns>
    public static GuardrailPipelineResult Empty()
    {
        return new GuardrailPipelineResult
        {
            IsAllowed = true,
            Results = [],
            AllViolations = [],
            GuardrailsExecuted = 0
        };
    }
}
