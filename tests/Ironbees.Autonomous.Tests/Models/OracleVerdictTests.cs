using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests.Models;

public class OracleVerdictTests
{
    // --- Factory: GoalAchieved ---

    [Fact]
    public void GoalAchieved_ShouldSetCompleteAndNoMoreContinue()
    {
        var verdict = OracleVerdict.GoalAchieved("Task done");

        Assert.True(verdict.IsComplete);
        Assert.False(verdict.CanContinue);
        Assert.Equal("Task done", verdict.Analysis);
        Assert.Equal(1.0, verdict.Confidence);
    }

    [Fact]
    public void GoalAchieved_CustomConfidence_ShouldSet()
    {
        var verdict = OracleVerdict.GoalAchieved("done", 0.85);

        Assert.Equal(0.85, verdict.Confidence);
    }

    // --- Factory: ContinueToNextIteration ---

    [Fact]
    public void ContinueToNextIteration_ShouldSetNotCompleteAndContinue()
    {
        var verdict = OracleVerdict.ContinueToNextIteration("In progress");

        Assert.False(verdict.IsComplete);
        Assert.True(verdict.CanContinue);
        Assert.Equal("In progress", verdict.Analysis);
        Assert.Null(verdict.NextPromptSuggestion);
        Assert.Equal(0.5, verdict.Confidence);
    }

    // --- Factory: RetryWithRefinedPrompt ---

    [Fact]
    public void RetryWithRefinedPrompt_ShouldSetNextPrompt()
    {
        var verdict = OracleVerdict.RetryWithRefinedPrompt("Try again with X", "needs refinement");

        Assert.False(verdict.IsComplete);
        Assert.True(verdict.CanContinue);
        Assert.Equal("Try again with X", verdict.NextPromptSuggestion);
        Assert.Equal("needs refinement", verdict.Analysis);
        Assert.Equal(0.3, verdict.Confidence);
    }

    // --- Factory: Stop ---

    [Fact]
    public void Stop_ShouldSetNotCompleteAndNoContinue()
    {
        var verdict = OracleVerdict.Stop("Resource exhausted");

        Assert.False(verdict.IsComplete);
        Assert.False(verdict.CanContinue);
        Assert.Equal("Resource exhausted", verdict.Analysis);
        Assert.Equal(0, verdict.Confidence);
    }

    // --- Factory: Error ---

    [Fact]
    public void Error_Default_ShouldAllowContinue()
    {
        var verdict = OracleVerdict.Error("Something went wrong");

        Assert.False(verdict.IsComplete);
        Assert.True(verdict.CanContinue);
        Assert.Equal("Something went wrong", verdict.Analysis);
        Assert.Equal(0, verdict.Confidence);
    }

    [Fact]
    public void Error_DisallowContinue_ShouldStopExecution()
    {
        var verdict = OracleVerdict.Error("Fatal error", allowContinue: false);

        Assert.False(verdict.CanContinue);
    }

    // --- Factory: Progress ---

    [Fact]
    public void Progress_ShouldSetAnalysisAndConfidence()
    {
        var verdict = OracleVerdict.Progress("Making progress", 0.7);

        Assert.False(verdict.IsComplete);
        Assert.True(verdict.CanContinue);
        Assert.Equal("Making progress", verdict.Analysis);
        Assert.Equal(0.7, verdict.Confidence);
        Assert.Null(verdict.NextPromptSuggestion);
    }

    [Fact]
    public void Progress_ContinueToNextFalse_ShouldStopContinue()
    {
        var verdict = OracleVerdict.Progress("Done enough", 0.6, continueToNext: false);

        Assert.False(verdict.CanContinue);
    }

    // --- TokenUsage ---

    [Fact]
    public void TokenUsage_TotalTokens_ShouldSumInputAndOutput()
    {
        var usage = new TokenUsage { InputTokens = 100, OutputTokens = 50 };

        Assert.Equal(150, usage.TotalTokens);
    }

