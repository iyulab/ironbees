using System.Runtime.CompilerServices;
using Ironbees.Core.Conversation;
using Microsoft.Extensions.AI;

namespace Ironbees.Core;

/// <summary>
/// Orchestrates agent loading, selection, and execution
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IAgentLoader _loader;
    private readonly IAgentRegistry _registry;
    private readonly ILLMFrameworkAdapter _frameworkAdapter;
    private readonly IAgentSelector _agentSelector;
    private readonly IConversationStore? _conversationStore;
    private readonly string? _agentsDirectory;

    public AgentOrchestrator(
        IAgentLoader loader,
        IAgentRegistry registry,
        ILLMFrameworkAdapter frameworkAdapter,
        IAgentSelector agentSelector,
        string? agentsDirectory = null,
        IConversationStore? conversationStore = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _frameworkAdapter = frameworkAdapter ?? throw new ArgumentNullException(nameof(frameworkAdapter));
        _agentSelector = agentSelector ?? throw new ArgumentNullException(nameof(agentSelector));
        _agentsDirectory = agentsDirectory;
        _conversationStore = conversationStore;
    }

    /// <inheritdoc />
    public async Task LoadAgentsAsync(CancellationToken cancellationToken = default)
    {
        // Load agent configurations
        var configs = await _loader.LoadAllConfigsAsync(_agentsDirectory, cancellationToken);

        if (!configs.Any())
        {
            throw new AgentLoadException("No agents found to load");
        }

        // Create and register each agent, collecting any errors
        var errors = new List<AgentLoadError>();
        foreach (var config in configs)
        {
            try
            {
                var agent = await _frameworkAdapter.CreateAgentAsync(config, cancellationToken);
                _registry.Register(config.Name, agent);
            }
            catch (Exception ex)
            {
                errors.Add(new AgentLoadError(
                    config.Name,
                    _agentsDirectory ?? "agents",
                    ex));
            }
        }

        // Check results
        var loadedAgents = _registry.ListAgents();
        if (!loadedAgents.Any())
        {
            throw new AgentLoadException("Failed to load any agents", errors);
        }

        // If some agents failed but others succeeded, throw with partial failure info
        if (errors.Count > 0)
        {
            throw new AgentLoadException(
                $"Loaded {loadedAgents.Count} agent(s) but {errors.Count} failed",
                errors);
        }
    }

    /// <inheritdoc />
    public async Task<string> ProcessAsync(
        string input,
        string agentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var agent = _registry.Get(agentName)
            ?? throw new AgentNotFoundException(agentName);

        return await _frameworkAdapter.RunAsync(agent, input, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ProcessAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        // Use intelligent agent selection
        var selectionResult = await SelectAgentAsync(input, cancellationToken);

        if (selectionResult.SelectedAgent == null)
        {
            throw new InvalidOperationException(
                $"No suitable agent found for input. {selectionResult.SelectionReason}");
        }

        return await _frameworkAdapter.RunAsync(
            selectionResult.SelectedAgent,
            input,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ProcessAsync(
        string input,
        ProcessOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(options);

        // Load conversation history if ConversationId is provided
        IReadOnlyList<ChatMessage>? history = null;
        ConversationState? conversationState = null;
        string? previousAgentName = null;

        if (options.ConversationId is not null && _conversationStore is not null)
        {
            conversationState = await _conversationStore.LoadAsync(options.ConversationId, cancellationToken);
            previousAgentName = conversationState?.AgentName;
            history = BuildHistory(conversationState, options.MaxHistoryTurns);
        }

        // Select agent
        var agent = await ResolveAgentAsync(input, options, previousAgentName, cancellationToken);

        // Execute
        var response = await _frameworkAdapter.RunAsync(agent, input, history, cancellationToken);

        // Save conversation
        if (options.ConversationId is not null && _conversationStore is not null)
        {
            await SaveConversationTurnAsync(
                options.ConversationId, agent.Name, previousAgentName,
                input, response, conversationState, cancellationToken);
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<AgentSelectionResult> SelectAgentAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var agentNames = _registry.ListAgents();
        if (agentNames == null || !agentNames.Any())
        {
            return new AgentSelectionResult
            {
                SelectedAgent = null,
                ConfidenceScore = 0,
                SelectionReason = "No agents available",
                AllScores = Array.Empty<AgentScore>()
            };
        }

        // Get all agents
        var agents = agentNames
            .Select(name => _registry.Get(name))
            .Where(agent => agent != null)
            .Cast<IAgent>()
            .ToList();

        // Use selector to find best match
        return await _agentSelector.SelectAgentAsync(input, agents, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamAsync(
        string input,
        string agentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var agent = _registry.Get(agentName)
            ?? throw new AgentNotFoundException(agentName);

        return _frameworkAdapter.StreamAsync(agent, input, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        // Use intelligent agent selection
        var selectionResult = await SelectAgentAsync(input, cancellationToken);

        if (selectionResult.SelectedAgent == null)
        {
            yield return $"\u26a0\ufe0f No suitable agent found for this request. {selectionResult.SelectionReason}";
            yield break;
        }

        // Stream using selected agent
        await foreach (var chunk in _frameworkAdapter.StreamAsync(
            selectionResult.SelectedAgent,
            input,
            cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        ProcessOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(options);

        // Load conversation history if ConversationId is provided
        IReadOnlyList<ChatMessage>? history = null;
        ConversationState? conversationState = null;
        string? previousAgentName = null;

        if (options.ConversationId is not null && _conversationStore is not null)
        {
            conversationState = await _conversationStore.LoadAsync(options.ConversationId, cancellationToken);
            previousAgentName = conversationState?.AgentName;
            history = BuildHistory(conversationState, options.MaxHistoryTurns);
        }

        // Select agent
        var agent = await ResolveAgentAsync(input, options, previousAgentName, cancellationToken);

        // Stream with history
        var responseBuilder = new System.Text.StringBuilder();
        await foreach (var chunk in _frameworkAdapter.StreamAsync(agent, input, history, cancellationToken))
        {
            responseBuilder.Append(chunk);
            yield return chunk;
        }

        // Save conversation after streaming completes
        if (options.ConversationId is not null && _conversationStore is not null)
        {
            await SaveConversationTurnAsync(
                options.ConversationId, agent.Name, previousAgentName,
                input, responseBuilder.ToString(), conversationState, cancellationToken);
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ListAgents()
    {
        return _registry.ListAgents();
    }

    /// <inheritdoc />
    public IAgent? GetAgent(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _registry.Get(name);
    }

    /// <summary>
    /// Resolves the agent to use based on options, stickiness, and scoring.
    /// </summary>
    private async Task<IAgent> ResolveAgentAsync(
        string input,
        ProcessOptions options,
        string? previousAgentName,
        CancellationToken cancellationToken)
    {
        // If AgentName is explicitly provided, use it directly
        if (options.AgentName is not null)
        {
            return _registry.Get(options.AgentName)
                ?? throw new AgentNotFoundException(options.AgentName);
        }

        // Get all agents for scoring
        var agentNames = _registry.ListAgents();
        var agents = agentNames
            .Select(name => _registry.Get(name))
            .Where(agent => agent != null)
            .Cast<IAgent>()
            .ToList();

        if (agents.Count == 0)
        {
            throw new InvalidOperationException("No agents available");
        }

        // Score all agents
        var scores = await _agentSelector.ScoreAgentsAsync(input, agents, cancellationToken);
        var bestCandidate = scores.FirstOrDefault();

        if (bestCandidate == null)
        {
            throw new InvalidOperationException("No suitable agent found for input");
        }

        // Apply stickiness: if there was a previous agent, keep it unless the best candidate
        // scores significantly higher
        if (previousAgentName is not null)
        {
            var currentAgentScore = scores.FirstOrDefault(s => s.Agent.Name == previousAgentName);
            if (currentAgentScore is not null)
            {
                var delta = bestCandidate.Score - currentAgentScore.Score;
                if (delta <= options.StickinessThreshold)
                {
                    // Keep the current agent — delta is not significant enough
                    return currentAgentScore.Agent;
                }
            }
        }

        return bestCandidate.Agent;
    }

    /// <summary>
    /// Builds ChatMessage history from conversation state with optional turn limit.
    /// </summary>
    private static IReadOnlyList<ChatMessage>? BuildHistory(
        ConversationState? state,
        int? maxHistoryTurns)
    {
        if (state is null || state.Messages.Count == 0)
            return null;

        var messages = state.Messages
            .Where(m => m.Role is "user" or "assistant")
            .Select(m => m.ToChatMessage())
            .ToList();

        if (maxHistoryTurns.HasValue && maxHistoryTurns.Value > 0)
        {
            // Each turn = user + assistant = 2 messages
            var maxMessages = maxHistoryTurns.Value * 2;
            if (messages.Count > maxMessages)
            {
                messages = messages.Skip(messages.Count - maxMessages).ToList();
            }
        }

        return messages.Count > 0 ? messages : null;
    }

    /// <summary>
    /// Saves user input and assistant response to the conversation store.
    /// </summary>
    private async Task SaveConversationTurnAsync(
        string conversationId,
        string agentName,
        string? previousAgentName,
        string input,
        string response,
        ConversationState? existingState,
        CancellationToken cancellationToken)
    {
        if (_conversationStore is null) return;

        // Build metadata for agent switches
        Dictionary<string, string>? metadata = null;
        if (previousAgentName is not null && previousAgentName != agentName)
        {
            metadata = existingState?.Metadata is not null
                ? new Dictionary<string, string>(existingState.Metadata)
                : new Dictionary<string, string>();
            metadata["agent_switched_from"] = previousAgentName;
            metadata["agent_switched_at"] = DateTimeOffset.UtcNow.ToString("o");
        }

        if (existingState is null)
        {
            // Create new conversation with both messages
            var state = new ConversationState
            {
                ConversationId = conversationId,
                AgentName = agentName,
                Messages = new List<ConversationMessage>
                {
                    new() { Role = "user", Content = input },
                    new() { Role = "assistant", Content = response }
                },
                Metadata = metadata
            };
            await _conversationStore.SaveAsync(state, cancellationToken);
        }
        else
        {
            // Update agent name if switched
            if (existingState.AgentName != agentName)
            {
                existingState.AgentName = agentName;
            }

            // Append messages
            await _conversationStore.AppendMessageAsync(
                conversationId,
                new ConversationMessage { Role = "user", Content = input },
                cancellationToken);
            await _conversationStore.AppendMessageAsync(
                conversationId,
                new ConversationMessage { Role = "assistant", Content = response },
                cancellationToken);
        }
    }
}
