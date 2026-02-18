// Copyright (c) Ironbees. All rights reserved.
// Licensed under the MIT License.

using Ironbees.Core.Guardrails;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Ironbees.Core.Tests.Guardrails;

public class GuardrailPipelineTests
{
    [Fact]
    public async Task ValidateInputAsync_NoGuardrails_ReturnsEmpty()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();

        // Act
        var result = await pipeline.ValidateInputAsync("Test input");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(result.Results);
        Assert.Equal(0, result.GuardrailsExecuted);
    }

    [Fact]
    public async Task ValidateInputAsync_AllPass_ReturnsAllowed()
    {
        // Arrange
        var guardrail1 = CreateMockGuardrail("Guard1", true);
        var guardrail2 = CreateMockGuardrail("Guard2", true);
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail1, guardrail2]);

        // Act
        var result = await pipeline.ValidateInputAsync("Test");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal(2, result.GuardrailsExecuted);
    }

    [Fact]
    public async Task ValidateInputAsync_OneFails_ReturnsBlocked()
    {
        // Arrange
        var guardrail1 = CreateMockGuardrail("Guard1", true);
        var guardrail2 = CreateMockGuardrail("Guard2", false, "Blocked by Guard2");
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail1, guardrail2]);

        // Act
        var result = await pipeline.ValidateInputAsync("Test");

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateInputAsync_FailFast_StopsOnFirstViolation()
    {
        // Arrange
        var guardrail1 = CreateMockGuardrail("Guard1", false, "First failure");
        var guardrail2 = CreateMockGuardrail("Guard2", false, "Second failure");
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail1, guardrail2],
            options: new GuardrailPipelineOptions { FailFast = true });

        // Act
        var result = await pipeline.ValidateInputAsync("Test");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(1, result.GuardrailsExecuted);
        await guardrail2.DidNotReceive().ValidateInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateInputAsync_NoFailFast_ExecutesAll()
    {
        // Arrange
        var guardrail1 = CreateMockGuardrail("Guard1", false, "First failure");
        var guardrail2 = CreateMockGuardrail("Guard2", false, "Second failure");
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail1, guardrail2],
            options: new GuardrailPipelineOptions { FailFast = false });

        // Act
        var result = await pipeline.ValidateInputAsync("Test");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(2, result.GuardrailsExecuted);
    }

    [Fact]
    public async Task ValidateInputAsync_ThrowOnViolation_ThrowsException()
    {
        // Arrange
        var guardrail = CreateMockGuardrail("Guard1", false, "Blocked");
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail],
            options: new GuardrailPipelineOptions { ThrowOnViolation = true });

        // Act & Assert
        await Assert.ThrowsAsync<GuardrailViolationException>(
            () => pipeline.ValidateInputAsync("Test"));
    }

    [Fact]
    public async Task ValidateOutputAsync_UsesOutputGuardrails()
    {
        // Arrange
        var inputGuardrail = CreateMockGuardrail("Input", true);
        var outputGuardrail = CreateMockGuardrail("Output", false, "Output blocked");
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [inputGuardrail],
            outputGuardrails: [outputGuardrail]);

        // Act
        var inputResult = await pipeline.ValidateInputAsync("Test");
        var outputResult = await pipeline.ValidateOutputAsync("Test");

        // Assert
        Assert.True(inputResult.IsAllowed);
        Assert.False(outputResult.IsAllowed);
    }

    [Fact]
    public void InputGuardrailCount_ReturnsCorrectCount()
    {
        // Arrange
        var guardrail1 = CreateMockGuardrail("G1", true);
        var guardrail2 = CreateMockGuardrail("G2", true);
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail1, guardrail2]);

        // Assert
        Assert.Equal(2, pipeline.InputGuardrailCount);
    }

    [Fact]
    public void OutputGuardrailCount_ReturnsCorrectCount()
    {
        // Arrange
        var guardrail1 = CreateMockGuardrail("G1", true);
        var pipeline = new GuardrailPipeline(
            outputGuardrails: [guardrail1]);

        // Assert
        Assert.Equal(1, pipeline.OutputGuardrailCount);
    }

    [Fact]
    public async Task ValidateInputAsync_CollectsAllViolations()
    {
        // Arrange
        var guardrail1 = CreateMockGuardrailWithViolation("G1", "V1");
        var guardrail2 = CreateMockGuardrailWithViolation("G2", "V2");
        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail1, guardrail2],
            options: new GuardrailPipelineOptions { FailFast = false });

        // Act
        var result = await pipeline.ValidateInputAsync("Test");

        // Assert
        Assert.Equal(2, result.AllViolations.Count);
    }

    [Fact]
    public async Task ValidateInputAsync_SupportsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var guardrail = CreateMockGuardrail("G1", true);
        var pipeline = new GuardrailPipeline(inputGuardrails: [guardrail]);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => pipeline.ValidateInputAsync("Test", cts.Token));
    }

    [Fact]
    public async Task ValidateInputAsync_GuardrailError_HandledGracefully()
    {
        // Arrange
        var guardrail = Substitute.For<IContentGuardrail>();
        guardrail.Name.Returns("ErrorGuard");
        guardrail.ValidateInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Guardrail crashed"));

        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail],
            options: new GuardrailPipelineOptions { ThrowOnGuardrailError = false });

        // Act
        var result = await pipeline.ValidateInputAsync("Test");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Single(result.AllViolations);
        Assert.Equal("GuardrailError", result.AllViolations[0].ViolationType);
    }

    [Fact]
    public async Task ValidateInputAsync_GuardrailError_ThrowsWhenConfigured()
    {
        // Arrange
        var guardrail = Substitute.For<IContentGuardrail>();
        guardrail.Name.Returns("ErrorGuard");
        guardrail.ValidateInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Guardrail crashed"));

        var pipeline = new GuardrailPipeline(
            inputGuardrails: [guardrail],
            options: new GuardrailPipelineOptions { ThrowOnGuardrailError = true });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.ValidateInputAsync("Test"));
    }

    private static IContentGuardrail CreateMockGuardrail(string name, bool allowed, string? reason = null)
    {
        var mock = Substitute.For<IContentGuardrail>();
        mock.Name.Returns(name);

        var result = allowed
            ? GuardrailResult.Allowed(name)
            : GuardrailResult.Blocked(name, reason ?? "Blocked");

        mock.ValidateInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);
        mock.ValidateOutputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);

        return mock;
    }

    private static IContentGuardrail CreateMockGuardrailWithViolation(string name, string violationType)
    {
        var mock = Substitute.For<IContentGuardrail>();
        mock.Name.Returns(name);

        var result = GuardrailResult.Blocked(name, "Blocked",
            GuardrailViolation.Create(violationType, "Violation"));

        mock.ValidateInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);
        mock.ValidateOutputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);

        return mock;
    }
}
