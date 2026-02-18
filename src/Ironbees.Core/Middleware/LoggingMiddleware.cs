using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ironbees.Core.Middleware;

/// <summary>
/// Middleware that logs LLM API requests and responses for diagnostics and debugging.
/// </summary>
/// <remarks>
/// This middleware should typically be placed at the beginning of the pipeline
/// to capture the full request/response cycle including any modifications
/// from other middleware.
/// </remarks>
public sealed partial class LoggingMiddleware : DelegatingChatClient
{
    private readonly ILogger<LoggingMiddleware> _logger;
    private readonly LoggingOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="LoggingMiddleware"/>.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="options">Optional logging configuration.</param>
    /// <param name="logger">Optional logger for output.</param>
    public LoggingMiddleware(
        IChatClient innerClient,
        LoggingOptions? options = null,
        ILogger<LoggingMiddleware>? logger = null)
        : base(innerClient)
    {
        _options = options ?? new LoggingOptions();
        _logger = logger ?? NullLogger<LoggingMiddleware>.Instance;
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();

        LogRequest(requestId, messages, options);

        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);

            stopwatch.Stop();
            LogResponse(requestId, response, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError(requestId, ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();
        var updateCount = 0;

        LogRequest(requestId, messages, options, isStreaming: true);

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updateCount++;
            yield return update;
        }

        stopwatch.Stop();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogStreamingCompleted(_logger, requestId, updateCount, stopwatch.ElapsedMilliseconds);
        }
    }

    private void LogRequest(
        string requestId,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        bool isStreaming = false)
    {
        var messageList = messages.ToList();
        var messageCount = messageList.Count;
        var totalLength = messageList.Sum(m => m.Text?.Length ?? 0);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogRequest(_logger, requestId, isStreaming ? "Streaming" : "Standard", messageCount, totalLength, options?.ModelId ?? "default");
        }

        if (_options.LogMessageContent && _logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var message in messageList)
            {
                var content = message.Text ?? string.Empty;
                if (_options.MaxContentLength > 0 && content.Length > _options.MaxContentLength)
                {
                    content = string.Concat(content.AsSpan(0, _options.MaxContentLength), "...");
                }
                LogMessageContent(_logger, requestId, message.Role.Value, content);
            }
        }
    }

    private void LogResponse(string requestId, ChatResponse response, long elapsedMs)
    {
        var firstMessage = response.Messages.FirstOrDefault();
        var contentLength = firstMessage?.Text?.Length ?? 0;

        if (response.Usage != null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogResponseWithUsage(_logger, requestId, contentLength, response.Usage.InputTokenCount ?? 0, response.Usage.OutputTokenCount ?? 0, elapsedMs);
            }
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogResponseWithoutUsage(_logger, requestId, contentLength, elapsedMs);
            }
        }

        if (_options.LogMessageContent && _logger.IsEnabled(LogLevel.Debug))
        {
            var content = firstMessage?.Text ?? string.Empty;
            if (_options.MaxContentLength > 0 && content.Length > _options.MaxContentLength)
            {
                content = string.Concat(content.AsSpan(0, _options.MaxContentLength), "...");
            }
            LogResponseContent(_logger, requestId, content);
        }
    }

    private void LogError(string requestId, Exception ex, long elapsedMs)
    {
        LogRequestFailed(_logger, ex, requestId, elapsedMs, ex.Message);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[{RequestId}] Streaming completed: {UpdateCount} updates in {ElapsedMs}ms")]
    private static partial void LogStreamingCompleted(ILogger logger, string requestId, int updateCount, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{RequestId}] {RequestType} request: {MessageCount} messages, ~{TotalChars} chars, Model={ModelId}")]
    private static partial void LogRequest(ILogger logger, string requestId, string requestType, int messageCount, int totalChars, string modelId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[{RequestId}] {Role}: {Content}")]
    private static partial void LogMessageContent(ILogger logger, string requestId, string role, string content);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{RequestId}] Response: {ContentLength} chars, Input={InputTokens}, Output={OutputTokens}, Time={ElapsedMs}ms")]
    private static partial void LogResponseWithUsage(ILogger logger, string requestId, int contentLength, long inputTokens, long outputTokens, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{RequestId}] Response: {ContentLength} chars, Time={ElapsedMs}ms")]
    private static partial void LogResponseWithoutUsage(ILogger logger, string requestId, int contentLength, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[{RequestId}] Response content: {Content}")]
    private static partial void LogResponseContent(ILogger logger, string requestId, string content);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{RequestId}] Request failed after {ElapsedMs}ms: {ErrorMessage}")]
    private static partial void LogRequestFailed(ILogger logger, Exception exception, string requestId, long elapsedMs, string errorMessage);
}

/// <summary>
/// Configuration options for <see cref="LoggingMiddleware"/>.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// Whether to log message content at Debug level. Default is false.
    /// </summary>
    public bool LogMessageContent { get; set; }

    /// <summary>
    /// Maximum content length to log (0 for unlimited). Default is 500.
    /// </summary>
    public int MaxContentLength { get; set; } = 500;

    /// <summary>
    /// Creates options for development with full content logging.
    /// </summary>
    public static LoggingOptions Development => new()
    {
        LogMessageContent = true,
        MaxContentLength = 1000
    };

    /// <summary>
    /// Creates options for production with minimal logging.
    /// </summary>
    public static LoggingOptions Production => new()
    {
        LogMessageContent = false,
        MaxContentLength = 200
    };
}
