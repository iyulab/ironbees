using Ironbees.Autonomous;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests;

/// <summary>
/// <c>WithFinalIterationEnforcement</c> does what it promises: the last iteration runs with the warning in front of its
/// prompt, and when it still misses the goal the enforcer's result is the recorded outcome. Before 0.22.0 the warning was
/// stored and never read and <c>ForceCompletionAsync</c> had no caller, so the method did nothing.
/// </summary>
public class FinalIterationEnforcementTests
{
    private static AutonomousOrchestrator<EnforcementRequest, EnforcementResult> Build(
        PromptRecordingExecutor executor, IOracleVerifier oracle, Func<FinalIterationContext<EnforcementRequest, EnforcementResult>, EnforcementResult> enforcer) =>
        AutonomousOrchestrator.Create<EnforcementRequest, EnforcementResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new EnforcementRequest(id, prompt))
            .WithOracle(oracle)
            .WithAutoContinue()
            .WithAutoContinueOnIncomplete()
            .WithMaxOracleIterations(1)
            .WithMaxIterations(2)
            .WithFinalIterationEnforcement(enforcer, "LAST CHANCE")
            .Build();

    [Fact]
    public async Task The_last_iteration_runs_with_the_warning_and_a_missed_goal_records_the_forced_result()
    {
        var executor = new PromptRecordingExecutor();
        var orchestrator = Build(executor, new NeverCompleteOracle(),
            ctx => new EnforcementResult(ctx.OriginalRequest.RequestId, "FORCED ANSWER after " + ctx.PreviousOutputs.Count));
        var events = new List<AutonomousEvent>();
        orchestrator.OnEvent += events.Add;

        orchestrator.EnqueuePrompt("Guess the word");
        await orchestrator.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, executor.Prompts.Count);
        Assert.DoesNotContain("LAST CHANCE", executor.Prompts[0], StringComparison.Ordinal);
        Assert.StartsWith("LAST CHANCE", executor.Prompts[1], StringComparison.Ordinal);

        var forced = Assert.Single(events, e => e.Type == AutonomousEventType.ForcedCompletion);
        Assert.Equal("FORCED ANSWER after 2", forced.HistoryEntry?.ExecutionOutput);
        Assert.Contains(orchestrator.GetHistory(), h => h.ExecutionOutput == "FORCED ANSWER after 2");
    }

    [Fact]
    public async Task A_goal_reached_before_the_end_never_calls_the_enforcer()
    {
        var executor = new PromptRecordingExecutor();
        var called = false;
        var orchestrator = Build(executor, new AlwaysCompleteOracle(), ctx =>
        {
            called = true;
            return new EnforcementResult(ctx.OriginalRequest.RequestId, "unused");
        });
        var events = new List<AutonomousEvent>();
        orchestrator.OnEvent += events.Add;

        orchestrator.EnqueuePrompt("Guess the word");
        await orchestrator.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(called);
        Assert.DoesNotContain(events, e => e.Type == AutonomousEventType.ForcedCompletion);
    }
}

public sealed record EnforcementRequest(string RequestId, string Prompt) : ITaskRequest;

public sealed record EnforcementResult(string RequestId, string Output) : ITaskResult
{
    public bool Success => true;
    public string? ErrorOutput => null;
}

public sealed class PromptRecordingExecutor : ITaskExecutor<EnforcementRequest, EnforcementResult>
{
    public List<string> Prompts { get; } = [];

    public Task<EnforcementResult> ExecuteAsync(
        EnforcementRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        Prompts.Add(request.Prompt);
        return Task.FromResult(new EnforcementResult(request.RequestId, $"attempt {Prompts.Count}"));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class NeverCompleteOracle : IOracleVerifier
{
    public bool IsConfigured => true;

    public Task<OracleVerdict> VerifyAsync(string originalPrompt, string executionOutput, OracleConfig? config = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OracleVerdict { IsComplete = false, CanContinue = true, Analysis = "not yet", NextPromptSuggestion = "Guess the word" });

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null) => originalPrompt;
}

public sealed class AlwaysCompleteOracle : IOracleVerifier
{
    public bool IsConfigured => true;

    public Task<OracleVerdict> VerifyAsync(string originalPrompt, string executionOutput, OracleConfig? config = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OracleVerdict { IsComplete = true, CanContinue = false, Confidence = 1.0, Analysis = "done" });

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null) => originalPrompt;
}
