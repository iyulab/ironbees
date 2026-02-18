using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// Unit tests for MafWorkflowExecutor.
/// Tests verify that the executor correctly handles workflow conversion and execution events.
/// </summary>
public class MafWorkflowExecutorTests
{
    private readonly IWorkflowConverter _mockConverter;
    private readonly ILogger<MafWorkflowExecutor> _mockLogger;

    public MafWorkflowExecutorTests()
    {
        _mockConverter = Substitute.For<IWorkflowConverter>();
        _mockLogger = Substitute.For<ILogger<MafWorkflowExecutor>>();
    }

    private MafWorkflowExecutor CreateExecutor()
    {
        return new MafWorkflowExecutor(_mockConverter, _mockLogger);
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
            new MafWorkflowExecutor(null!, _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullLogger_Succeeds()
    {
        // Act - logger is optional
        var executor = new MafWorkflowExecutor(_mockConverter, null);

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
            .ConvertAsync(
                definition,
                Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Microsoft.Agents.AI.Workflows.Workflow>(x => throw new InvalidOperationException("Conversion failed"));

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
            .ConvertAsync(
                definition,
                Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Microsoft.Agents.AI.Workflows.Workflow>(x => throw new InvalidOperationException("Conversion failed"));

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
            .ConvertAsync(
                definition,
                Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Microsoft.Agents.AI.Workflows.Workflow>(x => throw new InvalidOperationException("Conversion failed"));

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
            .ConvertAsync(
                definition,
                Arg.Any<Func<string, CancellationToken, Task<AIAgent>>>(),
                Arg.Any<CancellationToken>())
            .Returns<Microsoft.Agents.AI.Workflows.Workflow>(x => throw new InvalidOperationException("Conversion failed"));

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
        Assert.True(Enum.IsDefined(WorkflowExecutionEventType.WorkflowStarted));
        Assert.True(Enum.IsDefined(WorkflowExecutionEventType.AgentStarted));
        Assert.True(Enum.IsDefined(WorkflowExecutionEventType.AgentMessage));
        Assert.True(Enum.IsDefined(WorkflowExecutionEventType.AgentCompleted));
        Assert.True(Enum.IsDefined(WorkflowExecutionEventType.SuperStepCompleted));
        Assert.True(Enum.IsDefined(WorkflowExecutionEventType.WorkflowCompleted));
        Assert.True(Enum.IsDefined(WorkflowExecutionEventType.Error));
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
            var mockChatClient = Substitute.For<IChatClient>();
            AIAgent agent = new ChatClientAgent(
                mockChatClient,
                instructions: $"Test agent: {name}",
                name: name);
            return Task.FromResult(agent);
        };
    }

    private static Microsoft.Agents.AI.Workflows.Workflow CreateMockMafWorkflow()
    {
        // Create a minimal MAF workflow for testing
        var mockChatClient = Substitute.For<IChatClient>();
        var agent = new ChatClientAgent(
            mockChatClient,
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
