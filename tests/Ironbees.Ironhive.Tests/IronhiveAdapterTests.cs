using Ironbees.Core;
using Ironbees.Ironhive.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IronHiveAgent = IronHive.Abstractions.Agent.IAgent;

namespace Ironbees.Ironhive.Tests;

public class IronhiveAdapterTests
{
    private readonly Mock<IHiveService> _hiveServiceMock;
    private readonly Mock<IIronhiveOrchestratorFactory> _orchestratorFactoryMock;
    private readonly OrchestrationEventMapper _eventMapper;
    private readonly IronhiveAdapter _adapter;

    public IronhiveAdapterTests()
    {
        _hiveServiceMock = new Mock<IHiveService>();
        _orchestratorFactoryMock = new Mock<IIronhiveOrchestratorFactory>();
        _eventMapper = new OrchestrationEventMapper();
        _adapter = new IronhiveAdapter(
            _hiveServiceMock.Object,
            _orchestratorFactoryMock.Object,
            _eventMapper,
            NullLogger<IronhiveAdapter>.Instance);
    }

    [Fact]
    public async Task CreateAgentAsync_ValidConfig_ReturnsWrapper()
    {
        // Arrange
        var config = CreateTestConfig();
        var mockAgent = new Mock<IronHiveAgent>();
        _hiveServiceMock
            .Setup(s => s.CreateAgent(It.IsAny<Action<IronHive.Abstractions.Agent.AgentConfig>>()))
            .Returns(mockAgent.Object);

        // Act
        var result = await _adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-agent", result.Name);
        Assert.Equal("Test agent", result.Description);
        Assert.Same(config, result.Config);
        _hiveServiceMock.Verify(s => s.CreateAgent(It.IsAny<Action<IronHive.Abstractions.Agent.AgentConfig>>()), Times.Once);
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
    public async Task CreateAgentAsync_BasicConfig_PassesConfigToBuilder()
    {
        // Arrange
        var config = CreateTestConfig();
        IronHive.Abstractions.Agent.AgentConfig? capturedConfig = null;
        var mockAgent = new Mock<IronHiveAgent>();
        _hiveServiceMock
            .Setup(s => s.CreateAgent(It.IsAny<Action<IronHive.Abstractions.Agent.AgentConfig>>()))
            .Callback<Action<IronHive.Abstractions.Agent.AgentConfig>>(configure =>
            {
                capturedConfig = new IronHive.Abstractions.Agent.AgentConfig();
                configure(capturedConfig);
            })
            .Returns(mockAgent.Object);

        // Act
        await _adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(capturedConfig);
        Assert.Equal("test-agent", capturedConfig!.Name);
        Assert.Equal("openai", capturedConfig.Provider);
        Assert.Equal("gpt-4o", capturedConfig.Model);
        Assert.Equal("You are a helpful assistant.", capturedConfig.Instructions);
    }

    [Fact]
    public async Task CreateAgentAsync_NoSystemPrompt_SetsNullInstructions()
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
        IronHive.Abstractions.Agent.AgentConfig? capturedConfig = null;
        var mockAgent = new Mock<IronHiveAgent>();
        _hiveServiceMock
            .Setup(s => s.CreateAgent(It.IsAny<Action<IronHive.Abstractions.Agent.AgentConfig>>()))
            .Callback<Action<IronHive.Abstractions.Agent.AgentConfig>>(configure =>
            {
                capturedConfig = new IronHive.Abstractions.Agent.AgentConfig();
                configure(capturedConfig);
            })
            .Returns(mockAgent.Object);

        // Act
        await _adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(capturedConfig);
        Assert.Equal("", capturedConfig!.Instructions);
    }

