// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;

namespace Ironbees.Core.Tests.Guardrails;

public class GuardrailResultTests
{
    [Fact]
    public void Allowed_CreatesAllowedResult()
    {
        // Act
        var result = GuardrailResult.Allowed("TestGuardrail");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal("TestGuardrail", result.GuardrailName);
        Assert.Equal("Content passed validation", result.Reason);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Allowed_WithoutName_CreatesAllowedResult()
    {
        // Act
        var result = GuardrailResult.Allowed();

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Null(result.GuardrailName);
    }

    [Fact]
    public void Blocked_CreatesBlockedResult()
    {
        // Arrange
        var violation = GuardrailViolation.Create("TestViolation", "Test description");

        // Act
        var result = GuardrailResult.Blocked("TestGuardrail", "Content blocked", violation);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal("TestGuardrail", result.GuardrailName);
        Assert.Equal("Content blocked", result.Reason);
        Assert.Single(result.Violations);
        Assert.Equal("TestViolation", result.Violations[0].ViolationType);
    }

    [Fact]
    public void Blocked_WithMultipleViolations_IncludesAllViolations()
    {
        // Arrange
        var violations = new[]
        {
            GuardrailViolation.Create("Violation1", "First violation"),
            GuardrailViolation.Create("Violation2", "Second violation")
        };

        // Act
        var result = GuardrailResult.Blocked("TestGuardrail", "Multiple violations", violations);

        // Assert
        Assert.Equal(2, result.Violations.Count);
    }

    [Fact]
    public void Blocked_WithEnumerable_IncludesAllViolations()
    {
        // Arrange
        var violations = new List<GuardrailViolation>
        {
            GuardrailViolation.Create("Violation1", "First"),
            GuardrailViolation.Create("Violation2", "Second"),
            GuardrailViolation.Create("Violation3", "Third")
        };

        // Act
        var result = GuardrailResult.Blocked("Test", "Blocked", violations);

        // Assert
        Assert.Equal(3, result.Violations.Count);
    }

    [Fact]
    public void Result_HasTimestamp()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = GuardrailResult.Allowed();
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.InRange(result.Timestamp, before, after);
    }

    [Fact]
    public void Result_DefaultMetadataIsEmpty()
    {
        // Act
        var result = GuardrailResult.Allowed();

        // Assert
        Assert.Empty(result.Metadata);
    }
}
