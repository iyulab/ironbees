using Ironbees.Autonomous.Models;

namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Context-aware oracle verifier with execution history and workflow metadata.
/// Extends IOracleVerifier with richer context for improved decision making.
/// </summary>
/// <remarks>
/// v0.4.0: Integrates with DefaultContextManager for automatic context tracking.
/// Provides oracle with iteration history, previous verdicts, and workflow metadata
/// to enable learning and context-aware completion verification.
/// </remarks>
public interface IContextAwareOracleVerifier : IOracleVerifier
{
    /// <summary>
    /// Verify task execution with full workflow context
    /// </summary>
    /// <param name="context">Execution context including history and metadata</param>
    /// <param name="executionOutput">Output from current task execution</param>
    /// <param name="config">Optional configuration override</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enhanced oracle verdict with context-aware insights</returns>
    Task<EnhancedOracleVerdict> VerifyWithContextAsync(
        OracleContext context,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default);
}
