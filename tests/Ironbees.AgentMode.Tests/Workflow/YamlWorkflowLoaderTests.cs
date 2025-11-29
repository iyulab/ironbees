using Ironbees.AgentMode.Core.Exceptions;
using Ironbees.AgentMode.Core.Workflow;
using Xunit;

namespace Ironbees.AgentMode.Tests.Workflow;

public class YamlWorkflowLoaderTests
{
    private readonly YamlWorkflowLoader _loader = new();

    [Fact]
    public async Task LoadFromStringAsync_ValidYaml_ReturnsWorkflowDefinition()
    {
        // Arrange
        var yaml = """
            name: TestWorkflow
            version: "1.0"
            description: "Test workflow"
            states:
              - id: START
                type: start
                next: END
              - id: END
                type: terminal
            """;

        // Act
        var result = await _loader.LoadFromStringAsync(yaml);

        // Assert
        Assert.Equal("TestWorkflow", result.Name);
        Assert.Equal("1.0", result.Version);
        Assert.Equal("Test workflow", result.Description);
        Assert.Equal(2, result.States.Count);
    }

    [Fact]
    public async Task LoadFromStringAsync_WithAgents_ParsesAgentReferences()
    {
        // Arrange
        var yaml = """
            name: AgentWorkflow
            agents:
              - ref: agents/planner
                alias: planner
              - ref: agents/coder
            states:
              - id: START
                type: start
            """;

        // Act
        var result = await _loader.LoadFromStringAsync(yaml);

        // Assert
        Assert.Equal(2, result.Agents.Count);
        Assert.Equal("agents/planner", result.Agents[0].Ref);
        Assert.Equal("planner", result.Agents[0].Alias);
        Assert.Equal("agents/coder", result.Agents[1].Ref);
        Assert.Null(result.Agents[1].Alias);
    }

    [Fact]
    public async Task LoadFromStringAsync_WithTrigger_ParsesTriggerDefinition()
    {
        // Arrange
        var yaml = """
            name: TriggerWorkflow
            states:
              - id: WAIT_FILE
                type: agent
                executor: processor
                trigger:
                  type: file_exists
                  path: requirements.md
                next: END
              - id: END
                type: terminal
            """;

        // Act
        var result = await _loader.LoadFromStringAsync(yaml);

        // Assert
        var state = result.States.First(s => s.Id == "WAIT_FILE");
        Assert.NotNull(state.Trigger);
        Assert.Equal(TriggerType.FileExists, state.Trigger.Type);
        Assert.Equal("requirements.md", state.Trigger.Path);
    }

    [Fact]
    public async Task LoadFromStringAsync_WithHumanGate_ParsesHumanGateSettings()
    {
        // Arrange
        var yaml = """
            name: ApprovalWorkflow
            states:
              - id: APPROVAL
                type: human_gate
                human_gate:
                  approval_mode: always_require
                  timeout: "1h"
                  on_approve: NEXT
                  on_reject: BACK
              - id: NEXT
                type: terminal
              - id: BACK
                type: terminal
            """;

        // Act
        var result = await _loader.LoadFromStringAsync(yaml);

        // Assert
        var state = result.States.First(s => s.Id == "APPROVAL");
        Assert.Equal(WorkflowStateType.HumanGate, state.Type);
        Assert.NotNull(state.HumanGate);
        Assert.Equal("always_require", state.HumanGate.ApprovalMode);
        Assert.Equal(TimeSpan.FromHours(1), state.HumanGate.Timeout);
        Assert.Equal("NEXT", state.HumanGate.OnApprove);
        Assert.Equal("BACK", state.HumanGate.OnReject);
    }

