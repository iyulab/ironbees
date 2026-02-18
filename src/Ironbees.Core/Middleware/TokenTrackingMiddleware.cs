using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TokenMeter;

namespace Ironbees.Core.Middleware;

/// <summary>
/// Middleware that tracks token usage from LLM API calls.
/// Implements <see cref="DelegatingChatClient"/> to intercept responses
/// and record usage statistics.
/// </summary>
/// <remarks>
/// This middleware should be added to the chat client pipeline to
/// automatically track all token consumption for cost monitoring
/// and optimization purposes.
/// </remarks>
public sealed partial class TokenTrackingMiddleware : DelegatingChatClient
{
    private readonly ITokenUsageStore _store;
    private readonly ILogger<TokenTrackingMiddleware> _logger;
    private readonly TokenTrackingOptions _options;
    private readonly ICostCalculator? _costCalculator;

    /// <summary>
    /// Initializes a new instance of <see cref="TokenTrackingMiddleware"/>.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="store">The store for recording token usage.</param>
    /// <param name="options">Optional configuration for token tracking.</param>
    /// <param name="costCalculator">Optional cost calculator from TokenMeter.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public TokenTrackingMiddleware(
        IChatClient innerClient,
        ITokenUsageStore store,
        TokenTrackingOptions? options = null,
        ICostCalculator? costCalculator = null,
        ILogger<TokenTrackingMiddleware>? logger = null)
        : base(innerClient)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new TokenTrackingOptions();
        _costCalculator = costCalculator;
        _logger = logger ?? NullLogger<TokenTrackingMiddleware>.Instance;
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        await RecordUsageAsync(response, options, cancellationToken);

        return response;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long totalInputTokens = 0;
        long totalOutputTokens = 0;
        string? modelId = null;

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            // Accumulate usage from streaming updates
            if (update.Contents != null)
            {
                foreach (var content in update.Contents)
                {
                    if (content is UsageContent usageContent)
                    {
                        totalInputTokens += usageContent.Details?.InputTokenCount ?? 0;
                        totalOutputTokens += usageContent.Details?.OutputTokenCount ?? 0;
                    }
                }
            }

            modelId ??= update.ModelId;

            yield return update;
        }

        // Record accumulated usage after streaming completes
        if (totalInputTokens > 0 || totalOutputTokens > 0)
        {
            var usage = CreateUsageRecord(
                modelId ?? options?.ModelId ?? "unknown",
                totalInputTokens,
                totalOutputTokens,
                options);

            await RecordUsageInternalAsync(usage, cancellationToken);
        }
    }

    private async Task RecordUsageAsync(
        ChatResponse response,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        if (response.Usage == null)
        {
            LogNoUsageInformation(_logger);
            return;
        }

        var usage = CreateUsageRecord(
            response.ModelId ?? options?.ModelId ?? "unknown",
            response.Usage.InputTokenCount ?? 0,
            response.Usage.OutputTokenCount ?? 0,
            options);

        await RecordUsageInternalAsync(usage, cancellationToken);
    }

    private TokenUsage CreateUsageRecord(
        string modelId,
        long inputTokens,
        long outputTokens,
        ChatOptions? options)
    {
        var metadata = new Dictionary<string, string>();

        // Extract agent name from options if available
        string? agentName = null;
        if (options?.AdditionalProperties?.TryGetValue("AgentName", out var agentNameObj) == true)
        {
            agentName = agentNameObj?.ToString();
        }

        // Extract session ID from options if available
        string? sessionId = null;
        if (options?.AdditionalProperties?.TryGetValue("SessionId", out var sessionIdObj) == true)
        {
            sessionId = sessionIdObj?.ToString();
        }

        // Add custom metadata from options
        if (_options.IncludeRequestMetadata && options?.AdditionalProperties != null)
        {
            foreach (var kvp in options.AdditionalProperties)
            {
                if (kvp.Key != "AgentName" && kvp.Key != "SessionId" && kvp.Value != null)
                {
                    metadata[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                }
            }
        }

        decimal? estimatedCost = null;
        if (_options.EnableCostTracking && _costCalculator is not null)
        {
            estimatedCost = _costCalculator.CalculateCost(modelId, (int)inputTokens, (int)outputTokens);
        }

        return new TokenUsage
        {
            ModelId = modelId,
            AgentName = agentName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            SessionId = sessionId,
            Metadata = metadata.Count > 0 ? metadata : null,
            EstimatedCost = estimatedCost
        };
    }

    private async Task RecordUsageInternalAsync(TokenUsage usage, CancellationToken cancellationToken)
    {
        try
        {
            await _store.RecordAsync(usage, cancellationToken);

            if (_options.LogUsage)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    LogTokenUsageRecorded(_logger, usage.ModelId, usage.InputTokens, usage.OutputTokens, usage.TotalTokens);
                }
            }
        }
        catch (Exception ex)
        {
            // Don't fail the request if recording fails
            LogTokenUsageRecordingFailed(_logger, ex);
        }
    }
    [LoggerMessage(Level = LogLevel.Debug, Message = "No usage information in response, skipping token tracking")]
    private static partial void LogNoUsageInformation(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Token usage recorded: Model={ModelId}, Input={InputTokens}, Output={OutputTokens}, Total={TotalTokens}")]
    private static partial void LogTokenUsageRecorded(ILogger logger, string modelId, long inputTokens, long outputTokens, long totalTokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to record token usage")]
    private static partial void LogTokenUsageRecordingFailed(ILogger logger, Exception exception);
}

/// <summary>
/// Configuration options for <see cref="TokenTrackingMiddleware"/>.
/// </summary>
public sealed class TokenTrackingOptions
{
    /// <summary>
    /// Whether to log token usage information. Default is false.
    /// </summary>
    public bool LogUsage { get; set; }

    /// <summary>
    /// Whether to include additional request metadata in usage records. Default is true.
    /// </summary>
    public bool IncludeRequestMetadata { get; set; } = true;

    /// <summary>
    /// Whether to calculate and record estimated costs using TokenMeter. Default is false.
    /// Requires an <see cref="ICostCalculator"/> to be provided.
    /// </summary>
    public bool EnableCostTracking { get; set; }
}
