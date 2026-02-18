using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests.Models;

public class EnhancedOracleVerdictTests
{
    // --- Default property values ---

    [Fact]
    public void Default_CompletedGoals_ShouldBeEmpty()
    {
        var verdict = new EnhancedOracleVerdict { Analysis = "test" };
        Assert.Empty(verdict.CompletedGoals);
    }

    [Fact]
    public void Default_RemainingGoals_ShouldBeEmpty()
    {
        var verdict = new EnhancedOracleVerdict { Analysis = "test" };
        Assert.Empty(verdict.RemainingGoals);
    }

    [Fact]
    public void Default_ConfidenceHistory_ShouldBeEmpty()
    {
        var verdict = new EnhancedOracleVerdict { Analysis = "test" };
        Assert.Empty(verdict.ConfidenceHistory);
    }

    [Fact]
    public void Default_ContextInsights_ShouldBeEmpty()
    {
        var verdict = new EnhancedOracleVerdict { Analysis = "test" };
        Assert.Empty(verdict.ContextInsights);
    }

    [Fact]
    public void Default_Metadata_ShouldBeEmpty()
    {
        var verdict = new EnhancedOracleVerdict { Analysis = "test" };
        Assert.Empty(verdict.Metadata);
    }

    // --- FromBase ---

    [Fact]
    public void FromBase_ShouldCopyBaseProperties()
    {
        var baseVerdict = new OracleVerdict
        {
            IsComplete = true,
            CanContinue = false,
            Analysis = "Goal achieved",
            NextPromptSuggestion = "next",
            Confidence = 0.95,
            TokenUsage = new TokenUsage { InputTokens = 100, OutputTokens = 50 },
            Reflection = new OracleReflection { WhatWorkedWell = "everything" }
        };

        var enhanced = EnhancedOracleVerdict.FromBase(baseVerdict);

        Assert.True(enhanced.IsComplete);
        Assert.False(enhanced.CanContinue);
        Assert.Equal("Goal achieved", enhanced.Analysis);
        Assert.Equal("next", enhanced.NextPromptSuggestion);
        Assert.Equal(0.95, enhanced.Confidence);
        Assert.NotNull(enhanced.TokenUsage);
        Assert.Equal(150, enhanced.TokenUsage.TotalTokens);
        Assert.NotNull(enhanced.Reflection);
    }

    [Fact]
    public void FromBase_WithGoals_ShouldSetGoals()
    {
        var baseVerdict = OracleVerdict.GoalAchieved("done");

        var enhanced = EnhancedOracleVerdict.FromBase(
            baseVerdict,
            completedGoals: ["goal1", "goal2"],
            remainingGoals: ["goal3"]);

        Assert.Equal(2, enhanced.CompletedGoals.Count);
        Assert.Equal("goal1", enhanced.CompletedGoals[0]);
        Assert.Single(enhanced.RemainingGoals);
    }

    [Fact]
    public void FromBase_WithConfidenceHistory_ShouldSetHistory()
    {
        var baseVerdict = OracleVerdict.GoalAchieved("done");
        var history = new Dictionary<int, double> { [1] = 0.3, [2] = 0.6, [3] = 0.9 };

        var enhanced = EnhancedOracleVerdict.FromBase(baseVerdict, confidenceHistory: history);

        Assert.Equal(3, enhanced.ConfidenceHistory.Count);
        Assert.Equal(0.3, enhanced.ConfidenceHistory[1]);
        Assert.Equal(0.9, enhanced.ConfidenceHistory[3]);
    }

    [Fact]
    public void FromBase_WithContextInsightsAndMetadata_ShouldSet()
    {
        var baseVerdict = OracleVerdict.GoalAchieved("done");

        var enhanced = EnhancedOracleVerdict.FromBase(
            baseVerdict,
            contextInsights: ["insight1", "insight2"],
            metadata: new Dictionary<string, object> { ["key"] = "value" });

        Assert.Equal(2, enhanced.ContextInsights.Count);
        Assert.Single(enhanced.Metadata);
        Assert.Equal("value", enhanced.Metadata["key"]);
    }

    [Fact]
    public void FromBase_NullOptionals_ShouldDefaultToEmpty()
    {
        var baseVerdict = OracleVerdict.GoalAchieved("done");

        var enhanced = EnhancedOracleVerdict.FromBase(baseVerdict);

        Assert.Empty(enhanced.CompletedGoals);
        Assert.Empty(enhanced.RemainingGoals);
        Assert.Empty(enhanced.ConfidenceHistory);
        Assert.Empty(enhanced.ContextInsights);
        Assert.Empty(enhanced.Metadata);
    }

    // --- GoalAchieved ---

    [Fact]
    public void GoalAchieved_ShouldSetCompleteAndStop()
    {
        var verdict = EnhancedOracleVerdict.GoalAchieved("All goals done", 0.99,
            ["goal1", "goal2"]);

        Assert.True(verdict.IsComplete);
        Assert.False(verdict.CanContinue);
        Assert.Equal("All goals done", verdict.Analysis);
        Assert.Equal(0.99, verdict.Confidence);
        Assert.Equal(2, verdict.CompletedGoals.Count);
    }

    [Fact]
    public void GoalAchieved_DefaultConfidence_ShouldBe1()
    {
        var verdict = EnhancedOracleVerdict.GoalAchieved("done");

        Assert.Equal(1.0, verdict.Confidence);
    }

    [Fact]
    public void GoalAchieved_NullGoals_ShouldDefaultToEmpty()
    {
        var verdict = EnhancedOracleVerdict.GoalAchieved("done");

        Assert.Empty(verdict.CompletedGoals);
    }

    // --- ContinueWithProgress ---

    [Fact]
    public void ContinueWithProgress_ShouldSetIncompleteAndContinue()
    {
        var verdict = EnhancedOracleVerdict.ContinueWithProgress(
            "Making progress", 0.6,
            completedGoals: ["step1"],
            remainingGoals: ["step2", "step3"]);

        Assert.False(verdict.IsComplete);
        Assert.True(verdict.CanContinue);
        Assert.Equal("Making progress", verdict.Analysis);
        Assert.Equal(0.6, verdict.Confidence);
        Assert.Single(verdict.CompletedGoals);
        Assert.Equal(2, verdict.RemainingGoals.Count);
    }

    [Fact]
    public void ContinueWithProgress_NullGoals_ShouldDefaultToEmpty()
    {
        var verdict = EnhancedOracleVerdict.ContinueWithProgress("progress", 0.5);

        Assert.Empty(verdict.CompletedGoals);
        Assert.Empty(verdict.RemainingGoals);
    }

    // --- Inheritance from OracleVerdict ---

    [Fact]
    public void EnhancedVerdict_IsOracleVerdict()
    {
        var enhanced = EnhancedOracleVerdict.GoalAchieved("done");

        Assert.IsAssignableFrom<OracleVerdict>(enhanced);
    }
}
