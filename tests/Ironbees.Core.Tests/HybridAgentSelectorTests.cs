using Moq;

namespace Ironbees.Core.Tests;

public class HybridAgentSelectorTests
{
    private readonly Mock<IEmbeddingProvider> _mockEmbeddingProvider;

    public HybridAgentSelectorTests()
    {
        _mockEmbeddingProvider = new Mock<IEmbeddingProvider>();
        _mockEmbeddingProvider.Setup(p => p.Dimensions).Returns(384);
        _mockEmbeddingProvider.Setup(p => p.ModelName).Returns("test-model");
    }

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

    private static float[] CreateNormalizedVector(params float[] values)
    {
        return VectorSimilarity.Normalize(values);
    }

    [Fact]
    public async Task SelectAgentAsync_BothSelectorsAgree_SelectsAgreedAgent()
    {
        // Arrange
        var codingEmbedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var writingEmbedding = CreateNormalizedVector(0.0f, 1.0f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(0.9f, 0.1f, 0.0f);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { codingEmbedding, writingEmbedding });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Expert code developer for programming tasks",
            capabilities: new List<string> { "code-generation", "programming" },
            tags: new List<string> { "code", "programming", "development" });

        var writingAgent = CreateTestAgent(
            "writing-agent",
            "Content writer for documentation",
            capabilities: new List<string> { "content-writing" },
            tags: new List<string> { "writing", "documentation" });

        var agents = new List<IAgent> { codingAgent, writingAgent };

        // Act
        var result = await hybridSelector.SelectAgentAsync("Write some code for programming", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("coding-agent", result.SelectedAgent.Name);
        Assert.Contains("Both selectors agreed", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_SelectorsDisagree_HybridScoreDeterminesWinner()
    {
        // Arrange - Set up embedding to favor writing-agent
        var codingEmbedding = CreateNormalizedVector(0.0f, 1.0f, 0.0f);
        var writingEmbedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(0.8f, 0.2f, 0.0f); // Closer to writing embedding

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { codingEmbedding, writingEmbedding });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        // Embedding-focused configuration (30/70)
        var hybridSelector = new HybridAgentSelector(
            keywordSelector,
            embeddingSelector,
            keywordWeight: 0.1,  // Low keyword weight
            embeddingWeight: 0.9); // High embedding weight

        // Keywords favor coding, embeddings favor writing
        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with coding tasks",
            capabilities: new List<string> { "code-generation", "programming" },
            tags: new List<string> { "code", "programming" });

        var writingAgent = CreateTestAgent(
            "writing-agent",
            "Content writer",
            capabilities: new List<string> { "content-writing" },
            tags: new List<string> { "writing" });

        var agents = new List<IAgent> { codingAgent, writingAgent };

        // Act - Query has "code" keyword but embedding favors writing
        var result = await hybridSelector.SelectAgentAsync("code review task", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        // With high embedding weight, writing-agent should win due to embedding similarity
        Assert.Equal("writing-agent", result.SelectedAgent.Name);
        Assert.Contains("disagreed", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_EmptyAgents_ReturnsNull()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agents = new List<IAgent>();

        // Act
        var result = await hybridSelector.SelectAgentAsync("Any query", agents);

        // Assert
        Assert.Null(result.SelectedAgent);
        Assert.Equal(0, result.ConfidenceScore);
        Assert.Equal("No agents available", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_SingleAgent_ReturnsAgentWithFullConfidence()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agent = CreateTestAgent("single-agent", "The only agent");
        var agents = new List<IAgent> { agent };

        // Act
        var result = await hybridSelector.SelectAgentAsync("Any query", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("single-agent", result.SelectedAgent.Name);
        Assert.Equal(1.0, result.ConfidenceScore);
        Assert.Contains("Only agent available", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_NullInput_ThrowsArgumentException()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agents = new List<IAgent>();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await hybridSelector.SelectAgentAsync(null!, agents));
    }

    [Fact]
    public async Task SelectAgentAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agents = new List<IAgent>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await hybridSelector.SelectAgentAsync("", agents));
    }

    [Fact]
    public async Task SelectAgentAsync_NullAgents_ThrowsArgumentNullException()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await hybridSelector.SelectAgentAsync("test", null!));
    }

