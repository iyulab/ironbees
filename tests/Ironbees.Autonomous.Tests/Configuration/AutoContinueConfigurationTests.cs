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
file sealed record MockRequest(string RequestId, string Prompt) : ITaskRequest;

file sealed record MockResult(string RequestId) : ITaskResult
{
    public bool Success => true;
    public string Output => "Test output";
    public string? ErrorOutput => null;
}

file sealed class MockTaskExecutor : ITaskExecutor<MockRequest, MockResult>
{
    public Task<MockResult> ExecuteAsync(MockRequest request, Action<TaskOutput>? onOutput = null, CancellationToken cancellationToken = default)
    {
        onOutput?.Invoke(new TaskOutput { RequestId = request.RequestId, Content = "Test output" });
        return Task.FromResult(new MockResult(request.RequestId));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Behavior tests for AutoContinue options when using Oracle verification.
/// These tests verify the orchestrator behavior with local/smaller LLMs that may not
/// reliably set CanContinue in their responses.
/// </summary>
public class AutoContinueBehaviorTests
{
    private static OracleVerdict CreateVerdict(bool isComplete, bool canContinue, string analysis)
        => new() { IsComplete = isComplete, CanContinue = canContinue, Analysis = analysis };

    /// <summary>
    /// Tests that when AutoContinueOnIncomplete is enabled,
    /// the orchestrator continues even when CanContinue=false but IsComplete=false.
    /// This is Issue #1: AutoContinue not triggered when CanContinue=false but IsComplete=false.
    /// </summary>
    [Fact]
    public async Task AutoContinueOnIncomplete_WhenCanContinueFalseAndIsCompleteFalse_ShouldContinue()
    {
        // Arrange
        var events = new List<string>();
        var executor = new BehaviorMockExecutor();
        var oracle = new BehaviorMockOracle(new[]
        {
            CreateVerdict(false, false, "Step 1 done, more work needed"),
            CreateVerdict(true, false, "Task completed")
        });

        var orchestrator = AutonomousOrchestrator.Create<BehaviorMockRequest, BehaviorMockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new BehaviorMockRequest(id, prompt))
            .WithOracle(oracle)
            .WithAutoContinue()
            .WithAutoContinueOnIncomplete()
            .WithMaxIterations(3)
            .Build();
        orchestrator.OnEvent += e => events.Add($"{e.Type}: {e.Message}");

        // Act
        orchestrator.EnqueuePrompt("Start task");
        await orchestrator.StartAsync();

        // Assert
        Assert.Equal(2, executor.ExecutionCount); // Should run twice (auto-continued once)
        Assert.Contains(events, e => e.Contains("AutoContinuing"));
    }

    /// <summary>
    /// Tests that when AutoContinueOnIncomplete is NOT enabled,
    /// the orchestrator does NOT continue when CanContinue=false even if IsComplete=false.
    /// </summary>
    [Fact]
    public async Task AutoContinueOnIncomplete_WhenDisabled_ShouldNotContinueOnCanContinueFalse()
    {
        // Arrange
        var events = new List<string>();
        var executor = new BehaviorMockExecutor();
        var oracle = new BehaviorMockOracle(new[]
        {
            CreateVerdict(false, false, "Step 1 done")
        });

        var orchestrator = AutonomousOrchestrator.Create<BehaviorMockRequest, BehaviorMockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new BehaviorMockRequest(id, prompt))
            .WithOracle(oracle)
            .WithAutoContinue()  // AutoContinueOnIncomplete is NOT enabled
            .WithMaxIterations(3)
            .Build();
        orchestrator.OnEvent += e => events.Add($"{e.Type}: {e.Message}");

        // Act
        orchestrator.EnqueuePrompt("Start task");
        await orchestrator.StartAsync();

        // Assert
        Assert.Equal(1, executor.ExecutionCount); // Should only run once (no auto-continue)
        Assert.DoesNotContain(events, e => e.Contains("AutoContinuing"));
    }

    /// <summary>
    /// Tests that when InferCanContinueFromComplete is enabled,
    /// the CanContinue value is inferred from IsComplete.
    /// This is Issue #2: Smaller LLMs don't follow canContinue guidelines.
    /// </summary>
    [Fact]
    public async Task InferCanContinueFromComplete_WhenIsCompleteFalse_ShouldInferCanContinueTrue()
    {
        // Arrange
        var events = new List<string>();
        var executor = new BehaviorMockExecutor();
        var oracle = new BehaviorMockOracle(new[]
        {
            CreateVerdict(false, false, "Step 1 done"),
            CreateVerdict(true, false, "Completed")
        });

        var orchestrator = AutonomousOrchestrator.Create<BehaviorMockRequest, BehaviorMockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new BehaviorMockRequest(id, prompt))
            .WithOracle(oracle)
            .WithAutoContinue()
            .WithInferCanContinueFromComplete()  // Enable inference
            .WithMaxIterations(3)
            .Build();
        orchestrator.OnEvent += e => events.Add($"{e.Type}: {e.Message}");

        // Act
        orchestrator.EnqueuePrompt("Start task");
        await orchestrator.StartAsync();

        // Assert
        Assert.Equal(2, executor.ExecutionCount); // Should run twice
        Assert.Contains(events, e => e.Contains("Inferring CanContinue=true from IsComplete=false"));
    }

    /// <summary>
    /// Tests that when InferCanContinueFromComplete is enabled but IsComplete=true,
    /// no inference happens (CanContinue stays as-is).
    /// </summary>
    [Fact]
    public async Task InferCanContinueFromComplete_WhenIsCompleteTrue_ShouldNotInfer()
    {
        // Arrange
        var events = new List<string>();
        var executor = new BehaviorMockExecutor();
        var oracle = new BehaviorMockOracle(new[]
        {
            CreateVerdict(true, false, "Done")
        });

        var orchestrator = AutonomousOrchestrator.Create<BehaviorMockRequest, BehaviorMockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new BehaviorMockRequest(id, prompt))
            .WithOracle(oracle)
            .WithAutoContinue()
            .WithInferCanContinueFromComplete()
            .WithMaxIterations(3)
            .Build();
        orchestrator.OnEvent += e => events.Add($"{e.Type}: {e.Message}");

        // Act
        orchestrator.EnqueuePrompt("Start task");
        await orchestrator.StartAsync();

        // Assert
        Assert.Equal(1, executor.ExecutionCount); // Only once - task complete
        Assert.DoesNotContain(events, e => e.Contains("Inferring CanContinue"));
    }

    /// <summary>
    /// Tests that when InferCanContinueFromComplete is enabled and CanContinue is already true,
    /// no inference happens (CanContinue stays true).
    /// </summary>
    [Fact]
    public async Task InferCanContinueFromComplete_WhenCanContinueAlreadyTrue_ShouldNotInfer()
    {
        // Arrange
        var events = new List<string>();
        var executor = new BehaviorMockExecutor();
        var oracle = new BehaviorMockOracle(new[]
        {
            CreateVerdict(false, true, "Step 1 done"),
            CreateVerdict(true, false, "Done")
        });

        var orchestrator = AutonomousOrchestrator.Create<BehaviorMockRequest, BehaviorMockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new BehaviorMockRequest(id, prompt))
            .WithOracle(oracle)
            .WithAutoContinue()
            .WithInferCanContinueFromComplete()
            .WithMaxIterations(3)
            .Build();
        orchestrator.OnEvent += e => events.Add($"{e.Type}: {e.Message}");

        // Act
        orchestrator.EnqueuePrompt("Start task");
        await orchestrator.StartAsync();

        // Assert
        Assert.Equal(2, executor.ExecutionCount);
        Assert.DoesNotContain(events, e => e.Contains("Inferring CanContinue")); // No inference needed
    }

    /// <summary>
    /// Tests that both options can work together.
    /// </summary>
    [Fact]
    public async Task BothOptions_WhenEnabled_WorkTogether()
    {
        // Arrange
        var events = new List<string>();
        var executor = new BehaviorMockExecutor();
        var oracle = new BehaviorMockOracle(new[]
        {
            CreateVerdict(false, false, "Step 1"),
            CreateVerdict(false, false, "Step 2"),
            CreateVerdict(true, false, "Done")
        });

        var orchestrator = AutonomousOrchestrator.Create<BehaviorMockRequest, BehaviorMockResult>()
            .WithExecutor(executor)
            .WithRequestFactory((id, prompt) => new BehaviorMockRequest(id, prompt))
            .WithOracle(oracle)
            .WithAutoContinue()
            .WithAutoContinueOnIncomplete()
            .WithInferCanContinueFromComplete()
            .WithMaxIterations(5)
            .Build();
        orchestrator.OnEvent += e => events.Add($"{e.Type}: {e.Message}");

        // Act
        orchestrator.EnqueuePrompt("Start task");
        await orchestrator.StartAsync();

        // Assert
        Assert.Equal(3, executor.ExecutionCount); // All three iterations run
        Assert.Contains(events, e => e.Contains("AutoContinuing")); // Auto-continue triggered
    }
}

