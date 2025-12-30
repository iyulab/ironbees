// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;

namespace Ironbees.Core.Tests.Guardrails;

public class GuardrailViolationTests
{
    [Fact]
    public void Create_SetsTypeAndDescription()
    {
        // Act
        var violation = GuardrailViolation.Create("TestType", "Test description");

        // Assert
        Assert.Equal("TestType", violation.ViolationType);
        Assert.Equal("Test description", violation.Description);
        Assert.Equal(ViolationSeverity.Medium, violation.Severity);
    }

    [Fact]
    public void CreateWithPosition_SetsAllProperties()
    {
        // Act
        var violation = GuardrailViolation.CreateWithPosition(
            "PositionViolation",
            "Found at position",
            10,
            5,
            "match");

        // Assert
        Assert.Equal("PositionViolation", violation.ViolationType);
        Assert.Equal("Found at position", violation.Description);
        Assert.Equal(10, violation.Position);
        Assert.Equal(5, violation.Length);
        Assert.Equal("match", violation.MatchedContent);
    }

    [Fact]
    public void CreateWithPosition_WithoutMatchedContent_SetsNullMatch()
    {
        // Act
        var violation = GuardrailViolation.CreateWithPosition(
            "Type",
            "Description",
            0,
            10);

        // Assert
        Assert.Null(violation.MatchedContent);
    }

    [Fact]
    public void Violation_DefaultContextIsEmpty()
    {
        // Act
        var violation = GuardrailViolation.Create("Type", "Desc");

        // Assert
        Assert.Empty(violation.Context);
    }

    [Theory]
    [InlineData(ViolationSeverity.Low)]
    [InlineData(ViolationSeverity.Medium)]
    [InlineData(ViolationSeverity.High)]
    [InlineData(ViolationSeverity.Critical)]
    public void Violation_CanSetSeverity(ViolationSeverity severity)
    {
        // Act
        var violation = new GuardrailViolation
        {
            ViolationType = "Test",
            Description = "Test",
            Severity = severity
        };

        // Assert
        Assert.Equal(severity, violation.Severity);
    }
}
