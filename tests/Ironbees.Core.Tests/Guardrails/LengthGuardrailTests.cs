// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;

namespace Ironbees.Core.Tests.Guardrails;

public class LengthGuardrailTests
{
    [Fact]
    public async Task ValidateInputAsync_UnderMaxLength_ReturnsAllowed()
    {
        // Arrange
        var guardrail = new LengthGuardrail(maxInputLength: 100);

        // Act
        var result = await guardrail.ValidateInputAsync("Short input");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_ExceedsMaxLength_ReturnsBlocked()
    {
        // Arrange
        var guardrail = new LengthGuardrail(maxInputLength: 10);
        var input = new string('x', 20);

        // Act
        var result = await guardrail.ValidateInputAsync(input);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.Violations);
        Assert.Equal("LengthExceeded", result.Violations[0].ViolationType);
    }

    [Fact]
    public async Task ValidateInputAsync_NoMaxLength_AllowsAnyLength()
    {
        // Arrange
        var guardrail = new LengthGuardrail();
        var input = new string('x', 100000);

        // Act
        var result = await guardrail.ValidateInputAsync(input);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_BelowMinLength_ReturnsBlocked()
    {
        // Arrange
        var guardrail = new LengthGuardrail(new LengthGuardrailOptions
        {
            MinInputLength = 10
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Short");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.Violations);
        Assert.Equal("LengthTooShort", result.Violations[0].ViolationType);
    }

    [Fact]
    public async Task ValidateOutputAsync_ExceedsMaxLength_ReturnsBlocked()
    {
        // Arrange
        var guardrail = new LengthGuardrail(maxOutputLength: 50);
        var output = new string('y', 100);

        // Act
        var result = await guardrail.ValidateOutputAsync(output);

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateOutputAsync_UnderMaxLength_ReturnsAllowed()
    {
        // Arrange
        var guardrail = new LengthGuardrail(maxOutputLength: 100);

        // Act
        var result = await guardrail.ValidateOutputAsync("Short output");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_NullInput_TreatsAsZeroLength()
    {
        // Arrange
        var guardrail = new LengthGuardrail(new LengthGuardrailOptions
        {
            MinInputLength = 1
        });

        // Act
        var result = await guardrail.ValidateInputAsync(null!);

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_EmptyInput_TreatsAsZeroLength()
    {
        // Arrange
        var guardrail = new LengthGuardrail(maxInputLength: 100);

        // Act
        var result = await guardrail.ValidateInputAsync("");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void Name_ReturnsConfiguredName()
    {
        // Arrange
        var guardrail = new LengthGuardrail(new LengthGuardrailOptions
        {
            Name = "CustomLengthGuardrail"
        });

        // Assert
        Assert.Equal("CustomLengthGuardrail", guardrail.Name);
    }

    [Fact]
    public async Task Violation_IncludesExcessLengthInContext()
    {
        // Arrange
        var guardrail = new LengthGuardrail(maxInputLength: 10);
        var input = new string('x', 25);

        // Act
        var result = await guardrail.ValidateInputAsync(input);

        // Assert
        Assert.False(result.IsAllowed);
        var violation = result.Violations[0];
        Assert.Equal(25, violation.Context["ActualLength"]);
        Assert.Equal(10, violation.Context["MaxLength"]);
        Assert.Equal(15, violation.Context["ExcessLength"]);
    }

    [Fact]
    public void InputOnly_CreatesInputOnlyOptions()
    {
        // Act
        var options = LengthGuardrailOptions.InputOnly(100);

        // Assert
        Assert.Equal(100, options.MaxInputLength);
        Assert.Null(options.MaxOutputLength);
    }

    [Fact]
    public void OutputOnly_CreatesOutputOnlyOptions()
    {
        // Act
        var options = LengthGuardrailOptions.OutputOnly(200);

        // Assert
        Assert.Null(options.MaxInputLength);
        Assert.Equal(200, options.MaxOutputLength);
    }

    [Fact]
    public void Both_CreatesBothOptions()
    {
        // Act
        var options = LengthGuardrailOptions.Both(100, 200);

        // Assert
        Assert.Equal(100, options.MaxInputLength);
        Assert.Equal(200, options.MaxOutputLength);
    }
}
