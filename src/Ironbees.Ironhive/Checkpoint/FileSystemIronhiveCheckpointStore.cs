// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Ironbees.Core.Orchestration;
using Microsoft.Extensions.Logging;

namespace Ironbees.Ironhive.Checkpoint;

/// <summary>
/// File system implementation of IronHive's ICheckpointStore.
/// Stores orchestration checkpoints in the .ironbees/checkpoints directory.
/// </summary>
public class FileSystemIronhiveCheckpointStore : ICheckpointStore
{
    private const string DefaultCheckpointDirectory = ".ironbees/checkpoints";
    private readonly string _checkpointDirectory;
    private readonly ILogger<FileSystemIronhiveCheckpointStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemIronhiveCheckpointStore(ILogger<FileSystemIronhiveCheckpointStore> logger)
        : this(DefaultCheckpointDirectory, logger)
    {
    }

    public FileSystemIronhiveCheckpointStore(
        string checkpointDirectory,
        ILogger<FileSystemIronhiveCheckpointStore> logger)
    {
        _checkpointDirectory = checkpointDirectory ?? throw new ArgumentNullException(nameof(checkpointDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public async Task SaveCheckpointAsync(
        string orchestrationId,
        OrchestrationCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);
        ArgumentNullException.ThrowIfNull(checkpoint);

        var filePath = GetCheckpointFilePath(orchestrationId, checkpoint.CheckpointId);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("Created checkpoint directory: {Directory}", directory);
        }

        var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation(
            "Saved checkpoint {CheckpointId} for orchestration {OrchestrationId} at {FilePath}",
            checkpoint.CheckpointId, orchestrationId, filePath);
    }

    /// <inheritdoc />
    public async Task<OrchestrationCheckpoint?> LoadCheckpointAsync(
        string orchestrationId,
        string? checkpointId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);

        string? filePath;

        if (string.IsNullOrEmpty(checkpointId))
        {
            // Load the latest checkpoint
            filePath = await GetLatestCheckpointFilePathAsync(orchestrationId, cancellationToken);
            if (filePath is null)
            {
                _logger.LogDebug("No checkpoints found for orchestration {OrchestrationId}", orchestrationId);
                return null;
            }
        }
        else
        {
            filePath = GetCheckpointFilePath(orchestrationId, checkpointId);
        }

        if (!File.Exists(filePath))
        {
            _logger.LogDebug(
                "Checkpoint file not found: {FilePath} for orchestration {OrchestrationId}",
                filePath, orchestrationId);
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var checkpoint = JsonSerializer.Deserialize<OrchestrationCheckpoint>(json, _jsonOptions);

        _logger.LogInformation(
            "Loaded checkpoint {CheckpointId} for orchestration {OrchestrationId}",
            checkpoint?.CheckpointId, orchestrationId);

        return checkpoint;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrchestrationCheckpoint>> ListCheckpointsAsync(
        string orchestrationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);

        var orchestrationDirectory = GetOrchestrationDirectory(orchestrationId);

        if (!Directory.Exists(orchestrationDirectory))
        {
            return Array.Empty<OrchestrationCheckpoint>();
        }

        var checkpointFiles = Directory.GetFiles(orchestrationDirectory, "*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f));

        var checkpoints = new List<OrchestrationCheckpoint>();

        foreach (var filePath in checkpointFiles)
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var checkpoint = JsonSerializer.Deserialize<OrchestrationCheckpoint>(json, _jsonOptions);
            if (checkpoint is not null)
            {
                checkpoints.Add(checkpoint);
            }
        }

        _logger.LogDebug(
            "Listed {Count} checkpoints for orchestration {OrchestrationId}",
            checkpoints.Count, orchestrationId);

        return checkpoints;
    }

    /// <inheritdoc />
    public Task DeleteCheckpointAsync(
        string orchestrationId,
        string checkpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);
        ArgumentNullException.ThrowIfNull(checkpointId);

        var filePath = GetCheckpointFilePath(orchestrationId, checkpointId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation(
                "Deleted checkpoint {CheckpointId} for orchestration {OrchestrationId}",
                checkpointId, orchestrationId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAllCheckpointsAsync(
        string orchestrationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orchestrationId);

        var orchestrationDirectory = GetOrchestrationDirectory(orchestrationId);

        if (Directory.Exists(orchestrationDirectory))
        {
            Directory.Delete(orchestrationDirectory, recursive: true);
            _logger.LogInformation(
                "Deleted all checkpoints for orchestration {OrchestrationId}",
                orchestrationId);
        }

        return Task.CompletedTask;
    }

    private string GetOrchestrationDirectory(string orchestrationId)
    {
        var sanitizedId = SanitizeFileName(orchestrationId);
        return Path.Combine(_checkpointDirectory, sanitizedId);
    }

    private string GetCheckpointFilePath(string orchestrationId, string checkpointId)
    {
        var orchestrationDirectory = GetOrchestrationDirectory(orchestrationId);
        var sanitizedCheckpointId = SanitizeFileName(checkpointId);
        return Path.Combine(orchestrationDirectory, $"{sanitizedCheckpointId}.json");
    }

    private async Task<string?> GetLatestCheckpointFilePathAsync(
        string orchestrationId,
        CancellationToken cancellationToken)
    {
        var orchestrationDirectory = GetOrchestrationDirectory(orchestrationId);

        if (!Directory.Exists(orchestrationDirectory))
        {
            return null;
        }

        var latestFile = Directory.GetFiles(orchestrationDirectory, "*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();

        return await Task.FromResult(latestFile);
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrEmpty(sanitized) ? "default" : sanitized;
    }
}
