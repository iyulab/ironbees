// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Guardrails;

/// <summary>
/// Represents the result of a guardrail validation.
/// </summary>
public sealed class GuardrailResult
{
    /// <summary>
    /// Gets a value indicating whether the content is allowed to proceed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the name of the guardrail that produced this result.
    /// </summary>
    public string? GuardrailName { get; init; }

    /// <summary>
    /// Gets a human-readable reason for the decision.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the violations detected by the guardrail, if any.
    /// </summary>
    public IReadOnlyList<GuardrailViolation> Violations { get; init; } = [];

    /// <summary>
    /// Gets additional metadata about the validation.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the timestamp when the validation was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a result indicating the content is allowed.
    /// </summary>
    /// <param name="guardrailName">The name of the guardrail.</param>
    /// <returns>An allowed result.</returns>
    public static GuardrailResult Allowed(string? guardrailName = null)
    {
        return new GuardrailResult
        {
            IsAllowed = true,
            GuardrailName = guardrailName,
            Reason = "Content passed validation"
        };
    }

    /// <summary>
    /// Creates a result indicating the content is blocked.
    /// </summary>
    /// <param name="guardrailName">The name of the guardrail.</param>
    /// <param name="reason">The reason for blocking.</param>
    /// <param name="violations">The violations detected.</param>
    /// <returns>A blocked result.</returns>
    public static GuardrailResult Blocked(
        string? guardrailName,
        string reason,
        params GuardrailViolation[] violations)
    {
        return new GuardrailResult
        {
            IsAllowed = false,
            GuardrailName = guardrailName,
            Reason = reason,
            Violations = violations
        };
    }

    /// <summary>
    /// Creates a result indicating the content is blocked.
    /// </summary>
    /// <param name="guardrailName">The name of the guardrail.</param>
    /// <param name="reason">The reason for blocking.</param>
    /// <param name="violations">The violations detected.</param>
    /// <returns>A blocked result.</returns>
    public static GuardrailResult Blocked(
        string? guardrailName,
        string reason,
        IEnumerable<GuardrailViolation> violations)
    {
        return new GuardrailResult
        {
            IsAllowed = false,
            GuardrailName = guardrailName,
            Reason = reason,
            Violations = violations.ToList()
        };
    }
}
