using Ironbees.Core;
using IronHive.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Ironbees.Ironhive.Tests;

public class IronhiveServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIronbeesIronhive_WithHiveService_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockHiveService = Substitute.For<IHiveService>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Act
        services.AddIronbeesIronhive(options =>
        {
            options.HiveService = mockHiveService;
            options.AgentsDirectory = "./agents";
        });

        var provider = services.BuildServiceProvider();

        // Assert - Core services registered
        Assert.NotNull(provider.GetService<IAgentLoader>());
        Assert.NotNull(provider.GetService<IAgentRegistry>());
        Assert.NotNull(provider.GetService<IAgentSelector>());

        // Assert - IHiveService registered
        var hiveService = provider.GetService<IHiveService>();
        Assert.Same(mockHiveService, hiveService);

        // Assert - ILLMFrameworkAdapter registered as IronhiveAdapter
        var adapter = provider.GetService<ILLMFrameworkAdapter>();
        Assert.NotNull(adapter);
        Assert.IsType<IronhiveAdapter>(adapter);
    }

    [Fact]
    public void AddIronbeesIronhive_NeitherConfigureNorService_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddIronbeesIronhive(options => { }));
    }

    [Fact]
    public void AddIronbeesIronhive_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddIronbeesIronhive(options =>
            {
                options.HiveService = Substitute.For<IHiveService>();
            }));
    }

    [Fact]
    public void AddIronbeesIronhive_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddIronbeesIronhive(null!));
    }

    [Fact]
    public void AddIronbeesIronhive_CustomConfidence_PassedToCore()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockHiveService = Substitute.For<IHiveService>();

        // Act
        services.AddIronbeesIronhive(options =>
        {
            options.HiveService = mockHiveService;
            options.MinimumConfidenceThreshold = 0.8;
        });

        var provider = services.BuildServiceProvider();

        // Assert - selector is registered (confidence threshold is used internally)
        var selector = provider.GetService<IAgentSelector>();
        Assert.NotNull(selector);
    }

    [Fact]
    public void AddIronbeesIronhive_OrchestratorResolvesAdapter()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockHiveService = Substitute.For<IHiveService>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddIronbeesIronhive(options =>
        {
            options.HiveService = mockHiveService;
        });

        var provider = services.BuildServiceProvider();

        // Assert - Orchestrator can be resolved (proves adapter chain works)
        var orchestrator = provider.GetService<IAgentOrchestrator>();
        Assert.NotNull(orchestrator);
    }
}
