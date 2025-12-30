// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;

namespace Ironbees.Core.Tests.Guardrails;

public class RegexGuardrailTests
{
    [Fact]
    public async Task ValidateInputAsync_NoMatch_ReturnsAllowed()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN")]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("This is safe content");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_MatchesPattern_ReturnsBlocked()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN")]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("My SSN is 123-45-6789");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.Violations);
    }

    [Fact]
    public async Task ValidateInputAsync_EmailPattern_DetectsEmails()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(
                @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
                "Email",
                "Email address detected")]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Contact me at test@example.com");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_MultiplePatterns_MatchesAny()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns =
            [
                PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN"),
                PatternDefinition.Create(@"\d{16}", "CreditCard")
            ]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Card number: 1234567890123456");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_FindAllViolations_ReturnsAllMatches()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN")],
            FindAllViolations = true
        });

        // Act
        var result = await guardrail.ValidateInputAsync("SSN 123-45-6789 and 987-65-4321");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(2, result.Violations.Count);
    }

    [Fact]
    public async Task ValidateInputAsync_NotFindAllViolations_StopsAtFirst()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN")],
            FindAllViolations = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("SSN 123-45-6789 and 987-65-4321");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.Violations);
    }

    [Fact]
    public async Task ValidateInputAsync_IncludeMatchedContent_ShowsMatch()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN")],
            IncludeMatchedContent = true
        });

        // Act
        var result = await guardrail.ValidateInputAsync("SSN is 123-45-6789");

        // Assert
        Assert.Equal("123-45-6789", result.Violations[0].MatchedContent);
    }

    [Fact]
    public async Task ValidateInputAsync_TruncatesLongMatch()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"[a-z]{100,}", "LongString")],
            IncludeMatchedContent = true,
            MaxMatchedContentLength = 10
        });
        var longString = new string('a', 200);

        // Act
        var result = await guardrail.ValidateInputAsync(longString);

        // Assert
        Assert.Equal("aaaaaaaaaa...", result.Violations[0].MatchedContent);
    }

    [Fact]
    public async Task ValidateInputAsync_DisabledForInput_SkipsValidation()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d+", "Numbers")],
            ValidateInput = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Contains 12345");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateOutputAsync_MatchesPattern_ReturnsBlocked()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN")]
        });

        // Act
        var result = await guardrail.ValidateOutputAsync("User SSN: 123-45-6789");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateOutputAsync_DisabledForOutput_SkipsValidation()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d+", "Numbers")],
            ValidateOutput = false
        });

        // Act
        var result = await guardrail.ValidateOutputAsync("Contains 12345");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_EmptyInput_ReturnsAllowed()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d+", "Numbers")]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_CaseInsensitive_MatchesAnyCase()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"secret", "Secret")],
            RegexOptions = System.Text.RegularExpressions.RegexOptions.IgnoreCase
        });

        // Act
        var result = await guardrail.ValidateInputAsync("This is SECRET information");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_ViolationIncludesPosition()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            BlockedPatterns = [PatternDefinition.Create(@"\d{3}-\d{2}-\d{4}", "SSN")]
        });

        // Act
        var result = await guardrail.ValidateInputAsync("SSN: 123-45-6789");

        // Assert
        var violation = result.Violations[0];
        Assert.Equal(5, violation.Position);
        Assert.Equal(11, violation.Length);
    }

    [Fact]
    public void Name_ReturnsConfiguredName()
    {
        // Arrange
        var guardrail = new RegexGuardrail(new RegexGuardrailOptions
        {
            Name = "PIIDetector"
        });

        // Assert
        Assert.Equal("PIIDetector", guardrail.Name);
    }

    [Fact]
    public void PatternDefinition_Create_SetsProperties()
    {
        // Act
        var pattern = PatternDefinition.Create(@"\d+", "Numbers", "Detects numeric sequences");

        // Assert
        Assert.Equal(@"\d+", pattern.Pattern);
        Assert.Equal("Numbers", pattern.Name);
        Assert.Equal("Detects numeric sequences", pattern.Description);
        Assert.Equal("Numbers", pattern.ViolationType);
    }
}
