using System.Runtime.CompilerServices;
using Ironbees.Core;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using Microsoft.Extensions.Logging;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive;

/// <summary>
/// Adapter that bridges Ironbees to IronHive for agent execution
/// </summary>
public class IronhiveAdapter : ILLMFrameworkAdapter
{
    private readonly IHiveService _hiveService;
    private readonly ILogger<IronhiveAdapter> _logger;

    public IronhiveAdapter(IHiveService hiveService, ILogger<IronhiveAdapter> logger)
    {
        _hiveService = hiveService ?? throw new ArgumentNullException(nameof(hiveService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        _logger.LogDebug("Creating IronHive agent: {AgentName} with provider {Provider}, model {Model}",
            config.Name, config.Model.Provider, config.Model.Deployment);

        var ironhiveAgent = _hiveService.CreateAgentFromYaml(BuildAgentYaml(config));
        var wrapper = new IronhiveAgentWrapper(ironhiveAgent, config);

        return Task.FromResult<IAgent>(wrapper);
    }

    /// <inheritdoc />
    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        var ironhiveAgent = GetIronhiveAgent(agent);
        var messages = CreateMessages(input);

        _logger.LogDebug("Running IronHive agent {AgentName} with input length {InputLength}",
            agent.Name, input.Length);

        var response = await ironhiveAgent.InvokeAsync(messages, cancellationToken);
        return ExtractText(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ironhiveAgent = GetIronhiveAgent(agent);
        var messages = CreateMessages(input);

        _logger.LogDebug("Streaming IronHive agent {AgentName} with input length {InputLength}",
            agent.Name, input.Length);

        await foreach (var chunk in ironhiveAgent.InvokeStreamingAsync(messages, cancellationToken))
        {
            if (chunk is StreamingContentDeltaResponse delta
                && delta.Delta is TextDeltaContent textDelta)
            {
                yield return textDelta.Value;
            }
        }
    }

    private static IronHiveAgent GetIronhiveAgent(IAgent agent)
    {
        if (agent is IronhiveAgentWrapper wrapper)
        {
            return wrapper.IronhiveAgent;
        }

        throw new InvalidOperationException(
            $"Agent '{agent.Name}' is not an IronHive agent. " +
            $"Expected IronhiveAgentWrapper but got {agent.GetType().Name}.");
    }

    private static IEnumerable<Message> CreateMessages(string input)
    {
        return
        [
            new UserMessage
            {
                Content = [new TextMessageContent { Value = input }]
            }
        ];
    }

    private static string ExtractText(MessageResponse response)
    {
        var textParts = response.Message.Content
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return string.Join("", textParts);
    }

    internal static string BuildAgentYaml(AgentConfig config)
    {
        // IronHive AgentService uses CamelCaseNamingConvention for YAML deserialization
        var lines = new List<string>
        {
            $"name: {EscapeYamlValue(config.Name)}",
            $"description: {EscapeYamlValue(config.Description)}",
            $"provider: {EscapeYamlValue(config.Model.Provider)}",
            $"model: {EscapeYamlValue(config.Model.Deployment)}"
        };

        if (!string.IsNullOrWhiteSpace(config.SystemPrompt))
        {
            lines.Add("instructions: |");
            foreach (var line in config.SystemPrompt.Split('\n'))
            {
                lines.Add($"  {line}");
            }
        }

        // Parameters mapping
        var hasParams = false;
        var paramLines = new List<string>();
        if (config.Model.MaxTokens != 4000) // non-default
        {
            paramLines.Add($"  maxTokens: {config.Model.MaxTokens}");
            hasParams = true;
        }
        if (config.Model.Temperature != 0.7) // non-default
        {
            paramLines.Add($"  temperature: {config.Model.Temperature}");
            hasParams = true;
        }
        if (config.Model.TopP.HasValue)
        {
            paramLines.Add($"  topP: {config.Model.TopP.Value}");
            hasParams = true;
        }
        if (hasParams)
        {
            lines.Add("parameters:");
            lines.AddRange(paramLines);
        }

        return string.Join("\n", lines);
    }

    private static string EscapeYamlValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(':') || value.Contains('#') || value.Contains('"')
            || value.StartsWith(' ') || value.EndsWith(' '))
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
        return value;
    }
}
