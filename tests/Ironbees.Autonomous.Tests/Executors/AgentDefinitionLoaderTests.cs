using Ironbees.Autonomous.Executors;
using Xunit;

namespace Ironbees.Autonomous.Tests.Executors;

public class AgentDefinitionLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AgentDefinitionLoader _loader = new();

    public AgentDefinitionLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agents-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // --- LoadAgentAsync ---

    [Fact]
    public async Task LoadAgentAsync_MissingAgentYaml_ShouldThrow()
    {
        var agentDir = Path.Combine(_tempDir, "missing-agent");
        Directory.CreateDirectory(agentDir);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _loader.LoadAgentAsync(agentDir));
    }

    [Fact]
    public async Task LoadAgentAsync_MinimalYaml_ShouldUseDefaults()
    {
        var agentDir = CreateAgentDir("minimal", "{}");

        var agent = await _loader.LoadAgentAsync(agentDir);

        Assert.Equal("minimal", agent.Id); // falls back to directory name
        Assert.Equal("minimal", agent.Name);
        Assert.Equal("agent", agent.Role);
        Assert.Equal("json", agent.Output.Type);
    }

    [Fact]
    public async Task LoadAgentAsync_FullYaml_ShouldMapAllFields()
    {
        var yaml = """
            id: "questioner"
            name: "Question Generator"
            description: "Generates yes/no questions"
            role: "questioner"
            output:
              type: "json"
              schema: '{"question": "string"}'
              example: '{"question": "Is it alive?"}'
            llm:
              max_output_tokens: 300
              temperature: 0.5
              top_p: 0.95
            fallback:
              enabled: true
              items:
                - "Is it alive?"
                - "Is it man-made?"
              strategy: "random"
            variables:
              category: "animals"
              difficulty: "easy"
            """;
        var agentDir = CreateAgentDir("q-agent", yaml);

        var agent = await _loader.LoadAgentAsync(agentDir);

        Assert.Equal("questioner", agent.Id);
        Assert.Equal("Question Generator", agent.Name);
        Assert.Equal("Generates yes/no questions", agent.Description);
        Assert.Equal("questioner", agent.Role);
        Assert.Equal("json", agent.Output.Type);
        Assert.NotNull(agent.Output.Schema);
        Assert.NotNull(agent.Output.Example);
        Assert.NotNull(agent.Llm);
        Assert.Equal(300, agent.Llm.MaxOutputTokens);
        Assert.Equal(0.5f, agent.Llm.Temperature);
        Assert.Equal(0.95f, agent.Llm.TopP);
        Assert.NotNull(agent.Fallback);
        Assert.True(agent.Fallback.Enabled);
        Assert.Equal(2, agent.Fallback.Items.Count);
        Assert.Equal("random", agent.Fallback.Strategy);
        Assert.Equal(2, agent.Variables.Count);
        Assert.Equal("animals", agent.Variables["category"]);
    }

    [Fact]
    public async Task LoadAgentAsync_WithSystemPromptFile_ShouldLoadFromFile()
    {
        var agentDir = CreateAgentDir("prompter", "{}");
        await File.WriteAllTextAsync(
            Path.Combine(agentDir, "system-prompt.md"),
            "You are a helpful assistant.");

        var agent = await _loader.LoadAgentAsync(agentDir);

        Assert.Equal("You are a helpful assistant.", agent.SystemPrompt);
    }

    [Fact]
    public async Task LoadAgentAsync_WithoutSystemPromptFile_ShouldUseYamlPrompt()
    {
        var yaml = """
            system_prompt: "Inline prompt from YAML"
            """;
        var agentDir = CreateAgentDir("inline", yaml);

        var agent = await _loader.LoadAgentAsync(agentDir);

        Assert.Equal("Inline prompt from YAML", agent.SystemPrompt);
    }

    [Fact]
    public async Task LoadAgentAsync_SystemPromptFileTakesPrecedence()
    {
        var yaml = """
            system_prompt: "YAML prompt"
            """;
        var agentDir = CreateAgentDir("precedence", yaml);
        await File.WriteAllTextAsync(
            Path.Combine(agentDir, "system-prompt.md"),
            "File prompt takes precedence");

        var agent = await _loader.LoadAgentAsync(agentDir);

        Assert.Equal("File prompt takes precedence", agent.SystemPrompt);
    }

    // --- LoadAgentsAsync ---

    [Fact]
    public async Task LoadAgentsAsync_NonExistentDirectory_ShouldReturnEmpty()
    {
        var result = await _loader.LoadAgentsAsync(Path.Combine(_tempDir, "nonexistent"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAgentsAsync_EmptyDirectory_ShouldReturnEmpty()
    {
        var agentsDir = Path.Combine(_tempDir, "empty-agents");
        Directory.CreateDirectory(agentsDir);

        var result = await _loader.LoadAgentsAsync(agentsDir);

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAgentsAsync_MultipleAgents_ShouldLoadAll()
    {
        var agentsDir = Path.Combine(_tempDir, "multi-agents");
        Directory.CreateDirectory(agentsDir);

        CreateAgentDirIn(agentsDir, "agent-a", """
            id: "agent-a"
            name: "Agent A"
            """);
        CreateAgentDirIn(agentsDir, "agent-b", """
            id: "agent-b"
            name: "Agent B"
            """);

        var result = await _loader.LoadAgentsAsync(agentsDir);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("agent-a"));
        Assert.True(result.ContainsKey("agent-b"));
        Assert.Equal("Agent A", result["agent-a"].Name);
        Assert.Equal("Agent B", result["agent-b"].Name);
    }

    [Fact]
    public async Task LoadAgentsAsync_SkipsDirectoriesWithoutAgentYaml()
    {
        var agentsDir = Path.Combine(_tempDir, "mixed");
        Directory.CreateDirectory(agentsDir);

        CreateAgentDirIn(agentsDir, "valid", """
            id: "valid"
            """);
        // Create directory without agent.yaml
        Directory.CreateDirectory(Path.Combine(agentsDir, "no-yaml"));

        var result = await _loader.LoadAgentsAsync(agentsDir);

        Assert.Single(result);
        Assert.True(result.ContainsKey("valid"));
    }

    // --- LoadFallbackItemsAsync ---

    [Fact]
    public async Task LoadFallbackItemsAsync_FileNotFound_ShouldReturnEmpty()
    {
        var result = await _loader.LoadFallbackItemsAsync(Path.Combine(_tempDir, "missing.yaml"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadFallbackItemsAsync_ValidFile_ShouldReturnItems()
    {
        var yaml = """
            items:
              - "Is it alive?"
              - "Is it man-made?"
              - "Can you eat it?"
            """;
        var filePath = Path.Combine(_tempDir, "fallback.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var result = await _loader.LoadFallbackItemsAsync(filePath);

        Assert.Equal(3, result.Count);
        Assert.Contains("Is it alive?", result);
        Assert.Contains("Is it man-made?", result);
    }

    // --- Helpers ---

    private string CreateAgentDir(string name, string yamlContent)
    {
        var agentDir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "agent.yaml"), yamlContent);
        return agentDir;
    }

    private static void CreateAgentDirIn(string parentDir, string name, string yamlContent)
    {
        var agentDir = Path.Combine(parentDir, name);
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "agent.yaml"), yamlContent);
    }
}
