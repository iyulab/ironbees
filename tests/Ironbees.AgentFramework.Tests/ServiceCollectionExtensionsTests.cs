using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Ironbees.AgentFramework.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact, Trait("Category", "Integration")]
    public void AddIronbees_ValidConfiguration_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

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
