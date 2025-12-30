// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Guardrails;

/// <summary>
/// Exception thrown when content violates a guardrail policy.
/// </summary>
[Serializable]
public class GuardrailViolationException : Exception
{
    /// <summary>
    /// Gets the guardrail result that caused this exception.
    /// </summary>
    public GuardrailResult Result { get; }

    /// <summary>
    /// Gets the name of the guardrail that was violated.
    /// </summary>
    public string? GuardrailName => Result.GuardrailName;

    /// <summary>
    /// Gets the violations that caused this exception.
    /// </summary>
    public IReadOnlyList<GuardrailViolation> Violations => Result.Violations;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardrailViolationException"/> class.
    /// </summary>
    /// <param name="result">The guardrail result that caused this exception.</param>
    public GuardrailViolationException(GuardrailResult result)
        : base(FormatMessage(result))
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardrailViolationException"/> class.
    /// </summary>
    /// <param name="result">The guardrail result that caused this exception.</param>
    /// <param name="innerException">The inner exception.</param>
    public GuardrailViolationException(GuardrailResult result, Exception innerException)
        : base(FormatMessage(result), innerException)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    private static string FormatMessage(GuardrailResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var guardrailPart = string.IsNullOrEmpty(result.GuardrailName)
            ? "Guardrail"
            : $"Guardrail '{result.GuardrailName}'";

        var violationCount = result.Violations.Count;
        var violationPart = violationCount > 0
            ? $" ({violationCount} violation{(violationCount > 1 ? "s" : "")} detected)"
            : "";

        return $"{guardrailPart} blocked content: {result.Reason ?? "No reason provided"}{violationPart}";
    }
}
