// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;

namespace Ironbees.Core.Tests.Guardrails;

public class GuardrailViolationExceptionTests
{
    [Fact]
    public void Constructor_SetsResultProperty()
    {
        // Arrange
        var result = GuardrailResult.Blocked("TestGuardrail", "Blocked",
            GuardrailViolation.Create("Type", "Desc"));

        // Act
        var exception = new GuardrailViolationException(result);

        // Assert
        Assert.Same(result, exception.Result);
    }

    [Fact]
    public void Constructor_FormatsMessageWithGuardrailName()
    {
        // Arrange
        var result = GuardrailResult.Blocked("TestGuardrail", "Content was blocked");

        // Act
        var exception = new GuardrailViolationException(result);

        // Assert
        Assert.Contains("TestGuardrail", exception.Message);
        Assert.Contains("Content was blocked", exception.Message);
    }

    [Fact]
    public void Constructor_FormatsMessageWithViolationCount()
    {
        // Arrange
        var violations = new[]
        {
            GuardrailViolation.Create("V1", "First"),
            GuardrailViolation.Create("V2", "Second")
        };
        var result = GuardrailResult.Blocked("Test", "Blocked", violations);

        // Act
        var exception = new GuardrailViolationException(result);

        // Assert
        Assert.Contains("2 violations", exception.Message);
    }

    [Fact]
    public void Constructor_HandlesSingleViolation()
    {
        // Arrange
        var result = GuardrailResult.Blocked("Test", "Blocked",
            GuardrailViolation.Create("V1", "First"));

        // Act
        var exception = new GuardrailViolationException(result);

        // Assert
        Assert.Contains("1 violation", exception.Message);
        Assert.DoesNotContain("violations", exception.Message);
    }

    [Fact]
    public void GuardrailName_ReturnsResultGuardrailName()
    {
        // Arrange
        var result = GuardrailResult.Blocked("MyGuardrail", "Blocked");

        // Act
        var exception = new GuardrailViolationException(result);

        // Assert
        Assert.Equal("MyGuardrail", exception.GuardrailName);
    }

    [Fact]
    public void Violations_ReturnsResultViolations()
    {
        // Arrange
        var violations = new[] { GuardrailViolation.Create("V1", "First") };
        var result = GuardrailResult.Blocked("Test", "Blocked", violations);

        // Act
        var exception = new GuardrailViolationException(result);

        // Assert
        Assert.Equal(violations.Length, exception.Violations.Count);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        // Arrange
        var result = GuardrailResult.Blocked("Test", "Blocked");
        var innerException = new InvalidOperationException("Inner");

        // Act
        var exception = new GuardrailViolationException(result, innerException);

        // Assert
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_ThrowsOnNullResult()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GuardrailViolationException(null!));
    }
}