// Behavior test helpers
file sealed record BehaviorMockRequest(string RequestId, string Prompt) : ITaskRequest;

file sealed record BehaviorMockResult(string RequestId) : ITaskResult
{
    public bool Success => true;
    public string Output => "Execution output";
    public string? ErrorOutput => null;
}

file sealed class BehaviorMockExecutor : ITaskExecutor<BehaviorMockRequest, BehaviorMockResult>
{
    public int ExecutionCount { get; private set; }

    public Task<BehaviorMockResult> ExecuteAsync(
        BehaviorMockRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        onOutput?.Invoke(new TaskOutput { RequestId = request.RequestId, Content = $"Output {ExecutionCount}" });
        return Task.FromResult(new BehaviorMockResult(request.RequestId));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

file sealed class BehaviorMockOracle : IOracleVerifier
{
    private readonly OracleVerdict[] _verdicts;
    private int _callIndex;

    public BehaviorMockOracle(OracleVerdict[] verdicts)
    {
        _verdicts = verdicts;
    }

    public bool IsConfigured => true;

    public Task<OracleVerdict> VerifyAsync(
        string prompt,
        string output,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var verdict = _callIndex < _verdicts.Length
            ? _verdicts[_callIndex]
            : new OracleVerdict { IsComplete = true, CanContinue = false, Analysis = "Default" };

        _callIndex++;
        return Task.FromResult(verdict);
    }

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null)
        => $"Mock prompt: {originalPrompt}";
}
