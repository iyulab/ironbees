using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Core.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace Ironbees.AgentFramework.Tests.Workflow;

/// <summary>
/// Integration tests for MAF Workflow conversion pipeline.
/// These tests verify that workflow definitions from YamlDrivenOrchestrator scenarios
/// can be successfully converted to MAF workflows.
/// </summary>
public class MafWorkflowIntegrationTests
{
    private readonly Mock<ILogger<MafWorkflowConverter>> _mockLogger;
    private readonly MafWorkflowConverter _converter;

    public MafWorkflowIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<MafWorkflowConverter>>();
        _converter = new MafWorkflowConverter(_mockLogger.Object);
    }

    #region Migration Compatibility Tests

    /// <summary>
    /// Verifies that a simple workflow (Start -> Terminal) can be validated by MafWorkflowConverter.
    /// This matches the SimpleWorkflow scenario from YamlDrivenOrchestratorTests.
    /// </summary>
    [Fact]
    public void Validate_SimpleWorkflowFromOrchestratorTests_IsValid()
    {
        // Arrange - Simple workflow from YamlDrivenOrchestratorTests
        var workflow = new WorkflowDefinition
        {
            Name = "SimpleWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(workflow);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Verifies that an agent workflow (Start -> Agent -> Terminal) can be converted.
    /// This matches the WithAgentState scenario from YamlDrivenOrchestratorTests.
    /// </summary>
    [Fact]
    public async Task ConvertAsync_AgentWorkflowFromOrchestratorTests_ReturnsValidWorkflow()
    {
        // Arrange - Agent workflow from YamlDrivenOrchestratorTests
        var workflow = new WorkflowDefinition
        {
            Name = "AgentWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent, Executor = "test-agent", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var mafWorkflow = await _converter.ConvertAsync(workflow, CreateAgentResolver());

        // Assert
        Assert.NotNull(mafWorkflow);
    }

    /// <summary>
    /// Verifies that a workflow with triggers can be validated.
    /// Triggers are handled by Ironbees, so they should generate a warning but not prevent conversion.
    /// This matches the WithTrigger scenario from YamlDrivenOrchestratorTests.
    /// </summary>
    [Fact]
    public void Validate_TriggerWorkflowFromOrchestratorTests_IsValidWithWarning()
    {
        // Arrange - Trigger workflow from YamlDrivenOrchestratorTests
        var workflow = new WorkflowDefinition
        {
            Name = "TriggerWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "WAIT" },
                new WorkflowStateDefinition
                {
                    Id = "WAIT",
                    Type = WorkflowStateType.Agent,
                    Executor = "worker",
                    Trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "ready.txt" },
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(workflow);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "WFC101"); // Triggers handled by Ironbees
    }

    /// <summary>
    /// Verifies that a workflow with conditions can be validated.
    /// Conditions are simplified to linear flow in MAF.
    /// This matches the WithConditions scenario from YamlDrivenOrchestratorTests.
    /// </summary>
    [Fact]
    public void Validate_ConditionalWorkflowFromOrchestratorTests_IsValidWithWarning()
    {
        // Arrange - Conditional workflow from YamlDrivenOrchestratorTests
        var workflow = new WorkflowDefinition
        {
            Name = "ConditionalWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "CHECK" },
                new WorkflowStateDefinition
                {
                    Id = "CHECK",
                    Type = WorkflowStateType.Agent,
                    Executor = "checker",
                    Next = "SUCCESS", // Default next for linear flow
                    Conditions =
                    [
                        new ConditionalTransition { If = "success", Then = "SUCCESS" },
                        new ConditionalTransition { Then = "FAILURE", IsDefault = true }
                    ]
                },
                new WorkflowStateDefinition { Id = "SUCCESS", Type = WorkflowStateType.Terminal },
                new WorkflowStateDefinition { Id = "FAILURE", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _converter.Validate(workflow);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "WFC100"); // Conditional transitions simplified
    }

    /// <summary>
    /// Verifies that a parallel workflow can be converted.
    /// This matches the ParallelState scenario from YamlDrivenOrchestratorTests.
    /// </summary>
    [Fact]
    public async Task ConvertAsync_ParallelWorkflowFromOrchestratorTests_ReturnsValidWorkflow()
    {
        // Arrange - Parallel workflow from YamlDrivenOrchestratorTests
        var workflow = new WorkflowDefinition
        {
            Name = "ParallelWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PARALLEL" },
                new WorkflowStateDefinition
                {
                    Id = "PARALLEL",
                    Type = WorkflowStateType.Parallel,
                    Executors = ["agent1", "agent2", "agent3"],
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var mafWorkflow = await _converter.ConvertAsync(workflow, CreateAgentResolver());

        // Assert
        Assert.NotNull(mafWorkflow);
    }

    /// <summary>
    /// Verifies that an iterative workflow can be validated and converted.
    /// This matches the WithIterationCount scenario from YamlDrivenOrchestratorTests.
    /// </summary>
    [Fact]
    public async Task ConvertAsync_IterativeWorkflowFromOrchestratorTests_ReturnsValidWorkflow()
    {
        // Arrange - Iterative workflow from YamlDrivenOrchestratorTests
        var workflow = new WorkflowDefinition
        {
            Name = "IterativeWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT" },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var mafWorkflow = await _converter.ConvertAsync(workflow, CreateAgentResolver());

        // Assert
        Assert.NotNull(mafWorkflow);
    }

    #endregion

    #region End-to-End Pipeline Tests

    /// <summary>
    /// Verifies the complete pipeline: Workflow Definition -> Validation -> Conversion -> MAF Workflow.
    /// </summary>
    [Fact]
    public async Task Pipeline_CompleteWorkflow_ConvertsSuccessfully()
    {
        // Arrange - Complex workflow combining multiple features
        var workflow = new WorkflowDefinition
        {
            Name = "CompletePipeline",
            Version = "1.0",
            Description = "End-to-end pipeline test workflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PLAN" },
                new WorkflowStateDefinition { Id = "PLAN", Type = WorkflowStateType.Agent, Executor = "planner", Next = "CODE" },
                new WorkflowStateDefinition { Id = "CODE", Type = WorkflowStateType.Agent, Executor = "coder", Next = "REVIEW" },
                new WorkflowStateDefinition { Id = "REVIEW", Type = WorkflowStateType.Agent, Executor = "reviewer", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act - Step 1: Validate
        var validation = _converter.Validate(workflow);

        // Assert - Step 1
        Assert.True(validation.IsValid, $"Validation failed: {string.Join(", ", validation.Errors.Select(e => e.Message))}");

        // Act - Step 2: Convert
        var mafWorkflow = await _converter.ConvertAsync(workflow, CreateAgentResolver());

        // Assert - Step 2
        Assert.NotNull(mafWorkflow);
    }

    /// <summary>
    /// Verifies that the pipeline correctly identifies and reports all validation issues.
    /// </summary>
    [Fact]
    public void Pipeline_WorkflowWithMultipleIssues_ReportsAllIssues()
    {
        // Arrange - Workflow with multiple validation issues
        var workflow = new WorkflowDefinition
        {
            Name = "MultiIssueWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "AGENT1" },
                new WorkflowStateDefinition
                {
                    Id = "AGENT1",
                    Type = WorkflowStateType.Agent,
                    Executor = "worker",
                    Next = "AGENT2",
                    Trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "input.txt" },
                    Conditions = [new ConditionalTransition { If = "success", Then = "END" }]
                },
                new WorkflowStateDefinition { Id = "AGENT2", Type = WorkflowStateType.Agent, Executor = "processor", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var validation = _converter.Validate(workflow);

        // Assert
        Assert.True(validation.IsValid); // Warnings don't prevent conversion
        Assert.Contains(validation.Warnings, w => w.Code == "WFC100"); // Conditional warning
        Assert.Contains(validation.Warnings, w => w.Code == "WFC101"); // Trigger warning
    }

    #endregion

    #region Backward Compatibility Tests

    /// <summary>
    /// Verifies that workflow definitions using older patterns still work.
    /// </summary>
    [Fact]
    public async Task ConvertAsync_LegacyWorkflowPattern_ConvertsSuccessfully()
    {
        // Arrange - Workflow using simple linear pattern
        var workflow = new WorkflowDefinition
        {
            Name = "LegacyPattern",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PROCESS" },
                new WorkflowStateDefinition { Id = "PROCESS", Type = WorkflowStateType.Agent, Executor = "legacy-agent", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var mafWorkflow = await _converter.ConvertAsync(workflow, CreateAgentResolver());

        // Assert
        Assert.NotNull(mafWorkflow);
    }

    /// <summary>
    /// Verifies that empty executor lists are properly detected.
    /// </summary>
    [Fact]
    public void Validate_ParallelWithEmptyExecutors_ReturnsError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "EmptyParallel",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PARALLEL" },
                new WorkflowStateDefinition
                {
                    Id = "PARALLEL",
                    Type = WorkflowStateType.Parallel,
                    Executors = [], // Empty!
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var validation = _converter.Validate(workflow);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Code == "WFC008");
    }

    #endregion

    #region Helper Methods

    private static Func<string, CancellationToken, Task<AIAgent>> CreateAgentResolver()
    {
        return (name, _) =>
        {
            var mockChatClient = new Mock<IChatClient>();
            AIAgent agent = mockChatClient.Object.CreateAIAgent(
                instructions: $"Test agent: {name}",
                name: name);
            return Task.FromResult(agent);
        };
    }

    #endregion
}
