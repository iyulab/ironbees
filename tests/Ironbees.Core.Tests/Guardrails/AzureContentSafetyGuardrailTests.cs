// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.AI.ContentSafety;
using Ironbees.Core.Guardrails;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Ironbees.Core.Tests.Guardrails;

public class AzureContentSafetyGuardrailTests
{
    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AzureContentSafetyGuardrail(null!));
    }

    [Fact]
    public void Name_ReturnsConfiguredName()
    {
        // Arrange
        var mockClient = Substitute.For<ContentSafetyClient>();
        var guardrail = new AzureContentSafetyGuardrail(mockClient, new AzureContentSafetyGuardrailOptions
        {
            Name = "CustomName"
        });

        // Assert
        Assert.Equal("CustomName", guardrail.Name);
    }

    [Fact]
    public void Name_DefaultOptions_ReturnsDefaultName()
    {
        // Arrange
        var mockClient = Substitute.For<ContentSafetyClient>();
        var guardrail = new AzureContentSafetyGuardrail(mockClient);

        // Assert
        Assert.Equal("AzureContentSafety", guardrail.Name);
    }

    [Fact]
    public async Task ValidateInputAsync_WhenValidateInputDisabled_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ContentSafetyClient>();
        var guardrail = new AzureContentSafetyGuardrail(mockClient, new AzureContentSafetyGuardrailOptions
        {
            ValidateInput = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Any content");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal("AzureContentSafety", result.GuardrailName);
    }

    [Fact]
    public async Task ValidateOutputAsync_WhenValidateOutputDisabled_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ContentSafetyClient>();
        var guardrail = new AzureContentSafetyGuardrail(mockClient, new AzureContentSafetyGuardrailOptions
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
        var mockClient = Substitute.For<ContentSafetyClient>();
        var guardrail = new AzureContentSafetyGuardrail(mockClient);

        // Act
        var result = await guardrail.ValidateInputAsync("");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_NullContent_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ContentSafetyClient>();
        var guardrail = new AzureContentSafetyGuardrail(mockClient);

        // Act
        var result = await guardrail.ValidateInputAsync(null!);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_ServiceError_FailOpenFalse_ReturnsBlocked()
    {
        // Arrange
        var mockClient = Substitute.For<ContentSafetyClient>();
        mockClient
            .AnalyzeTextAsync(Arg.Any<AnalyzeTextOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException("Service unavailable"));

        var guardrail = new AzureContentSafetyGuardrail(mockClient, new AzureContentSafetyGuardrailOptions
        {
            FailOpen = false
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Test content");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Contains("Service unavailable", result.Reason);
        Assert.Single(result.Violations);
        Assert.Equal("ServiceError", result.Violations[0].ViolationType);
    }

    [Fact]
    public async Task ValidateInputAsync_ServiceError_FailOpenTrue_ReturnsAllowed()
    {
        // Arrange
        var mockClient = Substitute.For<ContentSafetyClient>();
        mockClient
            .AnalyzeTextAsync(Arg.Any<AnalyzeTextOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException("Service unavailable"));

        var guardrail = new AzureContentSafetyGuardrail(mockClient, new AzureContentSafetyGuardrailOptions
        {
            FailOpen = true
        });

        // Act
        var result = await guardrail.ValidateInputAsync("Test content");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Contains("Service unavailable", result.Reason);
        Assert.True((bool)result.Metadata["FailOpen"]);
    }

    [Fact]
    public void Options_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new AzureContentSafetyGuardrailOptions();

        // Assert
        Assert.Equal("AzureContentSafety", options.Name);
        Assert.Equal(2, options.MaxAllowedSeverity);
        Assert.Null(options.HateSeverityThreshold);
        Assert.Null(options.SelfHarmSeverityThreshold);
        Assert.Null(options.SexualSeverityThreshold);
        Assert.Null(options.ViolenceSeverityThreshold);
        Assert.True(options.ValidateInput);
        Assert.True(options.ValidateOutput);
        Assert.True(options.IncludeMatchedContent);
        Assert.False(options.FailOpen);
        Assert.Empty(options.BlocklistNames);
        Assert.Null(options.HaltOnBlocklistHit);
    }

    [Fact]
    public void Options_CategoryThresholds_CanBeSet()
    {
        // Arrange & Act
        var options = new AzureContentSafetyGuardrailOptions
        {
            HateSeverityThreshold = 4,
            SelfHarmSeverityThreshold = 3,
            SexualSeverityThreshold = 2,
            ViolenceSeverityThreshold = 5
        };

        // Assert
        Assert.Equal(4, options.HateSeverityThreshold);
        Assert.Equal(3, options.SelfHarmSeverityThreshold);
        Assert.Equal(2, options.SexualSeverityThreshold);
        Assert.Equal(5, options.ViolenceSeverityThreshold);
    }

    [Fact]
    public void Options_BlocklistNames_CanBeConfigured()
    {
        // Arrange & Act
        var options = new AzureContentSafetyGuardrailOptions
        {
            BlocklistNames = ["blocklist1", "blocklist2"],
            HaltOnBlocklistHit = true
        };

        // Assert
        Assert.Equal(2, options.BlocklistNames.Count);
        Assert.Contains("blocklist1", options.BlocklistNames);
        Assert.Contains("blocklist2", options.BlocklistNames);
        Assert.True(options.HaltOnBlocklistHit);
    }
}