    [Fact]
    public void TokenUsage_Defaults_ShouldBeZero()
    {
        var usage = new TokenUsage();

        Assert.Equal(0, usage.InputTokens);
        Assert.Equal(0, usage.OutputTokens);
        Assert.Equal(0, usage.TotalTokens);
    }

    // --- OracleReflection ---

    [Fact]
    public void OracleReflection_ToLearning_ShouldMapFields()
    {
        var reflection = new OracleReflection
        {
            WhatWorkedWell = "Good approach",
            WhatCouldImprove = "Needs polish",
            LessonsLearned = "Key insight",
            SuggestedStrategy = "New strategy"
        };

        var learning = reflection.ToLearning(3);

        Assert.Equal(3, learning.IterationNumber);
        Assert.Equal(LearningType.Pattern, learning.Type);
        Assert.Equal("Key insight", learning.Summary);
        Assert.Contains("Good approach", learning.Details);
        Assert.Contains("Needs polish", learning.Details);
        Assert.Contains("New strategy", learning.Details);
        Assert.Equal(0.8, learning.Confidence);
    }

    [Fact]
    public void OracleReflection_ToLearning_NullLessons_ShouldFallbackToWorkedWell()
    {
        var reflection = new OracleReflection
        {
            WhatWorkedWell = "Good work"
        };

        var learning = reflection.ToLearning(1);

        Assert.Equal("Good work", learning.Summary);
    }

    [Fact]
    public void OracleReflection_ToLearning_AllNull_ShouldUseDefault()
    {
        var reflection = new OracleReflection();

        var learning = reflection.ToLearning(1);

        Assert.Equal("No specific learning captured", learning.Summary);
    }

    [Fact]
    public void OracleReflection_ToInsight_ShouldMapFields()
    {
        var reflection = new OracleReflection
        {
            WhatWorkedWell = "Good approach",
            WhatCouldImprove = "Room for improvement",
            SuggestedStrategy = "Do X"
        };

        var insight = reflection.ToInsight();

        Assert.Equal(ReflectionType.Critique, insight.Type);
        Assert.Equal("Room for improvement", insight.Summary);
        Assert.Contains("Good approach", insight.Analysis);
        Assert.Equal("Do X", insight.SuggestedAction);
        Assert.Equal(0.8, insight.Confidence);
    }

    [Fact]
    public void OracleReflection_ToInsight_NullImprove_ShouldFallbackToLessons()
    {
        var reflection = new OracleReflection
        {
            LessonsLearned = "Important lesson"
        };

        var insight = reflection.ToInsight();

        Assert.Equal("Important lesson", insight.Summary);
    }

    [Fact]
    public void OracleReflection_ToInsight_AllNull_ShouldUseDefault()
    {
        var reflection = new OracleReflection();

        var insight = reflection.ToInsight();

        Assert.Equal("Reflection captured", insight.Summary);
    }

    // --- OracleConfig defaults ---

    [Fact]
    public void OracleConfig_Defaults_ShouldBeCorrect()
    {
        var config = new OracleConfig();

        Assert.Equal("gpt-4o-mini", config.Model);
        Assert.Equal(1024, config.MaxTokens);
        Assert.Equal(0.3, config.Temperature);
        Assert.Equal(TimeSpan.FromSeconds(30), config.Timeout);
        Assert.True(config.EnableReflection);
        Assert.NotNull(config.SystemPrompt);
        Assert.NotNull(config.UserPromptTemplate);
        Assert.NotNull(config.ReflectionSystemPrompt);
        Assert.NotNull(config.ReflectionUserPromptTemplate);
    }

    [Fact]
    public void OracleConfig_DefaultPrompts_ShouldContainPlaceholders()
    {
        Assert.Contains("{original_prompt}", OracleConfig.DefaultUserPromptTemplate);
        Assert.Contains("{execution_output}", OracleConfig.DefaultUserPromptTemplate);
        Assert.Contains("{original_prompt}", OracleConfig.DefaultReflectionUserPromptTemplate);
        Assert.Contains("{context}", OracleConfig.DefaultReflectionUserPromptTemplate);
    }
}
