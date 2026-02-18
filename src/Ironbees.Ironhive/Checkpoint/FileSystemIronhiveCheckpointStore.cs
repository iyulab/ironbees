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
public partial class FileSystemIronhiveCheckpointStore : ICheckpointStore
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
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogCreatedCheckpointDirectory(_logger, directory);
            }
        }

        var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogSavedCheckpoint(_logger, checkpoint.CheckpointId, orchestrationId, filePath);
        }
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
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    LogNoCheckpointsFound(_logger, orchestrationId);
                }
                return null;
            }
        }
        else
        {
            filePath = GetCheckpointFilePath(orchestrationId, checkpointId);
        }

        if (!File.Exists(filePath))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogCheckpointFileNotFound(_logger, filePath, orchestrationId);
            }
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var checkpoint = JsonSerializer.Deserialize<OrchestrationCheckpoint>(json, _jsonOptions);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogLoadedCheckpoint(_logger, checkpoint?.CheckpointId, orchestrationId);
        }

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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogListedCheckpoints(_logger, checkpoints.Count, orchestrationId);
        }

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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogDeletedCheckpoint(_logger, checkpointId, orchestrationId);
            }
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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogDeletedAllCheckpoints(_logger, orchestrationId);
            }
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created checkpoint directory: {Directory}")]
    private static partial void LogCreatedCheckpointDirectory(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved checkpoint {CheckpointId} for orchestration {OrchestrationId} at {FilePath}")]
    private static partial void LogSavedCheckpoint(ILogger logger, string checkpointId, string orchestrationId, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No checkpoints found for orchestration {OrchestrationId}")]
    private static partial void LogNoCheckpointsFound(ILogger logger, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Checkpoint file not found: {FilePath} for orchestration {OrchestrationId}")]
    private static partial void LogCheckpointFileNotFound(ILogger logger, string filePath, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded checkpoint {CheckpointId} for orchestration {OrchestrationId}")]
    private static partial void LogLoadedCheckpoint(ILogger logger, string? checkpointId, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listed {Count} checkpoints for orchestration {OrchestrationId}")]
    private static partial void LogListedCheckpoints(ILogger logger, int count, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted checkpoint {CheckpointId} for orchestration {OrchestrationId}")]
    private static partial void LogDeletedCheckpoint(ILogger logger, string checkpointId, string orchestrationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted all checkpoints for orchestration {OrchestrationId}")]
    private static partial void LogDeletedAllCheckpoints(ILogger logger, string orchestrationId);

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
