# Agentic Patterns

**Version**: 0.2.4 | **Target**: .NET 10.0

## Overview

Agentic Patterns enable iterative, human-supervised workflows where an AI agent progressively processes data, builds confidence, and requests human intervention at critical checkpoints.

**Core Principle**: Ironbees **declares** patterns; execution is delegated to MS Agent Framework (MAF).

```
┌────────────────────────────────────────────────────────────┐
│                    Ironbees (Declaration)                   │
│  AgenticSettings → GoalDefinition → agentic-loop.yaml      │
└────────────────────────┬───────────────────────────────────┘
                         │ Convert
┌────────────────────────▼───────────────────────────────────┐
│              MS Agent Framework (Execution)                 │
│  - State machine execution    - HITL UI handling           │
│  - Sampling logic             - Confidence calculations    │
└────────────────────────────────────────────────────────────┘
```

## AgenticSettings Schema

The `AgenticSettings` class defines three key pattern configurations:

```csharp
public record AgenticSettings
{
    public SamplingSettings? Sampling { get; init; }
    public ConfidenceSettings? Confidence { get; init; }
    public HitlSettings? Hitl { get; init; }
}
```

### Sampling Settings

Control how data is progressively processed:

```yaml
agentic:
  sampling:
    strategy: progressive    # progressive | random | stratified | sequential
    initialBatchSize: 100    # First batch size
    growthFactor: 5.0        # Batch size multiplier
    maxSamples: 5000         # Maximum total samples
    minSamplesForConfidence: 50  # Minimum before confidence calc
```

| Property | Default | Description |
|----------|---------|-------------|
| `strategy` | `progressive` | How to select samples each iteration |
| `initialBatchSize` | `100` | Starting batch size |
| `growthFactor` | `5.0` | Multiplier for batch growth |
| `maxSamples` | `null` | Cap on total samples (null = unlimited) |
| `minSamplesForConfidence` | `50` | Required before confidence calculation |

**Strategies**:
- **Progressive**: Start small, grow exponentially
- **Random**: Uniform random sampling
- **Stratified**: Maintain population proportions
- **Sequential**: Process in order

### Confidence Settings

Determine when the system has learned enough:

```yaml
agentic:
  confidence:
    threshold: 0.98          # Target confidence level
    stabilityWindow: 3       # Consecutive stable iterations
    minConfidenceForHitl: 0.7  # Trigger HITL below this
    trackHistory: true       # Keep confidence history
```

| Property | Default | Description |
|----------|---------|-------------|
| `threshold` | `0.95` | Target confidence to consider complete |
| `stabilityWindow` | `3` | Required consecutive stable iterations |
| `minConfidenceForHitl` | `null` | Below this, request human help |
| `trackHistory` | `true` | Maintain confidence over time |

### HITL (Human-in-the-Loop) Settings

Configure human intervention points:

```yaml
agentic:
  hitl:
    policy: on-uncertainty     # always | on-uncertainty | on-threshold | on-exception | never
    uncertaintyThreshold: 0.7
    checkpoints:
      - after-initial-sample
      - before-batch-apply
      - on-exception
    responseTimeout: PT1H      # ISO 8601 duration
    timeoutAction: pause       # pause | continue-with-default | cancel | skip
```

| Property | Default | Description |
|----------|---------|-------------|
| `policy` | `on-uncertainty` | When to request human input |
| `uncertaintyThreshold` | `0.7` | Confidence level triggering HITL |
| `checkpoints` | `[]` | Named workflow checkpoints |
| `responseTimeout` | `null` | Max wait time for human response |
| `timeoutAction` | `pause` | Action when timeout occurs |

**HITL Policies**:
- **Always**: Request approval at every checkpoint
- **OnUncertainty**: Request when confidence < threshold
- **OnThreshold**: Request at specific iteration counts
- **OnException**: Request only when errors occur
- **Never**: Fully autonomous (no human intervention)

## Event Types

Agentic workflows emit events for monitoring and integration:

### HitlRequested

Emitted when human intervention is needed:

