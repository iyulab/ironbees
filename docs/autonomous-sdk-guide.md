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
var orchestrator = await AutonomousOrchestrator
    .FromSettingsFileAsync<MyRequest, MyResult>("settings.yaml")
    .WithExecutor(myExecutor)
    .WithRequestFactory(myFactory)
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
return OracleVerdict.ContinueToNextIteration("Making progress", confidence: 0.5);

// Retry within Oracle loop with refined prompt
return OracleVerdict.RetryWithRefinedPrompt(
    refinedPrompt: "Try a different approach",
    analysis: "Previous attempt was too vague",
    confidence: 0.3);

// Goal achieved - stop all execution
return OracleVerdict.GoalAchieved("Goal completed successfully", confidence: 0.95);

// Stop without achieving goal
return OracleVerdict.Stop("Cannot continue - resource exhausted");
```

### Final Iteration Strategy

Enforce completion behavior when max iterations is reached:

```csharp
// Option 1: Use built-in strategy
var orchestrator = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithFinalIterationStrategy(new PromptEnforcementFinalIterationStrategy<MyRequest, MyResult>(
        finalIterationWarning: "This is your final chance. Make a decision NOW.",
        requestModifier: ctx => {
            // Modify the request for final iteration
            return ctx.OriginalRequest with { MustComplete = true };
        },
        completionEnforcer: ctx => {
            // Force a result if goal not achieved
            return CreateFallbackResult(ctx);
        }))
    .Build();

// Option 2: Use lambda helper
var orchestrator = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithFinalIterationEnforcement(
        completionEnforcer: ctx => CreateFallbackResult(ctx),
        warningMessage: "Final iteration - must complete now!")
    .Build();
```

### Custom Implementation

```csharp
public class MyFinalIterationStrategy : IFinalIterationStrategy<MyRequest, MyResult>
{
    public async Task<MyRequest?> BeforeFinalIterationAsync(
        FinalIterationContext<MyRequest, MyResult> context)
    {
        if (context.IsLastIteration)
        {
            // Modify request to force completion
            return context.OriginalRequest with {
                Prompt = context.OriginalRequest.Prompt + "\n\n⚠️ FINAL ITERATION: You MUST provide a complete answer."
            };
        }

        if (context.IsInFinalPhase) // Last 3 iterations
        {
            // Warn about approaching limit
            return context.OriginalRequest with {
                Prompt = context.OriginalRequest.Prompt + $"\n\nNote: {context.RemainingIterations} iterations remaining."
            };
        }

        return null; // Use original request
    }

    public async Task<MyResult?> ForceCompletionAsync(
        FinalIterationContext<MyRequest, MyResult> context)
    {
        // Called if last iteration didn't achieve goal
        // Return a forced completion result
        return new MyResult {
            Output = "Forced completion based on best available data",
            IsComplete = true,
            Confidence = 0.5
        };
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
        Action<StreamOutput> onOutput,
        CancellationToken cancellationToken)
    {
        // Execute the task
        var result = await ProcessAsync(request, cancellationToken);

        // Stream output
        onOutput(new StreamOutput { Content = result.Output });

        return new MyResult
        {
            Output = result.Output,
            Success = result.Success,
            ErrorOutput = result.Error
        };
    }
}
```

### IOracleVerifier

```csharp
public class MyOracle : IOracleVerifier
{
    public bool IsConfigured => true;

    public async Task<OracleVerdict> VerifyAsync(
        string originalGoal,
        string executionOutput,
        OracleConfig config,
        CancellationToken cancellationToken)
    {
        // Analyze the output
        var analysis = await AnalyzeOutput(originalGoal, executionOutput);

        if (analysis.IsComplete)
        {
            return OracleVerdict.GoalAchieved(
                analysis.Summary,
                confidence: analysis.Confidence);
        }

        if (analysis.CanContinue)
        {
            return OracleVerdict.ContinueToNextIteration(
                analysis.Summary,
                confidence: analysis.Confidence);
        }

        return OracleVerdict.Stop(analysis.Reason);
    }
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
}

public record MyResult : ITaskResult
{
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
return OracleVerdict.GoalAchieved(analysis, confidence);
return OracleVerdict.ContinueToNextIteration(analysis, confidence);

// ❌ Avoid - Confusing
return new OracleVerdict {
    IsComplete = false,
    CanContinue = true,
    NextPromptSuggestion = null  // Easy to forget
};
```

### 2. Use YAML Configuration

```csharp
// ✅ Good - Configuration-driven
var orchestrator = await AutonomousOrchestrator
    .FromSettingsFileAsync<MyRequest, MyResult>("settings.yaml");

// ❌ Avoid - Hard-coded values scattered
var orchestrator = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithMaxIterations(20)
    .WithMaxOracleIterations(5)
    // ... many more settings
```

### 3. Implement Final Iteration Strategy for Bounded Tasks

```csharp
// ✅ Good - Ensures completion
.WithFinalIterationEnforcement(ctx => GenerateBestGuess(ctx.PreviousOutputs))

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

## Migration Guide (v0.3.0 → v0.3.1)

### Builder/StartAsync Config

**Before (v0.3.0)**:
```csharp
var settings = await loader.LoadWithEnvironmentAsync("settings.yaml");
var orchestrator = AutonomousOrchestrator.Create<TReq, TRes>()
    .WithSettings(settings)
    .Build();
await orchestrator.StartAsync(settings.ToAutonomousConfig()); // Manual!
```

**After (v0.3.1)**:
```csharp
var settings = await loader.LoadWithEnvironmentAsync("settings.yaml");
var orchestrator = AutonomousOrchestrator.Create<TReq, TRes>()
    .WithSettings(settings)
    .Build();
await orchestrator.StartAsync(); // Automatic!
```

### Oracle Loop Control

**Before (v0.3.0)**:
```csharp
return new OracleVerdict {
    IsComplete = false,
    CanContinue = true,
    NextPromptSuggestion = null, // Confusing
    Analysis = "Continue"
};
```

**After (v0.3.1)**:
```csharp
return OracleVerdict.ContinueToNextIteration("Continue"); // Clear!
```

## Troubleshooting

### Config Not Applied

**Symptom**: Settings from YAML not reflected at runtime

**Solution**: Ensure you're using v0.3.1 and calling `Build()` after `WithSettings()`:
```csharp
orchestrator.DumpConfiguration(Console.Out); // Verify settings
```

### Stuck in Oracle Loop

**Symptom**: Same iteration repeats instead of progressing

**Cause**: `NextPromptSuggestion` is set, triggering Oracle retry loop

**Solution**: Use `ContinueToNextIteration()` instead:
```csharp
// ❌ Wrong - triggers Oracle retry
return new OracleVerdict { NextPromptSuggestion = "continue" };

// ✅ Correct - triggers Main loop
return OracleVerdict.ContinueToNextIteration("continue");
```

### No Result on Max Iterations

**Symptom**: Task ends without producing final result

**Solution**: Implement Final Iteration Strategy:
```csharp
.WithFinalIterationEnforcement(ctx => CreateFallbackResult(ctx))
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

- [Lessons Learned: TwentyQuestions](../local-docs/LESSONS_LEARNED_TWENTYQUESTIONS.md)
- [Agentic Patterns Documentation](./agentic-patterns.md)
- [Configuration Reference](./autonomous-config-reference.md)
