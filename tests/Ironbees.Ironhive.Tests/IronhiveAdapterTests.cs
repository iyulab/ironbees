using Ironbees.Core;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive.Tests;

public class IronhiveAdapterTests
{
    private readonly Mock<IHiveService> _hiveServiceMock;
    private readonly IronhiveAdapter _adapter;

    public IronhiveAdapterTests()
    {
        _hiveServiceMock = new Mock<IHiveService>();
        _adapter = new IronhiveAdapter(
            _hiveServiceMock.Object,
            NullLogger<IronhiveAdapter>.Instance);
    }

    [Fact]
    public async Task CreateAgentAsync_ValidConfig_ReturnsWrapper()
    {
        // Arrange
        var config = CreateTestConfig();
        var mockAgent = new Mock<IronHiveAgent>();
        _hiveServiceMock
            .Setup(s => s.CreateAgentFromYaml(It.IsAny<string>()))
            .Returns(mockAgent.Object);

        // Act
        var result = await _adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-agent", result.Name);
        Assert.Equal("Test agent", result.Description);
        Assert.Same(config, result.Config);
        _hiveServiceMock.Verify(s => s.CreateAgentFromYaml(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateAgentAsync_NullConfig_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _adapter.CreateAgentAsync(null!));
    }

    [Fact]
    public async Task RunAsync_ValidInput_ReturnsText()
    {
        // Arrange
        var agent = CreateWrappedAgent("Hello, world!");

        // Act
        var result = await _adapter.RunAsync(agent, "Hi");

        // Assert
        Assert.Equal("Hello, world!", result);
    }

    [Fact]
    public async Task RunAsync_MultipleTextContent_ConcatenatesAll()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        var response = new MessageResponse
        {
            Id = "resp-1",
            Message = new AssistantMessage
            {
                Content = new List<MessageContent>
                {
                    new TextMessageContent { Value = "Part 1 " },
                    new TextMessageContent { Value = "Part 2" }
                }
            }
        };
        mockIronhiveAgent
            .Setup(a => a.InvokeAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        // Act
        var result = await _adapter.RunAsync(wrapper, "test input");

        // Assert
        Assert.Equal("Part 1 Part 2", result);
    }

    [Fact]
    public async Task RunAsync_NonWrapperAgent_ThrowsInvalidOperation()
    {
        // Arrange
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("fake-agent");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _adapter.RunAsync(mockAgent.Object, "Hi"));

        Assert.Contains("not an IronHive agent", ex.Message);
    }

    [Fact]
    public async Task StreamAsync_TextDeltas_YieldsTextOnly()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        var streamingResponses = new List<StreamingMessageResponse>
        {
            new StreamingMessageBeginResponse { Id = "msg-1" },
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = "Hello" }
            },
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = ", world!" }
            },
            new StreamingMessageDoneResponse
            {
                Id = "msg-1",
                Model = "gpt-4o",
                Timestamp = DateTime.UtcNow
            }
        };

        mockIronhiveAgent
            .Setup(a => a.InvokeStreamingAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .Returns(streamingResponses.ToAsyncEnumerable());

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _adapter.StreamAsync(wrapper, "test"))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.Equal("Hello", chunks[0]);
        Assert.Equal(", world!", chunks[1]);
    }

    [Fact]
    public async Task StreamAsync_NonTextDeltas_AreSkipped()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        var streamingResponses = new List<StreamingMessageResponse>
        {
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new ToolDeltaContent { Input = "{\"arg\":1}" }
            },
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = "text only" }
            }
        };

        mockIronhiveAgent
            .Setup(a => a.InvokeStreamingAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .Returns(streamingResponses.ToAsyncEnumerable());

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _adapter.StreamAsync(wrapper, "test"))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Equal("text only", chunks[0]);
    }

    [Fact]
    public void BuildAgentYaml_BasicConfig_ContainsRequiredFields()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        var yaml = IronhiveAdapter.BuildAgentYaml(config);

        // Assert
        Assert.Contains("name: test-agent", yaml);
        Assert.Contains("provider: openai", yaml);
        Assert.Contains("model: gpt-4o", yaml);
        Assert.Contains("instructions:", yaml);
    }

    [Fact]
    public void BuildAgentYaml_NoSystemPrompt_OmitsInstructions()
    {
        // Arrange
        var config = new AgentConfig
        {
            Name = "no-prompt",
            Description = "No prompt agent",
            Version = "1.0.0",
            SystemPrompt = "",
            Model = new ModelConfig { Provider = "openai", Deployment = "gpt-4o" }
        };

        // Act
        var yaml = IronhiveAdapter.BuildAgentYaml(config);

        // Assert
        Assert.DoesNotContain("instructions:", yaml);
    }

    [Fact]
    public void BuildAgentYaml_CustomParameters_IncludesParametersSection()
    {
        // Arrange
        var config = new AgentConfig
        {
            Name = "custom-params",
            Description = "Agent with custom params",
            Version = "1.0.0",
            SystemPrompt = "Be helpful",
            Model = new ModelConfig
            {
                Provider = "openai",
                Deployment = "gpt-4o",
                Temperature = 0.3,
                MaxTokens = 8000,
                TopP = 0.9
            }
        };

        // Act
        var yaml = IronhiveAdapter.BuildAgentYaml(config);

        // Assert
        Assert.Contains("parameters:", yaml);
        Assert.Contains("maxTokens: 8000", yaml);
        Assert.Contains("temperature: 0.3", yaml);
        Assert.Contains("topP: 0.9", yaml);
    }

    private static AgentConfig CreateTestConfig()
    {
        return new AgentConfig
        {
            Name = "test-agent",
            Description = "Test agent",
            Version = "1.0.0",
            SystemPrompt = "You are a helpful assistant.",
            Model = new ModelConfig
            {
                Provider = "openai",
                Deployment = "gpt-4o"
            }
        };
    }

    private IronhiveAgentWrapper CreateWrappedAgent(string responseText)
    {
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        var response = new MessageResponse
        {
            Id = "resp-1",
            Message = new AssistantMessage
            {
                Content = new List<MessageContent>
                {
                    new TextMessageContent { Value = responseText }
                }
            }
        };
        mockIronhiveAgent
            .Setup(a => a.InvokeAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var config = CreateTestConfig();
        return new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);
    }
}

/// <summary>
/// Helper to convert IEnumerable to IAsyncEnumerable for test mocking
/// </summary>
internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
