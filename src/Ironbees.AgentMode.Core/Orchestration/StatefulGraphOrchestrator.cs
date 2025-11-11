using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ironbees.AgentMode.Agents;
using Ironbees.AgentMode.Models;

namespace Ironbees.AgentMode.Orchestration;

/// <summary>
/// Stateful graph orchestrator implementing workflow state machine.
/// Manages agent coordination, state transitions, and HITL approval gates.
/// </summary>
public class StatefulGraphOrchestrator : IStatefulOrchestrator
{
    private readonly IEnumerable<ICodingAgent> _agents;
    private readonly ConcurrentDictionary<string, CodingState> _states = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ApprovalDecision>> _approvalGates = new();

    // State graph definition
    private static readonly Dictionary<string, Func<CodingState, string>> StateTransitions = new()
    {
        ["INIT"] = _ => "PLAN",
        ["PLAN"] = _ => "WAIT_PLAN_APPROVAL",
        ["WAIT_PLAN_APPROVAL"] = state => state.Plan != null ? "CODE" : "PLAN",
        ["CODE"] = _ => "VALIDATE",
        ["VALIDATE"] = state =>
        {
            if (state.BuildResult?.Success == true && state.TestResult?.Success == true)
                return "END";
            if (state.IterationCount >= state.MaxIterations)
                return "ERROR";
            return "CODE"; // Refine loop
        },
        ["ERROR"] = _ => "END",
        ["END"] = _ => "END"
    };

