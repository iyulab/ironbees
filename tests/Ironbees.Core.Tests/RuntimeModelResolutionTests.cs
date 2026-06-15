using NSubstitute;

namespace Ironbees.Core.Tests;

/// <summary>
/// Tests for runtime model resolution: agents may omit model.deployment and have the model
/// resolved at invoke time (ProcessOptions.ModelOverride) or from a configured default model.
/// </summary>
public class RuntimeModelResolutionTests
{
    private static AgentConfig MakeConfig(string name, string? deployment) =>
        new()
        {
            Name = name,
            Description = $"Description for {name}",
            Version = "1.0.0",
            SystemPrompt = "prompt",
            Model = new ModelConfig { Provider = "test", Deployment = deployment }
        };

    private static AgentOrchestrator CreateOrchestrator(
        IAgentLoader loader,
        IAgentRegistry registry,
        ILLMFrameworkAdapter adapter,
        string? defaultModelDeployment = null)
    {
        return new AgentOrchestrator(
            loader,
            registry,
            adapter,
            Substitute.For<IAgentSelector>(),
            agentsDirectory: null,
            conversationStore: null,
            defaultModelDeployment: defaultModelDeployment);
    }

    // --- Validator: empty deployment is allowed (deferred to runtime) ---

    [Fact]
    public void Validate_EmptyDeployment_DoesNotProduceError()
    {
        var config = MakeConfig("rag-agent", deployment: null);

        var result = AgentConfigValidator.Validate(config, "/test/path");

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.Contains("deployment", StringComparison.OrdinalIgnoreCase));
    }

    // --- LoadAgentsAsync: default model substitution ---

    [Fact]
    public async Task LoadAgentsAsync_EmptyDeploymentWithDefault_CreatesAgentWithDefaultModel()
    {
        var loader = Substitute.For<IAgentLoader>();
        loader.LoadAllConfigsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentConfig> { MakeConfig("rag-agent", deployment: null) });

        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        adapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IAgent>());

        var registry = new AgentRegistry();
        var orchestrator = CreateOrchestrator(loader, registry, adapter, defaultModelDeployment: "runtime-default");

        await orchestrator.LoadAgentsAsync();

        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Deployment == "runtime-default"),
            Arg.Any<CancellationToken>());
        Assert.Contains("rag-agent", registry.ListAgents());
    }

    [Fact]
    public async Task LoadAgentsAsync_ExplicitDeployment_IgnoresDefaultModel()
    {
        var loader = Substitute.For<IAgentLoader>();
        loader.LoadAllConfigsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentConfig> { MakeConfig("rag-agent", deployment: "yaml-model") });

        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        adapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IAgent>());

        var registry = new AgentRegistry();
        var orchestrator = CreateOrchestrator(loader, registry, adapter, defaultModelDeployment: "runtime-default");

        await orchestrator.LoadAgentsAsync();

        // yaml deployment wins; default is not applied
        await adapter.Received(1).CreateAgentAsync(
            Arg.Is<AgentConfig>(c => c.Model.Deployment == "yaml-model"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAgentsAsync_EmptyDeploymentNoDefault_FailsWithActionableError()
    {
        var loader = Substitute.For<IAgentLoader>();
        loader.LoadAllConfigsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentConfig> { MakeConfig("rag-agent", deployment: null) });

        var adapter = Substitute.For<ILLMFrameworkAdapter>();
        var registry = new AgentRegistry();
        var orchestrator = CreateOrchestrator(loader, registry, adapter, defaultModelDeployment: null);

        var ex = await Assert.ThrowsAsync<AgentLoadException>(() => orchestrator.LoadAgentsAsync());

        // The agent failed to load; the adapter was never asked to create it with an empty model.
        await adapter.DidNotReceive().CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>());
        Assert.Empty(registry.ListAgents());
        Assert.NotNull(ex);
    }
}
