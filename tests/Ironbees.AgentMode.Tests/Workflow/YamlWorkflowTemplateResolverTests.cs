// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using Ironbees.AgentMode.Workflow;
using Ironbees.Core.Goals;
using Xunit;

namespace Ironbees.AgentMode.Tests.Workflow;

public class YamlWorkflowTemplateResolverTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly YamlWorkflowLoader _loader;
    private readonly YamlWorkflowTemplateResolver _resolver;

    public YamlWorkflowTemplateResolverTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ironbees-template-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _loader = new YamlWorkflowLoader();
        _resolver = new YamlWorkflowTemplateResolver(
            _loader,
            new YamlWorkflowTemplateResolverOptions
            {
                TemplatesDirectory = _testDirectory
            });
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private void CreateTemplate(string name, string content)
    {
        var path = Path.Combine(_testDirectory, $"{name}.yaml");
        File.WriteAllText(path, content);
    }

    private static GoalDefinition CreateTestGoal(string id = "test-goal") => new()
    {
        Id = id,
        Name = "Test Goal",
        Description = "A test goal for template resolution",
        WorkflowTemplate = "goal-loop",
        Constraints = new GoalConstraints
        {
            MaxIterations = 5,
            MaxTokens = 10000
        },
        Checkpoint = new CheckpointSettings
        {
            Enabled = true,
            AfterEachIteration = true,
            CheckpointDirectory = "checkpoints"
        },
        Tags = ["test", "development"],
        Parameters = new Dictionary<string, object>
        {
            ["executor"] = "code-agent",
            ["evaluator"] = "review-agent"
        }
    };

    [Fact]
    public async Task ResolveAsync_WithGoal_SubstitutesParameters()
    {
        // Arrange
        // Note: YamlWorkflowLoader uses underscored naming convention (max_iterations not maxIterations)
        CreateTemplate("test-template", """
            name: "{{goal.id}}-workflow"
            version: "1.0"
            states:
              - id: START
                type: start
                next: EXECUTE
              - id: EXECUTE
                type: agent
                executor: "{{parameters.executor}}"
                max_iterations: {{goal.constraints.maxIterations}}
                next: END
              - id: END
                type: terminal
            """);

        var goal = CreateTestGoal("my-goal");

        // Act
        var result = await _resolver.ResolveAsync("test-template", goal);

        // Assert
        Assert.Equal("my-goal-workflow", result.Name);
        Assert.Equal(3, result.States.Count);
        Assert.Equal("code-agent", result.States[1].Executor);
        // MaxIterations should be substituted from goal.constraints.maxIterations
        Assert.NotNull(result.States[1].MaxIterations);
        Assert.Equal(5, result.States[1].MaxIterations!.Value);
    }

    [Fact]
    public async Task ResolveAsync_WithDictionary_SubstitutesParameters()
    {
        // Arrange
        CreateTemplate("dict-template", """
            name: "{{project}}-workflow"
            version: "{{version}}"
            states:
              - id: START
                type: start
            """);

        var parameters = new Dictionary<string, object>
        {
            ["project"] = "ironbees",
            ["version"] = "2.0"
        };

        // Act
        var result = await _resolver.ResolveAsync("dict-template", parameters);

        // Assert
        Assert.Equal("ironbees-workflow", result.Name);
        Assert.Equal("2.0", result.Version);
    }

    [Fact]
    public async Task ResolveAsync_MissingTemplate_ThrowsException()
    {
        // Arrange
        var goal = CreateTestGoal();

        // Act & Assert
        await Assert.ThrowsAsync<WorkflowTemplateNotFoundException>(
            () => _resolver.ResolveAsync("non-existent", goal));
    }

    [Fact]
    public async Task ResolveAsync_StrictMode_UnresolvedParameter_ThrowsException()
    {
        // Arrange
        CreateTemplate("strict-template", """
            name: "{{goal.id}}-{{missing.param}}"
            states:
              - id: START
                type: start
            """);

        var goal = CreateTestGoal();

        // Act & Assert
        await Assert.ThrowsAsync<WorkflowTemplateResolutionException>(
            () => _resolver.ResolveAsync("strict-template", goal));
    }

    [Fact]
    public async Task ResolveAsync_NonStrictMode_UnresolvedParameter_KeepsPlaceholder()
    {
        // Arrange
        var resolver = new YamlWorkflowTemplateResolver(
            _loader,
            new YamlWorkflowTemplateResolverOptions
            {
                TemplatesDirectory = _testDirectory,
                StrictMode = false
            });

        CreateTemplate("non-strict-template", """
            name: "{{goal.id}}-{{missing.param}}"
            states:
              - id: START
                type: start
            """);

        var goal = CreateTestGoal("test");

        // Act
        var result = await resolver.ResolveAsync("non-strict-template", goal);

        // Assert
        Assert.Equal("test-{{missing.param}}", result.Name);
    }

    [Fact]
    public async Task ResolveAsync_NonStrictMode_UseDefault_ReplacesWithDefault()
    {
        // Arrange
        var resolver = new YamlWorkflowTemplateResolver(
            _loader,
            new YamlWorkflowTemplateResolverOptions
            {
                TemplatesDirectory = _testDirectory,
                StrictMode = false,
                UseDefaultForMissing = true,
                DefaultValue = "default"
            });

        CreateTemplate("default-template", """
            name: "{{goal.id}}-{{missing.param}}"
            states:
              - id: START
                type: start
            """);

        var goal = CreateTestGoal("test");

        // Act
        var result = await resolver.ResolveAsync("default-template", goal);

        // Assert
        Assert.Equal("test-default", result.Name);
    }

    [Fact]
    public void TemplateExists_ExistingTemplate_ReturnsTrue()
    {
        // Arrange
        CreateTemplate("exists", "name: test\nstates: []");

        // Act
        var exists = _resolver.TemplateExists("exists");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void TemplateExists_NonExistingTemplate_ReturnsFalse()
    {
        // Act
        var exists = _resolver.TemplateExists("does-not-exist");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void GetAvailableTemplates_ReturnsTemplateNames()
    {
        // Arrange
        CreateTemplate("template-a", "name: a\nstates: []");
        CreateTemplate("template-b", "name: b\nstates: []");
        CreateTemplate("template-c", "name: c\nstates: []");

        // Act
        var templates = _resolver.GetAvailableTemplates();

        // Assert
        Assert.Equal(3, templates.Count);
        Assert.Contains("template-a", templates);
        Assert.Contains("template-b", templates);
        Assert.Contains("template-c", templates);
    }

    [Fact]
    public void GetAvailableTemplates_EmptyDirectory_ReturnsEmptyList()
    {
        // Act
        var templates = _resolver.GetAvailableTemplates();

        // Assert
        Assert.Empty(templates);
    }

    [Fact]
    public void ValidateTemplate_ValidTemplate_ReturnsSuccess()
    {
        // Arrange
        CreateTemplate("valid", """
            name: "{{goal.id}}"
            states:
              - id: START
                type: start
            """);

        // Act
        var result = _resolver.ValidateTemplate("valid");

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("goal.id", result.Parameters);
    }

    [Fact]
    public void ValidateTemplate_MissingNameField_ReturnsError()
    {
        // Arrange
        CreateTemplate("invalid", """
            states:
              - id: START
                type: start
            """);

        // Act
        var result = _resolver.ValidateTemplate("invalid");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
    }

    [Fact]
    public void ValidateTemplate_MissingStatesField_ReturnsError()
    {
        // Arrange
        CreateTemplate("no-states", "name: test");

        // Act
        var result = _resolver.ValidateTemplate("no-states");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("states"));
    }

    [Fact]
    public void ValidateTemplate_NoParameters_ReturnsWarning()
    {
        // Arrange
        CreateTemplate("no-params", """
            name: "static-name"
            states:
              - id: START
                type: start
            """);

        // Act
        var result = _resolver.ValidateTemplate("no-params");

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("no parameter"));
    }

    [Fact]
    public void ValidateTemplate_NonExistingTemplate_ReturnsError()
    {
        // Act
        var result = _resolver.ValidateTemplate("missing");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not found"));
    }

    [Fact]
    public async Task ResolveAsync_ComplexGoal_SubstitutesAllParameters()
    {
        // Arrange
        CreateTemplate("complex", """
            name: "{{goal.id}}-workflow"
            version: "{{goal.version}}"
            description: "{{goal.description}}"
            settings:
              defaultMaxIterations: {{goal.constraints.maxIterations}}
              enableCheckpointing: {{goal.checkpoint.enabled}}
              checkpointDirectory: "{{goal.checkpoint.directory}}"
            states:
              - id: START
                type: start
                next: EXECUTE
              - id: EXECUTE
                type: agent
                executor: "{{parameters.executor}}"
                next: EVALUATE
              - id: EVALUATE
                type: agent
                executor: "{{parameters.evaluator}}"
                next: END
              - id: END
                type: terminal
            """);

        var goal = CreateTestGoal();

        // Act
        var result = await _resolver.ResolveAsync("complex", goal);

        // Assert
        Assert.Equal("test-goal-workflow", result.Name);
        Assert.Equal("1.0", result.Version);
        Assert.Contains("test goal", result.Description?.ToLowerInvariant() ?? "");
        Assert.Equal(5, result.Settings.DefaultMaxIterations);
        Assert.True(result.Settings.EnableCheckpointing);
        // CheckpointDirectory from template parameter substitution
        Assert.Contains("checkpoints", result.Settings.CheckpointDirectory);
        Assert.Equal("code-agent", result.States[1].Executor);
        Assert.Equal("review-agent", result.States[2].Executor);
    }

    [Fact]
    public async Task ResolveAsync_WithSpacesInPlaceholder_ParsesCorrectly()
    {
        // Arrange
        CreateTemplate("spaces", """
            name: "{{ goal.id }}-workflow"
            version: "{{  goal.version  }}"
            states:
              - id: START
                type: start
            """);

        var goal = CreateTestGoal();

        // Act
        var result = await _resolver.ResolveAsync("spaces", goal);

        // Assert
        Assert.Equal("test-goal-workflow", result.Name);
        Assert.Equal("1.0", result.Version);
    }

    [Fact]
    public async Task ResolveAsync_CaseInsensitiveParameters_Works()
    {
        // Arrange
        CreateTemplate("case", """
            name: "{{GOAL.ID}}-workflow"
            states:
              - id: START
                type: start
            """);

        var goal = CreateTestGoal("my-goal");

        // Act
        var result = await _resolver.ResolveAsync("case", goal);

        // Assert
        Assert.Equal("my-goal-workflow", result.Name);
    }
}
