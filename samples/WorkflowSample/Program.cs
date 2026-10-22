using Ironbees.AgentMode.Workflow;

namespace WorkflowSample;

/// <summary>
/// Sample demonstrating YAML workflow definitions: loading a workflow from a string and from a file,
/// and validating a definition before it is handed to an orchestrator.
/// No API key is needed - nothing here calls a model.
/// </summary>
internal sealed class Program
{
    private static async Task Main()
    {
        Console.WriteLine("=== Ironbees Workflow Definition Sample ===");
        Console.WriteLine();

        var loader = new YamlWorkflowLoader();

        await LoadFromStringAsync(loader);
        await LoadFromFileAsync(loader);
        Validate(loader);
    }

    /// <summary>
    /// Loads a workflow definition from YAML text.
    /// </summary>
    private static async Task LoadFromStringAsync(YamlWorkflowLoader loader)
    {
        Console.WriteLine("--- 1. Load a workflow from YAML text ---");

        var yamlContent = @"
name: TestWorkflow
version: '1.0'
description: Test workflow for validation

states:
  - id: START
    type: start
    next: AGENT

  - id: AGENT
    type: agent
    executor: test-agent
    next: END

  - id: END
    type: terminal
";

        var workflow = await loader.LoadFromStringAsync(yamlContent);
        Print(workflow);
    }

    /// <summary>
    /// Loads the workflow file shipped with this sample (workflows/simple_workflow.yaml).
    /// </summary>
    private static async Task LoadFromFileAsync(YamlWorkflowLoader loader)
    {
        Console.WriteLine("--- 2. Load a workflow from a file ---");

        var path = Path.Combine(AppContext.BaseDirectory, "workflows", "simple_workflow.yaml");
        var workflow = await loader.LoadFromFileAsync(path);
        Print(workflow);
    }

    /// <summary>
    /// Validates three definitions: a valid one, one with no start state, and one that only draws warnings.
    /// </summary>
    private static void Validate(YamlWorkflowLoader loader)
    {
        Console.WriteLine("--- 3. Validate workflow definitions ---");

        var validWorkflow = new WorkflowDefinition
        {
            Name = "ValidWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PROCESS" },
                new WorkflowStateDefinition { Id = "PROCESS", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        PrintValidation(validWorkflow.Name, loader.Validate(validWorkflow));

        var invalidWorkflow = new WorkflowDefinition
        {
            Name = "InvalidWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "PROCESS", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        PrintValidation(invalidWorkflow.Name, loader.Validate(invalidWorkflow));

        var triggerWorkflow = new WorkflowDefinition
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
                    Trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "input.txt" },
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };
        PrintValidation(triggerWorkflow.Name, loader.Validate(triggerWorkflow));
    }

    private static void Print(WorkflowDefinition workflow)
    {
        Console.WriteLine($"Loaded workflow: {workflow.Name} (version {workflow.Version})");
        Console.WriteLine($"States: {workflow.States.Count}");
        foreach (var state in workflow.States)
        {
            Console.WriteLine($"  - {state.Id} ({state.Type})");
        }
        Console.WriteLine();
    }

    private static void PrintValidation(string name, WorkflowValidationResult result)
    {
        Console.WriteLine($"{name}: IsValid={result.IsValid}, errors={result.Errors.Count}, warnings={result.Warnings.Count}");
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"  error   [{error.Code}] {error.Message}");
        }
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"  warning [{warning.Code}] {warning.Message}");
        }
        Console.WriteLine();
    }
}