    [Fact]
    public async Task CreateAgentAsync_CustomParameters_SetsParametersConfig()
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
        IronHive.Abstractions.Agent.AgentConfig? capturedConfig = null;
        var mockAgent = new Mock<IronHiveAgent>();
        _hiveServiceMock
            .Setup(s => s.CreateAgent(It.IsAny<Action<IronHive.Abstractions.Agent.AgentConfig>>()))
            .Callback<Action<IronHive.Abstractions.Agent.AgentConfig>>(configure =>
            {
                capturedConfig = new IronHive.Abstractions.Agent.AgentConfig();
                configure(capturedConfig);
            })
            .Returns(mockAgent.Object);

        // Act
        await _adapter.CreateAgentAsync(config);

        // Assert
        Assert.NotNull(capturedConfig);
        Assert.NotNull(capturedConfig!.Parameters);
        Assert.Equal(8000, capturedConfig.Parameters!.MaxTokens);
        Assert.Equal(0.3f, capturedConfig.Parameters.Temperature);
        Assert.Equal(0.9f, capturedConfig.Parameters.TopP);
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

    [Fact]
    public async Task RunAsync_WithHistory_PassesAllMessages()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        IEnumerable<Message>? capturedMessages = null;
        var response = new MessageResponse
        {
            Id = "resp-1",
            Message = new AssistantMessage
            {
                Content = new List<MessageContent>
                {
                    new TextMessageContent { Value = "response" }
                }
            }
        };
        mockIronhiveAgent
            .Setup(a => a.InvokeAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Message>, CancellationToken>((msgs, _) => capturedMessages = msgs.ToList())
            .ReturnsAsync(response);

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            new(ChatRole.Assistant, "Hi there!"),
            new(ChatRole.User, "How are you?"),
            new(ChatRole.Assistant, "I'm fine!")
        };

        // Act
        await _adapter.RunAsync(wrapper, "What's new?", history);

        // Assert
        Assert.NotNull(capturedMessages);
        var messageList = capturedMessages!.ToList();
        Assert.Equal(5, messageList.Count); // 4 history + 1 current

        Assert.IsType<UserMessage>(messageList[0]);
        Assert.IsType<AssistantMessage>(messageList[1]);
        Assert.IsType<UserMessage>(messageList[2]);
        Assert.IsType<AssistantMessage>(messageList[3]);
        Assert.IsType<UserMessage>(messageList[4]);

