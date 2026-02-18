using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ironbees.Core.Embeddings;

/// <summary>
/// Downloads and manages ONNX embedding models from Hugging Face.
/// Handles automatic model download, caching, and version management.
/// </summary>
public partial class ModelDownloader
{
    private readonly string _cacheDirectory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModelDownloader>? _logger;

    /// <summary>
    /// Gets the default cache directory for downloaded models.
    /// Located at: ~/.ironbees/models/ (cross-platform)
    /// </summary>
    public static string DefaultCacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ironbees",
        "models");

    /// <summary>
    /// Creates a new instance of the model downloader.
    /// </summary>
    /// <param name="cacheDirectory">Directory to cache downloaded models. Defaults to ~/.ironbees/models/</param>
    /// <param name="httpClient">HTTP client for downloads. If null, creates a new instance.</param>
    /// <param name="logger">Optional logger for download progress.</param>
    public ModelDownloader(string? cacheDirectory = null, HttpClient? httpClient = null, ILogger<ModelDownloader>? logger = null)
    {
        _cacheDirectory = cacheDirectory ?? DefaultCacheDirectory;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _logger = logger;

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Gets the local path for a model, downloading it if necessary.
    /// </summary>
    /// <param name="modelName">Model name (e.g., "all-MiniLM-L6-v2")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the local model directory</returns>
    public async Task<string> EnsureModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        var modelPath = GetModelPath(modelName);

        // Check if model already exists
        if (IsModelCached(modelPath))
        {
            return modelPath;
        }

        // Download model
        await DownloadModelAsync(modelName, modelPath, cancellationToken);
        return modelPath;
    }

    /// <summary>
    /// Gets the local path for a model (may not exist yet).
    /// </summary>
    public string GetModelPath(string modelName)
    {
        return Path.Combine(_cacheDirectory, modelName);
    }

    /// <summary>
    /// Clears the model cache for a specific model.
    /// </summary>
    public void ClearModelCache(string modelName)
    {
        var modelPath = GetModelPath(modelName);
        if (Directory.Exists(modelPath))
        {
            Directory.Delete(modelPath, recursive: true);
        }
    }

    /// <summary>
    /// Clears all cached models.
    /// </summary>
    public void ClearAllCache()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    private static bool IsModelCached(string modelPath)
    {
        if (!Directory.Exists(modelPath))
        {
            return false;
        }

        // Check for required files
        var requiredFiles = new[] { "model.onnx", "tokenizer.json", "config.json" };
        return requiredFiles.All(file => File.Exists(Path.Combine(modelPath, file)));
    }

    private async Task DownloadModelAsync(string modelName, string modelPath, CancellationToken cancellationToken)
    {
        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            LogDownloadingModel(_logger, modelName);
        }

        // Create model directory
        Directory.CreateDirectory(modelPath);

        try
        {
            // Determine Hugging Face repository
            var repoId = GetHuggingFaceRepoId(modelName);

            // Download required files
            await DownloadFileAsync(repoId, "model.onnx", Path.Combine(modelPath, "model.onnx"), cancellationToken);
            await DownloadFileAsync(repoId, "tokenizer.json", Path.Combine(modelPath, "tokenizer.json"), cancellationToken);
            await DownloadFileAsync(repoId, "config.json", Path.Combine(modelPath, "config.json"), cancellationToken);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                LogModelDownloaded(_logger, modelName, modelPath);
            }
        }
        catch (Exception ex)
        {
            // Clean up partial download
            if (Directory.Exists(modelPath))
            {
                Directory.Delete(modelPath, recursive: true);
            }
            throw new InvalidOperationException($"Failed to download model '{modelName}': {ex.Message}", ex);
        }
    }

    private async Task DownloadFileAsync(string repoId, string fileName, string localPath, CancellationToken cancellationToken)
    {
        var url = $"https://huggingface.co/{repoId}/resolve/main/{fileName}";

        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            LogDownloadingFile(_logger, fileName);
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;
        var lastLoggedProgress = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            // Log progress at 25% intervals to avoid excessive logging
            if (totalBytes > 0)
            {
                var progress = (int)((double)downloadedBytes / totalBytes * 100);
                if (progress >= lastLoggedProgress + 25)
                {
                    lastLoggedProgress = progress;
                    if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    {
                        LogDownloadProgress(_logger, fileName, progress, downloadedBytes / 1024.0 / 1024.0, totalBytes / 1024.0 / 1024.0);
                    }
                }
            }
        }

        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            LogDownloadCompleted(_logger, fileName, downloadedBytes / 1024.0 / 1024.0);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading model '{ModelName}' from Hugging Face...")]
    private static partial void LogDownloadingModel(ILogger logger, string modelName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Model '{ModelName}' downloaded successfully to: {ModelPath}")]
    private static partial void LogModelDownloaded(ILogger logger, string modelName, string modelPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading {FileName}...")]
    private static partial void LogDownloadingFile(ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "  {FileName}: {Progress}% ({DownloadedMB:F1} MB / {TotalMB:F1} MB)")]
    private static partial void LogDownloadProgress(ILogger logger, string fileName, int progress, double downloadedMB, double totalMB);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Completed downloading {FileName} ({SizeMB:F1} MB)")]
    private static partial void LogDownloadCompleted(ILogger logger, string fileName, double sizeMB);

    private static string GetHuggingFaceRepoId(string modelName)
    {
        // Map model names to Hugging Face repository IDs
        return modelName switch
        {
            "all-MiniLM-L6-v2" => "sentence-transformers/all-MiniLM-L6-v2",
            "all-MiniLM-L12-v2" => "sentence-transformers/all-MiniLM-L12-v2",
            _ => throw new ArgumentException($"Unknown model name: {modelName}. Supported models: all-MiniLM-L6-v2, all-MiniLM-L12-v2", nameof(modelName))
        };
    }
}
