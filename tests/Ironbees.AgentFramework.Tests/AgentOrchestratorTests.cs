using Ironbees.Core;
using Moq;
using System.Runtime.CompilerServices;

namespace Ironbees.AgentFramework.Tests;

public class AgentOrchestratorTests
{
    // Helper method to create IAsyncEnumerable from array
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield(); // Simulate async behavior
            yield return item;
        }
    }

    [Fact]
    public async Task LoadAgentsAsync_WithValidConfigs_RegistersAgents()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var configs = new List<AgentConfig>
        {
            new AgentConfig
            {
                Name = "agent1",
                Description = "Test agent 1",
                Version = "1.0.0",
                SystemPrompt = "You are agent 1",
                Model = new ModelConfig
                {
                    Deployment = "gpt-4",
                    Temperature = 0.7,
                    MaxTokens = 1000
                }
            },
            new AgentConfig
            {
                Name = "agent2",
                Description = "Test agent 2",
                Version = "1.0.0",
                SystemPrompt = "You are agent 2",
                Model = new ModelConfig
                {
                    Deployment = "gpt-4",
                    Temperature = 0.7,
                    MaxTokens = 1000
                }
            }
        };

        mockLoader.Setup(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var mockAgent1 = new Mock<IAgent>();
        mockAgent1.Setup(a => a.Name).Returns("agent1");
        mockAgent1.Setup(a => a.Description).Returns("Test agent 1");

        var mockAgent2 = new Mock<IAgent>();
        mockAgent2.Setup(a => a.Name).Returns("agent2");
        mockAgent2.Setup(a => a.Description).Returns("Test agent 2");

        mockAdapter.SetupSequence(a => a.CreateAgentAsync(It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAgent1.Object)
            .ReturnsAsync(mockAgent2.Object);

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "agent1", "agent2" });

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        await orchestrator.LoadAgentsAsync();

        // Assert
        mockLoader.Verify(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockAdapter.Verify(a => a.CreateAgentAsync(It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        mockRegistry.Verify(r => r.Register("agent1", It.IsAny<IAgent>()), Times.Once);
        mockRegistry.Verify(r => r.Register("agent2", It.IsAny<IAgent>()), Times.Once);
    }

    [Fact]
    public async Task LoadAgentsAsync_NoConfigs_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockLoader.Setup(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentConfig>());

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<AgentLoadException>(
            async () => await orchestrator.LoadAgentsAsync());
    }

    [Fact]
    public async Task LoadAgentsAsync_AllAgentsFail_ThrowsAgentLoadException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var configs = new List<AgentConfig>
        {
            new AgentConfig
            {
                Name = "agent1",
                Description = "Test agent 1",
                Version = "1.0.0",
                SystemPrompt = "You are agent 1",
                Model = new ModelConfig
                {
                    Deployment = "gpt-4",
                    Temperature = 0.7,
                    MaxTokens = 1000
                }
            }
        };

        mockLoader.Setup(l => l.LoadAllConfigsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        mockAdapter.Setup(a => a.CreateAgentAsync(It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Failed to create agent"));

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string>());

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AgentLoadException>(
            async () => await orchestrator.LoadAgentsAsync());
        Assert.NotNull(ex.FailedAgents);
        Assert.Single(ex.FailedAgents);
    }

    [Fact]
    public async Task ProcessAsync_WithAgentName_CallsFrameworkAdapter()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");

        mockRegistry.Setup(r => r.Get("test-agent"))
            .Returns(mockAgent.Object);

        mockAdapter.Setup(a => a.RunAsync(It.IsAny<IAgent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test response");

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = await orchestrator.ProcessAsync("test input", "test-agent");

        // Assert
        Assert.Equal("Test response", result);
        mockRegistry.Verify(r => r.Get("test-agent"), Times.Once);
        mockAdapter.Verify(a => a.RunAsync(mockAgent.Object, "test input", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_AgentNotFound_ThrowsAgentNotFoundException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.Get("nonexistent-agent"))
            .Returns((IAgent?)null);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<AgentNotFoundException>(
            async () => await orchestrator.ProcessAsync("test input", "nonexistent-agent"));
    }

    [Fact]
    public async Task ProcessAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await orchestrator.ProcessAsync("", "test-agent"));
    }

    [Fact]
    public async Task ProcessAsync_AutoSelect_UsesFirstAgent()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("first-agent");

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "first-agent", "second-agent" });

        mockRegistry.Setup(r => r.Get("first-agent"))
            .Returns(mockAgent.Object);

        mockAdapter.Setup(a => a.RunAsync(It.IsAny<IAgent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test response");

        var mockSelector = new Mock<IAgentSelector>();
        mockSelector.Setup(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = mockAgent.Object,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = new List<AgentScore>()
            });

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = await orchestrator.ProcessAsync("test input");

        // Assert
        Assert.Equal("Test response", result);
        mockRegistry.Verify(r => r.ListAgents(), Times.Once);
        mockSelector.Verify(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_AutoSelectNoAgents_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string>());

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await orchestrator.ProcessAsync("test input"));
    }

    [Fact(Skip = "Requires proper mocking of IAsyncEnumerable")]
    public void StreamAsync_WithAgentName_ReturnsStream()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");

        mockRegistry.Setup(r => r.Get("test-agent"))
            .Returns(mockAgent.Object);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var stream = orchestrator.StreamAsync("test input", "test-agent");

        // Assert
        Assert.NotNull(stream);
        mockRegistry.Verify(r => r.Get("test-agent"), Times.Once);
    }

    [Fact]
    public void StreamAsync_AgentNotFound_ThrowsAgentNotFoundException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.Get("nonexistent-agent"))
            .Returns((IAgent?)null);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        Assert.Throws<AgentNotFoundException>(
            () => orchestrator.StreamAsync("test input", "nonexistent-agent"));
    }

    [Fact]
    public void ListAgents_ReturnsRegisteredAgents()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var agents = new List<string> { "agent1", "agent2", "agent3" };
        mockRegistry.Setup(r => r.ListAgents())
            .Returns(agents);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = orchestrator.ListAgents();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("agent1", result);
        Assert.Contains("agent2", result);
        Assert.Contains("agent3", result);
    }

    [Fact]
    public void GetAgent_ExistingAgent_ReturnsAgent()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");

        mockRegistry.Setup(r => r.Get("test-agent"))
            .Returns(mockAgent.Object);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = orchestrator.GetAgent("test-agent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-agent", result.Name);
    }

    [Fact]
    public void GetAgent_NonexistentAgent_ReturnsNull()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.Get("nonexistent-agent"))
            .Returns((IAgent?)null);

        var mockSelector = new Mock<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = orchestrator.GetAgent("nonexistent-agent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_NullLoader_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        // Act & Assert
        var mockSelector = new Mock<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(null!, mockRegistry.Object, mockAdapter.Object, mockSelector.Object));
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        // Act & Assert
        var mockSelector = new Mock<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader.Object, null!, mockAdapter.Object, mockSelector.Object));
    }

    [Fact]
    public void Constructor_NullAdapter_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();

        // Act & Assert
        var mockSelector = new Mock<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader.Object, mockRegistry.Object, null!, mockSelector.Object));
    }

    [Fact]
    public void Constructor_NullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader.Object, mockRegistry.Object, mockAdapter.Object, null!));
    }

    [Fact]
    public async Task StreamAsync_AutoSelect_ReturnsChunks()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "test-agent" });

        mockRegistry.Setup(r => r.Get("test-agent"))
            .Returns(mockAgent.Object);

        // Mock streaming response
        mockAdapter.Setup(a => a.StreamAsync(It.IsAny<IAgent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable("Hello", " ", "World"));

        var mockSelector = new Mock<IAgentSelector>();
        mockSelector.Setup(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = mockAgent.Object,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = new List<AgentScore>()
            });

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = new List<string>();
        await foreach (var chunk in orchestrator.StreamAsync("test input"))
        {
            result.Add(chunk);
        }

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Hello", result[0]);
        Assert.Equal(" ", result[1]);
        Assert.Equal("World", result[2]);
        mockSelector.Verify(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        mockAdapter.Verify(a => a.StreamAsync(
            mockAgent.Object,
            "test input",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamAsync_AutoSelectNoAgent_ReturnsWarningMessage()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "some-agent" });

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("some-agent");

        mockRegistry.Setup(r => r.Get("some-agent"))
            .Returns(mockAgent.Object);

        var mockSelector = new Mock<IAgentSelector>();
        mockSelector.Setup(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = null,
                ConfidenceScore = 0.0,
                SelectionReason = "No suitable agent found",
                AllScores = new List<AgentScore>()
            });

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act
        var result = new List<string>();
        await foreach (var chunk in orchestrator.StreamAsync("test input"))
        {
            result.Add(chunk);
        }

        // Assert
        Assert.Single(result);
        Assert.Contains("No suitable agent found", result[0]);
        Assert.Contains("⚠️", result[0]);
        mockSelector.Verify(s => s.SelectAgentAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<IAgent>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        mockAdapter.Verify(a => a.StreamAsync(
            It.IsAny<IAgent>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StreamAsync_AutoSelectEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();
        var mockSelector = new Mock<IAgentSelector>();

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in orchestrator.StreamAsync(""))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task StreamAsync_AutoSelect_LargeStream_NoMemoryLeak()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();
        var mockSelector = new Mock<IAgentSelector>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");
        mockAgent.Setup(a => a.Description).Returns("Test agent");

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "test-agent" }.AsReadOnly());

        mockSelector.Setup(s => s.SelectAgentAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<IAgent>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = mockAgent.Object,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = Array.Empty<AgentScore>()
            });

        // Generate large stream (1000 chunks)
        var largeStream = Enumerable.Range(0, 1000)
            .Select(i => $"chunk_{i}");

        mockAdapter.Setup(a => a.StreamAsync(
                It.IsAny<IAgent>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(largeStream.ToArray()));

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act - consume entire stream
        var collectedChunks = new List<string>();
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        await foreach (var chunk in orchestrator.StreamAsync("Large stream test"))
        {
            collectedChunks.Add(chunk);
        }

        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        var memoryGrowth = finalMemory - initialMemory;

        // Assert
        Assert.Equal(1000, collectedChunks.Count);
        // Memory growth should be reasonable (< 10MB for 1000 small strings)
        Assert.True(memoryGrowth < 10 * 1024 * 1024,
            $"Memory grew by {memoryGrowth / 1024 / 1024}MB, expected < 10MB");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task StreamAsync_AutoSelect_CancellationToken_ProperCleanup()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();
        var mockSelector = new Mock<IAgentSelector>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");
        mockAgent.Setup(a => a.Description).Returns("Test agent");

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "test-agent" }.AsReadOnly());

        mockSelector.Setup(s => s.SelectAgentAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<IAgent>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = mockAgent.Object,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = Array.Empty<AgentScore>()
            });

        var cts = new CancellationTokenSource();

        // Create a stream that respects cancellation
        async IAsyncEnumerable<string> CancellableStream([EnumeratorCancellation] CancellationToken ct)
        {
            for (int i = 0; i < 1000; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1, ct);
                yield return $"chunk_{i}";
            }
        }

        mockAdapter.Setup(a => a.StreamAsync(
                It.IsAny<IAgent>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((IAgent a, string i, CancellationToken ct) => CancellableStream(ct));

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act - cancel after 10 chunks
        var collectedChunks = new List<string>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in orchestrator.StreamAsync("Test", cts.Token))
            {
                collectedChunks.Add(chunk);
                if (collectedChunks.Count >= 10)
                {
                    cts.Cancel();
                }
            }
        });

        // Assert
        Assert.True(collectedChunks.Count >= 10, "Should have collected at least 10 chunks before cancellation");
        Assert.True(collectedChunks.Count < 1000, "Should not have collected all chunks");
        // Successful cancellation means OperationCanceledException was thrown
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task StreamAsync_AutoSelect_MultipleConcurrentStreams_NoResourceLeak()
    {
        // Arrange
        var mockLoader = new Mock<IAgentLoader>();
        var mockRegistry = new Mock<IAgentRegistry>();
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();
        var mockSelector = new Mock<IAgentSelector>();

        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("test-agent");
        mockAgent.Setup(a => a.Description).Returns("Test agent");

        mockRegistry.Setup(r => r.ListAgents())
            .Returns(new List<string> { "test-agent" }.AsReadOnly());

        mockSelector.Setup(s => s.SelectAgentAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<IAgent>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionResult
            {
                SelectedAgent = mockAgent.Object,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = Array.Empty<AgentScore>()
            });

        // Each stream returns 100 chunks
        mockAdapter.Setup(a => a.StreamAsync(
                It.IsAny<IAgent>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((IAgent a, string input, CancellationToken ct) =>
                ToAsyncEnumerable(Enumerable.Range(0, 100)
                    .Select(i => $"{input}_chunk_{i}")
                    .ToArray()));

        var orchestrator = new AgentOrchestrator(
            mockLoader.Object,
            mockRegistry.Object,
            mockAdapter.Object,
            mockSelector.Object);

        // Act - run 10 concurrent streams
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        var tasks = Enumerable.Range(0, 10).Select(async streamId =>
        {
            var chunks = new List<string>();
            await foreach (var chunk in orchestrator.StreamAsync($"stream_{streamId}"))
            {
                chunks.Add(chunk);
            }
            return chunks;
        });

        var results = await Task.WhenAll(tasks);

        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        var memoryGrowth = finalMemory - initialMemory;

        // Assert
        Assert.Equal(10, results.Length);
        Assert.All(results, r => Assert.Equal(100, r.Count));
        // Memory growth should be reasonable for 10 concurrent streams
        Assert.True(memoryGrowth < 20 * 1024 * 1024,
            $"Memory grew by {memoryGrowth / 1024 / 1024}MB, expected < 20MB");

        // Verify each stream got its own data
        for (int i = 0; i < 10; i++)
        {
            Assert.All(results[i], chunk => Assert.StartsWith($"stream_{i}_", chunk));
        }
    }
}
