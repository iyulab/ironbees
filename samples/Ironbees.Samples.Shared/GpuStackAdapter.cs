using Ironbees.Core;
using OpenAI;
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace Ironbees.Samples.Shared;

/// <summary>
/// LLM Framework Adapter for GPU-Stack API (OpenAI-compatible)
/// GPU-Stack provides OpenAI-compatible endpoints at /v1-openai
/// Documentation: https://docs.gpustack.ai/latest/integrations/openai-compatible-apis/
/// </summary>
public class GpuStackAdapter : ILLMFrameworkAdapter
{
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _defaultModel;

    /// <summary>
    /// Initializes a new instance of the GpuStackAdapter class
    /// </summary>
    /// <param name="endpoint">GPU-Stack server endpoint (e.g., http://localhost:8080)</param>
    /// <param name="apiKey">GPU-Stack API key</param>
    /// <param name="defaultModel">Default model name to use if not specified in agent config</param>
    public GpuStackAdapter(string endpoint, string apiKey, string defaultModel = "llama3.2")
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

        _endpoint = endpoint.TrimEnd('/');
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _defaultModel = defaultModel;
    }

    /// <inheritdoc />
    public Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Create a wrapper agent with the config
        IAgent agent = new GpuStackAgentWrapper(config, _endpoint, _apiKey, _defaultModel);
        return Task.FromResult(agent);
    }

    /// <inheritdoc />
    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (agent is not GpuStackAgentWrapper wrapper)
        {
            throw new InvalidOperationException("Agent must be created by GpuStackAdapter");
        }

        return await wrapper.RunAsync(input, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (agent is not GpuStackAgentWrapper wrapper)
        {
            throw new InvalidOperationException("Agent must be created by GpuStackAdapter");
        }

        await foreach (var chunk in wrapper.StreamAsync(input, cancellationToken))
        {
            yield return chunk;
        }
    }
}

/// <summary>
/// Wrapper for GPU-Stack-based agents
/// </summary>
internal class GpuStackAgentWrapper : IAgent
{
    private readonly AgentConfig _config;
    private readonly ChatClient _client;

    public GpuStackAgentWrapper(
        AgentConfig config,
        string endpoint,
        string apiKey,
        string defaultModel)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Use model from config or default
        var modelName = config.Model?.Deployment ?? defaultModel;

        // Create OpenAI client with custom endpoint
        // GPU-Stack serves OpenAI-compatible APIs at /v1-openai
        // We need to provide the complete base URL including /v1-openai
        var openAiClient = new OpenAIClient(
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri($"{endpoint}/v1-openai")
            });

        _client = openAiClient.GetChatClient(modelName);
    }

    public string Name => _config.Name;
    public string Description => _config.Description;
    public AgentConfig Config => _config;

    public async Task<string> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_config.SystemPrompt),
            new UserChatMessage(input)
        };

        var options = CreateChatOptions();

        var completion = await _client.CompleteChatAsync(messages, options, cancellationToken);
        return completion.Value.Content[0].Text;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_config.SystemPrompt),
            new UserChatMessage(input)
        };

        var options = CreateChatOptions();

        await foreach (var update in _client.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }
    }

    private ChatCompletionOptions CreateChatOptions()
    {
        var options = new ChatCompletionOptions
        {
            Temperature = (float?)_config.Model?.Temperature ?? 0.7f,
            MaxOutputTokenCount = _config.Model?.MaxTokens ?? 2000
        };

        if (_config.Model?.TopP.HasValue == true)
        {
            options.TopP = (float)_config.Model.TopP.Value;
        }

        if (_config.Model?.FrequencyPenalty.HasValue == true)
        {
            options.FrequencyPenalty = (float)_config.Model.FrequencyPenalty.Value;
        }

        if (_config.Model?.PresencePenalty.HasValue == true)
        {
            options.PresencePenalty = (float)_config.Model.PresencePenalty.Value;
        }

        return options;
    }
}
