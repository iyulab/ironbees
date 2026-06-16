using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ironbees.AgentFramework.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact, Trait("Category", "Integration")]
    public void AddIronbees_ValidConfiguration_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddIronbees(options =>
        {
            options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
            options.AzureOpenAIKey = "test-key";
            options.AgentsDirectory = "./agents";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var loader = provider.GetService<IAgentLoader>();
        var registry = provider.GetService<IAgentRegistry>();
        var adapter = provider.GetService<ILLMFrameworkAdapter>();
        var orchestrator = provider.GetService<IAgentOrchestrator>();

        Assert.NotNull(loader);
        Assert.NotNull(registry);
        Assert.NotNull(adapter);
        Assert.NotNull(orchestrator);

        Assert.IsType<FileSystemAgentLoader>(loader);
        Assert.IsType<AgentRegistry>(registry);
        Assert.IsType<AgentFrameworkAdapter>(adapter);
        Assert.IsType<AgentOrchestrator>(orchestrator);
    }

    [Fact]
    public void AddIronbees_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => services!.AddIronbees(options =>
            {
                options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
                options.AzureOpenAIKey = "test-key";
            }));
    }

    [Fact]
    public void AddIronbees_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => services.AddIronbees(null!));
    }

    [Fact]
    public void AddIronbees_MissingEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => services.AddIronbees(options =>
            {
                options.AzureOpenAIKey = "test-key";
                // Missing endpoint
            }));

        Assert.Contains("AzureOpenAIEndpoint", exception.Message);
    }

    [Fact]
    public void AddIronbees_EmptyEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => services.AddIronbees(options =>
            {
                options.AzureOpenAIEndpoint = "";
                options.AzureOpenAIKey = "test-key";
            }));

        Assert.Contains("AzureOpenAIEndpoint", exception.Message);
    }

    [Fact]
    public void AddIronbees_MissingKey_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => services.AddIronbees(options =>
            {
                options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
                // Missing key
            }));

        Assert.Contains("AzureOpenAIKey", exception.Message);
    }

    [Fact]
    public void AddIronbees_EmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => services.AddIronbees(options =>
            {
                options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
                options.AzureOpenAIKey = "";
            }));

        Assert.Contains("AzureOpenAIKey", exception.Message);
    }

    [Fact, Trait("Category", "Integration")]
    public void AddIronbees_WithAgentsDirectory_PassesToOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var testDirectory = "./test-agents";

        // Act
        services.AddIronbees(options =>
        {
            options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
            options.AzureOpenAIKey = "test-key";
            options.AgentsDirectory = testDirectory;
        });

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact, Trait("Category", "Integration")]
    public void AddIronbees_WithoutAgentsDirectory_UsesDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddIronbees(options =>
        {
            options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
            options.AzureOpenAIKey = "test-key";
            // AgentsDirectory not set
        });

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact, Trait("Category", "Integration")]
    public void AddIronbees_RegistersSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddIronbees(options =>
        {
            options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
            options.AzureOpenAIKey = "test-key";
        });

        var provider = services.BuildServiceProvider();

        // Assert - Get services twice and verify they're the same instance
        var loader1 = provider.GetRequiredService<IAgentLoader>();
        var loader2 = provider.GetRequiredService<IAgentLoader>();
        Assert.Same(loader1, loader2);

        var registry1 = provider.GetRequiredService<IAgentRegistry>();
        var registry2 = provider.GetRequiredService<IAgentRegistry>();
        Assert.Same(registry1, registry2);

        var adapter1 = provider.GetRequiredService<ILLMFrameworkAdapter>();
        var adapter2 = provider.GetRequiredService<ILLMFrameworkAdapter>();
        Assert.Same(adapter1, adapter2);

        var orchestrator1 = provider.GetRequiredService<IAgentOrchestrator>();
        var orchestrator2 = provider.GetRequiredService<IAgentOrchestrator>();
        Assert.Same(orchestrator1, orchestrator2);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task AddIronbees_WithDefaultModelDeployment_LoadsAgentOmittingDeployment()
    {
        // Reproduces the AIMS repro: an agent.yaml omits model.deployment and relies on the
        // DI-configured default. Without forwarding DefaultModelDeployment to the core orchestrator,
        // the agent fails to load ("Failed to load any agents").
        using var agents = new TempAgentsDirectory();
        agents.WriteAgent("rag-agent", deployment: null);

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddIronbees(options =>
        {
            options.OpenAIApiKey = "test-key";
            options.AgentsDirectory = agents.Path;
            options.DefaultModelDeployment = "gpt-4o";
        });

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();
        var registry = provider.GetRequiredService<IAgentRegistry>();

        await orchestrator.LoadAgentsAsync();

        Assert.Contains("rag-agent", registry.ListAgents());
    }

    [Fact, Trait("Category", "Integration")]
    public async Task AddIronbees_WithoutDefaultModelDeployment_FailsToLoadAgentOmittingDeployment()
    {
        // Counterpart proving the default is what enables the load: same deployment-less agent with
        // no DefaultModelDeployment configured fails with an actionable error.
        using var agents = new TempAgentsDirectory();
        agents.WriteAgent("rag-agent", deployment: null);

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddIronbees(options =>
        {
            options.OpenAIApiKey = "test-key";
            options.AgentsDirectory = agents.Path;
            // DefaultModelDeployment intentionally not set
        });

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

        await Assert.ThrowsAsync<AgentLoadException>(() => orchestrator.LoadAgentsAsync());
    }

    private sealed class TempAgentsDirectory : IDisposable
    {
        public string Path { get; }

        public TempAgentsDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ironbees-af-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path);
        }

        public void WriteAgent(string agentName, string? deployment)
        {
            var agentPath = System.IO.Path.Combine(Path, agentName);
            Directory.CreateDirectory(agentPath);

            var deploymentLine = deployment is null ? string.Empty : $"\n  deployment: {deployment}";
            var yaml = $@"
name: {agentName}
description: Test agent for {agentName}
version: 1.0.0
model:
  provider: openai{deploymentLine}
  temperature: 0.7
  maxTokens: 4000
capabilities:
  - test-capability
tags:
  - test
";
            File.WriteAllText(System.IO.Path.Combine(agentPath, "agent.yaml"), yaml);
            File.WriteAllText(System.IO.Path.Combine(agentPath, "system-prompt.md"), $"You are {agentName}.");
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    [Fact]
    public void AddIronbees_ReturnsServiceCollection_ForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddIronbees(options =>
        {
            options.AzureOpenAIEndpoint = "https://test.openai.azure.com";
            options.AzureOpenAIKey = "test-key";
        });

        // Assert
        Assert.Same(services, result);
    }
}
