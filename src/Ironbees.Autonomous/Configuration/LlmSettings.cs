namespace Ironbees.Autonomous.Configuration;

/// <summary>
/// LLM connection and generation settings.
/// Can be loaded from YAML configuration files.
/// </summary>
public record LlmSettings
{
    /// <summary>
    /// LLM API endpoint URL
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// API key for authentication (can reference environment variable with ${VAR_NAME})
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Model identifier
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Maximum output tokens for generation
    /// </summary>
    public int MaxOutputTokens { get; init; } = 200;

    /// <summary>
    /// Temperature for generation (0.0-2.0)
    /// </summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>
    /// Top-P sampling parameter
    /// </summary>
    public float? TopP { get; init; }

    /// <summary>
    /// Frequency penalty
    /// </summary>
    public float? FrequencyPenalty { get; init; }

    /// <summary>
    /// Presence penalty
    /// </summary>
    public float? PresencePenalty { get; init; }

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Enable debug output for API responses
    /// </summary>
    public bool EnableDebugOutput { get; init; } = false;

    /// <summary>
    /// Resolves the API key, expanding environment variable references
    /// </summary>
    public string ResolveApiKey()
    {
        if (string.IsNullOrEmpty(ApiKey))
            return string.Empty;

        // Support ${VAR_NAME} syntax
        if (ApiKey.StartsWith("${") && ApiKey.EndsWith("}"))
        {
            var varName = ApiKey[2..^1];
            return Environment.GetEnvironmentVariable(varName) ?? string.Empty;
        }

        return ApiKey;
    }

    /// <summary>
    /// Resolves the endpoint, expanding environment variable references
    /// </summary>
    public Uri? ResolveEndpoint()
    {
        if (string.IsNullOrEmpty(Endpoint))
            return null;

        var resolved = Endpoint;
        if (Endpoint.StartsWith("${") && Endpoint.EndsWith("}"))
        {
            var varName = Endpoint[2..^1];
            resolved = Environment.GetEnvironmentVariable(varName) ?? Endpoint;
        }

        return Uri.TryCreate(resolved, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>
    /// Resolves the model name, expanding environment variable references
    /// </summary>
    public string ResolveModel()
    {
        if (string.IsNullOrEmpty(Model))
            return string.Empty;

        if (Model.StartsWith("${") && Model.EndsWith("}"))
        {
            var varName = Model[2..^1];
            return Environment.GetEnvironmentVariable(varName) ?? Model;
        }

        return Model;
    }
}
