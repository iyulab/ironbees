using Ironbees.Core;
using System.IO;

namespace Ironbees.Core.Tests;

/// <summary>
/// Tests for FileSystemAgentLoader Phase 4.2 enhancements
/// - Validation logic
/// - Error messages
/// - Caching
/// - Hot reload
/// </summary>
public class FileSystemAgentLoaderEnhancedTests : IDisposable
{
    private readonly string _testAgentsDir;

    public FileSystemAgentLoaderEnhancedTests()
    {
        _testAgentsDir = Path.Combine(Path.GetTempPath(), $"ironbees-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testAgentsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testAgentsDir))
        {
            Directory.Delete(_testAgentsDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadConfigAsync_WithValidConfig_ReturnsConfig()
    {
        // Arrange
        var agentName = "valid-agent";
        var agentPath = CreateTestAgent(agentName, "Valid agent description", "1.0.0", "You are a helpful assistant.");
        var loader = new FileSystemAgentLoader();

        // Act
        var config = await loader.LoadConfigAsync(agentPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(agentName, config.Name);
        Assert.Equal("Valid agent description", config.Description);
    }

    [Fact]
    public async Task LoadConfigAsync_WithMissingYaml_ThrowsInvalidAgentDirectoryException()
    {
        // Arrange
        var agentPath = Path.Combine(_testAgentsDir, "missing-yaml");
        Directory.CreateDirectory(agentPath);
        File.WriteAllText(Path.Combine(agentPath, "system-prompt.md"), "Test prompt");

        var loader = new FileSystemAgentLoader();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidAgentDirectoryException>(
            () => loader.LoadConfigAsync(agentPath));

        Assert.Contains("agent.yaml", exception.Message);
        Assert.Contains("Expected at:", exception.Message);
    }

    [Fact]
    public async Task LoadConfigAsync_WithMissingPrompt_ThrowsInvalidAgentDirectoryException()
    {
        // Arrange
        var agentPath = Path.Combine(_testAgentsDir, "missing-prompt");
        Directory.CreateDirectory(agentPath);
        CreateAgentYaml(agentPath, "test-agent", "Test description", "1.0.0");

        var loader = new FileSystemAgentLoader();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidAgentDirectoryException>(
            () => loader.LoadConfigAsync(agentPath));

        Assert.Contains("system-prompt.md", exception.Message);
        Assert.Contains("Expected at:", exception.Message);
    }

    [Fact]
    public async Task LoadConfigAsync_WithInvalidYaml_ThrowsYamlParsingException()
    {
        // Arrange
        var agentPath = Path.Combine(_testAgentsDir, "invalid-yaml");
        Directory.CreateDirectory(agentPath);

        var invalidYaml = @"
name: test-agent
description: Test
version: 1.0.0
model:
  deployment: gpt-4
  temperature: 0.7
  invalid syntax here without proper indentation
  maxTokens: 1000
";
        File.WriteAllText(Path.Combine(agentPath, "agent.yaml"), invalidYaml);
        File.WriteAllText(Path.Combine(agentPath, "system-prompt.md"), "Test prompt");

        var loader = new FileSystemAgentLoader();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<YamlParsingException>(
            () => loader.LoadConfigAsync(agentPath));

        Assert.Contains("Failed to parse YAML", exception.Message);
        Assert.Contains("Common YAML issues", exception.Message);
    }

    [Fact]
    public async Task LoadConfigAsync_WithEmptyPrompt_ThrowsAgentConfigurationException()
    {
        // Arrange
        var agentPath = Path.Combine(_testAgentsDir, "empty-prompt");
        Directory.CreateDirectory(agentPath);
        CreateAgentYaml(agentPath, "test-agent", "Test description", "1.0.0");
        File.WriteAllText(Path.Combine(agentPath, "system-prompt.md"), "   ");

        var loader = new FileSystemAgentLoader();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AgentConfigurationException>(
            () => loader.LoadConfigAsync(agentPath));

        Assert.Contains("System prompt is empty", exception.Message);
    }

    [Fact]
    public async Task LoadConfigAsync_WithCaching_UsesCache()
    {
        // Arrange
        var agentName = "cached-agent";
        var agentPath = CreateTestAgent(agentName, "Cached agent", "1.0.0", "Test prompt");
        var options = new FileSystemAgentLoaderOptions { EnableCaching = true };
        var loader = new FileSystemAgentLoader(options);

        // Act - First load
        var config1 = await loader.LoadConfigAsync(agentPath);
        // Act - Second load (should use cache)
        var config2 = await loader.LoadConfigAsync(agentPath);

        // Assert
        Assert.Same(config1, config2); // Should be exact same instance from cache
    }

    [Fact]
    public async Task LoadConfigAsync_CacheInvalidatedOnFileChange()
    {
        // Arrange
        var agentName = "cache-invalidate";
        var agentPath = CreateTestAgent(agentName, "Original description", "1.0.0", "Test prompt");
        var options = new FileSystemAgentLoaderOptions { EnableCaching = true };
        var loader = new FileSystemAgentLoader(options);

        // Act - First load
        var config1 = await loader.LoadConfigAsync(agentPath);

        // Modify file
        await Task.Delay(50); // Ensure different timestamp
        CreateAgentYaml(agentPath, agentName, "Modified description", "1.0.0");

        // Second load
        var config2 = await loader.LoadConfigAsync(agentPath);

        // Assert
        Assert.NotSame(config1, config2);
        Assert.Equal("Modified description", config2.Description);
    }

    [Fact]
    public async Task LoadAllConfigsAsync_WithDuplicateNames_DetectsDuplicates()
    {
        // Arrange
        CreateTestAgent("duplicate-name", "First agent", "1.0.0", "Prompt 1");
        CreateTestAgent("duplicate-name", "Second agent", "1.0.0", "Prompt 2", subdir: "agent2");

        var options = new FileSystemAgentLoaderOptions
        {
            EnableValidation = true,
            StrictValidation = true
        };
        var loader = new FileSystemAgentLoader(options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AgentConfigurationException>(
            () => loader.LoadAllConfigsAsync(_testAgentsDir));

        Assert.Contains("Duplicate agent names", exception.Message);
    }

    [Fact]
    public async Task LoadAllConfigsAsync_StopOnFirstError_ThrowsOnFirstError()
    {
        // Arrange
        CreateTestAgent("valid-agent", "Valid", "1.0.0", "Test");
        var invalidPath = Path.Combine(_testAgentsDir, "invalid-agent");
        Directory.CreateDirectory(invalidPath);
        // Missing files

        var options = new FileSystemAgentLoaderOptions { StopOnFirstError = true };
        var loader = new FileSystemAgentLoader(options);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidAgentDirectoryException>(
            () => loader.LoadAllConfigsAsync(_testAgentsDir));
    }

    [Fact]
    public async Task LoadAllConfigsAsync_ContinueOnError_LoadsValidAgents()
    {
        // Arrange
        CreateTestAgent("valid-agent-1", "Valid 1", "1.0.0", "Prompt 1");
        CreateTestAgent("valid-agent-2", "Valid 2", "1.0.0", "Prompt 2");
        var invalidPath = Path.Combine(_testAgentsDir, "invalid-agent");
        Directory.CreateDirectory(invalidPath);
        // Missing files

        var options = new FileSystemAgentLoaderOptions { StopOnFirstError = false };
        var loader = new FileSystemAgentLoader(options);

        // Act
        var configs = await loader.LoadAllConfigsAsync(_testAgentsDir);

        // Assert
        Assert.Equal(2, configs.Count);
        Assert.Contains(configs, c => c.Name == "valid-agent-1");
        Assert.Contains(configs, c => c.Name == "valid-agent-2");
    }

    [Fact]
    public async Task ClearCache_RemovesCachedConfigs()
    {
        // Arrange
        var agentPath = CreateTestAgent("cache-clear", "Test", "1.0.0", "Prompt");
        var options = new FileSystemAgentLoaderOptions { EnableCaching = true };
        var loader = new FileSystemAgentLoader(options);

        // Load config (should cache)
        var config1 = await loader.LoadConfigAsync(agentPath);

        // Clear cache
        loader.ClearCache();

        // Load again (should not use cache)
        var config2 = await loader.LoadConfigAsync(agentPath);

        // Assert
        Assert.NotSame(config1, config2);
    }

    [Fact]
    public async Task HotReload_FileChanged_RaisesReloadEvent()
    {
        // Arrange
        var agentName = "hot-reload";
        var agentPath = CreateTestAgent(agentName, "Original", "1.0.0", "Prompt");
        var options = new FileSystemAgentLoaderOptions
        {
            EnableHotReload = true,
            EnableCaching = true
        };
        var loader = new FileSystemAgentLoader(options);

        // Load all configs to start file watcher
        await loader.LoadAllConfigsAsync(_testAgentsDir);

        var reloadEventRaised = false;
        AgentConfig? reloadedConfig = null;

        loader.AgentReloaded += (sender, args) =>
        {
            reloadEventRaised = true;
            reloadedConfig = args.Config;
        };

        // Act - Modify file
        await Task.Delay(200); // Wait for watcher to start
        CreateAgentYaml(agentPath, agentName, "Modified description", "1.0.0");

        // Wait for file watcher to detect change
        await Task.Delay(500);

        // Assert
        Assert.True(reloadEventRaised, "Reload event should have been raised");
        Assert.NotNull(reloadedConfig);
        Assert.Equal("Modified description", reloadedConfig.Description);

        // Cleanup
        loader.Dispose();
    }

    // Helper methods

    private string CreateTestAgent(
        string name,
        string description,
        string version,
        string systemPrompt,
        string? subdir = null)
    {
        var agentPath = subdir != null
            ? Path.Combine(_testAgentsDir, subdir)
            : Path.Combine(_testAgentsDir, name);

        Directory.CreateDirectory(agentPath);
        CreateAgentYaml(agentPath, name, description, version);
        File.WriteAllText(Path.Combine(agentPath, "system-prompt.md"), systemPrompt);

        return agentPath;
    }

    private void CreateAgentYaml(string agentPath, string name, string description, string version)
    {
        var yaml = $@"name: {name}
description: {description}
version: {version}
model:
  deployment: gpt-4
  temperature: 0.7
  maxTokens: 1000
  topP: 1.0
capabilities:
  - test-capability
tags:
  - test-tag
";
        File.WriteAllText(Path.Combine(agentPath, "agent.yaml"), yaml);
    }
}
