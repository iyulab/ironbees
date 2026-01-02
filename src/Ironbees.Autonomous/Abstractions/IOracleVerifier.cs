using Ironbees.Autonomous.Models;

namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Oracle verifier interface for goal-based completion checking
/// </summary>
public interface IOracleVerifier
{
    /// <summary>
    /// Check if the service is properly configured
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Verify task execution result against the original goal
    /// </summary>
    /// <param name="originalPrompt">Original task prompt/goal</param>
    /// <param name="executionOutput">Output from task execution</param>
    /// <param name="config">Optional configuration override</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Oracle verification verdict</returns>
    Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Build the verification prompt for debugging/logging
    /// </summary>
    string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null);
}
