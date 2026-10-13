# Ironbees Autonomous SDK Guide

## Overview

The Ironbees Autonomous SDK provides a framework for building goal-based iterative execution systems with Oracle verification, human-in-the-loop oversight, and reflection capabilities.

### Key Features

- **Goal-based Execution**: Define goals and let the system iterate until achieved
- **Oracle Verification**: AI-powered verification of goal completion
- **Human-in-the-Loop (HITL)**: Request human approval at critical points
- **Auto-Continue**: Automatic iteration without manual event handling
- **Reflection**: Learn from each iteration (Reflexion pattern)
- **Final Iteration Strategy**: Enforce completion behavior on last iteration
- **Configuration-driven**: YAML-based configuration with environment variable substitution

## Quick Start

### 1. Basic Usage

```csharp
using Ironbees.Autonomous;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Models;

// Create and configure the orchestrator
var orchestrator = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithExecutor(new MyTaskExecutor())
    .WithRequestFactory((id, prompt) => new MyRequest { RequestId = id, Prompt = prompt })
    .WithOracle(new MyOracle())
    .WithMaxIterations(20)
    .WithAutoContinue()
    .Build();

// Subscribe to events
orchestrator.OnEvent += evt => Console.WriteLine($"[{evt.Type}] {evt.Message}");

// Start execution
orchestrator.EnqueuePrompt("Achieve my goal");
await orchestrator.StartAsync();
```

### 2. YAML Configuration

Create a `settings.yaml` file:

```yaml
orchestration:
  max_iterations: 20
  max_oracle_iterations: 5
  completion_mode: until_goal_achieved
  auto_continue: true
  auto_continue_prompt_template: "Continue with iteration {iteration}"

context:
  enable_tracking: true
  enable_reflection: true
  max_learnings: 10
  max_outputs: 5

confidence:
  min_threshold: 0.7
  human_review_threshold: 0.5

debug:
  enabled: true
```

Load and use:

```csharp
var orchestrator = (await AutonomousOrchestrator.FromSettingsFileAsync<MyRequest, MyResult>("settings.yaml"))
    .WithExecutor(new MyTaskExecutor())
    .WithRequestFactory((id, prompt) => new MyRequest { RequestId = id, Prompt = prompt })
    .Build();

// Configuration is automatically applied to StartAsync()
await orchestrator.StartAsync();
```

## Core Concepts

### Two-Loop Architecture

The SDK uses two distinct loops:

| Loop | Trigger | Purpose |
|------|---------|---------|
| **Oracle Loop** | `NextPromptSuggestion != null` | Retry within same task |
| **Main Loop** | `AutoContinue` enabled | Move to next iteration |

#### Oracle Loop Control

```csharp
// Continue to next main iteration (triggers AutoContinue)
var next = OracleVerdict.ContinueToNextIteration("Making progress", confidence: 0.5);

// Retry within Oracle loop with refined prompt
var retry = OracleVerdict.RetryWithRefinedPrompt(
    refinedPrompt: "Try a different approach",
    analysis: "Previous attempt was too vague",
    confidence: 0.3);

// Goal achieved - stop all execution
var done = OracleVerdict.GoalAchieved("Goal completed successfully", confidence: 0.95);

// Stop without achieving goal
var stop = OracleVerdict.Stop("Cannot continue - resource exhausted");
```

### Final Iteration Strategy

Enforce completion behavior when max iterations is reached. The last iteration runs the strategy's modified request,
and when it still ends without reaching the goal the strategy's forced result is recorded as the outcome: a history
entry and an `AutonomousEventType.ForcedCompletion` event carrying it (`e.HistoryEntry.ExecutionOutput`).

```csharp
// Option 1: Use built-in strategy
var withStrategy = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithFinalIterationStrategy(new PromptEnforcementFinalIterationStrategy<MyRequest, MyResult>(
        // Modify the request for final iteration
        requestModifier: ctx => ctx.OriginalRequest with { MustComplete = true },
        // Force a result if goal not achieved
        completionEnforcer: ctx => new MyResult
        {
            RequestId = ctx.OriginalRequest.RequestId,
            Output = ctx.PreviousOutputs.LastOrDefault() ?? "No answer",
        }))
    .Build();

// Option 2: Use lambda helper — the last iteration runs the original prompt with the warning in front of it
// (default: AutonomousOrchestratorBuilder<,>.DefaultFinalIterationWarning), built with your request factory
var withEnforcement = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithFinalIterationEnforcement(
        completionEnforcer: ctx => new MyResult
        {
            RequestId = ctx.OriginalRequest.RequestId,
            Output = ctx.PreviousOutputs.LastOrDefault() ?? "No answer",
        },
        warningMessage: "Final iteration - must complete now!")
    .Build();
```

