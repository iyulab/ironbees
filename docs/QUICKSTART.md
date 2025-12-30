# Ironbees Quick Start

Get started with Ironbees multi-agent orchestration in 5 minutes.

## Prerequisites

- .NET 10.0+
- OpenAI API key (or Azure OpenAI)

## Installation

```bash
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework
```

## Basic Example

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

// Create chat client
var client = new OpenAIClient("your-api-key");
var chatClient = client.GetChatClient("gpt-4o-mini").AsIChatClient();

// Create and run agent
AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant.",
    name: "assistant");

var response = await agent.RunAsync("What is 2 + 2?");
Console.WriteLine(response.Text);
```

## Multi-Agent Workflow

### 1. Define Workflow (YAML)

```yaml
# workflows/review.yaml
name: ContentReviewWorkflow
version: "1.0"

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

### 2. Execute Workflow

```csharp
using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Core.Workflow;

var converter = new MafWorkflowConverter(logger);
var executor = new MafWorkflowExecutor(converter, logger);

// Agent resolver
Func<string, CancellationToken, Task<AIAgent>> agentResolver = (name, ct) =>
{
    var instructions = name switch
    {
        "writer" => "You are a creative writer.",
        "reviewer" => "You are a reviewer. Provide brief feedback.",
        _ => $"You are {name}."
    };
    return Task.FromResult(chatClient.CreateAIAgent(instructions, name));
};

// Define workflow
var workflow = new WorkflowDefinition
{
    Name = "ContentReviewWorkflow",
    States =
    [
        new() { Id = "START", Type = WorkflowStateType.Start, Next = "WRITE" },
        new() { Id = "WRITE", Type = WorkflowStateType.Agent, Executor = "writer", Next = "REVIEW" },
        new() { Id = "REVIEW", Type = WorkflowStateType.Agent, Executor = "reviewer", Next = "END" },
        new() { Id = "END", Type = WorkflowStateType.Terminal }
    ]
};

// Execute
await foreach (var evt in executor.ExecuteAsync(workflow, "Write about AI.", agentResolver))
{
    Console.WriteLine($"[{evt.Type}] {evt.AgentName}: {evt.Content}");
}
```

## Workflow Patterns

### Sequential

```yaml
states:
  - { id: START, type: start, next: STEP1 }
  - { id: STEP1, type: agent, executor: agent1, next: STEP2 }
  - { id: STEP2, type: agent, executor: agent2, next: END }
  - { id: END, type: terminal }
```

### Parallel

```yaml
states:
  - { id: START, type: start, next: PARALLEL }
  - { id: PARALLEL, type: parallel, executors: [agent1, agent2, agent3], next: END }
  - { id: END, type: terminal }
```

### Mixed

```yaml
states:
  - { id: START, type: start, next: ANALYZE }
  - { id: ANALYZE, type: parallel, executors: [data-analyzer, market-analyzer], next: SYNTHESIZE }
  - { id: SYNTHESIZE, type: agent, executor: synthesizer, next: END }
  - { id: END, type: terminal }
```

## Checkpoint & Resume

```csharp
using var store = new FileSystemCheckpointStore("./checkpoints");

// Save
await store.SaveAsync(new CheckpointData
{
    CheckpointId = "chk-001",
    ExecutionId = "exec-001",
    WorkflowName = "MyWorkflow",
    CurrentStateId = "PROCESSING",
    Input = "original input"
});

// Resume
var latest = await store.GetLatestForExecutionAsync("exec-001");
```

## Validation

```csharp
var result = converter.Validate(workflow);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"[{error.Code}] {error.Message}");
}
```

| Code | Description |
|------|-------------|
| WFC001 | Workflow name required |
| WFC002 | No states defined |
| WFC003 | Missing Start state |
| WFC004 | Missing Terminal state |
| WFC005 | Duplicate state IDs |
| WFC006 | Invalid state transition |
| WFC007 | Agent state missing executor |

## Guardrails (v0.3.0)

Content validation for input/output filtering:

```csharp
using Ironbees.Core.Guardrails;

// Configure guardrails via DI
services.AddGuardrails()
    .AddLengthGuardrail(maxInputLength: 10000, maxOutputLength: 50000)
    .AddKeywordGuardrail("forbidden", "blocked", "banned")
    .AddRegexGuardrail(
        new PatternDefinition { Pattern = @"\d{3}-\d{2}-\d{4}", Name = "SSN" },
        new PatternDefinition { Pattern = @"[\w.-]+@[\w.-]+\.\w+", Name = "Email" })
    .AddAzureContentSafety(
        endpoint: "https://your-resource.cognitiveservices.azure.com/",
        apiKey: "your-api-key")
    .AddOpenAIModeration(apiKey: "sk-your-openai-key")
    .AddAuditLogger<CustomAuditLogger>()
    .Build();

// Use pipeline directly
var pipeline = serviceProvider.GetRequiredService<GuardrailPipeline>();

var inputResult = await pipeline.ValidateInputAsync(userInput);
if (!inputResult.IsAllowed)
{
    foreach (var violation in inputResult.Violations)
    {
        Console.WriteLine($"[{violation.Severity}] {violation.GuardrailName}: {violation.Message}");
    }
    return; // Block processing
}

// Process with LLM...
var llmResponse = await agent.RunAsync(userInput);

var outputResult = await pipeline.ValidateOutputAsync(llmResponse.Text);
if (!outputResult.IsAllowed)
{
    // Handle output violations
}
```

### External Content Safety Services

**Azure AI Content Safety**:
```csharp
services.AddGuardrails()
    .AddAzureContentSafety(
        endpoint: new Uri("https://..."),
        credential: new AzureKeyCredential("..."),
        options => {
            options.MaxAllowedSeverity = 4; // 0-6 scale
            options.ValidateInput = true;
            options.ValidateOutput = true;
        })
    .Build();
```

**OpenAI Moderation API**:
```csharp
services.AddGuardrails()
    .AddOpenAIModeration(
        apiKey: "sk-...",
        options => {
            options.ScoreThreshold = 0.7f; // 0.0-1.0
            options.ValidateInput = true;
        })
    .Build();
```

## Samples

See `samples/` directory:
- **WorkflowSample**: MAF workflow execution
- **OpenAISample**: Basic agent orchestration
- **GpuStackSample**: Local LLM with GPUStack

## Next Steps

- [Architecture](./ARCHITECTURE.md)
- [LLM Providers](./PROVIDERS.md)
- [Deployment](./DEPLOYMENT.md)
