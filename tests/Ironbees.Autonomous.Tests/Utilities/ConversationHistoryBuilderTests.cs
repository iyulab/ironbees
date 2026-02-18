using Ironbees.Autonomous.Utilities;
using Xunit;

namespace Ironbees.Autonomous.Tests.Utilities;

public class ConversationHistoryBuilderTests
{
    // Build tests

    [Fact]
    public void Build_NoTurns_ShouldReturnEmptyMessage()
    {
        var builder = new ConversationHistoryBuilder();

        var result = builder.Build();

        Assert.Equal("No previous conversation.", result);
    }

    [Fact]
    public void Build_CustomEmptyMessage_ShouldUseCustom()
    {
        var options = new HistoryFormatOptions { EmptyHistoryMessage = "Nothing here." };
        var builder = new ConversationHistoryBuilder(options);

        var result = builder.Build();

        Assert.Equal("Nothing here.", result);
    }

    [Fact]
    public void Build_SingleTurn_ShouldFormatCorrectly()
    {
        var options = new HistoryFormatOptions { IncludeDeductions = false };
        var builder = new ConversationHistoryBuilder(options);
        builder.AddTurn(1, "Is it alive?", "Yes");

        var result = builder.Build();

        Assert.Contains("Q1: Is it alive?", result);
        Assert.Contains("A: Yes", result);
    }

    [Fact]
    public void Build_WithHeader_ShouldIncludeHeader()
    {
        var options = new HistoryFormatOptions
        {
            HeaderText = "MY HEADER",
            IncludeDeductions = false
        };
        var builder = new ConversationHistoryBuilder(options);
        builder.AddTurn(1, "question", "answer");

        var result = builder.Build();

        Assert.StartsWith("MY HEADER", result);
    }

    [Fact]
    public void Build_EmptyHeader_ShouldOmitHeader()
    {
        var options = new HistoryFormatOptions
        {
            HeaderText = "",
            IncludeDeductions = false
        };
        var builder = new ConversationHistoryBuilder(options);
        builder.AddTurn(1, "question", "answer");

        var result = builder.Build();

        Assert.StartsWith("Q1:", result);
    }

    [Fact]
    public void Build_WithMetadata_ShouldAppendInParens()
    {
        var options = new HistoryFormatOptions { IncludeDeductions = false };
        var builder = new ConversationHistoryBuilder(options);
        builder.AddTurn(1, "question", "answer", "confidence: high");

        var result = builder.Build();

        Assert.Contains("A: answer (confidence: high)", result);
    }

    [Fact]
    public void Build_MultipleTurns_ShouldIncludeAll()
    {
        var options = new HistoryFormatOptions { IncludeDeductions = false };
        var builder = new ConversationHistoryBuilder(options);
        builder.AddTurn(1, "Q one", "A one");
        builder.AddTurn(2, "Q two", "A two");

        var result = builder.Build();

        Assert.Contains("Q1: Q one", result);
        Assert.Contains("Q2: Q two", result);
    }

    [Fact]
    public void Build_WithDeductions_ShouldIncludeDeductionsSection()
    {
        var options = new HistoryFormatOptions { IncludeDeductions = true };
        var builder = new ConversationHistoryBuilder(options);
        builder.AddTurn(1, "Is it alive?", "Yes");
        builder.AddTurn(2, "Is it a plant?", "No");

        var result = builder.Build();

        Assert.Contains("=== DEDUCTIONS ===", result);
        Assert.Contains("Confirmed YES: Is it alive", result);
        Assert.Contains("Confirmed NO: Is it a plant", result);
    }

    // AddTurn fluent chaining

    [Fact]
    public void AddTurn_ShouldReturnSameBuilder()
    {
        var builder = new ConversationHistoryBuilder();

        var result = builder.AddTurn(1, "q", "a");

        Assert.Same(builder, result);
    }

    // AddTurns tests

