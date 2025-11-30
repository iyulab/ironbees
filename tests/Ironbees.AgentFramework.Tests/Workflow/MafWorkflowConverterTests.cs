using System.Collections.Immutable;
using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Core.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// Unit tests for <see cref="MafWorkflowConverter"/>.
/// Tests cover validation rules (WFC001-WFC008), warnings (WFC100-WFC101),
/// and workflow conversion scenarios (Sequential, Parallel, Mixed).
/// </summary>
public class MafWorkflowConverterTests
{
    private readonly Mock<ILogger<MafWorkflowConverter>> _mockLogger;
    private readonly MafWorkflowConverter _converter;

    public MafWorkflowConverterTests()
    {
        _mockLogger = new Mock<ILogger<MafWorkflowConverter>>();
        _converter = new MafWorkflowConverter(_mockLogger.Object);
    }

    #region Validation Error Tests (WFC001-WFC008)

    [Fact]
    public void Validate_EmptyName_ReturnsWFC001Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC001");
        Assert.Contains(result.Errors, e => e.Message.Contains("name is required"));
    }

    [Fact]
    public void Validate_WhitespaceName_ReturnsWFC001Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "   ",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC001");
    }

    [Fact]
    public void Validate_NoStates_ReturnsWFC002Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States = []
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC002");
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one state"));
    }

    [Fact]
    public void Validate_NoStartState_ReturnsWFC003Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "AGENT1", Type = WorkflowStateType.Agent, Executor = "agent1", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC003");
        Assert.Contains(result.Errors, e => e.Message.Contains("Start state"));
    }

    [Fact]
    public void Validate_MultipleStartStates_ReturnsWFC004Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START1", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "START2", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC004");
        Assert.Contains(result.Errors, e => e.Message.Contains("multiple Start states"));
    }

    [Fact]
    public void Validate_NoTerminalState_ReturnsWFC005Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT1" },
                new WorkflowStateDefinition { Id = "AGENT1", Type = WorkflowStateType.Agent, Executor = "agent1" }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC005");
        Assert.Contains(result.Errors, e => e.Message.Contains("Terminal state"));
    }

    [Fact]
    public void Validate_NonExistentNextState_ReturnsWFC006Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "NONEXISTENT" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC006");
        Assert.Contains(result.Errors, e => e.Message.Contains("non-existent state"));
        Assert.Contains(result.Errors, e => e.StateId == "START");
    }

    [Fact]
    public void Validate_AgentStateWithoutExecutor_ReturnsWFC007Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT1" },
                new WorkflowStateDefinition { Id = "AGENT1", Type = WorkflowStateType.Agent, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC007");
        Assert.Contains(result.Errors, e => e.Message.Contains("must specify an executor"));
        Assert.Contains(result.Errors, e => e.StateId == "AGENT1");
    }

    [Fact]
    public void Validate_ParallelStateWithoutExecutors_ReturnsWFC008Error()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PARALLEL1" },
                new WorkflowStateDefinition { Id = "PARALLEL1", Type = WorkflowStateType.Parallel, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WFC008");
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one executor"));
        Assert.Contains(result.Errors, e => e.StateId == "PARALLEL1");
    }

    #endregion

    #region Validation Warning Tests (WFC100-WFC101)

    [Fact]
    public void Validate_StateWithConditions_ReturnsWFC100Warning()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT1" },
                new WorkflowStateDefinition
                {
                    Id = "AGENT1",
                    Type = WorkflowStateType.Agent,
                    Executor = "agent1",
                    Next = "END",
                    Conditions =
                    [
                        new ConditionalTransition { If = "success", Then = "END" }
                    ]
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "WFC100");
        Assert.Contains(result.Warnings, w => w.Message.Contains("conditional transitions"));
        Assert.Contains(result.Warnings, w => w.StateId == "AGENT1");
    }

    [Fact]
    public void Validate_StateWithTrigger_ReturnsWFC101Warning()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT1" },
                new WorkflowStateDefinition
                {
                    Id = "AGENT1",
                    Type = WorkflowStateType.Agent,
                    Executor = "agent1",
                    Next = "END",
                    Trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "requirements.md" }
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "WFC101");
        Assert.Contains(result.Warnings, w => w.Message.Contains("trigger"));
        Assert.Contains(result.Warnings, w => w.StateId == "AGENT1");
    }

    #endregion

    #region Unsupported Features Tests

    [Fact]
    public void Validate_HumanGateState_AddsUnsupportedFeature()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "GATE" },
                new WorkflowStateDefinition { Id = "GATE", Type = WorkflowStateType.HumanGate, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.UnsupportedFeatures, f => f.Contains("HumanGate"));
    }

    [Fact]
    public void Validate_EscalationState_AddsUnsupportedFeature()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "TestWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "ESCALATE" },
                new WorkflowStateDefinition { Id = "ESCALATE", Type = WorkflowStateType.Escalation, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.UnsupportedFeatures, f => f.Contains("Escalation"));
    }

    #endregion

    #region Valid Workflow Tests

    [Fact]
    public void Validate_ValidSequentialWorkflow_ReturnsValid()
    {
        // Arrange
        var definition = CreateValidSequentialWorkflow();

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ValidParallelWorkflow_ReturnsValid()
    {
        // Arrange
        var definition = CreateValidParallelWorkflow();

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region ConvertAsync Tests

    [Fact]
    public async Task ConvertAsync_NullDefinition_ThrowsArgumentNullException()
    {
        // Arrange
        Func<string, CancellationToken, Task<AIAgent>> resolver = (_, _) => Task.FromResult<AIAgent>(null!);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _converter.ConvertAsync(null!, resolver));
    }

    [Fact]
    public async Task ConvertAsync_NullAgentResolver_ThrowsArgumentNullException()
    {
        // Arrange
        var definition = CreateValidSequentialWorkflow();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _converter.ConvertAsync(definition, null!));
    }

    [Fact]
    public async Task ConvertAsync_InvalidWorkflow_ThrowsWorkflowConversionException()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "",
            States = []
        };
        var mockAgent = CreateMockAgent("agent1");
        Func<string, CancellationToken, Task<AIAgent>> resolver = (_, _) => Task.FromResult(mockAgent);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<WorkflowConversionException>(
            () => _converter.ConvertAsync(definition, resolver));
        Assert.Contains("validation failed", ex.Message);
    }

    [Fact]
    public async Task ConvertAsync_SequentialWorkflow_ReturnsWorkflow()
    {
        // Arrange
        var definition = CreateValidSequentialWorkflow();
        var mockAgent1 = CreateMockAgent("planner");
        var mockAgent2 = CreateMockAgent("coder");

        Func<string, CancellationToken, Task<AIAgent>> resolver = (name, _) =>
        {
            return Task.FromResult(name switch
            {
                "planner" => mockAgent1,
                "coder" => mockAgent2,
                _ => throw new InvalidOperationException($"Unknown agent: {name}")
            });
        };

        // Act
        var workflow = await _converter.ConvertAsync(definition, resolver);

        // Assert
        Assert.NotNull(workflow);
    }

    [Fact]
    public async Task ConvertAsync_ParallelWorkflow_ReturnsWorkflow()
    {
        // Arrange
        var definition = CreateValidParallelWorkflow();
        var mockAgent1 = CreateMockAgent("analyzer1");
        var mockAgent2 = CreateMockAgent("analyzer2");

        Func<string, CancellationToken, Task<AIAgent>> resolver = (name, _) =>
        {
            return Task.FromResult(name switch
            {
                "analyzer1" => mockAgent1,
                "analyzer2" => mockAgent2,
                _ => throw new InvalidOperationException($"Unknown agent: {name}")
            });
        };

        // Act
        var workflow = await _converter.ConvertAsync(definition, resolver);

        // Assert
        Assert.NotNull(workflow);
    }

    [Fact]
    public async Task ConvertAsync_MixedWorkflow_ReturnsWorkflow()
    {
        // Arrange
        var definition = CreateValidMixedWorkflow();
        var mockAgent1 = CreateMockAgent("planner");
        var mockAgent2 = CreateMockAgent("analyzer1");
        var mockAgent3 = CreateMockAgent("analyzer2");

        Func<string, CancellationToken, Task<AIAgent>> resolver = (name, _) =>
        {
            return Task.FromResult(name switch
            {
                "planner" => mockAgent1,
                "analyzer1" => mockAgent2,
                "analyzer2" => mockAgent3,
                _ => throw new InvalidOperationException($"Unknown agent: {name}")
            });
        };

        // Act
        var workflow = await _converter.ConvertAsync(definition, resolver);

        // Assert
        Assert.NotNull(workflow);
    }

    [Fact]
    public async Task ConvertAsync_WorkflowWithNoAgents_ThrowsWorkflowConversionException()
    {
        // Arrange - workflow with only Start and Terminal states
        var definition = new WorkflowDefinition
        {
            Name = "EmptyWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        Func<string, CancellationToken, Task<AIAgent>> resolver = (_, _) =>
            Task.FromResult(CreateMockAgent("any"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<WorkflowConversionException>(
            () => _converter.ConvertAsync(definition, resolver));
        Assert.Contains("No agents found", ex.Message);
    }

    [Fact]
    public async Task ConvertAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var definition = CreateValidSequentialWorkflow();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<string, CancellationToken, Task<AIAgent>> resolver = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(CreateMockAgent("any"));
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _converter.ConvertAsync(definition, resolver, cts.Token));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_DoesNotThrow()
    {
        // Act & Assert
        var converter = new MafWorkflowConverter(null);
        Assert.NotNull(converter);
    }

    [Fact]
    public void Constructor_WithLogger_AcceptsLogger()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MafWorkflowConverter>>();

        // Act
        var converter = new MafWorkflowConverter(mockLogger.Object);

        // Assert
        Assert.NotNull(converter);
    }

    #endregion

    #region WorkflowConversionException Tests

    [Fact]
    public void WorkflowConversionException_DefaultConstructor_CreatesException()
    {
        // Act
        var ex = new WorkflowConversionException();

        // Assert
        Assert.NotNull(ex);
    }

    [Fact]
    public void WorkflowConversionException_WithMessage_SetsMessage()
    {
        // Arrange
        const string message = "Test error message";

        // Act
        var ex = new WorkflowConversionException(message);

        // Assert
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void WorkflowConversionException_WithMessageAndInner_SetsProperties()
    {
        // Arrange
        const string message = "Outer error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var ex = new WorkflowConversionException(message, innerException);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Same(innerException, ex.InnerException);
    }

    #endregion

    #region WorkflowConversionValidation Tests

    [Fact]
    public void WorkflowConversionValidation_Valid_CreatesValidResult()
    {
        // Act
        var result = WorkflowConversionValidation.Valid();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void WorkflowConversionValidation_ValidWithWarnings_CreatesValidResult()
    {
        // Arrange
        var warnings = new List<WorkflowConversionWarning>
        {
            new("WFC100", "Test warning", "STATE1")
        };

        // Act
        var result = WorkflowConversionValidation.Valid(warnings);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void WorkflowConversionValidation_Invalid_CreatesInvalidResult()
    {
        // Arrange
        var error = new WorkflowConversionError("WFC001", "Test error");

        // Act
        var result = WorkflowConversionValidation.Invalid(error);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("WFC001", result.Errors[0].Code);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validate_CircularReference_DoesNotCauseInfiniteLoop()
    {
        // Arrange - Create a workflow where AGENT1 -> AGENT2 -> AGENT1 (circular)
        var definition = new WorkflowDefinition
        {
            Name = "CircularWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT1" },
                new WorkflowStateDefinition { Id = "AGENT1", Type = WorkflowStateType.Agent, Executor = "a1", Next = "AGENT2" },
                new WorkflowStateDefinition { Id = "AGENT2", Type = WorkflowStateType.Agent, Executor = "a2", Next = "AGENT1" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act - Should complete without hanging
        var result = _converter.Validate(definition);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ConvertAsync_CircularReference_HandlesGracefully()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "CircularWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT1" },
                new WorkflowStateDefinition { Id = "AGENT1", Type = WorkflowStateType.Agent, Executor = "a1", Next = "AGENT2" },
                new WorkflowStateDefinition { Id = "AGENT2", Type = WorkflowStateType.Agent, Executor = "a2", Next = "AGENT1" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        Func<string, CancellationToken, Task<AIAgent>> resolver = (name, _) =>
            Task.FromResult(CreateMockAgent(name));

        // Act - Should complete without infinite loop
        var workflow = await _converter.ConvertAsync(definition, resolver);

        // Assert
        Assert.NotNull(workflow);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange - Workflow with multiple validation issues
        var definition = new WorkflowDefinition
        {
            Name = "", // WFC001
            States = [] // WFC002
        };

        // Act
        var result = _converter.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
        Assert.Contains(result.Errors, e => e.Code == "WFC001");
        Assert.Contains(result.Errors, e => e.Code == "WFC002");
    }

    #endregion

    #region Helper Methods

    private static WorkflowDefinition CreateValidSequentialWorkflow()
    {
        return new WorkflowDefinition
        {
            Name = "SequentialWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PLAN" },
                new WorkflowStateDefinition { Id = "PLAN", Type = WorkflowStateType.Agent, Executor = "planner", Next = "CODE" },
                new WorkflowStateDefinition { Id = "CODE", Type = WorkflowStateType.Agent, Executor = "coder", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
    }

    private static WorkflowDefinition CreateValidParallelWorkflow()
    {
        return new WorkflowDefinition
        {
            Name = "ParallelWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "ANALYZE" },
                new WorkflowStateDefinition
                {
                    Id = "ANALYZE",
                    Type = WorkflowStateType.Parallel,
                    Executors = ["analyzer1", "analyzer2"],
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
    }

    private static WorkflowDefinition CreateValidMixedWorkflow()
    {
        return new WorkflowDefinition
        {
            Name = "MixedWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PLAN" },
                new WorkflowStateDefinition { Id = "PLAN", Type = WorkflowStateType.Agent, Executor = "planner", Next = "ANALYZE" },
                new WorkflowStateDefinition
                {
                    Id = "ANALYZE",
                    Type = WorkflowStateType.Parallel,
                    Executors = ["analyzer1", "analyzer2"],
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
    }

    private static AIAgent CreateMockAgent(string name)
    {
        // Create a real ChatClientAgent with a mock IChatClient
        // This is the recommended way to create AIAgent instances for testing
        var mockChatClient = new Mock<IChatClient>();

        // Create ChatClientAgent using the extension method
        return mockChatClient.Object.CreateAIAgent(
            instructions: $"Test instructions for {name}",
            name: name);
    }

    #endregion
}
