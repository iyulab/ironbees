// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Guardrails;

/// <summary>
/// Defines an audit logger for tracking guardrail validation decisions.
/// </summary>
/// <remarks>
/// <para>
/// The audit logger provides a mechanism to track all guardrail validation decisions
/// for compliance, debugging, and monitoring purposes. Implementations can log to
/// files, databases, or external services.
/// </para>
/// <para>
/// Following the Thin Wrapper philosophy, Ironbees provides the interface and a
/// simple console implementation. Production implementations should be provided
/// by the user (e.g., structured logging, SIEM integration).
/// </para>
/// </remarks>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an input validation event.
    /// </summary>
    /// <param name="entry">The audit log entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogInputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an output validation event.
    /// </summary>
    /// <param name="entry">The audit log entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogOutputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an audit log entry for a guardrail validation.
/// </summary>
public sealed class GuardrailAuditEntry
{
    /// <summary>
    /// Gets or sets a unique identifier for this audit entry.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the timestamp of the validation.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the name of the guardrail that performed the validation.
    /// </summary>
    public required string GuardrailName { get; init; }

    /// <summary>
    /// Gets or sets the type of validation (Input or Output).
    /// </summary>
    public required ValidationDirection Direction { get; init; }

    /// <summary>
    /// Gets or sets the validation result.
    /// </summary>
    public required GuardrailResult Result { get; init; }

    /// <summary>
    /// Gets or sets the content that was validated (may be truncated or redacted).
    /// </summary>
    public string? ContentPreview { get; init; }

    /// <summary>
    /// Gets or sets the length of the original content.
    /// </summary>
    public int ContentLength { get; init; }

    /// <summary>
    /// Gets or sets optional correlation ID for tracing across systems.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets optional user or session identifier.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets or sets optional agent identifier.
    /// </summary>
    public string? AgentId { get; init; }

    /// <summary>
    /// Gets or sets additional contextual metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the duration of the validation in milliseconds.
    /// </summary>
    public double? DurationMs { get; init; }
}

/// <summary>
/// Specifies the direction of content validation.
/// </summary>
public enum ValidationDirection
{
    /// <summary>
    /// Validation of input content (user to agent).
    /// </summary>
    Input,

    /// <summary>
    /// Validation of output content (agent to user).
    /// </summary>
    Output
}

/// <summary>
/// A no-op audit logger that discards all log entries.
/// </summary>
/// <remarks>
/// Use this when audit logging is not required or for testing.
/// </remarks>
public sealed class NullAuditLogger : IAuditLogger
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static NullAuditLogger Instance { get; } = new();

    /// <inheritdoc />
    public Task LogInputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task LogOutputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
