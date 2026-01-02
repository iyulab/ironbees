using System.Threading.Channels;
using Ironbees.ClaudeCli.Models;

namespace Ironbees.ClaudeCli;

/// <summary>
/// Interface for Claude Code CLI execution
/// </summary>
public interface IClaudeCliExecutor : IAsyncDisposable
{
    /// <summary>
    /// Execute a CLI request with real-time output streaming
    /// </summary>
    /// <param name="request">The CLI request to execute</param>
    /// <param name="outputChannel">Channel to receive real-time output events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result</returns>
    Task<CliResult> ExecuteAsync(
        CliRequest request,
        ChannelWriter<CliOutputEvent> outputChannel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a CLI request and return the result (no streaming)
    /// </summary>
    /// <param name="request">The CLI request to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result</returns>
    Task<CliResult> ExecuteAsync(
        CliRequest request,
        CancellationToken cancellationToken = default);
}