### Custom Implementation

```csharp
public class MyFinalIterationStrategy : IFinalIterationStrategy<MyRequest, MyResult>
{
    public Task<MyRequest?> BeforeFinalIterationAsync(
        FinalIterationContext<MyRequest, MyResult> context, CancellationToken cancellationToken = default)
    {
        if (context.IsLastIteration)
        {
            // Modify request to force completion
            return Task.FromResult<MyRequest?>(context.OriginalRequest with {
                Prompt = context.OriginalRequest.Prompt + "\n\n⚠️ FINAL ITERATION: You MUST provide a complete answer."
            });
        }

        if (context.IsInFinalPhase) // Last 3 iterations
        {
            // Warn about approaching limit
            return Task.FromResult<MyRequest?>(context.OriginalRequest with {
                Prompt = context.OriginalRequest.Prompt + $"\n\nNote: {context.RemainingIterations} iterations remaining."
            });
        }

        return Task.FromResult<MyRequest?>(null); // Use original request
    }

    public Task<MyResult?> ForceCompletionAsync(
        FinalIterationContext<MyRequest, MyResult> context, CancellationToken cancellationToken = default)
    {
        // Called if last iteration didn't achieve goal
        // Return a forced completion result
        return Task.FromResult<MyResult?>(new MyResult {
            RequestId = context.OriginalRequest.RequestId,
            Output = "Forced completion based on best available data",
            IsComplete = true,
            Confidence = 0.5
        });
    }
}
```

### Configuration Diagnostics

Debug configuration issues:

```csharp
var orchestrator = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithSettings(settings)
    .Build();

// Dump configuration to console
orchestrator.DumpConfiguration(Console.Out);

// Output:
// ┌─── Autonomous Configuration ─────────────────────────────
// │ MaxIterations: 20
// │ MaxOracleIterations: 5
// │ CompletionMode: UntilGoalAchieved
// │ EnableOracle: True
// │ EnableFinalIterationStrategy: True
// │ AutoContinueOnOracle: True
// └──────────────────────────────────────────────────────────
```

## Implementing Core Interfaces

### ITaskExecutor

```csharp
public class MyTaskExecutor : ITaskExecutor<MyRequest, MyResult>
{
    public async Task<MyResult> ExecuteAsync(
        MyRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        // Execute the task (your model call, tool run, ...)
        var output = await Task.Run(() => request.Prompt.ToUpperInvariant(), cancellationToken);

        // Stream output
        onOutput?.Invoke(new TaskOutput { RequestId = request.RequestId, Type = TaskOutputType.Output, Content = output });

        return new MyResult
        {
            RequestId = request.RequestId,
            Output = output,
            Success = true
        };
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### IOracleVerifier

```csharp
public class MyOracle : IOracleVerifier
{
    public bool IsConfigured => true;

    public Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // Analyze the output (your model call or rule)
        if (executionOutput.Contains("DONE", StringComparison.Ordinal))
        {
            return Task.FromResult(OracleVerdict.GoalAchieved("The goal is met", confidence: 0.9));
        }

        if (executionOutput.Length > 0)
        {
            return Task.FromResult(OracleVerdict.ContinueToNextIteration("Partial progress", confidence: 0.5));
        }

        return Task.FromResult(OracleVerdict.Stop("No output to judge"));
    }

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null)
        => $"Goal: {originalPrompt}\nOutput: {executionOutput}\nIs the goal achieved?";
}
```

### ITaskRequest and ITaskResult

```csharp
public record MyRequest : ITaskRequest
{
    public required string RequestId { get; init; }
    public required string Prompt { get; init; }

    // Custom properties
    public int IterationNumber { get; init; }
    public List<string> History { get; init; } = [];
    public bool MustComplete { get; init; }
}

public record MyResult : ITaskResult
{
    public required string RequestId { get; init; }
    public required string Output { get; init; }
    public bool Success { get; init; }
    public string? ErrorOutput { get; init; }

    // Custom properties
    public double Confidence { get; init; }
    public bool IsComplete { get; init; }
}
```

## Best Practices

### 1. Use Static Helper Methods for OracleVerdict

```csharp
// ✅ Good - Clear intent
var achieved = OracleVerdict.GoalAchieved("All tests pass", confidence: 0.9);
var progress = OracleVerdict.ContinueToNextIteration("Two of five tests pass", confidence: 0.4);

