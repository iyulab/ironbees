using Ironbees.Core;
using Moq;

namespace Ironbees.Core.Tests;

public class KeywordAgentSelectorTests
{
    private IAgent CreateTestAgent(
        string name,
        string description,
        List<string>? capabilities = null,
        List<string>? tags = null)
    {
        var mockAgent = new Mock<IAgent>();
        var config = new AgentConfig
        {
            Name = name,
            Description = description,
            Version = "1.0.0",
            SystemPrompt = "Test prompt",
            Model = new ModelConfig
            {
                Deployment = "gpt-4",
                Temperature = 0.7,
                MaxTokens = 1000
            },
            Capabilities = capabilities ?? new List<string>(),
            Tags = tags ?? new List<string>()
        };

        mockAgent.Setup(a => a.Name).Returns(name);
        mockAgent.Setup(a => a.Description).Returns(description);
        mockAgent.Setup(a => a.Config).Returns(config);

        return mockAgent.Object;
    }

    [Fact]
    public async Task SelectAgentAsync_WithCapabilityMatch_SelectsCorrectAgent()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with coding tasks",
            capabilities: new List<string> { "code-generation", "code-review" },
            tags: new List<string> { "programming", "development" });

        var writingAgent = CreateTestAgent(
            "writing-agent",
            "Helps with writing tasks",
            capabilities: new List<string> { "content-writing", "editing" },
            tags: new List<string> { "writing", "documentation" });

        var agents = new List<IAgent> { codingAgent, writingAgent };

        // Act
        var result = await selector.SelectAgentAsync(
            "Write some code for me",
            agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("coding-agent", result.SelectedAgent.Name);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Fact]
    public async Task SelectAgentAsync_WithTagMatch_SelectsCorrectAgent()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with coding tasks",
            capabilities: new List<string> { "code-generation" },
            tags: new List<string> { "programming", "development" });

        var writingAgent = CreateTestAgent(
            "writing-agent",
            "Helps with writing tasks",
            capabilities: new List<string> { "content-writing" },
            tags: new List<string> { "writing", "documentation" });

        var agents = new List<IAgent> { codingAgent, writingAgent };

        // Act
        var result = await selector.SelectAgentAsync(
            "I need help with documentation",
            agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("writing-agent", result.SelectedAgent.Name);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Fact]
    public async Task SelectAgentAsync_NoMatch_ReturnsLowConfidence()
    {
        // Arrange
        var selector = new KeywordAgentSelector(minimumConfidenceThreshold: 0.5);

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with coding tasks",
            capabilities: new List<string> { "code-generation" },
            tags: new List<string> { "programming" });

        var agents = new List<IAgent> { codingAgent };

        // Act
        var result = await selector.SelectAgentAsync(
            "What's the weather like?",
            agents);

        // Assert
        Assert.True(result.ConfidenceScore < 0.5);
    }

    [Fact]
    public async Task SelectAgentAsync_EmptyAgents_ReturnsNull()
    {
        // Arrange
        var selector = new KeywordAgentSelector();
        var agents = new List<IAgent>();

        // Act
        var result = await selector.SelectAgentAsync(
            "Some input",
            agents);

        // Assert
        Assert.Null(result.SelectedAgent);
        Assert.Equal(0, result.ConfidenceScore);
        Assert.Equal("No agents available", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_WithFallback_UsesFallbackWhenNoMatch()
    {
        // Arrange
        var fallbackAgent = CreateTestAgent(
            "fallback-agent",
            "General purpose agent",
            capabilities: new List<string> { "general" });

        var selector = new KeywordAgentSelector(
            minimumConfidenceThreshold: 0.5,
            fallbackAgent: fallbackAgent);

        var specializedAgent = CreateTestAgent(
            "specialized-agent",
            "Very specific agent",
            capabilities: new List<string> { "specialized-task" },
            tags: new List<string> { "specific" });

        var agents = new List<IAgent> { specializedAgent };

        // Act
        var result = await selector.SelectAgentAsync(
            "Something completely different",
            agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("fallback-agent", result.SelectedAgent.Name);
        Assert.Equal(0.5, result.ConfidenceScore);
    }

    [Fact]
    public async Task ScoreAgentsAsync_ReturnsOrderedScores()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with coding and programming",
            capabilities: new List<string> { "code-generation", "debugging" },
            tags: new List<string> { "programming", "code" });

        var writingAgent = CreateTestAgent(
            "writing-agent",
            "Helps with writing",
            capabilities: new List<string> { "writing" },
            tags: new List<string> { "content" });

        var agents = new List<IAgent> { writingAgent, codingAgent };

        // Act
        var scores = await selector.ScoreAgentsAsync(
            "Help me write some code",
            agents);

        // Assert
        Assert.Equal(2, scores.Count);
        Assert.Equal("coding-agent", scores[0].Agent.Name);
        Assert.True(scores[0].Score > scores[1].Score);
    }

    [Fact]
    public async Task SelectAgentAsync_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var selector = new KeywordAgentSelector();
        var agents = new List<IAgent>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await selector.SelectAgentAsync(null!, agents));
    }

    [Fact]
    public async Task SelectAgentAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var selector = new KeywordAgentSelector();
        var agents = new List<IAgent>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await selector.SelectAgentAsync("", agents));
    }

    [Fact]
    public async Task SelectAgentAsync_NullAgents_ThrowsArgumentNullException()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await selector.SelectAgentAsync("test", null!));
    }

    [Fact]
    public void Constructor_InvalidThreshold_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new KeywordAgentSelector(minimumConfidenceThreshold: -0.1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new KeywordAgentSelector(minimumConfidenceThreshold: 1.1));
    }

    [Fact]
    public async Task SelectAgentAsync_MultipleMatches_SelectsBestMatch()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var pythonAgent = CreateTestAgent(
            "python-agent",
            "Python programming expert",
            capabilities: new List<string> { "python-coding", "python-debugging" },
            tags: new List<string> { "python", "programming" });

        var generalCodingAgent = CreateTestAgent(
            "general-coding-agent",
            "General coding helper",
            capabilities: new List<string> { "code-generation" },
            tags: new List<string> { "programming" });

        var agents = new List<IAgent> { generalCodingAgent, pythonAgent };

        // Act
        var result = await selector.SelectAgentAsync(
            "Help me with Python programming",
            agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("python-agent", result.SelectedAgent.Name);
        Assert.True(result.ConfidenceScore > 0.5);
    }
}