```csharp
public sealed record HitlRequested(
    string GoalId,
    string ExecutionId,
    HitlRequestType RequestType,  // Review | Approval | Decision | Correction
    string Reason,
    HitlRequestDetails Details
) : GoalExecutionEvent;
```

**Details** include:
- `CheckpointName`: Which checkpoint triggered this
- `Context`: Key-value data for the human reviewer
- `Options`: Available choices (continue, modify, cancel, etc.)

### HitlResponseReceived

Emitted when human responds:

```csharp
public sealed record HitlResponseReceived(
    string GoalId,
    string ExecutionId,
    string ResponseId,
    string SelectedOption,
    IReadOnlyDictionary<string, object>? Modifications
) : GoalExecutionEvent;
```

### ConfidenceUpdated

Emitted when confidence level changes:

```csharp
public sealed record ConfidenceUpdated(
    string GoalId,
    string ExecutionId,
    ConfidenceInfo Confidence
) : GoalExecutionEvent;
```

**ConfidenceInfo** includes:
- `CurrentConfidence`: Current level (0.0-1.0)
- `PreviousConfidence`: Last iteration's level
- `Delta`: Change amount
- `StableIterations`: Consecutive stable count
- `IsStable`: Whether stability window is met
- `ThresholdMet`: Whether target is achieved

### SamplingProgress

Emitted during data sampling:

```csharp
public sealed record SamplingProgress(
    string GoalId,
    string ExecutionId,
    SamplingProgressInfo Progress
) : GoalExecutionEvent;
```

**SamplingProgressInfo** includes:
- `CurrentBatch`: Batch number (1-based)
- `CurrentBatchSize`: Samples in this batch
- `TotalProcessed`: Cumulative samples
- `ProcessingPercentage`: Progress toward max samples

### PatternDiscovered / RulesStabilized

```csharp
public sealed record PatternDiscovered(
    string GoalId,
    string ExecutionId,
    int PatternCount,
    IReadOnlyList<string> NewPatterns
) : GoalExecutionEvent;

public sealed record RulesStabilized(
    string GoalId,
    string ExecutionId,
    int TotalRules,
    double FinalConfidence
) : GoalExecutionEvent;
```

## Workflow Template: agentic-loop.yaml

The `agentic-loop` template defines the standard iterative workflow:

```
┌─────────┐     ┌────────┐     ┌──────────┐     ┌───────────────────┐
│ SAMPLE  │────▶│ANALYZE │────▶│ EVALUATE │────▶│ CONFIDENCE MET?   │
└─────────┘     └────────┘     └──────────┘     └─────────┬─────────┘
     ▲                                                    │
     │                              ┌─────────────────────┼─────────────────┐
     │                              │ No                  │ Yes             │
     │                              ▼                     ▼                 │
     │                    ┌──────────────────┐  ┌──────────────────┐       │
     └────────────────────│ HITL_CHECKPOINT  │  │HITL_FINAL_APPROVAL│       │
                          └──────────────────┘  └────────┬─────────┘       │
                                                         │                  │
                                                         ▼                  │
                                               ┌──────────────────┐        │
                                               │  BATCH_PROCESS   │        │
                                               └────────┬─────────┘        │
                                                        │                   │
                                                        ▼                   │
                                               ┌──────────────────┐        │
                                               │     SUCCESS      │        │
                                               └──────────────────┘        │
```

### Key States

| State | Type | Description |
|-------|------|-------------|
| `INITIALIZE` | setup | Load goal context, resume checkpoint |
| `SAMPLE` | action | Get next batch using sampling strategy |
| `ANALYZE` | action | Process batch, discover patterns |
| `EVALUATE_CONFIDENCE` | decision | Check if ready to proceed |
| `HITL_CHECKPOINT` | hitl | Request human review mid-process |
| `HITL_FINAL_APPROVAL` | hitl | Request approval before bulk apply |
| `BATCH_PROCESS` | action | Apply rules to full dataset |
| `REPORT` | action | Generate final report |
| `SUCCESS` | terminal | Completed successfully |

## Example Goal Definition

Complete goal using agentic patterns:

