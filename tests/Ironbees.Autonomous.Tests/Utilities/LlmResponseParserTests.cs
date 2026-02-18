using Ironbees.Autonomous.Utilities;
using Xunit;

namespace Ironbees.Autonomous.Tests.Utilities;

public class LlmResponseParserTests
{
    // ExtractJson tests

    [Fact]
    public void ExtractJson_Null_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractJson(null!));
    }

    [Fact]
    public void ExtractJson_Empty_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractJson(""));
    }

    [Fact]
    public void ExtractJson_Whitespace_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractJson("   "));
    }

    [Fact]
    public void ExtractJson_MarkdownCodeBlock_ShouldExtract()
    {
        var content = "Here is the result:\n```json\n{\"key\": \"value\"}\n```\nDone.";

        var result = LlmResponseParser.ExtractJson(content);

        Assert.Equal("{\"key\": \"value\"}", result);
    }

    [Fact]
    public void ExtractJson_RawJsonObject_ShouldExtract()
    {
        var content = "The answer is {\"name\": \"test\"} and more text.";

        var result = LlmResponseParser.ExtractJson(content);

        Assert.Equal("{\"name\": \"test\"}", result);
    }

    [Fact]
    public void ExtractJson_RawJsonArray_ShouldExtract()
    {
        var content = "Results: [1, 2, 3] end.";

        var result = LlmResponseParser.ExtractJson(content);

        Assert.Equal("[1, 2, 3]", result);
    }

    [Fact]
    public void ExtractJson_NoJson_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractJson("This is plain text with no JSON."));
    }

    [Fact]
    public void ExtractJson_MarkdownPreferredOverRaw()
    {
        var content = "Text {\"raw\": 1}\n```json\n{\"markdown\": 2}\n```\nEnd.";

        var result = LlmResponseParser.ExtractJson(content);

        Assert.Equal("{\"markdown\": 2}", result);
    }

    // ParseJson tests

    [Fact]
    public void ParseJson_ValidJson_ShouldReturnDocument()
    {
        using var doc = LlmResponseParser.ParseJson("{\"key\": \"value\"}");

        Assert.NotNull(doc);
        Assert.Equal("value", doc!.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void ParseJson_InvalidJson_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ParseJson("not json at all"));
    }

    [Fact]
    public void ParseJson_TrailingCommas_ShouldParseLeniently()
    {
        using var doc = LlmResponseParser.ParseJson("{\"key\": \"value\",}");

        Assert.NotNull(doc);
    }

    // ExtractProperty tests

    [Fact]
    public void ExtractProperty_ExistingProperty_ShouldReturnValue()
    {
        Assert.Equal("test", LlmResponseParser.ExtractProperty("{\"name\": \"test\"}", "name"));
    }

    [Fact]
    public void ExtractProperty_MissingProperty_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractProperty("{\"name\": \"test\"}", "missing"));
    }

    // ExtractBoolProperty tests

    [Fact]
    public void ExtractBoolProperty_TrueValue_ShouldReturnTrue()
    {
        Assert.True(LlmResponseParser.ExtractBoolProperty("{\"success\": true}", "success"));
    }

    [Fact]
    public void ExtractBoolProperty_MissingProperty_ShouldReturnDefault()
    {
        Assert.False(LlmResponseParser.ExtractBoolProperty("{\"other\": true}", "success", defaultValue: false));
    }

    // ExtractDoubleProperty tests

    [Fact]
    public void ExtractDoubleProperty_ExistingValue_ShouldReturn()
    {
        var result = LlmResponseParser.ExtractDoubleProperty("{\"confidence\": 0.95}", "confidence");
        Assert.Equal(0.95, result, precision: 3);
    }

    [Fact]
    public void ExtractDoubleProperty_MissingProperty_ShouldReturnDefault()
    {
        Assert.Equal(0.5, LlmResponseParser.ExtractDoubleProperty("{\"other\": 1.0}", "confidence", defaultValue: 0.5));
    }

    // Deserialize tests

    [Fact]
    public void Deserialize_ValidJson_ShouldReturnTypedObject()
    {
        var result = LlmResponseParser.Deserialize<TestDto>("{\"name\": \"test\", \"count\": 42}");

        Assert.NotNull(result);
        Assert.Equal("test", result!.Name);
        Assert.Equal(42, result.Count);
    }

    [Fact]
    public void Deserialize_InvalidJson_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.Deserialize<TestDto>("not json"));
    }

    [Fact]
    public void Deserialize_CaseInsensitive_ShouldWork()
    {
        var result = LlmResponseParser.Deserialize<TestDto>("{\"Name\": \"test\", \"Count\": 10}");

        Assert.NotNull(result);
        Assert.Equal("test", result!.Name);
    }

    // ExtractQuestion tests

    [Fact]
    public void ExtractQuestion_WithQuestion_ShouldExtract()
    {
        Assert.Equal("What is the answer?", LlmResponseParser.ExtractQuestion("Some context.\nWhat is the answer?"));
    }

    [Fact]
    public void ExtractQuestion_NoQuestion_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractQuestion("This is a statement."));
    }

    [Fact]
    public void ExtractQuestion_NullInput_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractQuestion(null!));
    }

    [Fact]
    public void ExtractQuestion_EmptyInput_ShouldReturnNull()
    {
        Assert.Null(LlmResponseParser.ExtractQuestion(""));
    }

    // IsYesAnswer tests

    [Theory]
    [InlineData("yes")]
    [InlineData("Yes, I agree")]
    [InlineData("{\"answer\": \"yes\"}")]
    [InlineData("{\"answer\":\"yes\"}")]
    public void IsYesAnswer_YesPatterns_ShouldReturnTrue(string content)
    {
        Assert.True(LlmResponseParser.IsYesAnswer(content));
    }

    [Theory]
    [InlineData("no")]
    [InlineData("maybe")]
    [InlineData("I'm not sure")]
    public void IsYesAnswer_NonYesPatterns_ShouldReturnFalse(string content)
    {
        Assert.False(LlmResponseParser.IsYesAnswer(content));
    }

    // IsNoAnswer tests

    [Theory]
    [InlineData("no")]
    [InlineData("No, that's wrong")]
    [InlineData("{\"answer\": \"no\"}")]
    [InlineData("{\"answer\":\"no\"}")]
    public void IsNoAnswer_NoPatterns_ShouldReturnTrue(string content)
    {
        Assert.True(LlmResponseParser.IsNoAnswer(content));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("maybe")]
    public void IsNoAnswer_NonNoPatterns_ShouldReturnFalse(string content)
    {
        Assert.False(LlmResponseParser.IsNoAnswer(content));
    }

    // DefaultSerializerOptions tests

    [Fact]
    public void DefaultSerializerOptions_ShouldBeCaseInsensitive()
    {
        Assert.True(LlmResponseParser.DefaultSerializerOptions.PropertyNameCaseInsensitive);
    }

    private sealed class TestDto
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }
}
