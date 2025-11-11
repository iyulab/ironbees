using System.Collections.Immutable;
using Ironbees.AgentMode.Models;
using Ironbees.AgentMode.MCP;

namespace Ironbees.AgentMode.Agents;

/// <summary>
/// PlannerAgent: Analyzes user requests and creates structured execution plans.
/// MVP version with basic planning logic.
/// </summary>
public class PlannerAgent : ICodingAgent
{
    private readonly IToolRegistry? _toolRegistry;

    public string Name => "planner";
    public string Description => "Analyzes user request and creates execution plan";
    public IReadOnlyList<string> RequiredTools => new[] { "roslyn" };

    public PlannerAgent(IToolRegistry? toolRegistry = null)
    {
        _toolRegistry = toolRegistry;
    }

    public Task<AgentResponse> ExecuteAsync(
        CodingState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // MVP: Create a basic plan from user request
            var plan = CreatePlanFromRequest(state);
            var spec = CreateSpecFromRequest(state);

            return Task.FromResult(new AgentResponse
            {
                Updates = new Dictionary<string, object?>
                {
                    ["Spec"] = spec,
                    ["Plan"] = plan
                },
                NextNode = "WAIT_PLAN_APPROVAL",
                Metadata = new Dictionary<string, string>
                {
                    ["agent"] = Name,
                    ["version"] = "mvp"
                }
            });
        }
        catch (Exception ex)
        {
            throw new AgentExecutionException(Name, $"Failed to create execution plan: {ex.Message}", ex);
        }
    }

    private string CreateSpecFromRequest(CodingState state)
    {
        // Extract key information from user request
        var request = state.UserRequest;
        var solutionPath = state.Metadata.GetValueOrDefault("SolutionPath", "");
        var targetProject = state.Metadata.GetValueOrDefault("TargetProject", "");

        var spec = $@"## Specification

**User Request**: {request}

**Context**:
- Solution: {solutionPath}
- Target Project: {targetProject}

**Requirements**:
- Analyze the request and identify affected components
- Implement the requested changes
- Ensure code compiles and tests pass
- Follow .NET coding conventions

**Constraints**:
- Maximum 5 refinement iterations
- Must maintain backward compatibility
- Follow existing code patterns";

        // Include user feedback if this is a refinement
        if (!string.IsNullOrWhiteSpace(state.Spec))
        {
            spec += $@"

**Previous Feedback**:
{state.Spec}";
        }

        return spec;
    }

    private ExecutionPlan CreatePlanFromRequest(CodingState state)
    {
        var request = state.UserRequest.ToLowerInvariant();
        var steps = new List<PlanStep>();
        var affectedFiles = new List<string>();
        var complexity = "MEDIUM";

        // MVP: Simple keyword-based planning
        if (request.Contains("add") || request.Contains("create"))
        {
            steps.Add(new PlanStep
            {
                Number = 1,
                Description = $"Create new component based on request: {state.UserRequest}",
                ActionType = "CREATE",
                Rationale = "User requested creation of new functionality"
            });
            complexity = "MEDIUM";
        }
        else if (request.Contains("delete") || request.Contains("remove"))
        {
            steps.Add(new PlanStep
            {
                Number = 1,
                Description = $"Remove component: {state.UserRequest}",
                ActionType = "DELETE",
                Rationale = "User requested deletion"
            });
            complexity = "LOW";
        }
        else if (request.Contains("refactor") || request.Contains("reorganize"))
        {
            steps.Add(new PlanStep
            {
                Number = 1,
                Description = $"Refactor code: {state.UserRequest}",
                ActionType = "REFACTOR",
                Rationale = "User requested refactoring"
            });
            complexity = "HIGH";
        }
        else
        {
            // Default: modification
            steps.Add(new PlanStep
            {
                Number = 1,
                Description = $"Modify existing code: {state.UserRequest}",
                ActionType = "MODIFY",
                Rationale = "User requested changes to existing code"
            });
        }

        // Add validation step
        steps.Add(new PlanStep
        {
            Number = steps.Count + 1,
            Description = "Build and test the changes",
            ActionType = "MODIFY",
            Rationale = "Ensure changes compile and tests pass"
        });

        return new ExecutionPlan
        {
            Summary = $"Execute requested changes: {state.UserRequest}",
            Steps = steps.ToImmutableList(),
            AffectedFiles = affectedFiles.ToImmutableList(),
            Complexity = complexity
        };
    }
}
