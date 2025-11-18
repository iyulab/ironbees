using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Ironbees.AgentMode.Configuration;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace Ironbees.AgentMode.Providers;

/// <summary>
/// Adapter that bridges Anthropic.SDK to Microsoft.Extensions.AI.IChatClient interface.
/// Enables Anthropic Claude models to work with the unified AI abstraction.
/// </summary>
public class AnthropicChatClientAdapter : IChatClient
{
    private readonly AnthropicClient _client;
    private readonly LLMConfiguration _config;

    public AnthropicChatClientAdapter(AnthropicClient client, LLMConfiguration config)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public ChatClientMetadata Metadata => new(
        providerName: "Anthropic",
        providerUri: new Uri("https://api.anthropic.com"),
        defaultModelId: _config.Model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = BuildMessageParameters(chatMessages.ToList(), options);

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        return MapToResponse(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parameters = BuildMessageParameters(chatMessages.ToList(), options);

        await foreach (var chunk in _client.Messages.StreamClaudeMessageAsync(parameters, cancellationToken))
        {
            if (chunk.Delta?.Text != null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.Delta.Text);
            }
        }
    }

    public void Dispose()
    {
        // Anthropic.SDK client doesn't require disposal
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is null && serviceType?.IsInstanceOfType(this) is true ? this : null;
    }

    private MessageParameters BuildMessageParameters(
        IList<ChatMessage> chatMessages,
        ChatOptions? options)
    {
        var parameters = new MessageParameters
        {
            Model = _config.Model,
            MaxTokens = options?.MaxOutputTokens ?? _config.MaxOutputTokens,
            Temperature = (decimal?)(options?.Temperature ?? _config.Temperature),
            Messages = new List<Message>()
        };

        // Extract system prompt if present
        var systemMessage = chatMessages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMessage != null)
        {
            parameters.System = new List<SystemMessage>
            {
                new SystemMessage(systemMessage.Text ?? string.Empty)
            };
        }

        // Add user and assistant messages
        foreach (var msg in chatMessages.Where(m => m.Role != ChatRole.System))
        {
            var role = msg.Role == ChatRole.User
                ? RoleType.User
                : RoleType.Assistant;

            parameters.Messages.Add(new Message(role, msg.Text ?? string.Empty));
        }

        // Apply additional options
        if (options?.TopP.HasValue == true)
        {
            parameters.TopP = (decimal?)(double)options.TopP.Value;
        }

        if (options?.FrequencyPenalty.HasValue == true || options?.PresencePenalty.HasValue == true)
        {
            // Anthropic doesn't support frequency/presence penalty directly
            // Could be logged as a warning or handled differently
        }

        return parameters;
    }

    private static ChatResponse MapToResponse(MessageResponse response)
    {
        // Extract text content from response
        var textContent = response.Content?
            .OfType<Anthropic.SDK.Messaging.TextContent>()
            .FirstOrDefault()
            ?.Text ?? string.Empty;

        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, textContent))
        {
            ResponseId = response.Id,
            ModelId = response.Model,
            FinishReason = MapFinishReason(response.StopReason),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Add usage information
        if (response.Usage != null)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = response.Usage.InputTokens,
                OutputTokenCount = response.Usage.OutputTokens,
                TotalTokenCount = response.Usage.InputTokens + response.Usage.OutputTokens
            };
        }

        return chatResponse;
    }

    private static ChatFinishReason? MapFinishReason(string? stopReason)
    {
        return stopReason switch
        {
            "end_turn" => ChatFinishReason.Stop,
            "max_tokens" => ChatFinishReason.Length,
            "stop_sequence" => ChatFinishReason.Stop,
            "tool_use" => ChatFinishReason.ToolCalls,
            _ => null
        };
    }
}
