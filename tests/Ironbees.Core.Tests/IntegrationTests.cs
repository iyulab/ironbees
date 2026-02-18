using Ironbees.Core;
using Xunit;

namespace Ironbees.Core.Tests;

/// <summary>
/// Integration tests using real sample agents from /agents directory
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests
{
    [Fact]
    public async Task LoadSampleAgent_CodingAgent_Success()
    {
        // Arrange
        var loader = new FileSystemAgentLoader();
        var projectRoot = FindProjectRoot();
        var agentPath = Path.Combine(projectRoot, "agents", "coding-agent");

        // Skip test if sample agent doesn't exist
        if (!Directory.Exists(agentPath))
        {
            return;
        }

        // Act
        var config = await loader.LoadConfigAsync(agentPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("coding-agent", config.Name);
        Assert.Contains("code-generation", config.Capabilities);
        Assert.Contains("code-review", config.Capabilities);
        Assert.Equal("kanana-1.5", config.Model.Deployment);
        Assert.NotEmpty(config.SystemPrompt);
    }

    [Fact]
    public async Task LoadAllSampleAgents_FromAgentsDirectory_Success()
    {
        // Arrange
        var loader = new FileSystemAgentLoader();
        var projectRoot = FindProjectRoot();
        var agentsDir = Path.Combine(projectRoot, "agents");

        // Skip test if agents directory doesn't exist
        if (!Directory.Exists(agentsDir))
        {
            return;
        }

        // Act
        var configs = await loader.LoadAllConfigsAsync(agentsDir);

        // Assert
        Assert.NotEmpty(configs);
        Assert.All(configs, config =>
        {
            Assert.NotEmpty(config.Name);
            Assert.NotEmpty(config.Description);
            Assert.NotEmpty(config.SystemPrompt);
            Assert.NotNull(config.Model);
        });
    }

    [Fact]
    public async Task AgentRegistry_RegisterAndRetrieveSampleAgent_Success()
    {
        // Arrange
        var loader = new FileSystemAgentLoader();
        var registry = new AgentRegistry();
        var projectRoot = FindProjectRoot();
        var agentPath = Path.Combine(projectRoot, "agents", "coding-agent");

        // Skip test if sample agent doesn't exist
        if (!Directory.Exists(agentPath))
        {
            return;
        }

        var config = await loader.LoadConfigAsync(agentPath);
        var agent = new TestAgent(config);

        // Act
        registry.Register(config.Name, agent);
        var retrieved = registry.GetAgent("coding-agent");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("coding-agent", retrieved.Name);
        Assert.Same(agent, retrieved);
    }

    private static string FindProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            // Check for both .sln and .slnx solution files
            if (File.Exists(Path.Combine(directory, "Ironbees.sln")) ||
                File.Exists(Path.Combine(directory, "Ironbees.slnx")))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        throw new InvalidOperationException("Could not find project root (Ironbees.sln or Ironbees.slnx)");
    }

    private sealed class TestAgent : IAgent
    {
        public TestAgent(AgentConfig config)
        {
            Config = config;
            Name = config.Name;
            Description = config.Description;
        }

        public string Name { get; }
        public string Description { get; }
        public AgentConfig Config { get; }
    }
}