    public StatefulGraphOrchestrator(IEnumerable<ICodingAgent> agents)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
    }

    public async IAsyncEnumerable<CodingState> ExecuteAsync(
        string request,
        WorkflowContext? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request))
            throw new ArgumentNullException(nameof(request));

        // Initialize state
        var state = new CodingState
        {
            StateId = Guid.NewGuid().ToString(),
            UserRequest = request,
            CurrentNode = "INIT",
            Timestamp = DateTime.UtcNow,
            Metadata = BuildMetadata(context)
        };

        _states[state.StateId] = state;
        yield return state;

        // Execute state graph
        while (state.CurrentNode != "END" && !cancellationToken.IsCancellationRequested)
        {
            CodingState? nextState = null;
            Exception? executionError = null;

            try
            {
                nextState = await ExecuteNodeAsync(state, cancellationToken);
            }
            catch (Exception ex)
            {
                executionError = ex;
            }

            if (executionError != null)
            {
                // Handle error and transition to ERROR state
                state = state with
                {
                    CurrentNode = "ERROR",
                    ErrorContext = $"{executionError.GetType().Name}: {executionError.Message}\n{executionError.StackTrace}",
                    Timestamp = DateTime.UtcNow
                };
                _states[state.StateId] = state;
                yield return state;
                break;
            }

            if (nextState != null)
            {
                state = nextState;
                _states[state.StateId] = state;
                yield return state;
            }
        }

        // Cleanup
        _states.TryRemove(state.StateId, out _);
        _approvalGates.TryRemove(state.StateId, out _);
    }

    public async Task ApproveAsync(string stateId, ApprovalDecision decision)
    {
        if (!_states.ContainsKey(stateId))
            throw new StateNotFoundException(stateId);

        if (!_approvalGates.TryGetValue(stateId, out var approvalGate))
            throw new InvalidStateException(stateId, _states[stateId].CurrentNode,
                "State is not awaiting approval");

        approvalGate.SetResult(decision);
    }

    public Task CancelAsync(string stateId)
    {
        if (!_states.TryRemove(stateId, out _))
            throw new StateNotFoundException(stateId);

        if (_approvalGates.TryRemove(stateId, out var approvalGate))
        {
            approvalGate.TrySetCanceled();
        }

        return Task.CompletedTask;
    }

    public Task<CodingState> GetStateAsync(string stateId)
    {
        if (!_states.TryGetValue(stateId, out var state))
            throw new StateNotFoundException(stateId);

        return Task.FromResult(state);
    }

    private async Task<CodingState> ExecuteNodeAsync(CodingState state, CancellationToken cancellationToken)
    {
        // Determine next node
        var nextNode = StateTransitions.TryGetValue(state.CurrentNode, out var transitionFn)
            ? transitionFn(state)
            : throw new OrchestratorException($"Unknown node: {state.CurrentNode}", state.StateId, state.CurrentNode);

        // Update state with new node
        state = state with
        {
            CurrentNode = nextNode,
            Timestamp = DateTime.UtcNow
        };

        // Handle HITL approval gates
        if (nextNode == "WAIT_PLAN_APPROVAL")
        {
            return await WaitForApprovalAsync(state, cancellationToken);
        }

        // Execute agent for this node
        var agent = GetAgentForNode(nextNode);
        if (agent != null)
        {
            var response = await agent.ExecuteAsync(state, cancellationToken);
            state = ApplyAgentResponse(state, response);
        }

        return state;
    }

    private async Task<CodingState> WaitForApprovalAsync(CodingState state, CancellationToken cancellationToken)
    {
        var approvalGate = new TaskCompletionSource<ApprovalDecision>();
        _approvalGates[state.StateId] = approvalGate;

        // Wait for approval
        var decision = await approvalGate.Task.WaitAsync(cancellationToken);

        if (!decision.Approved && !string.IsNullOrWhiteSpace(decision.Feedback))
        {
            // User rejected with feedback - go back to planning
            state = state with
            {
                CurrentNode = "PLAN",
                Spec = state.Spec + $"\n\nUser Feedback: {decision.Feedback}",
                Timestamp = DateTime.UtcNow
            };
        }

        return state;
    }

    private ICodingAgent? GetAgentForNode(string node)
    {
        return node switch
        {
            "PLAN" => _agents.FirstOrDefault(a => a.Name == "planner"),
            "CODE" => _agents.FirstOrDefault(a => a.Name == "coder"),
            "VALIDATE" => _agents.FirstOrDefault(a => a.Name == "validator"),
            _ => null
        };
    }

    private CodingState ApplyAgentResponse(CodingState state, AgentResponse response)
    {
        var updates = response.Updates;
        var newState = state;

        // Apply updates using C# 'with' expression
        if (updates.TryGetValue("Plan", out var plan))
            newState = newState with { Plan = plan as ExecutionPlan };

        if (updates.TryGetValue("Spec", out var spec))
            newState = newState with { Spec = spec as string };

        if (updates.TryGetValue("CodeDiffs", out var codeDiffs))
            newState = newState with { CodeDiffs = codeDiffs as System.Collections.Immutable.ImmutableList<FileEdit>
                ?? newState.CodeDiffs };

        if (updates.TryGetValue("BuildResult", out var buildResult))
            newState = newState with { BuildResult = buildResult as BuildResult };

        if (updates.TryGetValue("TestResult", out var testResult))
            newState = newState with { TestResult = testResult as TestResult };

        if (updates.TryGetValue("ErrorContext", out var errorContext))
            newState = newState with { ErrorContext = errorContext as string };

        if (updates.TryGetValue("IterationCount", out var iterationCount))
            newState = newState with { IterationCount = Convert.ToInt32(iterationCount) };

        // Override next node if agent specified one
        if (response.NextNode != null)
            newState = newState with { CurrentNode = response.NextNode };

        newState = newState with { Timestamp = DateTime.UtcNow };

        return newState;
    }

    private System.Collections.Immutable.ImmutableDictionary<string, string> BuildMetadata(WorkflowContext? context)
    {
        if (context == null)
            return System.Collections.Immutable.ImmutableDictionary<string, string>.Empty;

        var metadata = new Dictionary<string, string>();

        if (context.SolutionPath != null)
            metadata["SolutionPath"] = context.SolutionPath;

        if (context.TargetProject != null)
            metadata["TargetProject"] = context.TargetProject;

        return metadata.ToImmutableDictionary();
    }
}
