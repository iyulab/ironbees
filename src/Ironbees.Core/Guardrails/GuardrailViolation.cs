// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Guardrails;

/// <summary>
/// Represents a specific violation detected by a guardrail.
/// </summary>
public sealed class GuardrailViolation
{
    /// <summary>
    /// Gets the type of violation (e.g., "ProfanityDetected", "PIIFound", "LengthExceeded").
    /// </summary>
    public required string ViolationType { get; init; }

    /// <summary>
    /// Gets a human-readable description of the violation.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the severity level of the violation.
    /// </summary>
    public ViolationSeverity Severity { get; init; } = ViolationSeverity.Medium;

    /// <summary>
    /// Gets the position in the content where the violation was detected, if applicable.
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// Gets the length of the violating content, if applicable.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// Gets the matched content that triggered the violation, if available.
    /// </summary>
    /// <remarks>
    /// This may be redacted or truncated for security/privacy reasons.
    /// </remarks>
    public string? MatchedContent { get; init; }

    /// <summary>
    /// Gets additional context about the violation.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a new violation with the specified type and description.
    /// </summary>
    /// <param name="violationType">The type of violation.</param>
    /// <param name="description">A description of the violation.</param>
    /// <returns>A new violation instance.</returns>
    public static GuardrailViolation Create(string violationType, string description)
    {
        return new GuardrailViolation
        {
            ViolationType = violationType,
            Description = description
        };
    }

    /// <summary>
    /// Creates a new violation with position information.
    /// </summary>
    /// <param name="violationType">The type of violation.</param>
    /// <param name="description">A description of the violation.</param>
    /// <param name="position">The position in the content.</param>
    /// <param name="length">The length of the violating content.</param>
    /// <param name="matchedContent">The matched content (may be redacted).</param>
    /// <returns>A new violation instance.</returns>
    public static GuardrailViolation CreateWithPosition(
        string violationType,
        string description,
        int position,
        int length,
        string? matchedContent = null)
    {
        return new GuardrailViolation
        {
            ViolationType = violationType,
            Description = description,
            Position = position,
            Length = length,
            MatchedContent = matchedContent
        };
    }
}

/// <summary>
/// Defines the severity levels for guardrail violations.
/// </summary>
public enum ViolationSeverity
{
    /// <summary>
    /// Low severity - informational, may not require action.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - should be reviewed.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - requires attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - immediate action required.
    /// </summary>
    Critical = 3
}