// ❌ Avoid - Confusing
var unclear = new OracleVerdict {
    IsComplete = false,
    CanContinue = true,
    NextPromptSuggestion = null,  // Easy to forget
    Analysis = "Two of five tests pass"
};
```

### 2. Use YAML Configuration

```csharp
// ✅ Good - Configuration-driven
var fromYaml = await AutonomousOrchestrator.FromSettingsFileAsync<MyRequest, MyResult>("settings.yaml");

// ❌ Avoid - Hard-coded values scattered
var hardCoded = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithMaxIterations(20)
    .WithMaxOracleIterations(5); // ... many more settings
```

### 3. Implement Final Iteration Strategy for Bounded Tasks

```csharp
// ✅ Good - Ensures completion
var bounded = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithFinalIterationEnforcement(ctx => new MyResult
    {
        RequestId = ctx.OriginalRequest.RequestId,
        Output = ctx.PreviousOutputs.LastOrDefault() ?? "No answer",
    });

// ❌ Risk - May run out of iterations without result
// No final iteration strategy
```

### 4. Use DumpConfiguration for Debugging

```csharp
// Before starting, verify configuration
if (settings.Debug.Enabled)
{
    orchestrator.DumpConfiguration(Console.Out);
}
```

## Troubleshooting

### Config Not Applied

**Symptom**: Settings from YAML not reflected at runtime

**Solution**: Call `Build()` after `WithSettings()` — `StartAsync()` with no argument uses the built configuration:
```csharp
orchestrator.DumpConfiguration(Console.Out); // Verify settings
```

### Stuck in Oracle Loop

**Symptom**: Same iteration repeats instead of progressing

**Cause**: `NextPromptSuggestion` is set, triggering Oracle retry loop

**Solution**: Use `ContinueToNextIteration()` instead:
```csharp
// ❌ Wrong - triggers Oracle retry
var retries = new OracleVerdict { Analysis = "continue", NextPromptSuggestion = "continue" };

// ✅ Correct - triggers Main loop
var moves = OracleVerdict.ContinueToNextIteration("continue");
```

### No Result on Max Iterations

**Symptom**: Task ends without producing final result

**Solution**: Implement Final Iteration Strategy:
```csharp
var orchestrator = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithFinalIterationEnforcement(ctx => new MyResult
    {
        RequestId = ctx.OriginalRequest.RequestId,
        Output = ctx.PreviousOutputs.LastOrDefault() ?? "No answer",
    })
    .Build();
```

## API Reference

### AutonomousOrchestratorBuilder Methods

| Method | Description |
|--------|-------------|
| `WithExecutor()` | Set task executor (required) |
| `WithRequestFactory()` | Set request factory (required) |
| `WithOracle()` | Add Oracle verification |
| `WithHumanInTheLoop()` | Add HITL oversight |
| `WithFallbackStrategy()` | Add fallback for failures |
| `WithFinalIterationStrategy()` | Add final iteration strategy |
| `WithFinalIterationEnforcement()` | Lambda-based final iteration |
| `WithMaxIterations()` | Set iteration limit |
| `WithMaxOracleIterations()` | Set Oracle retry limit |
| `WithAutoContinue()` | Enable auto-continue |
| `WithContextTracking()` | Enable context tracking |
| `WithReflection()` | Enable reflection mode |
| `WithSettings()` | Load from OrchestratorSettings |
| `WithSettingsFileAsync()` | Load from YAML file |
| `Build()` | Create orchestrator |
| `BuildAndStartAsync()` | Create and start |

### OracleVerdict Static Methods

| Method | Loop Effect | Use Case |
|--------|-------------|----------|
| `GoalAchieved()` | Stops all | Goal complete |
| `ContinueToNextIteration()` | Main loop continues | Progress made |
| `RetryWithRefinedPrompt()` | Oracle loop retries | Need refinement |
| `Stop()` | Stops all | Cannot continue |
| `Progress()` | Configurable | Partial progress |
| `Error()` | Allows continue | Error occurred |

### FinalIterationContext Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentIteration` | int | 1-based iteration number |
| `MaxIterations` | int | Configured maximum |
| `RemainingIterations` | int | Iterations left (including current) |
| `IsLastIteration` | bool | True if 1 remaining |
| `IsInFinalPhase` | bool | True if ≤3 remaining |
| `OriginalRequest` | TRequest | The current request |
| `LastResult` | TResult? | Result from execution |
| `PreviousOutputs` | IReadOnlyList<string> | All previous outputs |
| `SessionId` | string | Current session ID |

## Related Documentation