    [Fact]
    public void AddTurns_ShouldAddFromCollection()
    {
        var options = new HistoryFormatOptions { IncludeDeductions = false };
        var builder = new ConversationHistoryBuilder(options);
        var items = new[]
        {
            new { Num = 1, Q = "First", A = "One" },
            new { Num = 2, Q = "Second", A = "Two" }
        };

        builder.AddTurns(items, x => x.Num, x => x.Q, x => x.A);
        var result = builder.Build();

        Assert.Contains("Q1: First", result);
        Assert.Contains("Q2: Second", result);
    }

    [Fact]
    public void AddTurns_WithMetadataSelector_ShouldInclude()
    {
        var options = new HistoryFormatOptions { IncludeDeductions = false };
        var builder = new ConversationHistoryBuilder(options);
        var items = new[] { new { Num = 1, Q = "q", A = "a", M = "meta" } };

        builder.AddTurns(items, x => x.Num, x => x.Q, x => x.A, x => x.M);
        var result = builder.Build();

        Assert.Contains("(meta)", result);
    }

    // BuildDeductions tests

    [Fact]
    public void BuildDeductions_NoYesNo_ShouldReturnEmpty()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "question", "maybe");

        var result = builder.BuildDeductions();

        Assert.Equal("", result);
    }

    [Fact]
    public void BuildDeductions_YesResponses_ShouldListConfirmed()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "Is it alive?", "Yes, it is");
        builder.AddTurn(2, "Is it big?", "Yes");

        var result = builder.BuildDeductions();

        Assert.Contains("Confirmed YES: Is it alive, Is it big", result);
    }

    [Fact]
    public void BuildDeductions_NoResponses_ShouldListConfirmedNo()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "Is it a car?", "No, it's not");

        var result = builder.BuildDeductions();

        Assert.Contains("Confirmed NO: Is it a car", result);
    }

    [Fact]
    public void BuildDeductions_JsonYes_ShouldDetect()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "Is it warm?", "{\"answer\":\"yes\"}");

        var result = builder.BuildDeductions();

        Assert.Contains("Confirmed YES: Is it warm", result);
    }

    [Fact]
    public void BuildDeductions_JsonNo_ShouldDetect()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "Is it cold?", "{\"answer\": \"no\"}");

        var result = builder.BuildDeductions();

        Assert.Contains("Confirmed NO: Is it cold", result);
    }

    // GetTurns tests

    [Fact]
    public void GetTurns_NullFilter_ShouldReturnAll()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "q1", "a1");
        builder.AddTurn(2, "q2", "a2");

        var turns = builder.GetTurns(null);

        Assert.Equal(2, turns.Count);
    }

    [Fact]
    public void GetTurns_WithFilter_ShouldReturnFiltered()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "q1", "Yes");
        builder.AddTurn(2, "q2", "No");

        var turns = builder.GetTurns(t => t.Output.StartsWith("Yes", StringComparison.Ordinal));

        Assert.Single(turns);
        Assert.Equal(1, turns[0].Number);
    }

    // Clear tests

    [Fact]
    public void Clear_ShouldRemoveAllTurns()
    {
        var builder = new ConversationHistoryBuilder();
        builder.AddTurn(1, "q", "a");

        builder.Clear();

        Assert.Equal("No previous conversation.", builder.Build());
    }

    // Custom labels

    [Fact]
    public void Build_CustomLabels_ShouldUseCustom()
    {
        var options = new HistoryFormatOptions
        {
            InputLabel = "Question {n}",
            OutputLabel = "Answer {n}",
            IncludeDeductions = false
        };
        var builder = new ConversationHistoryBuilder(options);
        builder.AddTurn(3, "input", "output");

        var result = builder.Build();

        Assert.Contains("Question 3: input", result);
        Assert.Contains("Answer 3: output", result);
    }

    // QnA preset

    [Fact]
    public void QnAPreset_ShouldHaveCorrectDefaults()
    {
        var qna = HistoryFormatOptions.QnA;

        Assert.Contains("QUESTION HISTORY", qna.HeaderText);
        Assert.True(qna.IncludeDeductions);
        Assert.Contains("DEDUCTIONS", qna.DeductionsHeader);
    }
}
