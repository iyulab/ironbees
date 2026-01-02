using System.ClientModel;
using System.Text.Json;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Chat;

namespace Ironbees.Autonomous.OpenAI;

/// <summary>
/// Oracle verifier using OpenAI-compatible API (supports GPUStack, Ollama, vLLM, etc.)
/// </summary>
public class OpenAIOracleVerifier : IOracleVerifier
{
    private readonly ChatClient _chatClient;
    private readonly ILogger _logger;
    private readonly OracleConfig _defaultConfig;

    /// <summary>
    /// Check if the verifier is properly configured
    /// </summary>
    public bool IsConfigured => _chatClient != null;

    /// <summary>
    /// Create verifier with API key (uses official OpenAI endpoint)
    /// </summary>
    /// <param name="apiKey">OpenAI API key</param>
    /// <param name="model">Model name (default: gpt-4o-mini)</param>
    /// <param name="logger">Optional logger</param>
    public OpenAIOracleVerifier(
        string apiKey,
        string model = "gpt-4o-mini",
        ILogger? logger = null)
        : this(new OpenAIClient(apiKey), model, logger)
    {
    }

    /// <summary>
    /// Create verifier with custom endpoint (for GPUStack, Ollama, etc.)
    /// </summary>
    /// <param name="endpoint">API endpoint URL</param>
    /// <param name="apiKey">API key (can be empty for local services)</param>
    /// <param name="model">Model name</param>
    /// <param name="logger">Optional logger</param>
    public OpenAIOracleVerifier(
        Uri endpoint,
        string apiKey,
        string model,
        ILogger? logger = null)
        : this(CreateClientWithEndpoint(endpoint, apiKey), model, logger)
    {
    }

    /// <summary>
    /// Create verifier with existing OpenAI client
    /// </summary>
    public OpenAIOracleVerifier(
        OpenAIClient client,
        string model = "gpt-4o-mini",
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _chatClient = client.GetChatClient(model);
        _logger = logger ?? NullLogger.Instance;
        _defaultConfig = new OracleConfig { Model = model };
    }

    private static OpenAIClient CreateClientWithEndpoint(Uri endpoint, string apiKey)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        // For local services that don't require auth, use a dummy key
        var credential = new ApiKeyCredential(string.IsNullOrEmpty(apiKey) ? "not-required" : apiKey);
        return new OpenAIClient(credential, options);
    }

    /// <summary>
    /// Verify execution result against original goal
    /// </summary>
    public async Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= _defaultConfig;

        var userPrompt = BuildVerificationPrompt(originalPrompt, executionOutput, config);

        _logger.LogDebug("Sending verification request to oracle");

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(config.SystemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = config.MaxTokens,
                Temperature = (float)config.Temperature
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(config.Timeout);

            var response = await _chatClient.CompleteChatAsync(messages, options, cts.Token);
            var content = response.Value.Content[0].Text;

            _logger.LogDebug("Oracle response received: {Content}", content[..Math.Min(200, content.Length)]);

            var verdict = ParseVerdict(content);

            // Add token usage if available
            if (response.Value.Usage != null)
            {
                verdict = verdict with
                {
                    TokenUsage = new TokenUsage
                    {
                        InputTokens = response.Value.Usage.InputTokenCount,
                        OutputTokens = response.Value.Usage.OutputTokenCount
                    }
                };
            }

            return verdict;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Oracle verification timed out");
            return OracleVerdict.Error("Oracle verification timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oracle verification failed");
            return OracleVerdict.Error($"Oracle error: {ex.Message}");
        }
    }

    /// <summary>
    /// Build the verification prompt
    /// </summary>
    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null)
    {
        config ??= _defaultConfig;

        return config.UserPromptTemplate
            .Replace("{original_prompt}", originalPrompt)
            .Replace("{execution_output}", executionOutput);
    }

    private static OracleVerdict ParseVerdict(string content)
    {
        try
        {
            // Try to extract JSON from markdown code block if present
            var json = content;
            if (content.Contains("```"))
            {
                var startIdx = content.IndexOf('{');
                var endIdx = content.LastIndexOf('}');
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    json = content[startIdx..(endIdx + 1)];
                }
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var parsed = JsonSerializer.Deserialize<OracleVerdictDto>(json, options);
            if (parsed == null)
            {
                return OracleVerdict.Error("Failed to parse oracle response: null result");
            }

            return new OracleVerdict
            {
                IsComplete = parsed.IsComplete,
                CanContinue = parsed.CanContinue,
                Analysis = parsed.Analysis ?? "No analysis provided",
                NextPromptSuggestion = parsed.NextPromptSuggestion,
                Confidence = Math.Clamp(parsed.Confidence, 0, 1)
            };
        }
        catch (JsonException ex)
        {
            return OracleVerdict.Error($"Failed to parse oracle JSON response: {ex.Message}");
        }
    }

    private sealed record OracleVerdictDto
    {
        public bool IsComplete { get; init; }
        public bool CanContinue { get; init; }
        public string? Analysis { get; init; }
        public string? NextPromptSuggestion { get; init; }
        public double Confidence { get; init; }
    }
}
