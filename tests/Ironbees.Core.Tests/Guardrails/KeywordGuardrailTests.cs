// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;

namespace Ironbees.Core.Tests.Guardrails;

public class KeywordGuardrailTests
{
    [Fact]
    public async Task ValidateInputAsync_NoBlockedKeywords_ReturnsAllowed()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["blocked"]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("This is safe content");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_ContainsBlockedKeyword_ReturnsBlocked()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["badword"]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("This contains badword in the text");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.Violations);
        Assert.Equal("BlockedKeyword", result.Violations[0].ViolationType);
    }

    [Fact]
    public async Task ValidateInputAsync_CaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["badword"],
            CaseSensitive = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("This contains BADWORD");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_CaseSensitive_RequiresExactCase()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["BadWord"],
            CaseSensitive = true
        });

        // Act
        var resultLower = await guardrail.ValidateInputAsync("This contains badword");
        var resultExact = await guardrail.ValidateInputAsync("This contains BadWord");

        // Assert
        Assert.True(resultLower.IsAllowed);
        Assert.False(resultExact.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_WholeWordOnly_DoesNotMatchPartial()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad"],
            WholeWordOnly = true
        });

        // Act
        var resultPartial = await guardrail.ValidateInputAsync("This is badminton");
        var resultWhole = await guardrail.ValidateInputAsync("This is bad");

        // Assert
        Assert.True(resultPartial.IsAllowed);
        Assert.False(resultWhole.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_NotWholeWordOnly_MatchesPartial()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad"],
            WholeWordOnly = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("This is badminton");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_MultipleKeywords_MatchesAny()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad", "evil", "wrong"]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Something evil this way comes");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_FindAllViolations_ReturnsAllMatches()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad", "evil"],
            FindAllViolations = true
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Something bad and evil");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(2, result.Violations.Count);
    }

    [Fact]
    public async Task ValidateInputAsync_NotFindAllViolations_StopsAtFirst()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad", "evil"],
            FindAllViolations = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Something bad and evil");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.Violations);
    }

    [Fact]
    public async Task ValidateInputAsync_EmptyInput_ReturnsAllowed()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad"]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateOutputAsync_ContainsBlockedKeyword_ReturnsBlocked()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["secret"]
        });

        // Act
        var result = await guardrail.ValidateOutputAsync("The secret is revealed");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_DisabledForInput_SkipsValidation()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad"],
            ValidateInput = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("This is bad");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateOutputAsync_DisabledForOutput_SkipsValidation()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["bad"],
            ValidateOutput = false
        });

        // Act
        var result = await guardrail.ValidateOutputAsync("This is bad");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_IncludeMatchedContent_ShowsMatchedKeyword()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["secret"],
            IncludeMatchedContent = true
        });

        // Act
        var result = await guardrail.ValidateInputAsync("The secret word");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal("secret", result.Violations[0].MatchedContent);
    }

    [Fact]
    public async Task ValidateInputAsync_NotIncludeMatchedContent_HidesMatchedKeyword()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            BlockedKeywords = ["secret"],
            IncludeMatchedContent = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("The secret word");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Null(result.Violations[0].MatchedContent);
    }

    [Fact]
    public void Name_ReturnsConfiguredName()
    {
        // Arrange
        var guardrail = new KeywordGuardrail(new KeywordGuardrailOptions
        {
            Name = "ProfanityFilter"
        });

        // Assert
        Assert.Equal("ProfanityFilter", guardrail.Name);
    }
}
