using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using ChildProcessGuard;
using Ironbees.ClaudeCli.Models;

namespace Ironbees.ClaudeCli;

/// <summary>
/// Claude Code CLI executor using --print mode for non-interactive execution
/// </summary>
public sealed class ClaudeCliExecutor : IClaudeCliExecutor
{
    private readonly ProcessGuardian _guardian;
    private readonly string _claudePath;
    private bool _disposed;

    /// <summary>
    /// Create a new Claude CLI executor
    /// </summary>
    /// <param name="claudePath">Path to claude CLI (default: "claude")</param>
    /// <param name="options">Optional process guardian options</param>
    public ClaudeCliExecutor(string? claudePath = null, ProcessGuardianOptions? options = null)
    {
        _claudePath = claudePath ?? "claude";
        _guardian = new ProcessGuardian(options ?? new ProcessGuardianOptions
        {
            AutoCleanupDisposedProcesses = true,
            ForceKillOnTimeout = true,
            ProcessKillTimeout = TimeSpan.FromSeconds(30)
        });
    }

    /// <inheritdoc/>
    public async Task<CliResult> ExecuteAsync(
        CliRequest request,
        ChannelWriter<CliOutputEvent> outputChannel,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        int exitCode = -1;

        try
        {
            await WriteSystemEventAsync(outputChannel, request.RequestId,
                $"Starting execution in {request.WorkingDirectory}", cancellationToken);

            var startInfo = CreateStartInfo(request);
            var process = _guardian.StartProcessWithStartInfo(startInfo);

            await WriteSystemEventAsync(outputChannel, request.RequestId,
                $"Claude CLI started (PID: {process.Id})", cancellationToken);

            // Create linked cancellation for timeout
            using var cts = request.Timeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;
            cts?.CancelAfter(request.Timeout!.Value);
            var effectiveToken = cts?.Token ?? cancellationToken;

            // Stream output tasks
            var stdoutTask = ReadStreamAsync(
                process.StandardOutput,
                request.RequestId,
                CliOutputType.Stdout,
                outputChannel,
                stdout,
                effectiveToken);

            var stderrTask = ReadStreamAsync(
                process.StandardError,
                request.RequestId,
                CliOutputType.Stderr,
                outputChannel,
                stderr,
                effectiveToken);

            // Kill process on cancellation
            using var registration = effectiveToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { /* Ignore */ }
            });

            await process.WaitForExitAsync(effectiveToken);
            exitCode = process.ExitCode;

            await Task.WhenAll(stdoutTask, stderrTask);

            await WriteSystemEventAsync(outputChannel, request.RequestId,
                $"Execution completed with exit code: {exitCode}", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await WriteSystemEventAsync(outputChannel, request.RequestId,
                "Execution cancelled", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await WriteSystemEventAsync(outputChannel, request.RequestId,
                $"Execution failed: {ex.Message}", CancellationToken.None);
            stderr.AppendLine($"Error: {ex.Message}");
        }

        var completedAt = DateTimeOffset.UtcNow;

        return new CliResult
        {
            RequestId = request.RequestId,
            Success = exitCode == 0,
            ExitCode = exitCode,
            Output = stdout.ToString(),
            ErrorOutput = stderr.ToString(),
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Duration = completedAt - startedAt
        };
    }

    /// <inheritdoc/>
    public async Task<CliResult> ExecuteAsync(
        CliRequest request,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<CliOutputEvent>();

        // Start a background task to drain the channel
        _ = Task.Run(async () =>
        {
            await foreach (var _ in channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                // Discard output events
            }
        }, CancellationToken.None);

        try
        {
            return await ExecuteAsync(request, channel.Writer, cancellationToken);
        }
        finally
        {
            channel.Writer.Complete();
        }
    }

    private ProcessStartInfo CreateStartInfo(CliRequest request)
    {
        var args = new List<string> { "--print", request.Prompt };
        args.AddRange(request.AdditionalArgs);

        var claudeArgs = string.Join(" ", args.Select(EscapeArg));

        // Windows: Use cmd.exe for .cmd files
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {_claudePath} {claudeArgs}",
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        return new ProcessStartInfo
        {
            FileName = _claudePath,
            Arguments = claudeArgs,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private static string EscapeArg(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (!arg.Contains(' ') && !arg.Contains('"')) return arg;
        return $"\"{arg.Replace("\"", "\\\"")}\"";
    }

    private static async Task WriteSystemEventAsync(
        ChannelWriter<CliOutputEvent> channel,
        string requestId,
        string content,
        CancellationToken cancellationToken)
    {
        await channel.WriteAsync(new CliOutputEvent
        {
            RequestId = requestId,
            Type = CliOutputType.System,
            Content = content
        }, cancellationToken);
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        string requestId,
        CliOutputType outputType,
        ChannelWriter<CliOutputEvent> channel,
        StringBuilder buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                buffer.AppendLine(line);
                await channel.WriteAsync(new CliOutputEvent
                {
                    RequestId = requestId,
                    Type = outputType,
                    Content = line
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _guardian.DisposeAsync();
    }
}
