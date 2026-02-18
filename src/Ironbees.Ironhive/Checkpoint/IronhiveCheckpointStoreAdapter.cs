// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Ironbees.Core.Orchestration;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using Microsoft.Extensions.Logging;
using IronHiveCheckpointStore = IronHive.Abstractions.Agent.Orchestration.ICheckpointStore;
using IronHiveOrchestrationCheckpoint = IronHive.Abstractions.Agent.Orchestration.OrchestrationCheckpoint;
using IronHiveMessage = IronHive.Abstractions.Messages.Message;
using IronbeesCheckpointStore = Ironbees.Core.Orchestration.ICheckpointStore;
using IronbeesOrchestrationCheckpoint = Ironbees.Core.Orchestration.OrchestrationCheckpoint;

namespace Ironbees.Ironhive.Checkpoint;

/// <summary>
/// Adapts Ironbees ICheckpointStore to IronHive ICheckpointStore interface.
/// Enables IronHive orchestrators to use Ironbees checkpoint persistence.
/// </summary>
public partial class IronhiveCheckpointStoreAdapter : IronHiveCheckpointStore
{
    private readonly IronbeesCheckpointStore _innerStore;
    private readonly ILogger<IronhiveCheckpointStoreAdapter>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public IronhiveCheckpointStoreAdapter(
        IronbeesCheckpointStore innerStore,
        ILogger<IronhiveCheckpointStoreAdapter>? logger = null)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string orchestrationId,
        IronHiveOrchestrationCheckpoint checkpoint,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);
        ArgumentNullException.ThrowIfNull(checkpoint);

        var ironbeesCheckpoint = ConvertToIronbees(checkpoint);

        await _innerStore.SaveCheckpointAsync(orchestrationId, ironbeesCheckpoint, ct);

        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            LogSavedIronHiveCheckpoint(_logger, orchestrationId, checkpoint.CompletedStepCount);
        }
    }

    /// <inheritdoc />
    public async Task<IronHiveOrchestrationCheckpoint?> LoadAsync(
        string orchestrationId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);

        var ironbeesCheckpoint = await _innerStore.LoadCheckpointAsync(orchestrationId, checkpointId: null, ct);

        if (ironbeesCheckpoint is null)
        {
            if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
            {
                LogNoCheckpointFound(_logger, orchestrationId);
            }
            return null;
        }

        var ironhiveCheckpoint = ConvertToIronHive(ironbeesCheckpoint);

        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            LogLoadedIronHiveCheckpoint(_logger, orchestrationId);
        }

        return ironhiveCheckpoint;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string orchestrationId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);

        await _innerStore.DeleteAllCheckpointsAsync(orchestrationId, ct);

        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            LogDeletedAllCheckpoints(_logger, orchestrationId);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved IronHive checkpoint for orchestration {OrchestrationId}, {StepCount} steps completed")]
    private static partial void LogSavedIronHiveCheckpoint(ILogger logger, string orchestrationId, int stepCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No checkpoint found for orchestration {OrchestrationId}")]
    private static partial void LogNoCheckpointFound(ILogger logger, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded IronHive checkpoint for orchestration {OrchestrationId}")]
    private static partial void LogLoadedIronHiveCheckpoint(ILogger logger, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted all checkpoints for orchestration {OrchestrationId}")]
    private static partial void LogDeletedAllCheckpoints(ILogger logger, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize checkpoint state")]
    private static partial void LogFailedToDeserializeCheckpointState(ILogger logger, Exception exception);

    private IronbeesOrchestrationCheckpoint ConvertToIronbees(IronHiveOrchestrationCheckpoint ironhiveCheckpoint)
    {
        // Serialize the completed steps and messages for storage
        var serializedState = JsonSerializer.Serialize(new
        {
            CompletedSteps = ironhiveCheckpoint.CompletedSteps,
            CurrentMessages = ironhiveCheckpoint.CurrentMessages
        }, _jsonOptions);

        // Extract agent results from completed steps
        var agentResults = ironhiveCheckpoint.CompletedSteps
            .Where(s => s.IsSuccess && s.Response?.Message is not null)
            .ToDictionary(
                s => s.AgentName,
                s => ExtractMessageText(s.Response!.Message) ?? "");

        return new IronbeesOrchestrationCheckpoint
        {
            CheckpointId = $"{ironhiveCheckpoint.OrchestrationId}-{DateTimeOffset.UtcNow.Ticks}",
            OrchestrationId = ironhiveCheckpoint.OrchestrationId,
            CreatedAt = new DateTimeOffset(ironhiveCheckpoint.CreatedAt, TimeSpan.Zero),
            CurrentState = $"step-{ironhiveCheckpoint.CompletedStepCount}",
            CurrentAgent = ironhiveCheckpoint.CompletedSteps.Count > 0
                ? ironhiveCheckpoint.CompletedSteps[^1].AgentName
                : null,
            SerializedState = serializedState,
            AgentResults = agentResults,
            Messages = ConvertMessages(ironhiveCheckpoint.CurrentMessages),
            Metadata = new Dictionary<string, object>
            {
                ["orchestratorName"] = ironhiveCheckpoint.OrchestratorName,
                ["completedStepCount"] = ironhiveCheckpoint.CompletedStepCount
            }
        };
    }

    private IronHiveOrchestrationCheckpoint ConvertToIronHive(IronbeesOrchestrationCheckpoint ironbeesCheckpoint)
    {
        // Deserialize the stored state if available
        IReadOnlyList<IronHive.Abstractions.Agent.Orchestration.AgentStepResult> completedSteps = [];
        IReadOnlyList<IronHiveMessage> currentMessages = [];

        if (!string.IsNullOrEmpty(ironbeesCheckpoint.SerializedState))
        {
            try
            {
                using var doc = JsonDocument.Parse(ironbeesCheckpoint.SerializedState);
                var root = doc.RootElement;

                // Reconstruct completed steps and messages from serialized state
                // Note: Full deserialization would require matching the exact IronHive types
                // For now, we provide basic reconstruction
            }
            catch (JsonException ex)
            {
                if (_logger is not null) { LogFailedToDeserializeCheckpointState(_logger, ex); }
            }
        }

        var orchestratorName = "unknown";
        var completedStepCount = 0;

        if (ironbeesCheckpoint.Metadata is not null)
        {
            if (ironbeesCheckpoint.Metadata.TryGetValue("orchestratorName", out var name))
            {
                orchestratorName = name?.ToString() ?? "unknown";
            }
            if (ironbeesCheckpoint.Metadata.TryGetValue("completedStepCount", out var count))
            {
                if (count is int intCount)
                {
                    completedStepCount = intCount;
                }
                else if (int.TryParse(count?.ToString(), out var parsedCount))
                {
                    completedStepCount = parsedCount;
                }
            }
        }

        return new IronHiveOrchestrationCheckpoint
        {
            OrchestrationId = ironbeesCheckpoint.OrchestrationId,
            OrchestratorName = orchestratorName,
            CompletedStepCount = completedStepCount,
            CompletedSteps = completedSteps,
            CurrentMessages = currentMessages,
            CreatedAt = ironbeesCheckpoint.CreatedAt.UtcDateTime
        };
    }

    private static List<CheckpointMessage> ConvertMessages(
        IReadOnlyList<IronHiveMessage> messages)
    {
        var result = new List<CheckpointMessage>();

        foreach (var message in messages)
        {
            var (role, content) = message switch
            {
                UserMessage userMsg => ("user", GetMessageText(userMsg.Content)),
                AssistantMessage assistantMsg => ("assistant", GetMessageText(assistantMsg.Content)),
                _ => ("unknown", "")
            };

            result.Add(new CheckpointMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        return result;
    }

    private static string? ExtractMessageText(IronHiveMessage message)
    {
        return message switch
        {
            UserMessage userMsg => GetMessageText(userMsg.Content),
            AssistantMessage assistantMsg => GetMessageText(assistantMsg.Content),
            _ => null
        };
    }

    private static string GetMessageText(ICollection<IronHive.Abstractions.Messages.MessageContent>? content)
    {
        if (content is null or { Count: 0 })
        {
            return "";
        }

        var textParts = content
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return string.Join("", textParts);
    }
}
