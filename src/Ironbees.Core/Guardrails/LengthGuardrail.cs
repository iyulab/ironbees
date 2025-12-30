// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Guardrails;

/// <summary>
/// A guardrail that validates content length.
/// </summary>
/// <remarks>
/// <para>
/// This guardrail blocks content that exceeds configured length limits.
/// Common use cases include:
/// - DoS prevention (limiting very long inputs)
/// - Cost control (limiting token-heavy requests)
/// - UI constraints (character limits)
/// - API compliance (payload size limits)
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var guardrail = new LengthGuardrail(new LengthGuardrailOptions
/// {
///     Name = "Length-Limiter",
///     MaxInputLength = 10000,
///     MaxOutputLength = 50000
/// });
/// </code>
/// </example>
public sealed class LengthGuardrail : IContentGuardrail
{
    private readonly LengthGuardrailOptions _options;

    /// <inheritdoc />
    public string Name => _options.Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="LengthGuardrail"/> class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public LengthGuardrail(LengthGuardrailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LengthGuardrail"/> class with default options.
    /// </summary>
    /// <param name="maxInputLength">Maximum allowed input length.</param>
    /// <param name="maxOutputLength">Maximum allowed output length.</param>
    public LengthGuardrail(int? maxInputLength = null, int? maxOutputLength = null)
        : this(new LengthGuardrailOptions
        {
            MaxInputLength = maxInputLength,
            MaxOutputLength = maxOutputLength
        })
    {
    }

    /// <inheritdoc />
    public Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        var result = ValidateLength(
            input,
            _options.MinInputLength,
            _options.MaxInputLength,
            "input");

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        var result = ValidateLength(
            output,
            _options.MinOutputLength,
            _options.MaxOutputLength,
            "output");

        return Task.FromResult(result);
    }

    private GuardrailResult ValidateLength(string content, int? minLength, int? maxLength, string contentType)
    {
        var length = content?.Length ?? 0;
        var violations = new List<GuardrailViolation>();

        if (minLength.HasValue && length < minLength.Value)
        {
            violations.Add(GuardrailViolation.Create(
                "LengthTooShort",
                $"Content {contentType} length ({length}) is below minimum ({minLength.Value})"));
        }

        if (maxLength.HasValue && length > maxLength.Value)
        {
            violations.Add(new GuardrailViolation
            {
                ViolationType = "LengthExceeded",
                Description = $"Content {contentType} length ({length}) exceeds maximum ({maxLength.Value})",
                Severity = ViolationSeverity.High,
                Context = new Dictionary<string, object>
                {
                    ["ActualLength"] = length,
                    ["MaxLength"] = maxLength.Value,
                    ["ExcessLength"] = length - maxLength.Value
                }
            });
        }

        if (violations.Count > 0)
        {
            return GuardrailResult.Blocked(
                Name,
                $"Content {contentType} length validation failed",
                violations);
        }

        return GuardrailResult.Allowed(Name);
    }
}

/// <summary>
/// Configuration options for <see cref="LengthGuardrail"/>.
/// </summary>
public sealed class LengthGuardrailOptions
{
    /// <summary>
    /// Gets or sets the name of this guardrail.
    /// </summary>
    public string Name { get; set; } = "LengthGuardrail";

    /// <summary>
    /// Gets or sets the minimum allowed input length.
    /// Null means no minimum.
    /// </summary>
    public int? MinInputLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed input length.
    /// Null means no maximum.
    /// </summary>
    public int? MaxInputLength { get; set; }

    /// <summary>
    /// Gets or sets the minimum allowed output length.
    /// Null means no minimum.
    /// </summary>
    public int? MinOutputLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed output length.
    /// Null means no maximum.
    /// </summary>
    public int? MaxOutputLength { get; set; }

    /// <summary>
    /// Creates options for limiting input length only.
    /// </summary>
    /// <param name="maxLength">Maximum input length.</param>
    /// <returns>Options configured for input length limiting.</returns>
    public static LengthGuardrailOptions InputOnly(int maxLength)
    {
        return new LengthGuardrailOptions
        {
            MaxInputLength = maxLength
        };
    }

    /// <summary>
    /// Creates options for limiting output length only.
    /// </summary>
    /// <param name="maxLength">Maximum output length.</param>
    /// <returns>Options configured for output length limiting.</returns>
    public static LengthGuardrailOptions OutputOnly(int maxLength)
    {
        return new LengthGuardrailOptions
        {
            MaxOutputLength = maxLength
        };
    }

    /// <summary>
    /// Creates options for limiting both input and output length.
    /// </summary>
    /// <param name="maxInputLength">Maximum input length.</param>
    /// <param name="maxOutputLength">Maximum output length.</param>
    /// <returns>Options configured for both input and output length limiting.</returns>
    public static LengthGuardrailOptions Both(int maxInputLength, int maxOutputLength)
    {
        return new LengthGuardrailOptions
        {
            MaxInputLength = maxInputLength,
            MaxOutputLength = maxOutputLength
        };
    }
}
