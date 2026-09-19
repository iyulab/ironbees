using Ironbees.Core;
using Ironbees.Core.Orchestration;
using Ironbees.Ironhive.Orchestration;
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

        // Assert - the orchestration members (not on ILLMFrameworkAdapter) resolve without a cast, as the same instance
        Assert.Same(adapter, provider.GetService<IronhiveAdapter>());
    }

    [Fact]
    public async Task AddIronbeesIronhive_ApprovalHandler_ReachesTheRegisteredOrchestratorFactory()
    {
        // IronhiveOptions is not itself a DI service: a factory resolved from the container used to receive no options,
        // so the approval gate set here never reached an orchestrator even after the factory learned to translate it.
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var asked = 0;
        services.AddIronbeesIronhive(options =>
        {
            options.HiveService = Substitute.For<IHiveService>();
            options.ApprovalHandler = _ =>
            {
                Interlocked.Increment(ref asked);
                return Task.FromResult(false);
            };
        });
        using var provider = services.BuildServiceProvider();

        var ironhiveAgent = Substitute.For<IronHive.Abstractions.Agent.IAgent>();
        ironhiveAgent.Name.Returns("writer");
        var agent = new IronhiveAgentWrapper(ironhiveAgent, new AgentConfig
        {
            Name = "writer",
            Description = "Test agent writer",
            Version = "1.0.0",
            SystemPrompt = "Test system prompt",
            Model = new ModelConfig { Provider = "test", Deployment = "test-model" },
        });
        var orchestrator = provider.GetRequiredService<IIronhiveOrchestratorFactory>()
            .CreateOrchestrator(new OrchestratorSettings { Type = OrchestratorType.Sequential }, [agent]);

        var result = await orchestrator.RunAsync("draft the release notes", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(1, asked);
    }

    [Fact]
    public void AddIronbeesIronhive_WithConfigureHive_BootsWithValidateOnBuild()
    {
        // Regression: IronHive 0.10.0's AddHiveService factory overload defaults to Scoped,
        // while ILLMFrameworkAdapter is Singleton. The ConfigureHive path must register
        // IHiveService with a lifetime the singleton adapter can legally consume.
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddIronbeesIronhive(options =>
        {
            options.ConfigureHive = builder => { };
        });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddIronbeesIronhive_WithConfigureHive_RegistersHiveServiceAsSingleton()
    {
        // Both registration paths (HiveService instance / ConfigureHive factory) must agree
        // on the Singleton lifetime consumed by the singleton IronhiveAdapter.
        var services = new ServiceCollection();

        services.AddIronbeesIronhive(options =>
        {
            options.ConfigureHive = builder => { };
        });

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IHiveService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
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