```yaml
# goals/incremental-preprocessing/goal.yaml
id: incremental-preprocessing
name: "Incremental Data Preprocessing"
description: |
  Progressively sample and analyze data to discover preprocessing rules.
  Uses confidence-based iteration with HITL checkpoints.

version: "1.0"
workflowTemplate: agentic-loop

agentic:
  sampling:
    strategy: progressive
    initialBatchSize: 100
    growthFactor: 5
    maxSamples: 5000
    minSamplesForConfidence: 50

  confidence:
    threshold: 0.98
    stabilityWindow: 3
    minConfidenceForHitl: 0.7
    trackHistory: true

  hitl:
    policy: on-uncertainty
    uncertaintyThreshold: 0.7
    checkpoints:
      - after-initial-sample
      - before-batch-apply
      - on-exception
    responseTimeout: PT1H
    timeoutAction: pause

parameters:
  samplingAgent: data-sampler
  analysisAgent: pattern-analyzer
  processingAgent: batch-processor
  reportAgent: report-generator

constraints:
  maxIterations: 20
  maxTokens: 100000
  allowedAgents:
    - data-sampler
    - pattern-analyzer
    - batch-processor
    - report-generator

successCriteria:
  - id: rules-stable
    description: "Preprocessing rules have stabilized"
    type: Condition
    condition: "confidence >= 0.98 && stabilityWindow >= 3"
    required: true
    weight: 0.6

  - id: error-rate-acceptable
    description: "Error rate is within acceptable bounds"
    type: Condition
    condition: "errorRate <= 0.01"
    required: true
    weight: 0.4

checkpoint:
  enabled: true
  afterEachIteration: true
  checkpointDirectory: checkpoints

tags:
  - data-preprocessing
  - agentic-pattern
  - progressive-sampling
  - hitl
```

## Usage in Code

### Loading a Goal with Agentic Settings

```csharp
var goalLoader = new FileSystemGoalLoader(goalsDirectory);
var goal = await goalLoader.LoadGoalAsync("incremental-preprocessing");

if (goal.Agentic != null)
{
    Console.WriteLine($"Sampling: {goal.Agentic.Sampling?.Strategy}");
    Console.WriteLine($"Confidence: {goal.Agentic.Confidence?.Threshold}");
    Console.WriteLine($"HITL Policy: {goal.Agentic.Hitl?.Policy}");
}
```

### Handling Events

```csharp
bridge.OnEvent += (sender, evt) =>
{
    switch (evt)
    {
        case GoalExecutionEvent.HitlRequested hitl:
            Console.WriteLine($"HITL Required: {hitl.Reason}");
            // Present options to user
            foreach (var option in hitl.Details.Options)
            {
                Console.WriteLine($"  [{option.Id}] {option.Label}");
            }
            break;

        case GoalExecutionEvent.ConfidenceUpdated conf:
            Console.WriteLine($"Confidence: {conf.Confidence.CurrentConfidence:P}");
            if (conf.Confidence.IsStable)
                Console.WriteLine("Rules have stabilized!");
            break;

        case GoalExecutionEvent.SamplingProgress progress:
            Console.WriteLine($"Batch {progress.Progress.CurrentBatch}: " +
                            $"{progress.Progress.TotalProcessed} samples processed");
            break;
    }
};
```

## Scope Boundaries

### Ironbees Provides (Declaration)

- `AgenticSettings`, `SamplingSettings`, `ConfidenceSettings`, `HitlSettings` types
- Event types for monitoring (`HitlRequested`, `ConfidenceUpdated`, etc.)
- `agentic-loop.yaml` workflow template
- YAML schema for goal definitions
- MAF workflow conversion

### Application/MAF Provides (Execution)

- HITL UI implementation
- Sampling algorithm implementation
- Confidence calculation logic
- State machine execution
- Timeout handling
- User notification systems

## Related Documentation

- [Architecture](./ARCHITECTURE.md) - System overview
- [Quick Start](./QUICKSTART.md) - Getting started guide
- [Providers](./PROVIDERS.md) - LLM provider configuration
