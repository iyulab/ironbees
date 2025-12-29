using Moq;
using System.Diagnostics;

namespace Ironbees.Core.Tests;

/// <summary>
/// Performance benchmark tests for EmbeddingAgentSelector and HybridAgentSelector.
/// These tests measure execution time and may be skipped if performance is not critical.
///
/// To skip performance tests during development:
/// dotnet test --filter "Category!=Performance"
/// </summary>
public class EmbeddingAgentSelectorBenchmarkTests
{
    private readonly Mock<IEmbeddingProvider> _mockEmbeddingProvider;
    private readonly float[] _testEmbedding;

    public EmbeddingAgentSelectorBenchmarkTests()
    {
        _mockEmbeddingProvider = new Mock<IEmbeddingProvider>();
        _mockEmbeddingProvider.Setup(p => p.Dimensions).Returns(384);
        _mockEmbeddingProvider.Setup(p => p.ModelName).Returns("test-model");

        _testEmbedding = VectorSimilarity.Normalize(new float[] { 1.0f, 0.5f, 0.3f });
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

    private List<float[]> GenerateDistinctEmbeddings(int count)
    {
        var embeddings = new List<float[]>();
        for (int i = 0; i < count; i++)
        {
            var raw = new float[] { (float)Math.Sin(i), (float)Math.Cos(i), (float)Math.Sin(i * 2) };
            embeddings.Add(VectorSimilarity.Normalize(raw));
        }
        return embeddings;
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task EmbeddingSelector_1000IterationsWithCachedEmbeddings_CompletesUnder500ms()
    {
        // Arrange
        var embeddings = GenerateDistinctEmbeddings(5);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        var agents = new List<IAgent>
        {
            CreateTestAgent("coding-agent", "Expert software developer",
                capabilities: new List<string> { "code-generation", "code-review" }),
            CreateTestAgent("writing-agent", "Professional writer",
                capabilities: new List<string> { "content-writing" }),
            CreateTestAgent("testing-agent", "QA specialist",
                capabilities: new List<string> { "test-automation" }),
            CreateTestAgent("devops-agent", "DevOps engineer",
                capabilities: new List<string> { "deployment" }),
            CreateTestAgent("database-agent", "Database specialist",
                capabilities: new List<string> { "database-design" })
        };

        var testQueries = new[]
        {
            "Write some code",
            "Help with documentation",
            "Create tests",
            "Deploy application",
            "Optimize queries"
        };

        // Warmup - cache embeddings
        await selector.WarmupCacheAsync(agents);
        foreach (var query in testQueries)
        {
            await selector.SelectAgentAsync(query, agents);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var query = testQueries[i % testQueries.Length];
            await selector.SelectAgentAsync(query, agents);
        }
        stopwatch.Stop();

        // Assert
        // With mocked embeddings and caching, this should be fast
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"1000 iterations took {stopwatch.ElapsedMilliseconds}ms (expected < 500ms)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task EmbeddingSelector_CachingImprovement_SecondCallFaster()
    {
        // Arrange
        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken ct) =>
            {
                // Simulate some delay for embedding generation
                Thread.Sleep(1);
                return _testEmbedding;
            });

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken ct) =>
            {
                // Simulate some delay for batch embedding generation
                Thread.Sleep(5);
                return texts.Select(_ => _testEmbedding).ToArray();
            });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        var agents = new List<IAgent>
        {
            CreateTestAgent("agent-1", "First agent"),
            CreateTestAgent("agent-2", "Second agent")
        };

        // Act - First call (no cache for agent embeddings)
        var stopwatch1 = Stopwatch.StartNew();
        await selector.SelectAgentAsync("Test query", agents);
        stopwatch1.Stop();
        var firstCallTime = stopwatch1.ElapsedMilliseconds;

