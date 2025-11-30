# Ironbees Quick Start Guide

Get started with Ironbees multi-agent orchestration in 5 minutes.

## Prerequisites

- .NET 10.0 or later
- OpenAI API key (or compatible LLM provider)

## Installation

```bash
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework
```

## Quick Example: Agent Creation & Execution

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

// Create OpenAI client
var client = new OpenAIClient("your-api-key");
var chatClient = client.GetChatClient("gpt-4o-mini").AsIChatClient();

// Create AI Agent using Microsoft Agent Framework
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant.",
    name: "assistant");

// Run the agent
var response = await agent.RunAsync("What is 2 + 2?");
Console.WriteLine(response.Text); // "4" or "Four"
```

## Workflow Example: Multi-Agent Pipeline

Ironbees enables you to define workflows in YAML and execute them with the MAF execution engine.

### 1. Define Your Workflow (YAML)

```yaml
# workflows/review_workflow.yaml
name: ContentReviewWorkflow
version: "1.0"
description: "Writer creates content, Reviewer evaluates"

states:
  - id: START
    type: start
    next: WRITE

  - id: WRITE
    type: agent
    executor: writer
    next: REVIEW

  - id: REVIEW
    type: agent
    executor: reviewer
    next: END

  - id: END
    type: terminal
```

### 2. Execute the Workflow

```csharp
using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Core.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

// Setup
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

var converter = new MafWorkflowConverter(
    loggerFactory.CreateLogger<MafWorkflowConverter>());
var executor = new MafWorkflowExecutor(
    converter,
    loggerFactory.CreateLogger<MafWorkflowExecutor>());

// Create agent resolver
var client = new OpenAIClient("your-api-key");
var chatClient = client.GetChatClient("gpt-4o-mini").AsIChatClient();

Func<string, CancellationToken, Task<AIAgent>> agentResolver = (name, ct) =>
{
    var instructions = name switch
    {
        "writer" => "You are a creative writer. Write engaging content.",
        "reviewer" => "You are a reviewer. Provide brief feedback.",
        _ => $"You are {name}."
    };

    AIAgent agent = chatClient.CreateAIAgent(
        instructions: instructions,
        name: name);
    return Task.FromResult(agent);
};

// Define workflow
var workflow = new WorkflowDefinition
{
    Name = "ContentReviewWorkflow",
    States =
    [
        new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "WRITE" },
        new WorkflowStateDefinition { Id = "WRITE", Type = WorkflowStateType.Agent, Executor = "writer", Next = "REVIEW" },
        new WorkflowStateDefinition { Id = "REVIEW", Type = WorkflowStateType.Agent, Executor = "reviewer", Next = "END" },
        new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
    ]
};

// Execute
var input = "Write about the future of AI.";
await foreach (var evt in executor.ExecuteAsync(workflow, input, agentResolver))
{
    Console.WriteLine($"[{evt.Type}] {evt.AgentName}: {evt.Content}");
}
```

## Workflow Types

Ironbees supports three workflow patterns:

### Sequential Workflow
Agents execute one after another.

```yaml
states:
  - id: START
    type: start
    next: STEP1
  - id: STEP1
    type: agent
    executor: agent1
    next: STEP2
  - id: STEP2
    type: agent
    executor: agent2
    next: END
  - id: END
    type: terminal
```

### Parallel Workflow
Multiple agents execute concurrently.

```yaml
states:
  - id: START
    type: start
    next: PARALLEL
  - id: PARALLEL
    type: parallel
    executors: [agent1, agent2, agent3]
    next: END
  - id: END
    type: terminal
```

### Mixed Workflow
Combines sequential and parallel execution.

```yaml
states:
  - id: START
    type: start
    next: ANALYZE
  - id: ANALYZE
    type: parallel
    executors: [data-analyzer, market-analyzer]
    next: SYNTHESIZE
  - id: SYNTHESIZE
    type: agent
    executor: synthesizer
    next: END
  - id: END
    type: terminal
```

## Checkpoint & Resume

Enable long-running workflow persistence:

```csharp
using var checkpointStore = new FileSystemCheckpointStore("./checkpoints");

// Save checkpoint
var checkpoint = new CheckpointData
{
    CheckpointId = "chk-001",
    ExecutionId = "exec-001",
    WorkflowName = "MyWorkflow",
    CurrentStateId = "PROCESSING",
    Input = "original input"
};
await checkpointStore.SaveAsync(checkpoint);

// Resume from checkpoint
var latest = await checkpointStore.GetLatestForExecutionAsync("exec-001");
```

## Workflow Validation

Validate workflows before execution:

```csharp
var converter = new MafWorkflowConverter(logger);
var result = converter.Validate(workflow);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"[{error.Code}] {error.Message}");
    }
}

foreach (var warning in result.Warnings)
{
    Console.WriteLine($"[{warning.Code}] {warning.Message}");
}
```

### Validation Codes

| Code | Type | Description |
|------|------|-------------|
| WFC001 | Error | Workflow name required |
| WFC002 | Error | No states defined |
| WFC003 | Error | Missing Start state |
| WFC004 | Error | Missing Terminal state |
| WFC005 | Error | Duplicate state IDs |
| WFC006 | Error | Invalid state transition |
| WFC007 | Error | Agent state missing executor |
| WFC008 | Error | Parallel state missing executors |
| WFC100 | Warning | Conditional transitions simplified |
| WFC101 | Warning | Triggers handled by Ironbees |

## Samples

See the `samples/` directory for complete examples:

- **WorkflowSample**: MAF workflow execution with real API
- **OpenAISample**: Basic agent orchestration
- **AnthropicSample**: Anthropic Claude integration
- **GpuStackSample**: Local LLM with GPUStack

## Next Steps

- [API Reference](./API.md)
- [Advanced Workflow Patterns](./WORKFLOWS.md)
- [Production Deployment Guide](./DEPLOYMENT.md)
