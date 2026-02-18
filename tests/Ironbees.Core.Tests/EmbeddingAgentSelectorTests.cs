using NSubstitute;

namespace Ironbees.Core.Tests;

public class EmbeddingAgentSelectorTests
{
    private readonly IEmbeddingProvider _mockEmbeddingProvider;

    public EmbeddingAgentSelectorTests()
    {
        _mockEmbeddingProvider = Substitute.For<IEmbeddingProvider>();
        _mockEmbeddingProvider.Dimensions.Returns(384);
        _mockEmbeddingProvider.ModelName.Returns("test-model");
    }

    private static IAgent CreateTestAgent(
        string name,
        string description,
        List<string>? capabilities = null,
        List<string>? tags = null)
    {
        var mockAgent = Substitute.For<IAgent>();
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

        mockAgent.Name.Returns(name);
        mockAgent.Description.Returns(description);
        mockAgent.Config.Returns(config);

        return mockAgent;
    }

    private static float[] CreateNormalizedVector(params float[] values)
    {
        return VectorSimilarity.Normalize(values);
    }

    [Fact]
    public async Task SelectAgentAsync_WithSemanticMatch_SelectsCorrectAgent()
    {
        // Arrange
        var codingEmbedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var writingEmbedding = CreateNormalizedVector(0.0f, 1.0f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(0.9f, 0.1f, 0.0f); // Closer to coding

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(queryEmbedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { codingEmbedding, writingEmbedding });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with coding tasks",
            capabilities: new List<string> { "code-generation" });

        var writingAgent = CreateTestAgent(
            "writing-agent",
            "Helps with writing tasks",
            capabilities: new List<string> { "content-writing" });

        var agents = new List<IAgent> { codingAgent, writingAgent };

        // Act
        var result = await selector.SelectAgentAsync("Write some code for me", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("coding-agent", result.SelectedAgent.Name);
        Assert.True(result.ConfidenceScore > 0.5);
    }

    [Fact]
    public async Task SelectAgentAsync_EmptyAgents_ReturnsNull()
    {
        // Arrange
        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);
        var agents = new List<IAgent>();

        // Act
        var result = await selector.SelectAgentAsync("Some input", agents);

        // Assert
        Assert.Null(result.SelectedAgent);
        Assert.Equal(0, result.ConfidenceScore);
        Assert.Equal("No agents available", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_SingleAgent_ReturnsAgentWithFullConfidence()
    {
        // Arrange
        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent = CreateTestAgent(
            "single-agent",
            "The only agent");

        var agents = new List<IAgent> { agent };

        // Act
        var result = await selector.SelectAgentAsync("Any query", agents);

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
        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);
        var agents = new List<IAgent>();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await selector.SelectAgentAsync(null!, agents));
    }

