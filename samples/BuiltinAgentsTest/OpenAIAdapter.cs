using Ironbees.Core;
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace BuiltinAgentsTest;

/// <summary>
/// LLM Framework Adapter for OpenAI API (non-Azure)
/// </summary>
public class OpenAIAdapter : ILLMFrameworkAdapter
{
    private readonly string _apiKey;
    private readonly string _defaultModel;

    public OpenAIAdapter(string apiKey, string defaultModel = "gpt-4")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _defaultModel = defaultModel;
    }

    public Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        // Create a wrapper agent with the config
        IAgent agent = new OpenAIAgentWrapper(config, _apiKey, _defaultModel);
        return Task.FromResult(agent);
    }

    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        if (agent is not OpenAIAgentWrapper wrapper)
        {
            throw new InvalidOperationException("Agent must be created by OpenAIAdapter");
        }

        return await wrapper.RunAsync(input, cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (agent is not OpenAIAgentWrapper wrapper)
        {
            throw new InvalidOperationException("Agent must be created by OpenAIAdapter");
        }

        await foreach (var chunk in wrapper.StreamAsync(input, cancellationToken))
        {
            yield return chunk;
        }
    }
}

/// <summary>
/// Wrapper for OpenAI-based agents
/// </summary>
internal class OpenAIAgentWrapper : IAgent
{
    private readonly AgentConfig _config;
    private readonly ChatClient _client;

    public OpenAIAgentWrapper(AgentConfig config, string apiKey, string defaultModel)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Use model from config or default
        var modelName = config.Model?.Deployment ?? defaultModel;

        // Create OpenAI client
        _client = new ChatClient(modelName, apiKey);
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

        var options = new ChatCompletionOptions
        {
            Temperature = (float?)_config.Model?.Temperature ?? 0.7f,
            MaxOutputTokenCount = _config.Model?.MaxTokens ?? 2000
        };

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

        var options = new ChatCompletionOptions
        {
            Temperature = (float?)_config.Model?.Temperature ?? 0.7f,
            MaxOutputTokenCount = _config.Model?.MaxTokens ?? 2000
        };

        await foreach (var update in _client.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                yield return contentPart.Text;
            }
        }
    }
}