    [Fact]
    public void Constructor_NullKeywordSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new HybridAgentSelector(null!, embeddingSelector));
    }

    [Fact]
    public void Constructor_NullEmbeddingSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new HybridAgentSelector(keywordSelector, null!));
    }

    [Fact]
    public void Constructor_NegativeKeywordWeight_ThrowsArgumentException()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new HybridAgentSelector(keywordSelector, embeddingSelector, keywordWeight: -0.1));
    }

    [Fact]
    public void Constructor_NegativeEmbeddingWeight_ThrowsArgumentException()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new HybridAgentSelector(keywordSelector, embeddingSelector, embeddingWeight: -0.1));
    }

    [Fact]
    public void Constructor_BothWeightsZero_ThrowsArgumentException()
    {
        // Arrange
        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new HybridAgentSelector(keywordSelector, embeddingSelector,
                keywordWeight: 0.0, embeddingWeight: 0.0));
    }

    [Fact]
    public async Task ScoreAgentsAsync_ReturnsOrderedScores()
    {
        // Arrange
        var embedding1 = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var embedding2 = CreateNormalizedVector(0.0f, 1.0f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(0.7f, 0.3f, 0.0f);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { embedding1, embedding2 });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var scores = await hybridSelector.ScoreAgentsAsync("Test query", agents);

        // Assert
        Assert.Equal(2, scores.Count);
        Assert.True(scores[0].Score >= scores[1].Score); // Ordered descending
    }

    [Fact]
    public async Task SelectAgentAsync_SelectionReasonShowsBothSelectorResults()
    {
        // Arrange
        var embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { embedding, CreateNormalizedVector(0.0f, 1.0f, 0.0f) });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agent1 = CreateTestAgent("agent-1", "Coding expert",
            capabilities: new List<string> { "code" });
        var agent2 = CreateTestAgent("agent-2", "Writing expert",
            capabilities: new List<string> { "writing" });
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await hybridSelector.SelectAgentAsync("code review", agents);

        // Assert
        Assert.Contains("Keyword", result.SelectionReason);
        Assert.Contains("Embedding", result.SelectionReason);
        Assert.Contains("Hybrid selection", result.SelectionReason);
    }

    [Fact]
    public void HybridSelectorConfig_Balanced_Returns50_50Split()
    {
        // Arrange & Act
        var config = HybridSelectorConfig.Balanced;

        // Assert
        Assert.Equal(0.5, config.KeywordWeight);
        Assert.Equal(0.5, config.EmbeddingWeight);
    }

    [Fact]
    public void HybridSelectorConfig_KeywordFocused_Returns70_30Split()
    {
        // Arrange & Act
        var config = HybridSelectorConfig.KeywordFocused;

        // Assert
        Assert.Equal(0.7, config.KeywordWeight);
        Assert.Equal(0.3, config.EmbeddingWeight);
    }

    [Fact]
    public void HybridSelectorConfig_EmbeddingFocused_Returns30_70Split()
    {
        // Arrange & Act
        var config = HybridSelectorConfig.EmbeddingFocused;

        // Assert
        Assert.Equal(0.3, config.KeywordWeight);
        Assert.Equal(0.7, config.EmbeddingWeight);
    }

    [Fact]
    public async Task SelectAgentAsync_WithConfig_UsesConfigWeights()
    {
        // Arrange
        var embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { embedding, embedding });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var config = HybridSelectorConfig.KeywordFocused;
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector, config);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await hybridSelector.SelectAgentAsync("Test query", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Contains("70 %", result.SelectionReason); // Keyword weight (P0 format includes space)
        Assert.Contains("30 %", result.SelectionReason); // Embedding weight (P0 format includes space)
    }

    [Fact]
    public async Task SelectAgentAsync_RunnerUpMentionedInReason()
    {
        // Arrange
        var embedding1 = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var embedding2 = CreateNormalizedVector(0.8f, 0.2f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { embedding1, embedding2 });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agent1 = CreateTestAgent("best-agent", "Best agent");
        var agent2 = CreateTestAgent("runner-up", "Runner up agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await hybridSelector.SelectAgentAsync("Query", agents);

        // Assert
        Assert.Contains("Runner-up", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_AllScoresContainHybridScore()
    {
        // Arrange
        var embedding1 = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var embedding2 = CreateNormalizedVector(0.0f, 1.0f, 0.0f);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding1);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { embedding1, embedding2 });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await hybridSelector.SelectAgentAsync("Query", agents);

        // Assert
        Assert.Equal(2, result.AllScores.Count);
        foreach (var score in result.AllScores)
        {
            Assert.NotEmpty(score.Reasons);
            Assert.Contains("Hybrid", score.Reasons[0]);
        }
    }

    [Fact]
    public async Task SelectAgentAsync_WeightsNormalized_DifferentTotals()
    {
        // Arrange - Test with weights that don't sum to 1.0
        var embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { embedding, embedding });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        // Weights sum to 2.0, should be normalized to 0.5/0.5
        var hybridSelector = new HybridAgentSelector(
            keywordSelector, embeddingSelector,
            keywordWeight: 1.0, embeddingWeight: 1.0);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await hybridSelector.SelectAgentAsync("Test query", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Contains("50 %", result.SelectionReason); // Both weights should be 50% (P0 format includes space)
    }
}
