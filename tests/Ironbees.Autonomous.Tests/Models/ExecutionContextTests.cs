using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests.Models;

public class ExecutionContextTests
{
    // --- Initial ---

    [Fact]
    public void Initial_ShouldSetSessionIdAndGoal()
    {
        var ctx = AutonomousExecutionContext.Initial("session-1", "Build a feature");

        Assert.Equal("session-1", ctx.SessionId);
        Assert.Equal("Build a feature", ctx.OriginalGoal);
        Assert.Equal(0, ctx.CurrentIteration);
        Assert.Equal(0, ctx.CurrentOracleIteration);
    }

    [Fact]
    public void Initial_CollectionsShouldBeEmpty()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal");

        Assert.Empty(ctx.Learnings);
        Assert.Empty(ctx.ErrorResolutions);
        Assert.Empty(ctx.Metadata);
        Assert.Empty(ctx.PreviousOutputs);
        Assert.Empty(ctx.HumanFeedbackHistory);
        Assert.Empty(ctx.Reflections);
    }

    // --- WithNextIteration ---

    [Fact]
    public void WithNextIteration_ShouldUpdateIterationNumbers()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithNextIteration(3, 2);

        Assert.Equal(3, ctx.CurrentIteration);
        Assert.Equal(2, ctx.CurrentOracleIteration);
        Assert.Equal("s1", ctx.SessionId); // preserved
    }

    // --- WithLearning ---

    [Fact]
    public void WithLearning_ShouldAccumulate()
    {
        var learning1 = new IterationLearning { Summary = "First", IterationNumber = 1 };
        var learning2 = new IterationLearning { Summary = "Second", IterationNumber = 2 };

        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithLearning(learning1)
            .WithLearning(learning2);

        Assert.Equal(2, ctx.Learnings.Count);
        Assert.Equal("First", ctx.Learnings[0].Summary);
        Assert.Equal("Second", ctx.Learnings[1].Summary);
    }

    // --- WithErrorResolution ---

    [Fact]
    public void WithErrorResolution_ShouldAccumulate()
    {
        var resolution = new ErrorResolution
        {
            ErrorSummary = "Timeout",
            ResolutionApplied = "Increased timeout",
            Category = ErrorCategory.Timeout,
            WasSuccessful = true
        };

        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithErrorResolution(resolution);

        Assert.Single(ctx.ErrorResolutions);
        Assert.Equal("Timeout", ctx.ErrorResolutions[0].ErrorSummary);
    }

    // --- WithMetadata ---

    [Fact]
    public void WithMetadata_ShouldAddKeyValue()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithMetadata("key1", "value1")
            .WithMetadata("key2", 42);

        Assert.Equal(2, ctx.Metadata.Count);
        Assert.Equal("value1", ctx.Metadata["key1"]);
        Assert.Equal(42, ctx.Metadata["key2"]);
    }

    [Fact]
    public void WithMetadata_SameKey_ShouldOverwrite()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithMetadata("key", "old")
            .WithMetadata("key", "new");

        Assert.Equal("new", ctx.Metadata["key"]);
    }

    // --- WithPreviousOutput ---

    [Fact]
    public void WithPreviousOutput_ShouldAccumulate()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithPreviousOutput("output1")
            .WithPreviousOutput("output2");

        Assert.Equal(2, ctx.PreviousOutputs.Count);
    }

    [Fact]
    public void WithPreviousOutput_ShouldKeepOnlyLast5()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal");
        for (int i = 1; i <= 7; i++)
        {
            ctx = ctx.WithPreviousOutput($"output-{i}");
        }

        Assert.Equal(5, ctx.PreviousOutputs.Count);
        Assert.Equal("output-3", ctx.PreviousOutputs[0]); // oldest kept
        Assert.Equal("output-7", ctx.PreviousOutputs[4]); // newest
    }

    // --- WithHumanFeedback ---

    [Fact]
    public void WithHumanFeedback_ShouldAccumulate()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithHumanFeedback("Good job")
            .WithHumanFeedback("Try harder");

        Assert.Equal(2, ctx.HumanFeedbackHistory.Count);
    }

    // --- WithReflection ---

    [Fact]
    public void WithReflection_ShouldAccumulate()
    {
        var insight = new ReflectionInsight
        {
            Summary = "Important insight",
            Type = ReflectionType.Critique
        };

        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithReflection(insight);

        Assert.Single(ctx.Reflections);
        Assert.Equal("Important insight", ctx.Reflections[0].Summary);
    }

    // --- BuildContextSummary ---

    [Fact]
    public void BuildContextSummary_Empty_ShouldReturnNoPriorContext()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal");

        Assert.Equal("No prior context.", ctx.BuildContextSummary());
    }

    [Fact]
    public void BuildContextSummary_WithLearnings_ShouldIncludeLearnings()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithLearning(new IterationLearning
            {
                Summary = "Pattern found",
                Type = LearningType.Pattern,
                IterationNumber = 1
            });

        var summary = ctx.BuildContextSummary();

        Assert.Contains("Previous Learnings (1)", summary);
        Assert.Contains("[Pattern] Pattern found", summary);
    }

    [Fact]
    public void BuildContextSummary_WithErrors_ShouldIncludeResolutions()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithErrorResolution(new ErrorResolution
            {
                ErrorSummary = "Timeout occurred",
                ResolutionApplied = "Increased timeout",
                IterationNumber = 1
            });

        var summary = ctx.BuildContextSummary();

        Assert.Contains("Error Resolutions (1)", summary);
        Assert.Contains("Timeout occurred", summary);
        Assert.Contains("Increased timeout", summary);
    }

    [Fact]
    public void BuildContextSummary_WithFeedback_ShouldIncludeFeedback()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithHumanFeedback("Please improve accuracy");

        var summary = ctx.BuildContextSummary();

        Assert.Contains("Human Feedback (1)", summary);
        Assert.Contains("Please improve accuracy", summary);
    }

    [Fact]
    public void BuildContextSummary_WithReflections_ShouldIncludeReflections()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal")
            .WithReflection(new ReflectionInsight
            {
                Summary = "Need better approach",
                Type = ReflectionType.Critique
            });

        var summary = ctx.BuildContextSummary();

        Assert.Contains("Reflections (1)", summary);
        Assert.Contains("Need better approach", summary);
    }

    [Fact]
    public void BuildContextSummary_LimitsToRecentItems()
    {
        var ctx = AutonomousExecutionContext.Initial("s1", "goal");
        for (int i = 1; i <= 5; i++)
        {
            ctx = ctx.WithLearning(new IterationLearning
            {
                Summary = $"Learning {i}",
                Type = LearningType.Pattern,
                IterationNumber = i
            });
        }

        var summary = ctx.BuildContextSummary();

        // Should show last 3 learnings
        Assert.Contains("Previous Learnings (5)", summary);
        Assert.Contains("Learning 3", summary);
        Assert.Contains("Learning 4", summary);
        Assert.Contains("Learning 5", summary);
        Assert.DoesNotContain("Learning 1", summary);
    }

    // --- Immutability ---

    [Fact]
    public void WithMethods_ShouldNotMutateOriginal()
    {
        var original = AutonomousExecutionContext.Initial("s1", "goal");

        var modified = original
            .WithLearning(new IterationLearning { Summary = "test" })
            .WithMetadata("key", "val")
            .WithPreviousOutput("output")
            .WithHumanFeedback("feedback");

        Assert.Empty(original.Learnings);
        Assert.Empty(original.Metadata);
        Assert.Empty(original.PreviousOutputs);
        Assert.Empty(original.HumanFeedbackHistory);
        Assert.NotEmpty(modified.Learnings);
    }
}
