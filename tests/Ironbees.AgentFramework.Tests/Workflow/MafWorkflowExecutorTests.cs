using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// Unit tests for MafWorkflowExecutor.
/// Tests verify that the executor correctly handles workflow conversion and execution events.
/// </summary>
public class MafWorkflowExecutorTests
{
    private readonly Mock<IWorkflowConverter> _mockConverter;
    private readonly Mock<ILogger<MafWorkflowExecutor>> _mockLogger;

    public MafWorkflowExecutorTests()
    {
        _mockConverter = new Mock<IWorkflowConverter>();
        _mockLogger = new Mock<ILogger<MafWorkflowExecutor>>();
    }

    private MafWorkflowExecutor CreateExecutor()
    {
        return new MafWorkflowExecutor(_mockConverter.Object, _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        var executor = CreateExecutor();

        // Assert
        Assert.NotNull(executor);
    }

    [Fact]
    public void Constructor_WithNullConverter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MafWorkflowExecutor(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Succeeds()
    {
        // Act - logger is optional
        var executor = new MafWorkflowExecutor(_mockConverter.Object, null);

        // Assert
        Assert.NotNull(executor);
    }

    #endregion

    #region ExecuteAsync Input Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithNullDefinition_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = CreateExecutor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in executor.ExecuteAsync(null!, "input", CreateAgentResolver())) { }
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinition();

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in executor.ExecuteAsync(definition, null!, CreateAgentResolver())) { }
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinition();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in executor.ExecuteAsync(definition, "", CreateAgentResolver())) { }
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceInput_ThrowsArgumentException()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinition();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in executor.ExecuteAsync(definition, "   ", CreateAgentResolver())) { }
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNullAgentResolver_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinition();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in executor.ExecuteAsync(definition, "input", null!)) { }
        });
    }

    #endregion

    #region ExecuteAsync Event Generation Tests

    [Fact]
    public async Task ExecuteAsync_AlwaysYieldsWorkflowStartedEventFirst()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinition();

        _mockConverter
            .Setup(c => c.ConvertAsync(
                definition,
                It.IsAny<Func<string, CancellationToken, Task<AIAgent>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Conversion failed"));

        // Act
        var events = await CollectEventsAsync(executor, definition, "input");

        // Assert
        Assert.NotEmpty(events);
        Assert.Equal(WorkflowExecutionEventType.WorkflowStarted, events[0].Type);
    }

    [Fact]
    public async Task ExecuteAsync_WorkflowStartedEventContainsMetadata()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinitionWithName("TestWorkflow");

        _mockConverter
            .Setup(c => c.ConvertAsync(
                definition,
                It.IsAny<Func<string, CancellationToken, Task<AIAgent>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Conversion failed"));

        // Act
        var events = await CollectEventsAsync(executor, definition, "input");

        // Assert
        var startEvent = events.First(e => e.Type == WorkflowExecutionEventType.WorkflowStarted);
        Assert.NotNull(startEvent.Metadata);
        Assert.Equal("TestWorkflow", startEvent.Metadata["workflowName"]);
        Assert.Equal(definition.States.Count, startEvent.Metadata["stateCount"]);
    }

    [Fact]
    public async Task ExecuteAsync_ConversionFailure_YieldsErrorEvent()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinition();

        _mockConverter
            .Setup(c => c.ConvertAsync(
                definition,
                It.IsAny<Func<string, CancellationToken, Task<AIAgent>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Conversion failed"));

        // Act
        var events = await CollectEventsAsync(executor, definition, "input");

        // Assert
        Assert.Equal(2, events.Count); // WorkflowStarted + Error
        Assert.Equal(WorkflowExecutionEventType.Error, events[1].Type);
        Assert.Contains("Conversion failed", events[1].Content);
    }

    [Fact]
    public async Task ExecuteAsync_ConversionFailure_ErrorEventContainsMetadata()
    {
        // Arrange
        var executor = CreateExecutor();
        var definition = CreateSimpleWorkflowDefinition();

        _mockConverter
            .Setup(c => c.ConvertAsync(
                definition,
                It.IsAny<Func<string, CancellationToken, Task<AIAgent>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Conversion failed"));

        // Act
        var events = await CollectEventsAsync(executor, definition, "input");

        // Assert
        var errorEvent = events.First(e => e.Type == WorkflowExecutionEventType.Error);
        Assert.NotNull(errorEvent.Metadata);
        Assert.Equal("InvalidOperationException", errorEvent.Metadata["exception"]);
        Assert.Equal("Conversion failed", errorEvent.Metadata["message"]);
    }

    #endregion

    #region ExecuteWorkflowAsync Input Validation Tests

    [Fact]
    public async Task ExecuteWorkflowAsync_WithNullWorkflow_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = CreateExecutor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in executor.ExecuteWorkflowAsync(null!, "input")) { }
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var executor = CreateExecutor();
        var workflow = CreateMockMafWorkflow();

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in executor.ExecuteWorkflowAsync(workflow, null!)) { }
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithEmptyInput_ThrowsArgumentException()
    {
        // Arrange
        var executor = CreateExecutor();
        var workflow = CreateMockMafWorkflow();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in executor.ExecuteWorkflowAsync(workflow, "")) { }
        });
    }

    #endregion

    #region WorkflowExecutionEvent Tests

    [Fact]
    public void WorkflowExecutionEvent_DefaultTimestamp_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var evt = new WorkflowExecutionEvent
        {
            Type = WorkflowExecutionEventType.WorkflowStarted
        };

        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(evt.Timestamp >= before);
        Assert.True(evt.Timestamp <= after);
    }

    [Fact]
    public void WorkflowExecutionEvent_WithAllProperties_PropertiesAreSet()
    {
        // Arrange & Act
        var checkpoint = new { id = "test" };
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var timestamp = DateTimeOffset.UtcNow;

        var evt = new WorkflowExecutionEvent
        {
            Type = WorkflowExecutionEventType.AgentCompleted,
            AgentName = "test-agent",
            Content = "Test content",
            Checkpoint = checkpoint,
            Timestamp = timestamp,
            Metadata = metadata
        };

        // Assert
        Assert.Equal(WorkflowExecutionEventType.AgentCompleted, evt.Type);
        Assert.Equal("test-agent", evt.AgentName);
        Assert.Equal("Test content", evt.Content);
        Assert.Equal(checkpoint, evt.Checkpoint);
        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal(metadata, evt.Metadata);
    }

    #endregion

    #region WorkflowExecutionEventType Tests

    [Fact]
    public void WorkflowExecutionEventType_ContainsExpectedValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(WorkflowExecutionEventType), WorkflowExecutionEventType.WorkflowStarted));
        Assert.True(Enum.IsDefined(typeof(WorkflowExecutionEventType), WorkflowExecutionEventType.AgentStarted));
        Assert.True(Enum.IsDefined(typeof(WorkflowExecutionEventType), WorkflowExecutionEventType.AgentMessage));
        Assert.True(Enum.IsDefined(typeof(WorkflowExecutionEventType), WorkflowExecutionEventType.AgentCompleted));
        Assert.True(Enum.IsDefined(typeof(WorkflowExecutionEventType), WorkflowExecutionEventType.SuperStepCompleted));
        Assert.True(Enum.IsDefined(typeof(WorkflowExecutionEventType), WorkflowExecutionEventType.WorkflowCompleted));
        Assert.True(Enum.IsDefined(typeof(WorkflowExecutionEventType), WorkflowExecutionEventType.Error));
    }

    #endregion

    #region Helper Methods

    private static WorkflowDefinition CreateSimpleWorkflowDefinition()
    {
        return CreateSimpleWorkflowDefinitionWithName("TestWorkflow");
    }

    private static WorkflowDefinition CreateSimpleWorkflowDefinitionWithName(string name)
    {
        return new WorkflowDefinition
        {
            Name = name,
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
    }

    private static Func<string, CancellationToken, Task<AIAgent>> CreateAgentResolver()
    {
        return (name, _) =>
        {
            var mockChatClient = new Mock<IChatClient>();
            AIAgent agent = new ChatClientAgent(
                mockChatClient.Object,
                instructions: $"Test agent: {name}",
                name: name);
            return Task.FromResult(agent);
        };
    }

    private static Microsoft.Agents.AI.Workflows.Workflow CreateMockMafWorkflow()
    {
        // Create a minimal MAF workflow for testing
        var mockChatClient = new Mock<IChatClient>();
        var agent = new ChatClientAgent(
            mockChatClient.Object,
            instructions: "Test agent",
            name: "test");

        return Microsoft.Agents.AI.Workflows.AgentWorkflowBuilder.BuildSequential(agent);
    }

    private static async Task<List<WorkflowExecutionEvent>> CollectEventsAsync(
        MafWorkflowExecutor executor,
        WorkflowDefinition definition,
        string input)
    {
        var events = new List<WorkflowExecutionEvent>();
        await foreach (var evt in executor.ExecuteAsync(definition, input, CreateAgentResolver()))
        {
            events.Add(evt);
        }
        return events;
    }

    #endregion
}
