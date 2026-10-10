using Ironbees.Autonomous;
using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests;

/// <summary>
/// <c>Configure</c> changes the configuration the orchestrator runs with. Before, it took an <c>Action</c> over the immutable
/// <see cref="AutonomousConfig"/>, so nothing a caller wrote could reach the orchestrator.
/// </summary>
public class BuilderConfigureTests
{
    [Fact]
    public void Configure_ReplacesTheConfiguration_AndKeepsWhatEarlierCallsSet()
    {
        var orchestrator = AutonomousOrchestrator.Create<EnforcementRequest, EnforcementResult>()
            .WithExecutor(new PromptRecordingExecutor())
            .WithRequestFactory((id, prompt) => new EnforcementRequest(id, prompt))
            .WithMaxOracleIterations(3)
            .Configure(c => c with { MaxIterations = 7, CompletionMode = CompletionMode.UntilGoalAchieved })
            .Build();

        Assert.Equal(7, orchestrator.Status.MaxIterations);
        Assert.Equal(CompletionMode.UntilGoalAchieved, orchestrator.Status.CompletionMode);
        Assert.Equal(3, orchestrator.Status.MaxOracleIterations);
    }

    [Fact]
    public void Configure_ReturningNull_Throws()
    {
        var builder = AutonomousOrchestrator.Create<EnforcementRequest, EnforcementResult>();
        Assert.Throws<InvalidOperationException>(() => builder.Configure(_ => null!));
    }
}
