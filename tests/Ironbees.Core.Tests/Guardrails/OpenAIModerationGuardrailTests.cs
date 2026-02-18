// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using System.ClientModel;
using Ironbees.Core.Guardrails;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenAI.Moderations;

namespace Ironbees.Core.Tests.Guardrails;

public class OpenAIModerationGuardrailTests
{
    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OpenAIModerationGuardrail(null!));
    }

    [Fact]
    public void Name_ReturnsConfiguredName()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        var guardrail = new OpenAIModerationGuardrail(mockClient, new OpenAIModerationGuardrailOptions
        {
            Name = "CustomModeration"
        });

        // Assert
        Assert.Equal("CustomModeration", guardrail.Name);
    }

    [Fact]
    public void Name_DefaultOptions_ReturnsDefaultName()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        var guardrail = new OpenAIModerationGuardrail(mockClient);

        // Assert
        Assert.Equal("OpenAIModeration", guardrail.Name);
    }

    [Fact]
    public async Task ValidateInputAsync_WhenValidateInputDisabled_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        var guardrail = new OpenAIModerationGuardrail(mockClient, new OpenAIModerationGuardrailOptions
        {
            ValidateInput = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Any content");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal("OpenAIModeration", result.GuardrailName);
    }

    [Fact]
    public async Task ValidateOutputAsync_WhenValidateOutputDisabled_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        var guardrail = new OpenAIModerationGuardrail(mockClient, new OpenAIModerationGuardrailOptions
        {
            ValidateOutput = false
        });

        // Act
        var result = await guardrail.ValidateOutputAsync("Any content");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_EmptyContent_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        var guardrail = new OpenAIModerationGuardrail(mockClient);

        // Act
        var result = await guardrail.ValidateInputAsync("");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_NullContent_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        var guardrail = new OpenAIModerationGuardrail(mockClient);

        // Act
        var result = await guardrail.ValidateInputAsync(null!);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_ServiceError_FailOpenFalse_ReturnsBlocked()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        mockClient
            .ClassifyTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ClientResultException("API error"));

        var guardrail = new OpenAIModerationGuardrail(mockClient, new OpenAIModerationGuardrailOptions
        {
            FailOpen = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Test content");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Contains("API error", result.Reason);
        Assert.Single(result.Violations);
        Assert.Equal("ServiceError", result.Violations[0].ViolationType);
    }

    [Fact]
    public async Task ValidateInputAsync_ServiceError_FailOpenTrue_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ModerationClient>();
        mockClient
            .ClassifyTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ClientResultException("API error"));

        var guardrail = new OpenAIModerationGuardrail(mockClient, new OpenAIModerationGuardrailOptions
        {
            FailOpen = true
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Test content");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Contains("API error", result.Reason);
        Assert.True((bool)result.Metadata["FailOpen"]);
    }

    [Fact]
    public void Options_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new OpenAIModerationGuardrailOptions();

        // Assert
        Assert.Equal("OpenAIModeration", options.Name);
        Assert.Equal(0.7f, options.ScoreThreshold);
        Assert.True(options.UseScoreThreshold);
        Assert.True(options.BlockOnFlagged);
        Assert.True(options.ValidateInput);
        Assert.True(options.ValidateOutput);
        Assert.False(options.FailOpen);
        Assert.Null(options.EnabledCategories);
        Assert.Null(options.DisabledCategories);
    }

    [Fact]
    public void Options_CategoryThresholds_CanBeSet()
    {
        // Arrange & Act
        var options = new OpenAIModerationGuardrailOptions
        {
            SexualThreshold = 0.5f,
            SexualMinorsThreshold = 0.1f,
            HateThreshold = 0.6f,
            HateThreateningThreshold = 0.4f,
            HarassmentThreshold = 0.7f,
            HarassmentThreateningThreshold = 0.5f,
            SelfHarmThreshold = 0.3f,
            SelfHarmIntentThreshold = 0.2f,
            SelfHarmInstructionsThreshold = 0.1f,
            ViolenceThreshold = 0.8f,
            ViolenceGraphicThreshold = 0.6f
        };

        // Assert
        Assert.Equal(0.5f, options.SexualThreshold);
        Assert.Equal(0.1f, options.SexualMinorsThreshold);
        Assert.Equal(0.6f, options.HateThreshold);
        Assert.Equal(0.4f, options.HateThreateningThreshold);
        Assert.Equal(0.7f, options.HarassmentThreshold);
        Assert.Equal(0.5f, options.HarassmentThreateningThreshold);
        Assert.Equal(0.3f, options.SelfHarmThreshold);
        Assert.Equal(0.2f, options.SelfHarmIntentThreshold);
        Assert.Equal(0.1f, options.SelfHarmInstructionsThreshold);
        Assert.Equal(0.8f, options.ViolenceThreshold);
        Assert.Equal(0.6f, options.ViolenceGraphicThreshold);
    }

    [Fact]
    public void Options_CategoryFiltering_CanBeConfigured()
    {
        // Arrange & Act - Enabled categories
        var options1 = new OpenAIModerationGuardrailOptions
        {
            EnabledCategories = new HashSet<string> { "Sexual", "Violence" }
        };

        // Assert
        Assert.Equal(2, options1.EnabledCategories!.Count);
        Assert.Contains("Sexual", options1.EnabledCategories);
        Assert.Contains("Violence", options1.EnabledCategories);

        // Arrange & Act - Disabled categories
        var options2 = new OpenAIModerationGuardrailOptions
        {
            DisabledCategories = new HashSet<string> { "Harassment" }
        };

        // Assert
        Assert.Single(options2.DisabledCategories!);
        Assert.Contains("Harassment", options2.DisabledCategories);
    }

    [Fact]
    public void Options_ScoreThreshold_CanBeCustomized()
    {
        // Arrange & Act
        var options = new OpenAIModerationGuardrailOptions
        {
            ScoreThreshold = 0.9f,
            UseScoreThreshold = false,
            BlockOnFlagged = false
        };

        // Assert
        Assert.Equal(0.9f, options.ScoreThreshold);
        Assert.False(options.UseScoreThreshold);
        Assert.False(options.BlockOnFlagged);
    }
}