        var lastMsg = (UserMessage)messageList[4];
        var lastText = lastMsg.Content.OfType<TextMessageContent>().First();
        Assert.Equal("What's new?", lastText.Value);
    }

    [Fact]
    public async Task RunAsync_WithNullHistory_WorksLikeBasic()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        IEnumerable<Message>? capturedMessages = null;
        var response = new MessageResponse
        {
            Id = "resp-1",
            Message = new AssistantMessage
            {
                Content = new List<MessageContent>
                {
                    new TextMessageContent { Value = "response" }
                }
            }
        };
        mockIronhiveAgent
            .Setup(a => a.InvokeAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Message>, CancellationToken>((msgs, _) => capturedMessages = msgs.ToList())
            .ReturnsAsync(response);

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        // Act
        await _adapter.RunAsync(wrapper, "Hello", conversationHistory: null);

        // Assert
        Assert.NotNull(capturedMessages);
        var messageList = capturedMessages!.ToList();
        Assert.Single(messageList);
        Assert.IsType<UserMessage>(messageList[0]);
    }

    [Fact]
    public async Task RunAsync_WithEmptyHistory_WorksLikeBasic()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        IEnumerable<Message>? capturedMessages = null;
        var response = new MessageResponse
        {
            Id = "resp-1",
            Message = new AssistantMessage
            {
                Content = new List<MessageContent>
                {
                    new TextMessageContent { Value = "response" }
                }
            }
        };
        mockIronhiveAgent
            .Setup(a => a.InvokeAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Message>, CancellationToken>((msgs, _) => capturedMessages = msgs.ToList())
            .ReturnsAsync(response);

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        // Act
        await _adapter.RunAsync(wrapper, "Hello", new List<ChatMessage>());

        // Assert
        Assert.NotNull(capturedMessages);
        var messageList = capturedMessages!.ToList();
        Assert.Single(messageList);
        Assert.IsType<UserMessage>(messageList[0]);
    }

    [Fact]
    public async Task StreamAsync_WithHistory_PassesAllMessages()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        IEnumerable<Message>? capturedMessages = null;
        var streamingResponses = new List<StreamingMessageResponse>
        {
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = "response" }
            }
        };
        mockIronhiveAgent
            .Setup(a => a.InvokeStreamingAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Message>, CancellationToken>((msgs, _) => capturedMessages = msgs.ToList())
            .Returns(streamingResponses.ToAsyncEnumerable());

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "First message"),
            new(ChatRole.Assistant, "First reply")
        };

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _adapter.StreamAsync(wrapper, "Second message", history))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Equal("response", chunks[0]);

        Assert.NotNull(capturedMessages);
        var messageList = capturedMessages!.ToList();
        Assert.Equal(3, messageList.Count); // 2 history + 1 current
        Assert.IsType<UserMessage>(messageList[0]);
        Assert.IsType<AssistantMessage>(messageList[1]);
        Assert.IsType<UserMessage>(messageList[2]);
    }

    [Fact]
    public async Task StreamAsync_ErrorResponse_YieldsErrorAndStops()
    {
        // Arrange
        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        var streamingResponses = new List<StreamingMessageResponse>
        {
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = "partial" }
            },
            new StreamingMessageErrorResponse
            {
                Code = 500,
                Message = "Internal server error"
            },
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = "should not appear" }
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
        Assert.Equal("partial", chunks[0]);
        Assert.Equal("[Error 500]: Internal server error", chunks[1]);
    }

    [Fact]
    public async Task RunAsync_WithTokenUsage_LogsTokenUsage()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<IronhiveAdapter>>();
        var adapter = new IronhiveAdapter(
            _hiveServiceMock.Object,
            _orchestratorFactoryMock.Object,
            _eventMapper,
            mockLogger.Object);

        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        var response = new MessageResponse
        {
            Id = "resp-1",
            Message = new AssistantMessage
            {
                Content = new List<MessageContent>
                {
                    new TextMessageContent { Value = "response" }
                }
            },
            TokenUsage = new MessageTokenUsage
            {
                InputTokens = 100,
                OutputTokens = 50
            }
        };
        mockIronhiveAgent
            .Setup(a => a.InvokeAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        // Act
        var result = await adapter.RunAsync(wrapper, "test");

        // Assert
        Assert.Equal("response", result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    o.ToString()!.Contains("100 input, 50 output") &&
                    o.ToString()!.Contains("unknown")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamAsync_DoneWithTokenUsage_LogsTokenUsage()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<IronhiveAdapter>>();
        var adapter = new IronhiveAdapter(
            _hiveServiceMock.Object,
            _orchestratorFactoryMock.Object,
            _eventMapper,
            mockLogger.Object);

        var mockIronhiveAgent = new Mock<IronHiveAgent>();
        var streamingResponses = new List<StreamingMessageResponse>
        {
            new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = "text" }
            },
            new StreamingMessageDoneResponse
            {
                Id = "msg-1",
                Model = "gpt-4o",
                Timestamp = DateTime.UtcNow,
                TokenUsage = new MessageTokenUsage
                {
                    InputTokens = 200,
                    OutputTokens = 75
                }
            }
        };

        mockIronhiveAgent
            .Setup(a => a.InvokeStreamingAsync(It.IsAny<IEnumerable<Message>>(), It.IsAny<CancellationToken>()))
            .Returns(streamingResponses.ToAsyncEnumerable());

        var config = CreateTestConfig();
        var wrapper = new IronhiveAgentWrapper(mockIronhiveAgent.Object, config);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in adapter.StreamAsync(wrapper, "test"))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Equal("text", chunks[0]);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    o.ToString()!.Contains("200 input, 75 output") &&
                    o.ToString()!.Contains("gpt-4o")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
