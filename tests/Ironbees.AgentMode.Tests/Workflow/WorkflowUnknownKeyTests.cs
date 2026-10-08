using Ironbees.AgentMode.Exceptions;
using Ironbees.AgentMode.Workflow;
using Xunit;

namespace Ironbees.AgentMode.Tests.Workflow;

/// <summary>Workflow YAML is snake_case; a key no field reads is named instead of dropped, and the bundled sample loads cleanly.</summary>
public sealed class WorkflowUnknownKeyTests
{
    [Fact]
    public async Task A_camelCase_setting_fails_and_names_the_snake_case_spelling()
    {
        var ex = await Assert.ThrowsAsync<WorkflowParseException>(() => new YamlWorkflowLoader().LoadFromStringAsync("""
            name: W
            settings:
              defaultMaxIterations: 5
            states:
              - id: START
                type: start
            """, TestContext.Current.CancellationToken));

        Assert.Contains("settings.defaultMaxIterations", ex.Message, StringComparison.Ordinal);
        Assert.Contains("default_max_iterations", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_bundled_sample_workflow_loads()
    {
        string? root = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null && root is null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ironbees.slnx")))
                root = dir.FullName;
        }

        Assert.NotNull(root);
        var workflow = await new YamlWorkflowLoader().LoadFromFileAsync(
            Path.Combine(root, "samples", "WorkflowSample", "workflows", "simple_workflow.yaml"), TestContext.Current.CancellationToken);
        Assert.NotEmpty(workflow.States);
    }
}
