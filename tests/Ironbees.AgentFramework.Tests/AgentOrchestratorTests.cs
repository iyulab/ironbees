using Ironbees.Core;
using NSubstitute;
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
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

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

        mockLoader.LoadAllConfigsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(configs);

        var mockAgent1 = Substitute.For<IAgent>();
        mockAgent1.Name.Returns("agent1");
        mockAgent1.Description.Returns("Test agent 1");

        var mockAgent2 = Substitute.For<IAgent>();
        mockAgent2.Name.Returns("agent2");
        mockAgent2.Description.Returns("Test agent 2");

        mockAdapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns(mockAgent1, mockAgent2);

        mockRegistry.ListAgents()
            .Returns(new List<string> { "agent1", "agent2" });

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act
        await orchestrator.LoadAgentsAsync();

        // Assert
        await mockLoader.Received(1).LoadAllConfigsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await mockAdapter.Received(2).CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>());
        mockRegistry.Received(1).Register("agent1", Arg.Any<IAgent>());
        mockRegistry.Received(1).Register("agent2", Arg.Any<IAgent>());
    }

    [Fact]
    public async Task LoadAgentsAsync_NoConfigs_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        mockLoader.LoadAllConfigsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentConfig>());

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act & Assert
        await Assert.ThrowsAsync<AgentLoadException>(
            async () => await orchestrator.LoadAgentsAsync());
    }

    [Fact]
    public async Task LoadAgentsAsync_AllAgentsFail_ThrowsAgentLoadException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

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

        mockLoader.LoadAllConfigsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(configs);

        mockAdapter.CreateAgentAsync(Arg.Any<AgentConfig>(), Arg.Any<CancellationToken>())
            .Returns<IAgent>(x => throw new InvalidOperationException("Failed to create agent"));

        mockRegistry.ListAgents()
            .Returns(new List<string>());

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("test-agent");

        mockRegistry.GetAgent("test-agent")
            .Returns(mockAgent);

        mockAdapter.RunAsync(Arg.Any<IAgent>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Test response");

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act
        var result = await orchestrator.ProcessAsync("test input", "test-agent");

        // Assert
        Assert.Equal("Test response", result);
        mockRegistry.Received(1).GetAgent("test-agent");
        await mockAdapter.Received(1).RunAsync(mockAgent, "test input", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_AgentNotFound_ThrowsAgentNotFoundException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        mockRegistry.GetAgent("nonexistent-agent")
            .Returns((IAgent?)null);

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act & Assert
        await Assert.ThrowsAsync<AgentNotFoundException>(
            async () => await orchestrator.ProcessAsync("test input", "nonexistent-agent"));
    }

    [Fact]
    public async Task ProcessAsync_EmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await orchestrator.ProcessAsync("", "test-agent"));
    }

    [Fact]
    public async Task ProcessAsync_AutoSelect_UsesFirstAgent()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("first-agent");

        mockRegistry.ListAgents()
            .Returns(new List<string> { "first-agent", "second-agent" });

        mockRegistry.GetAgent("first-agent")
            .Returns(mockAgent);

        mockAdapter.RunAsync(Arg.Any<IAgent>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Test response");

        var mockSelector = Substitute.For<IAgentSelector>();
        mockSelector.SelectAgentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new AgentSelectionResult
            {
                SelectedAgent = mockAgent,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = new List<AgentScore>()
            });

        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act
        var result = await orchestrator.ProcessAsync("test input");

        // Assert
        Assert.Equal("Test response", result);
        mockRegistry.Received(1).ListAgents();
        await mockSelector.Received(1).SelectAgentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_AutoSelectNoAgents_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        mockRegistry.ListAgents()
            .Returns(new List<string>());

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await orchestrator.ProcessAsync("test input"));
    }

    [Fact]
    public void StreamAsync_WithAgentName_ReturnsStream()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("test-agent");

        mockRegistry.GetAgent("test-agent")
            .Returns(mockAgent);

        mockAdapter.StreamAsync(
            Arg.Any<IAgent>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<string>());

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act
        var stream = orchestrator.StreamAsync("test input", "test-agent");

        // Assert
        Assert.NotNull(stream);
        mockRegistry.Received(1).GetAgent("test-agent");
    }

    [Fact]
    public void StreamAsync_AgentNotFound_ThrowsAgentNotFoundException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        mockRegistry.GetAgent("nonexistent-agent")
            .Returns((IAgent?)null);

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act & Assert
        Assert.Throws<AgentNotFoundException>(
            () => orchestrator.StreamAsync("test input", "nonexistent-agent"));
    }

    [Fact]
    public void ListAgents_ReturnsRegisteredAgents()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        var agents = new List<string> { "agent1", "agent2", "agent3" };
        mockRegistry.ListAgents()
            .Returns(agents);

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("test-agent");

        mockRegistry.GetAgent("test-agent")
            .Returns(mockAgent);

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        mockRegistry.GetAgent("nonexistent-agent")
            .Returns((IAgent?)null);

        var mockSelector = Substitute.For<IAgentSelector>();
        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act
        var result = orchestrator.GetAgent("nonexistent-agent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_NullLoader_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        // Act & Assert
        var mockSelector = Substitute.For<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(null!, mockRegistry, mockAdapter, mockSelector));
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        // Act & Assert
        var mockSelector = Substitute.For<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader, null!, mockAdapter, mockSelector));
    }

    [Fact]
    public void Constructor_NullAdapter_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();

        // Act & Assert
        var mockSelector = Substitute.For<IAgentSelector>();
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader, mockRegistry, null!, mockSelector));
    }

    [Fact]
    public void Constructor_NullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new AgentOrchestrator(mockLoader, mockRegistry, mockAdapter, null!));
    }

    [Fact]
    public async Task StreamAsync_AutoSelect_ReturnsChunks()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("test-agent");

        mockRegistry.ListAgents()
            .Returns(new List<string> { "test-agent" });

        mockRegistry.GetAgent("test-agent")
            .Returns(mockAgent);

        // Mock streaming response
        mockAdapter.StreamAsync(Arg.Any<IAgent>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable("Hello", " ", "World"));

        var mockSelector = Substitute.For<IAgentSelector>();
        mockSelector.SelectAgentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new AgentSelectionResult
            {
                SelectedAgent = mockAgent,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = new List<AgentScore>()
            });

        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
        await mockSelector.Received(1).SelectAgentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>());
        mockAdapter.Received(1).StreamAsync(
            mockAgent,
            "test input",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_AutoSelectNoAgent_ReturnsWarningMessage()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();

        mockRegistry.ListAgents()
            .Returns(new List<string> { "some-agent" });

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("some-agent");

        mockRegistry.GetAgent("some-agent")
            .Returns(mockAgent);

        var mockSelector = Substitute.For<IAgentSelector>();
        mockSelector.SelectAgentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>())
            .Returns(new AgentSelectionResult
            {
                SelectedAgent = null,
                ConfidenceScore = 0.0,
                SelectionReason = "No suitable agent found",
                AllScores = new List<AgentScore>()
            });

        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

        // Act
        var result = new List<string>();
        await foreach (var chunk in orchestrator.StreamAsync("test input"))
        {
            result.Add(chunk);
        }

        // Assert
        Assert.Single(result);
        Assert.Contains("No suitable agent found", result[0]);
        Assert.Contains("\u26a0\ufe0f", result[0]);
        await mockSelector.Received(1).SelectAgentAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<IAgent>>(),
            Arg.Any<CancellationToken>());
        mockAdapter.DidNotReceive().StreamAsync(
            Arg.Any<IAgent>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_AutoSelectEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();
        var mockSelector = Substitute.For<IAgentSelector>();

        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();
        var mockSelector = Substitute.For<IAgentSelector>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("test-agent");
        mockAgent.Description.Returns("Test agent");

        mockRegistry.ListAgents()
            .Returns(new List<string> { "test-agent" }.AsReadOnly());

        mockSelector.SelectAgentAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<IAgent>>(),
                Arg.Any<CancellationToken>())
            .Returns(new AgentSelectionResult
            {
                SelectedAgent = mockAgent,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = Array.Empty<AgentScore>()
            });

        // Generate large stream (1000 chunks)
        var largeStream = Enumerable.Range(0, 1000)
            .Select(i => $"chunk_{i}");

        mockAdapter.StreamAsync(
                Arg.Any<IAgent>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(largeStream.ToArray()));

        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();
        var mockSelector = Substitute.For<IAgentSelector>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("test-agent");
        mockAgent.Description.Returns("Test agent");

        mockRegistry.ListAgents()
            .Returns(new List<string> { "test-agent" }.AsReadOnly());

        mockSelector.SelectAgentAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<IAgent>>(),
                Arg.Any<CancellationToken>())
            .Returns(new AgentSelectionResult
            {
                SelectedAgent = mockAgent,
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

        mockAdapter.StreamAsync(
                Arg.Any<IAgent>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => CancellableStream(callInfo.ArgAt<CancellationToken>(2)));

        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
        var mockLoader = Substitute.For<IAgentLoader>();
        var mockRegistry = Substitute.For<IAgentRegistry>();
        var mockAdapter = Substitute.For<ILLMFrameworkAdapter>();
        var mockSelector = Substitute.For<IAgentSelector>();

        var mockAgent = Substitute.For<IAgent>();
        mockAgent.Name.Returns("test-agent");
        mockAgent.Description.Returns("Test agent");

        mockRegistry.ListAgents()
            .Returns(new List<string> { "test-agent" }.AsReadOnly());

        mockSelector.SelectAgentAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<IAgent>>(),
                Arg.Any<CancellationToken>())
            .Returns(new AgentSelectionResult
            {
                SelectedAgent = mockAgent,
                ConfidenceScore = 0.9,
                SelectionReason = "Test selection",
                AllScores = Array.Empty<AgentScore>()
            });

        // Each stream returns 100 chunks
        mockAdapter.StreamAsync(
                Arg.Any<IAgent>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                ToAsyncEnumerable(Enumerable.Range(0, 100)
                    .Select(i => $"{callInfo.ArgAt<string>(1)}_chunk_{i}")
                    .ToArray()));

        var orchestrator = new AgentOrchestrator(
            mockLoader,
            mockRegistry,
            mockAdapter,
            mockSelector);

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
