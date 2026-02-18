using Ironbees.Core;
using Xunit;

namespace Ironbees.Core.Tests;

public class FileSystemAgentLoaderTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemAgentLoader _loader;

    public FileSystemAgentLoaderTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ironbees-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _loader = new FileSystemAgentLoader();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private string CreateTestAgent(string agentName, bool includeSystemPrompt = true)
    {
        var agentPath = Path.Combine(_testDirectory, agentName);
        Directory.CreateDirectory(agentPath);

        // Create agent.yaml
        var yamlContent = $@"
name: {agentName}
description: Test agent for {agentName}
version: 1.0.0
model:
  provider: azure-openai
  deployment: gpt-4o
  temperature: 0.7
  maxTokens: 4000
capabilities:
  - test-capability
tags:
  - test
";
        File.WriteAllText(Path.Combine(agentPath, "agent.yaml"), yamlContent);

        // Create system-prompt.md
        if (includeSystemPrompt)
        {
            var promptContent = $"You are {agentName}, a test agent.";
            File.WriteAllText(Path.Combine(agentPath, "system-prompt.md"), promptContent);
        }

        return agentPath;
    }

    [Fact]
    public async Task LoadConfigAsync_ValidAgent_Success()
    {
        // Arrange
        var agentPath = CreateTestAgent("test-agent");

        // Act
        var config = await _loader.LoadConfigAsync(agentPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-agent", config.Name);
        Assert.Equal("Test agent for test-agent", config.Description);
        Assert.Equal("1.0.0", config.Version);
        Assert.Contains("You are test-agent", config.SystemPrompt);
        Assert.Equal("gpt-4o", config.Model.Deployment);
        Assert.Equal(0.7, config.Model.Temperature);
    }

    [Fact]
    public async Task LoadConfigAsync_MissingDirectory_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidAgentDirectoryException>(
            () => _loader.LoadConfigAsync(nonExistentPath));
    }

    [Fact]
    public async Task LoadConfigAsync_MissingAgentYaml_ThrowsException()
    {
        // Arrange
        var agentPath = Path.Combine(_testDirectory, "incomplete-agent");
        Directory.CreateDirectory(agentPath);
        File.WriteAllText(Path.Combine(agentPath, "system-prompt.md"), "Test prompt");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidAgentDirectoryException>(
            () => _loader.LoadConfigAsync(agentPath));
    }

    [Fact]
    public async Task LoadConfigAsync_MissingSystemPrompt_ThrowsException()
    {
        // Arrange
        CreateTestAgent("incomplete-agent", includeSystemPrompt: false);
        var agentPath = Path.Combine(_testDirectory, "incomplete-agent");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidAgentDirectoryException>(
            () => _loader.LoadConfigAsync(agentPath));
    }

    [Fact]
    public async Task LoadConfigAsync_EmptySystemPrompt_ThrowsException()
    {
        // Arrange
        var agentPath = CreateTestAgent("test-agent");
        File.WriteAllText(Path.Combine(agentPath, "system-prompt.md"), "   \n  \n  ");

        // Act & Assert
        await Assert.ThrowsAsync<AgentConfigurationException>(
            () => _loader.LoadConfigAsync(agentPath));
    }

    [Fact]
    public async Task LoadAllConfigsAsync_MultipleAgents_LoadsAll()
    {
        // Arrange
        var agentsDir = Path.Combine(_testDirectory, "agents");
        Directory.CreateDirectory(agentsDir);

        // Create test directory and agents inside it
        var agent1Path = Path.Combine(agentsDir, "agent1");
        var agent2Path = Path.Combine(agentsDir, "agent2");

        Directory.CreateDirectory(agent1Path);
        Directory.CreateDirectory(agent2Path);

        // Create agent1
        File.WriteAllText(Path.Combine(agent1Path, "agent.yaml"), @"
name: agent1
description: First test agent
version: 1.0.0
model:
  deployment: gpt-4o
");
        File.WriteAllText(Path.Combine(agent1Path, "system-prompt.md"), "You are agent1");

        // Create agent2
        File.WriteAllText(Path.Combine(agent2Path, "agent.yaml"), @"
name: agent2
description: Second test agent
version: 1.0.0
model:
  deployment: gpt-4o
");
        File.WriteAllText(Path.Combine(agent2Path, "system-prompt.md"), "You are agent2");

        // Act
        var configs = await _loader.LoadAllConfigsAsync(agentsDir);

        // Assert
        Assert.Equal(2, configs.Count);
        Assert.Contains(configs, c => c.Name == "agent1");
        Assert.Contains(configs, c => c.Name == "agent2");
    }

    [Fact]
    public async Task LoadAllConfigsAsync_NonExistentDirectory_ReturnsEmpty()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "non-existent");

        // Act
        var configs = await _loader.LoadAllConfigsAsync(nonExistentDir);

        // Assert
        Assert.Empty(configs);
    }

    [Fact]
    public async Task LoadAllConfigsAsync_SkipsInvalidAgents()
    {
        // Arrange
        var agentsDir = Path.Combine(_testDirectory, "mixed-agents");
        Directory.CreateDirectory(agentsDir);

        // Create valid agent
        var validPath = Path.Combine(agentsDir, "valid-agent");
        Directory.CreateDirectory(validPath);
        File.WriteAllText(Path.Combine(validPath, "agent.yaml"), @"
name: valid-agent
description: Valid agent
version: 1.0.0
model:
  deployment: gpt-4o
");
        File.WriteAllText(Path.Combine(validPath, "system-prompt.md"), "Valid prompt");

        // Create invalid agent (missing system-prompt.md)
        var invalidPath = Path.Combine(agentsDir, "invalid-agent");
        Directory.CreateDirectory(invalidPath);
        File.WriteAllText(Path.Combine(invalidPath, "agent.yaml"), @"
name: invalid-agent
description: Invalid agent
version: 1.0.0
model:
  deployment: gpt-4o
");

        // Act
        var configs = await _loader.LoadAllConfigsAsync(agentsDir);

        // Assert
        Assert.Single(configs);
        Assert.Equal("valid-agent", configs[0].Name);
    }

    [Fact]
    public async Task ValidateAgentDirectoryAsync_ValidDirectory_ReturnsTrue()
    {
        // Arrange
        var agentPath = CreateTestAgent("valid-agent");

        // Act
        var isValid = await _loader.ValidateAgentDirectoryAsync(agentPath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAgentDirectoryAsync_InvalidDirectory_ReturnsFalse()
    {
        // Arrange
        var agentPath = CreateTestAgent("invalid-agent", includeSystemPrompt: false);

        // Act
        var isValid = await _loader.ValidateAgentDirectoryAsync(agentPath);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateAgentDirectoryAsync_NonExistentDirectory_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent");

        // Act
        var isValid = await _loader.ValidateAgentDirectoryAsync(nonExistentPath);

        // Assert
        Assert.False(isValid);
    }
}
