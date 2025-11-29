using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ironbees.Core.AgentDirectory;

/// <summary>
/// Represents the priority level of an agent message.
/// </summary>
public enum MessagePriority
{
    /// <summary>Low priority messages processed when idle.</summary>
    Low = 0,

    /// <summary>Normal priority for standard messages.</summary>
    Normal = 1,

    /// <summary>High priority messages processed first.</summary>
    High = 2,

    /// <summary>Critical messages that require immediate attention.</summary>
    Critical = 3
}

/// <summary>
/// Represents the status of an agent message.
/// </summary>
public enum MessageStatus
{
    /// <summary>Message is pending processing.</summary>
    Pending,

    /// <summary>Message is currently being processed.</summary>
    Processing,

    /// <summary>Message has been successfully processed.</summary>
    Completed,

    /// <summary>Message processing failed.</summary>
    Failed,

    /// <summary>Message was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Represents a message in the agent's inbox or outbox.
/// Messages follow a file-based convention for stigmergic agent collaboration.
/// </summary>
/// <remarks>
/// Message file format: {timestamp}_{id}.json
/// Example: 20250101120000_abc123.json
/// </remarks>
public sealed record AgentMessage
{
    /// <summary>
    /// Gets the unique identifier for this message.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// Gets the timestamp when this message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the name of the sender agent (null for external senders).
    /// </summary>
    public string? FromAgent { get; init; }

    /// <summary>
    /// Gets the name of the target agent.
    /// </summary>
    public required string ToAgent { get; init; }

    /// <summary>
    /// Gets the message type/action identifier.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Gets the message payload (can be any JSON-serializable object).
    /// </summary>
    public JsonElement? Payload { get; init; }

    /// <summary>
    /// Gets the message priority.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Gets the current status of this message.
    /// </summary>
    public MessageStatus Status { get; init; } = MessageStatus.Pending;

    /// <summary>
    /// Gets the correlation ID for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the reply-to address for response messages.
    /// </summary>
    public string? ReplyTo { get; init; }

    /// <summary>
    /// Gets optional metadata for this message.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Gets the time-to-live for this message (null for no expiration).
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Gets whether this message has expired based on its TTL.
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => TimeToLive.HasValue &&
                             DateTimeOffset.UtcNow > Timestamp.Add(TimeToLive.Value);

    /// <summary>
    /// Generates the file name for this message.
    /// </summary>
    /// <returns>The message file name in format: {timestamp}_{id}.json</returns>
    public string ToFileName()
    {
        return $"{Timestamp:yyyyMMddHHmmss}_{Id}.json";
    }

    /// <summary>
    /// Parses a message ID from a file name.
    /// </summary>
    /// <param name="fileName">The file name to parse.</param>
    /// <returns>The message ID, or null if the file name is invalid.</returns>
    public static string? ParseIdFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Remove extension
        var baseName = Path.GetFileNameWithoutExtension(fileName);

        // Expected format: {timestamp}_{id}
        var parts = baseName.Split('_', 2);
        return parts.Length == 2 ? parts[1] : null;
    }

    /// <summary>
    /// Serializes this message to JSON.
    /// </summary>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The JSON string representation.</returns>
    public string ToJson(JsonSerializerOptions? options = null)
    {
        options ??= DefaultJsonOptions;
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes a message from JSON.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized message.</returns>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    public static AgentMessage FromJson(string json, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        options ??= DefaultJsonOptions;
        return JsonSerializer.Deserialize<AgentMessage>(json, options)
            ?? throw new JsonException("Failed to deserialize message: result was null");
    }

    /// <summary>
    /// Creates a typed payload from the JSON payload.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized payload, or default if payload is null.</returns>
    public T? GetPayload<T>(JsonSerializerOptions? options = null)
    {
        if (Payload == null)
            return default;

        options ??= DefaultJsonOptions;
        return Payload.Value.Deserialize<T>(options);
    }

    /// <summary>
    /// Creates a new message with an updated status.
    /// </summary>
    /// <param name="status">The new status.</param>
    /// <returns>A new message instance with the updated status.</returns>
    public AgentMessage WithStatus(MessageStatus status)
    {
        return this with { Status = status };
    }

    /// <summary>
    /// Creates a reply message to this message.
    /// </summary>
    /// <param name="fromAgent">The sender agent name.</param>
    /// <param name="messageType">The reply message type.</param>
    /// <param name="payload">Optional payload for the reply.</param>
    /// <returns>A new message configured as a reply.</returns>
    public AgentMessage CreateReply(string fromAgent, string messageType, object? payload = null)
    {
        return new AgentMessage
        {
            FromAgent = fromAgent,
            ToAgent = ReplyTo ?? FromAgent ?? throw new InvalidOperationException(
                "Cannot create reply: no ReplyTo or FromAgent specified"),
            MessageType = messageType,
            Payload = payload != null
                ? JsonSerializer.SerializeToElement(payload, DefaultJsonOptions)
                : null,
            CorrelationId = CorrelationId ?? Id,
            Priority = Priority
        };
    }

    /// <summary>
    /// Default JSON serializer options for message serialization.
    /// </summary>
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
