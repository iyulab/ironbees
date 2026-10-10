using Ironbees.Autonomous;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests;

/// <summary>
/// <c>Stop()</c> while a task runs ends the session as stopped by the user. The oracle and session loops tested the token
/// in their conditions: a stop that landed while the executor was working (an executor that does not observe the token)
/// ended both loops quietly — the iteration was reported completed and the session returned still <c>Running</c>.
/// </summary>
public class StopDuringTaskTests
{
    [Fact]
    public async Task Stop_while_a_task_runs_ends_the_session_stopped_by_the_user()
    {
        AutonomousOrchestrator<EnforcementRequest, EnforcementResult>? orchestrator = null;
        var executor = new StoppingExecutor(() => orchestrator!.Stop());
        orchestrator = AutonomousOrchestrator.Create<EnforcementRequest, EnforcementResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new EnforcementRequest(id, prompt))
            .WithOracle(new NeverCompleteOracle())
            .WithAutoContinue()
            .WithMaxOracleIterations(3)
            .WithMaxIterations(5)
            .Build();
        var events = new List<AutonomousEvent>();
        orchestrator.OnEvent += events.Add;

        orchestrator.EnqueuePrompt("Guess the word");
        await orchestrator.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, executor.Calls);
        Assert.Equal(AutonomousState.StoppedByUser, orchestrator.State);
        Assert.Contains(events, e => e.Type == AutonomousEventType.Stopped);
        Assert.DoesNotContain(events, e => e.Type == AutonomousEventType.IterationCompleted);
    }

    private sealed class StoppingExecutor(Action stop) : ITaskExecutor<EnforcementRequest, EnforcementResult>
    {
        public int Calls { get; private set; }

        public Task<EnforcementResult> ExecuteAsync(
            EnforcementRequest request,
            Action<TaskOutput>? onOutput = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            stop();
            return Task.FromResult(new EnforcementResult(request.RequestId, $"attempt {Calls}"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