    [Fact]
    public async Task LoadFromStringAsync_WithConditions_ParsesConditionalTransitions()
    {
        // Arrange
        var yaml = """
            name: ConditionalWorkflow
            states:
              - id: VALIDATE
                type: agent
                executor: validator
                conditions:
                  - if: "build.success"
                    then: SUCCESS
                  - if: "iteration_count >= 5"
                    then: FAIL
                  - then: RETRY
                    else: true
              - id: SUCCESS
                type: terminal
              - id: FAIL
                type: terminal
              - id: RETRY
                type: agent
            """;

        // Act
        var result = await _loader.LoadFromStringAsync(yaml);

        // Assert
        var state = result.States.First(s => s.Id == "VALIDATE");
        Assert.Equal(3, state.Conditions.Count);
        Assert.Equal("build.success", state.Conditions[0].If);
        Assert.Equal("SUCCESS", state.Conditions[0].Then);
        Assert.True(state.Conditions[2].IsDefault);
    }

    [Fact]
    public async Task LoadFromStringAsync_WithSettings_ParsesWorkflowSettings()
    {
        // Arrange
        var yaml = """
            name: SettingsWorkflow
            settings:
              default_timeout: "45m"
              default_max_iterations: 10
              enable_checkpointing: false
              checkpoint_directory: ".custom/checkpoints"
            states:
              - id: START
                type: start
            """;

        // Act
        var result = await _loader.LoadFromStringAsync(yaml);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(45), result.Settings.DefaultTimeout);
        Assert.Equal(10, result.Settings.DefaultMaxIterations);
        Assert.False(result.Settings.EnableCheckpointing);
        Assert.Equal(".custom/checkpoints", result.Settings.CheckpointDirectory);
    }

    [Fact]
    public async Task LoadFromStringAsync_InvalidYaml_ThrowsWorkflowParseException()
    {
        // Arrange
        var yaml = """
            name: BadWorkflow
            states:
              - id: MISSING_COLON
                type agent  # Missing colon
            """;

        // Act & Assert
        await Assert.ThrowsAsync<WorkflowParseException>(
            () => _loader.LoadFromStringAsync(yaml));
    }

    [Fact]
    public async Task LoadFromStringAsync_MissingName_ThrowsWorkflowParseException()
    {
        // Arrange
        var yaml = """
            version: "1.0"
            states:
              - id: START
                type: start
            """;

        // Act & Assert
        await Assert.ThrowsAsync<WorkflowParseException>(
            () => _loader.LoadFromStringAsync(yaml));
    }

    [Fact]
    public void Validate_ValidWorkflow_ReturnsSuccess()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "ValidWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        // Act
        var result = _loader.Validate(workflow);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_DuplicateStateIds_ReturnsError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "DuplicateWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start },
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Agent }
            ]
        };

        // Act
        var result = _loader.Validate(workflow);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WF003");
    }

    [Fact]
    public void Validate_InvalidTransitionTarget_ReturnsError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "InvalidTransitionWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "NONEXISTENT" }
            ]
        };

        // Act
        var result = _loader.Validate(workflow);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "WF004");
    }

    [Fact]
    public void Validate_NoTerminalState_ReturnsWarning()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            Name = "NoTerminalWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start },
                new WorkflowStateDefinition { Id = "AGENT", Type = WorkflowStateType.Agent }
            ]
        };

        // Act
        var result = _loader.Validate(workflow);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Code == "WF102");
    }

    [Theory]
    [InlineData("30m", 30)]
    [InlineData("1h", 60)]
    [InlineData("2d", 2880)]
    [InlineData("90s", 1.5)]
    public async Task LoadFromStringAsync_TimeSpanFormats_ParsesCorrectly(string input, double expectedMinutes)
    {
        // Arrange
        var yaml = $"""
            name: TimeoutWorkflow
            states:
              - id: WORK
                type: agent
                executor: worker
                timeout: "{input}"
            """;

        // Act
        var result = await _loader.LoadFromStringAsync(yaml);

        // Assert
        var state = result.States.First();
        Assert.NotNull(state.Timeout);
        Assert.Equal(expectedMinutes, state.Timeout.Value.TotalMinutes);
    }
}
