using System.ClientModel;
using Ironbees.Autonomous.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace TwentyQuestionsSample;

/// <summary>
/// Factory for creating LLM clients from configuration settings
/// </summary>
public static class LlmClientFactory
{
    /// <summary>
    /// Create a ChatClient from OrchestratorSettings
    /// </summary>
    public static ChatClient CreateChatClient(OrchestratorSettings settings)
    {
        var llm = settings.Llm;

        var endpoint = llm.ResolveEndpoint()
            ?? throw new InvalidOperationException("LLM endpoint is required");
        var apiKey = llm.ResolveApiKey();
        var model = llm.ResolveModel();

        if (string.IsNullOrEmpty(model))
        {
            throw new InvalidOperationException("LLM model is required");
        }

        var options = new OpenAIClientOptions { Endpoint = endpoint };
        var credential = new ApiKeyCredential(string.IsNullOrEmpty(apiKey) ? "not-required" : apiKey);
        var client = new OpenAIClient(credential, options);

        return client.GetChatClient(model);
    }

    /// <summary>
    /// Create ChatCompletionOptions from LlmSettings
    /// </summary>
    public static ChatCompletionOptions CreateCompletionOptions(
        LlmSettings settings,
        int? maxTokensOverride = null,
        float? temperatureOverride = null)
    {
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = maxTokensOverride ?? settings.MaxOutputTokens,
            Temperature = temperatureOverride ?? settings.Temperature
        };

        if (settings.TopP.HasValue)
        {
            options.TopP = settings.TopP.Value;
        }

        if (settings.FrequencyPenalty.HasValue)
        {
            options.FrequencyPenalty = settings.FrequencyPenalty.Value;
        }

        if (settings.PresencePenalty.HasValue)
        {
            options.PresencePenalty = settings.PresencePenalty.Value;
        }

        return options;
    }
}