    [Fact]
    public async Task SelectAgentAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);
        var agents = new List<IAgent>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await selector.SelectAgentAsync("", agents));
    }

    [Fact]
    public async Task SelectAgentAsync_WhitespaceInput_ThrowsArgumentException()
    {
        // Arrange
        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);
        var agents = new List<IAgent>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await selector.SelectAgentAsync("   ", agents));
    }

    [Fact]
    public async Task SelectAgentAsync_NullAgents_ThrowsArgumentNullException()
    {
        // Arrange
        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await selector.SelectAgentAsync("test", null!));
    }

    [Fact]
    public async Task ScoreAgentsAsync_ReturnsOrderedScores()
    {
        // Arrange
        var agent1Embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var agent2Embedding = CreateNormalizedVector(0.0f, 1.0f, 0.0f);
        var agent3Embedding = CreateNormalizedVector(0.5f, 0.5f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(0.8f, 0.2f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(queryEmbedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { agent1Embedding, agent2Embedding, agent3Embedding });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agent3 = CreateTestAgent("agent-3", "Third agent");

        var agents = new List<IAgent> { agent1, agent2, agent3 };

        // Act
        var scores = await selector.ScoreAgentsAsync("Test query", agents);

        // Assert
        Assert.Equal(3, scores.Count);
        // Should be ordered by score descending
        Assert.True(scores[0].Score >= scores[1].Score);
        Assert.True(scores[1].Score >= scores[2].Score);
    }

    [Fact]
    public async Task SelectAgentAsync_IdenticalEmbeddings_ReturnsHighConfidence()
    {
        // Arrange
        var embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(embedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { embedding });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent = CreateTestAgent("perfect-match", "Exactly matching agent");
        var agents = new List<IAgent> { agent };

        // Note: Single agent returns 1.0 regardless of embedding
        // Let's test with two agents
        var agent2 = CreateTestAgent("other", "Other agent");
        var agent2Embedding = CreateNormalizedVector(0.0f, 1.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { embedding, agent2Embedding });

        agents = new List<IAgent> { agent, agent2 };

        // Act
        var result = await selector.SelectAgentAsync("Query", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("perfect-match", result.SelectedAgent.Name);
        Assert.True(result.ConfidenceScore >= 0.99);
    }

    [Fact]
    public async Task SelectAgentAsync_OrthogonalEmbeddings_ReturnsZeroConfidence()
    {
        // Arrange
        var agentEmbedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(0.0f, 1.0f, 0.0f); // Orthogonal

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(queryEmbedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { agentEmbedding, agentEmbedding }); // Two agents with same embedding

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await selector.SelectAgentAsync("Unrelated query", agents);

        // Assert
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal(0.0, result.ConfidenceScore, precision: 5);
    }

    [Fact]
    public async Task WarmupCacheAsync_CachesAgentEmbeddings()
    {
        // Arrange
        var embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { embedding });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent = CreateTestAgent("cached-agent", "Agent to cache");
        var agents = new List<IAgent> { agent };

        // Act
        await selector.WarmupCacheAsync(agents);

        // Verify embeddings were generated
        await _mockEmbeddingProvider.Received(1)
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());

        // Now make a selection - should not call GenerateEmbeddingsAsync again
        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(embedding);

        await selector.SelectAgentAsync("Query", agents);

        // Should still be only once (cached)
        await _mockEmbeddingProvider.Received(1)
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearCache_RemovesCachedEmbeddings()
    {
        // Arrange
        var embedding1 = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var embedding2 = CreateNormalizedVector(0.0f, 1.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { embedding1, embedding2 });

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(embedding1);

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        // Need at least 2 agents to trigger embedding generation (single agent returns 1.0 without embedding)
        var agent1 = CreateTestAgent("agent-1", "Test agent 1");
        var agent2 = CreateTestAgent("agent-2", "Test agent 2");
        var agents = new List<IAgent> { agent1, agent2 };

        // Warm up cache
        await selector.WarmupCacheAsync(agents);

        // Act
        selector.ClearCache();

        // Now selection should regenerate embeddings
        await selector.SelectAgentAsync("Query", agents);

        // Assert - Should have called GenerateEmbeddingsAsync twice now
        await _mockEmbeddingProvider.Received(2)
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAgentAsync_SelectionReasonContainsSimilarity()
    {
        // Arrange
        var embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(embedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { embedding, CreateNormalizedVector(0.0f, 1.0f, 0.0f) });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent1 = CreateTestAgent("best-agent", "Best agent");
        var agent2 = CreateTestAgent("other-agent", "Other agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await selector.SelectAgentAsync("Query", agents);

        // Assert
        Assert.Contains("semantic similarity", result.SelectionReason);
        Assert.Contains("best-agent", result.SelectionReason);
    }

    [Fact]
    public async Task SelectAgentAsync_AllScoresContainReasons()
    {
        // Arrange
        var embedding1 = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var embedding2 = CreateNormalizedVector(0.0f, 1.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(embedding1);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { embedding1, embedding2 });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await selector.SelectAgentAsync("Query", agents);

        // Assert
        Assert.Equal(2, result.AllScores.Count);
        foreach (var score in result.AllScores)
        {
            Assert.NotEmpty(score.Reasons);
            Assert.Contains("Semantic similarity", score.Reasons[0]);
        }
    }

    [Fact]
    public void Constructor_NullEmbeddingProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EmbeddingAgentSelector(null!));
    }

    [Fact]
    public async Task SelectAgentAsync_ConcurrentCalls_DoesNotCauseRaceCondition()
    {
        // Arrange
        var embedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(embedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Simulate some delay
                Thread.Sleep(10);
                var texts = callInfo.ArgAt<IReadOnlyList<string>>(0);
                return texts.Select(_ => embedding).ToArray();
            });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act - Run multiple concurrent selections
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => selector.SelectAgentAsync("Query", agents))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All should complete successfully
        Assert.All(results, r => Assert.NotNull(r.SelectedAgent));
    }

    [Fact]
    public async Task SelectAgentAsync_NegativeCosineSimilarity_NormalizedToZero()
    {
        // Arrange
        var agentEmbedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(-1.0f, 0.0f, 0.0f); // Opposite direction

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(queryEmbedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { agentEmbedding, agentEmbedding });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent1 = CreateTestAgent("agent-1", "First agent");
        var agent2 = CreateTestAgent("agent-2", "Second agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await selector.SelectAgentAsync("Opposite query", agents);

        // Assert - Negative similarity should be normalized to 0
        Assert.True(result.ConfidenceScore >= 0.0);
    }

    [Fact]
    public async Task SelectAgentAsync_RunnerUpMentionedInReason()
    {
        // Arrange
        var embedding1 = CreateNormalizedVector(0.9f, 0.1f, 0.0f);
        var embedding2 = CreateNormalizedVector(0.7f, 0.3f, 0.0f);
        var queryEmbedding = CreateNormalizedVector(1.0f, 0.0f, 0.0f);

        _mockEmbeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(queryEmbedding);

        _mockEmbeddingProvider
            .GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { embedding1, embedding2 });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider);

        var agent1 = CreateTestAgent("best-agent", "Best agent");
        var agent2 = CreateTestAgent("runner-up", "Runner up agent");
        var agents = new List<IAgent> { agent1, agent2 };

        // Act
        var result = await selector.SelectAgentAsync("Query", agents);

        // Assert
        Assert.Contains("Runner-up", result.SelectionReason);
        Assert.Contains("runner-up", result.SelectionReason);
    }
}
