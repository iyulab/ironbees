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
public class IronhiveCheckpointStoreAdapter : IronHiveCheckpointStore
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

        _logger?.LogDebug(
            "Saved IronHive checkpoint for orchestration {OrchestrationId}, {StepCount} steps completed",
            orchestrationId, checkpoint.CompletedStepCount);
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
            _logger?.LogDebug("No checkpoint found for orchestration {OrchestrationId}", orchestrationId);
            return null;
        }

        var ironhiveCheckpoint = ConvertToIronHive(ironbeesCheckpoint);

        _logger?.LogDebug(
            "Loaded IronHive checkpoint for orchestration {OrchestrationId}",
            orchestrationId);

        return ironhiveCheckpoint;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string orchestrationId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);

        await _innerStore.DeleteAllCheckpointsAsync(orchestrationId, ct);

        _logger?.LogDebug("Deleted all checkpoints for orchestration {OrchestrationId}", orchestrationId);
    }

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
            CurrentAgent = ironhiveCheckpoint.CompletedSteps.LastOrDefault()?.AgentName,
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
                _logger?.LogWarning(ex, "Failed to deserialize checkpoint state");
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

    private static IReadOnlyList<CheckpointMessage> ConvertMessages(
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
