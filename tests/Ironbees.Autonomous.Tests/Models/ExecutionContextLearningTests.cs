using Xunit;
using Ironbees.Autonomous.Models;

namespace Ironbees.Autonomous.Tests.Models;

public class ExecutionContextLearningTests
{
    private static IterationLearning Learning(int n) => new() { IterationNumber = n, Summary = $"lesson {n}" };

    [Fact]
    public void WithLearning_AndALimit_KeepsOnlyTheMostRecent()
    {
        // AutonomousConfig.MaxContextLearnings ("maximum number of learnings to keep in context") was never applied:
        // learnings grew without bound while the sibling MaxContextOutputs was enforced.
        var context = AutonomousExecutionContext.Initial("s", "goal");
        for (var i = 1; i <= 5; i++)
        {
            context = context.WithLearning(Learning(i), maxLearnings: 3);
        }

        Assert.Equal([3, 4, 5], context.Learnings.Select(l => l.IterationNumber));
    }

    [Fact]
    public void WithLearning_WithoutALimit_KeepsEverything()
    {
        var context = AutonomousExecutionContext.Initial("s", "goal");
        for (var i = 1; i <= 5; i++)
        {
            context = context.WithLearning(Learning(i));
        }

        Assert.Equal(5, context.Learnings.Count);
    }
}
