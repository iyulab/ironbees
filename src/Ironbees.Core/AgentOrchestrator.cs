using System.Runtime.CompilerServices;

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
    private readonly string? _agentsDirectory;

    public AgentOrchestrator(
        IAgentLoader loader,
        IAgentRegistry registry,
        ILLMFrameworkAdapter frameworkAdapter,
        IAgentSelector agentSelector,
        string? agentsDirectory = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _frameworkAdapter = frameworkAdapter ?? throw new ArgumentNullException(nameof(frameworkAdapter));
        _agentSelector = agentSelector ?? throw new ArgumentNullException(nameof(agentSelector));
        _agentsDirectory = agentsDirectory;
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
            yield return $"⚠️ No suitable agent found for this request. {selectionResult.SelectionReason}";
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
}