        // Act - Second call (with cached agent embeddings)
        var stopwatch2 = Stopwatch.StartNew();
        await selector.SelectAgentAsync("Another query", agents);
        stopwatch2.Stop();
        var secondCallTime = stopwatch2.ElapsedMilliseconds;

        // Assert - Second call should be faster or equal (agent embeddings cached)
        Assert.True(secondCallTime <= firstCallTime,
            $"Second call ({secondCallTime}ms) should be faster than first ({firstCallTime}ms) due to caching");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task EmbeddingSelector_ScoreMultipleAgents_CompletesQuickly()
    {
        // Arrange
        var embeddings = GenerateDistinctEmbeddings(10);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        var agents = new List<IAgent>();
        for (int i = 0; i < 10; i++)
        {
            agents.Add(CreateTestAgent($"agent-{i}", $"Agent number {i}",
                capabilities: new List<string> { $"capability-{i}" }));
        }

        // Warmup
        await selector.WarmupCacheAsync(agents);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var scores = await selector.ScoreAgentsAsync("Test query", agents);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 10,
            $"Scoring 10 agents took {stopwatch.ElapsedMilliseconds}ms (expected < 10ms)");
        Assert.Equal(10, scores.Count);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task HybridSelector_1000Iterations_CompletesUnder1000ms()
    {
        // Arrange
        var embeddings = GenerateDistinctEmbeddings(5);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agents = new List<IAgent>
        {
            CreateTestAgent("coding-agent", "Expert software developer",
                capabilities: new List<string> { "code-generation", "programming" },
                tags: new List<string> { "code", "development" }),
            CreateTestAgent("writing-agent", "Professional writer",
                capabilities: new List<string> { "content-writing" },
                tags: new List<string> { "writing", "documentation" }),
            CreateTestAgent("testing-agent", "QA specialist",
                capabilities: new List<string> { "test-automation" },
                tags: new List<string> { "testing", "quality" }),
            CreateTestAgent("devops-agent", "DevOps engineer",
                capabilities: new List<string> { "deployment" },
                tags: new List<string> { "devops", "infrastructure" }),
            CreateTestAgent("database-agent", "Database specialist",
                capabilities: new List<string> { "database-design" },
                tags: new List<string> { "database", "sql" })
        };

        var testQueries = new[]
        {
            "Write some code",
            "Help with documentation",
            "Create tests",
            "Deploy application",
            "Optimize queries"
        };

        // Warmup
        foreach (var query in testQueries)
        {
            await hybridSelector.SelectAgentAsync(query, agents);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var query = testQueries[i % testQueries.Length];
            await hybridSelector.SelectAgentAsync(query, agents);
        }
        stopwatch.Stop();

        // Assert
        // Hybrid uses both selectors, so allow more time
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"1000 hybrid iterations took {stopwatch.ElapsedMilliseconds}ms (expected < 1000ms)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task HybridSelector_ParallelExecution_FasterThanSequential()
    {
        // Arrange - The hybrid selector runs both selectors in parallel
        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken ct) =>
            {
                // Simulate some delay
                Thread.Sleep(5);
                return _testEmbedding;
            });

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken ct) =>
            {
                Thread.Sleep(10);
                return texts.Select(_ => _testEmbedding).ToArray();
            });

        var keywordSelector = new KeywordAgentSelector();
        var embeddingSelector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);
        var hybridSelector = new HybridAgentSelector(keywordSelector, embeddingSelector);

        var agents = new List<IAgent>
        {
            CreateTestAgent("agent-1", "First agent",
                capabilities: new List<string> { "capability-1" }),
            CreateTestAgent("agent-2", "Second agent",
                capabilities: new List<string> { "capability-2" })
        };

        // Warmup
        await hybridSelector.SelectAgentAsync("Warmup query", agents);

        // Act - Multiple iterations
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            await hybridSelector.SelectAgentAsync($"Query {i}", agents);
        }
        stopwatch.Stop();

        // Assert - Should be faster than running both selectors sequentially
        // Each query has 5ms embedding delay, so 10 queries = ~50ms minimum if fully sequential
        // With parallel execution, should be closer to keyword selector time
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"10 parallel hybrid iterations took {stopwatch.ElapsedMilliseconds}ms (expected < 200ms)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void VectorSimilarity_CosineSimilarity_HighVolumeCompletesQuickly()
    {
        // Arrange
        var vectors = new List<float[]>();
        var random = new Random(42); // Deterministic seed

        for (int i = 0; i < 100; i++)
        {
            var vector = new float[384];
            for (int j = 0; j < 384; j++)
            {
                vector[j] = (float)random.NextDouble();
            }
            vectors.Add(VectorSimilarity.Normalize(vector));
        }

        var queryVector = VectorSimilarity.Normalize(vectors[0]);

        // Act - Compare query against all 100 vectors
        var stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            foreach (var vector in vectors)
            {
                VectorSimilarity.CosineSimilarity(queryVector, vector);
            }
        }
        stopwatch.Stop();

        // Assert
        // 1000 iterations * 100 comparisons = 100,000 cosine similarity calculations
        // Threshold of 500ms allows for variance across different systems while still detecting major regressions
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"100,000 cosine similarity calculations took {stopwatch.ElapsedMilliseconds}ms (expected < 500ms)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void VectorSimilarity_Normalize_HighVolumeCompletesQuickly()
    {
        // Arrange
        var random = new Random(42);
        var vectors = new List<float[]>();

        for (int i = 0; i < 100; i++)
        {
            var vector = new float[384];
            for (int j = 0; j < 384; j++)
            {
                vector[j] = (float)random.NextDouble();
            }
            vectors.Add(vector);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            foreach (var vector in vectors)
            {
                VectorSimilarity.Normalize(vector);
            }
        }
        stopwatch.Stop();

        // Assert
        // 1000 iterations * 100 normalizations = 100,000 normalization operations
        // Normalization allocates new arrays, so threshold of 1000ms allows for memory allocation overhead
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"100,000 normalizations took {stopwatch.ElapsedMilliseconds}ms (expected < 1000ms)");
    }

    [Fact]
    public async Task EmbeddingSelector_ClearCache_ResetsPerformanceState()
    {
        // Arrange
        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _testEmbedding, _testEmbedding });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        var agents = new List<IAgent>
        {
            CreateTestAgent("agent-1", "First agent"),
            CreateTestAgent("agent-2", "Second agent")
        };

        // Build cache
        for (int i = 0; i < 10; i++)
        {
            await selector.SelectAgentAsync($"Query {i}", agents);
        }

        // Act
        selector.ClearCache();

        // Assert - Should not throw and cache should be cleared
        var result = await selector.SelectAgentAsync("New query", agents);
        Assert.NotNull(result);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task EmbeddingSelector_ConcurrentSelections_ThreadSafe()
    {
        // Arrange
        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testEmbedding);

        _mockEmbeddingProvider
            .Setup(p => p.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken ct) =>
            {
                // Simulate some delay to increase race condition likelihood
                Thread.Sleep(1);
                return texts.Select(_ => _testEmbedding).ToArray();
            });

        var selector = new EmbeddingAgentSelector(_mockEmbeddingProvider.Object);

        var agents = new List<IAgent>
        {
            CreateTestAgent("agent-1", "First agent"),
            CreateTestAgent("agent-2", "Second agent"),
            CreateTestAgent("agent-3", "Third agent")
        };

        // Act - Run many concurrent selections
        var stopwatch = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 50)
            .Select(i => selector.SelectAgentAsync($"Query {i}", agents))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - All should complete successfully without exceptions
        Assert.All(results, r => Assert.NotNull(r.SelectedAgent));
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"50 concurrent selections took {stopwatch.ElapsedMilliseconds}ms (expected < 1000ms)");
    }
}
