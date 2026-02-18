using Ironbees.Core;

namespace Ironbees.Core.Tests;

public class KeywordNormalizerTests
{
    private readonly KeywordNormalizer _normalizer = new();

    // Synonym mapping tests

    [Theory]
    [InlineData("coding", "code")]
    [InlineData("programming", "code")]
    [InlineData("script", "code")]
    [InlineData("scripting", "code")]
    public void Normalize_ProgrammingSynonyms_ShouldMapToCanonical(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    [Theory]
    [InlineData("development", "develop")]
    [InlineData("developer", "develop")]
    [InlineData("dev", "develop")]
    public void Normalize_DevelopmentSynonyms_ShouldMapToCanonical(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    [Theory]
    [InlineData("debugging", "debug")]
    [InlineData("troubleshoot", "debug")]
    [InlineData("troubleshooting", "debug")]
    public void Normalize_DebugSynonyms_ShouldMapToCanonical(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    [Theory]
    [InlineData("c#", "csharp")]
    [InlineData("cs", "csharp")]
    [InlineData(".net", "dotnet")]
    [InlineData("asp.net", "aspnet")]
    public void Normalize_DotNetSynonyms_ShouldMapToCanonical(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    [Theory]
    [InlineData("endpoint", "api")]
    [InlineData("webservice", "api")]
    [InlineData("db", "database")]
    [InlineData("auth", "auth")]
    [InlineData("login", "auth")]
    [InlineData("config", "config")]
    [InlineData("settings", "config")]
    public void Normalize_ConceptSynonyms_ShouldMapToCanonical(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    // Case-insensitivity tests

    [Theory]
    [InlineData("CODING", "code")]
    [InlineData("Coding", "code")]
    [InlineData("CoDiNg", "code")]
    public void Normalize_CaseInsensitive_ShouldNormalize(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    // Stemming tests

    [Theory]
    [InlineData("coded", "code")]
    [InlineData("codes", "code")]
    [InlineData("tests", "test")]
    [InlineData("tested", "test")]
    [InlineData("fixed", "fix")]
    [InlineData("fixes", "fix")]
    public void Normalize_StemmingForms_ShouldReduceToBase(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    // Pass-through for unknown words

    [Theory]
    [InlineData("kubernetes")]
    [InlineData("docker")]
    [InlineData("python")]
    public void Normalize_UnknownWords_ShouldReturnLowercased(string input)
    {
        Assert.Equal(input.ToLowerInvariant(), _normalizer.Normalize(input));
    }

    // NormalizeWords batch tests

    [Fact]
    public void NormalizeWords_SameCanonical_ShouldDeduplicateToOne()
    {
        var words = new[] { "coding", "programming", "scripting" };

        var result = _normalizer.NormalizeWords(words);

        // All should map to "code"
        Assert.Single(result);
        Assert.Contains("code", result);
    }

    [Fact]
    public void NormalizeWords_MixedWords_ShouldDeduplicateAfterNormalization()
    {
        var words = new[] { "debug", "debugging", "test", "testing", "fix" };

        var result = _normalizer.NormalizeWords(words);

        Assert.Equal(3, result.Count);
        Assert.Contains("debug", result);
        Assert.Contains("test", result);
        Assert.Contains("fix", result);
    }

    [Fact]
    public void NormalizeWords_EmptyCollection_ShouldReturnEmptySet()
    {
        var result = _normalizer.NormalizeWords([]);

        Assert.Empty(result);
    }

    // Synonym → Stemming chain

    [Fact]
    public void Normalize_SynonymChain_QaShouldMapToTest()
    {
        // "qa" → synonym → "test"
        Assert.Equal("test", _normalizer.Normalize("qa"));
    }
}
