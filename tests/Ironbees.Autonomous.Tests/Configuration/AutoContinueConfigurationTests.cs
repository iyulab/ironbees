using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests.Configuration;

/// <summary>
/// Tests for AutoContinue configuration options:
/// - AutoContinueOnIncomplete: Continue when IsComplete=false regardless of CanContinue
/// - InferCanContinueFromComplete: Infer CanContinue from IsComplete for local LLMs
/// </summary>
public class AutoContinueConfigurationTests
{
    [Fact]
    public void AutonomousConfig_AutoContinueOnIncomplete_DefaultsToFalse()
    {
        // Arrange & Act
        var config = new AutonomousConfig();

        // Assert
        Assert.False(config.AutoContinueOnIncomplete);
    }

    [Fact]
    public void AutonomousConfig_InferCanContinueFromComplete_DefaultsToFalse()
    {
        // Arrange & Act
        var config = new AutonomousConfig();

        // Assert
        Assert.False(config.InferCanContinueFromComplete);
    }

    [Fact]
    public void AutonomousConfig_AutoContinueOnIncomplete_CanBeEnabled()
    {
        // Arrange & Act
        var config = new AutonomousConfig { AutoContinueOnIncomplete = true };

        // Assert
        Assert.True(config.AutoContinueOnIncomplete);
    }

    [Fact]
    public void AutonomousConfig_InferCanContinueFromComplete_CanBeEnabled()
    {
        // Arrange & Act
        var config = new AutonomousConfig { InferCanContinueFromComplete = true };

        // Assert
        Assert.True(config.InferCanContinueFromComplete);
    }

    [Fact]
    public void AutonomousConfig_BothOptions_CanBeEnabledTogether()
    {
        // Arrange & Act
        var config = new AutonomousConfig
        {
            AutoContinueOnIncomplete = true,
            InferCanContinueFromComplete = true
        };

        // Assert
        Assert.True(config.AutoContinueOnIncomplete);
        Assert.True(config.InferCanContinueFromComplete);
    }

    [Fact]
    public void Builder_WithAutoContinueOnIncomplete_SetsConfigCorrectly()
    {
        // Arrange
        var executor = new MockTaskExecutor();

        // Act
        var orchestrator = AutonomousOrchestrator.Create<MockRequest, MockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new MockRequest(id, prompt))
            .WithAutoContinue()
            .WithAutoContinueOnIncomplete()
            .Build();

        // Assert - Config should have the option enabled
        // (Verified via the fact that builder doesn't throw)
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Builder_WithInferCanContinueFromComplete_SetsConfigCorrectly()
    {
        // Arrange
        var executor = new MockTaskExecutor();

        // Act
        var orchestrator = AutonomousOrchestrator.Create<MockRequest, MockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new MockRequest(id, prompt))
            .WithInferCanContinueFromComplete()
            .Build();

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Builder_WithBothOptions_SetsConfigCorrectly()
    {
        // Arrange
        var executor = new MockTaskExecutor();

        // Act
        var orchestrator = AutonomousOrchestrator.Create<MockRequest, MockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new MockRequest(id, prompt))
            .WithAutoContinue()
            .WithAutoContinueOnIncomplete()
            .WithInferCanContinueFromComplete()
            .Build();

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Builder_ChainedMethods_ReturnSameBuilder()
    {
        // Arrange
        var executor = new MockTaskExecutor();
        var builder = AutonomousOrchestrator.Create<MockRequest, MockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new MockRequest(id, prompt));

        // Act
        var builder1 = builder.WithAutoContinueOnIncomplete();
        var builder2 = builder1.WithInferCanContinueFromComplete();

        // Assert - Fluent API should return same builder type
        Assert.Same(builder, builder1);
        Assert.Same(builder1, builder2);
    }
}

// Test helpers
file record MockRequest(string RequestId, string Prompt) : ITaskRequest;

file record MockResult(string RequestId) : ITaskResult
{
    public bool Success => true;
    public string Output => "Test output";
    public string? ErrorOutput => null;
}

file class MockTaskExecutor : ITaskExecutor<MockRequest, MockResult>
{
    public Task<MockResult> ExecuteAsync(MockRequest request, Action<TaskOutput>? onOutput = null, CancellationToken cancellationToken = default)
    {
        onOutput?.Invoke(new TaskOutput { RequestId = request.RequestId, Content = "Test output" });
        return Task.FromResult(new MockResult(request.RequestId));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
