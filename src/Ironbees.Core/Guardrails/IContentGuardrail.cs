// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Guardrails;

/// <summary>
/// Defines a content guardrail for validating agent inputs and outputs.
/// </summary>
/// <remarks>
/// <para>
/// Guardrails provide a mechanism to validate content before it reaches an agent (input)
/// or before it is returned to the user (output). This enables security controls,
/// content filtering, and policy compliance.
/// </para>
/// <para>
/// Following the Thin Wrapper philosophy, Ironbees provides the interface and simple
/// implementations. Complex guardrails (ML-based, external APIs) should be implemented
/// by the user or integrated via adapters.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class ProfanityGuardrail : IContentGuardrail
/// {
///     public Task&lt;GuardrailResult&gt; ValidateInputAsync(string input, CancellationToken ct)
///     {
///         // Check for profanity...
///         return Task.FromResult(GuardrailResult.Allowed());
///     }
///
///     public Task&lt;GuardrailResult&gt; ValidateOutputAsync(string output, CancellationToken ct)
///     {
///         // Check agent response...
///         return Task.FromResult(GuardrailResult.Allowed());
///     }
/// }
/// </code>
/// </example>
public interface IContentGuardrail
{
    /// <summary>
    /// Gets the unique name of this guardrail.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates input content before it is processed by an agent.
    /// </summary>
    /// <param name="input">The input content to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GuardrailResult"/> indicating whether the input is allowed.</returns>
    Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates output content before it is returned to the user.
    /// </summary>
    /// <param name="output">The output content to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GuardrailResult"/> indicating whether the output is allowed.</returns>
    Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default);
}
