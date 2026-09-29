# Ironbees

[![CI](https://github.com/iyulab/ironbees/actions/workflows/ci.yml/badge.svg)](https://github.com/iyulab/ironbees/actions/workflows/ci.yml)
[![NuGet - Core](https://img.shields.io/nuget/v/Ironbees.Core?label=Ironbees.Core)](https://www.nuget.org/packages/Ironbees.Core)
[![NuGet - AgentFramework](https://img.shields.io/nuget/v/Ironbees.AgentFramework?label=Ironbees.AgentFramework)](https://www.nuget.org/packages/Ironbees.AgentFramework)
[![NuGet - Ironhive](https://img.shields.io/nuget/v/Ironbees.Ironhive?label=Ironbees.Ironhive)](https://www.nuget.org/packages/Ironbees.Ironhive)
[![License](https://img.shields.io/github/license/iyulab/ironbees)](LICENSE)

> GitOps-style declarative AI agent management for .NET

Ironbees brings **filesystem conventions** and **declarative agent definitions** to .NET AI development. Define agents as YAML files, let Ironbees handle loading, routing, and orchestration - then plug in any LLM backend.

## Why Ironbees?

| Feature | What it means |
|---------|--------------|
| **GitOps-Ready** | Agent definitions are YAML files under version control - review, diff, rollback |
| **Zero-Code Agent Setup** | `agent.yaml` + `system-prompt.md` = fully configured agent |
| **Observable** | All state lives in the filesystem - debug with `ls`, `grep`, `cat` |
| **Portable** | Swap between IronHive and Microsoft Agent Framework without changing agent definitions |
| **Intelligent Routing** | Keyword, embedding, and hybrid agent selection out of the box |
| **Cost Tracking** | Records the token usage each provider reports and prices it with [TokenMeter](https://github.com/iyulab/TokenMeter) |

## Packages

| Package | Purpose |
|---|---|
| `Ironbees.Core` | Agent loading from `agents/<name>/agent.yaml`, routing, guardrails, token and cost tracking (`AddIronbeesCore`) |
| `Ironbees.AgentMode` | YAML workflow definitions, templates and goals (`GoalDefinition`, `IGoalExecutionBridge`) |
| `Ironbees.Ironhive` | IronHive backend (multi-provider) and declarative multi-agent orchestration (`AddIronbeesIronhive`) |
| `Ironbees.AgentFramework` | Azure OpenAI / Microsoft Agent Framework backend and the goal execution bridge (`AddIronbees`) |
| `Ironbees.Autonomous` | Iterative autonomous execution with oracle verification (`AutonomousOrchestrator.Create<TRequest, TResult>()`) |

## Features

- **Agents from files** — `agent.yaml` + `system-prompt.md` per agent directory, loaded by the orchestrator. An agent that lists `tools` gets exactly those.
- **Routing** — `IAgentOrchestrator.SelectAgentAsync(input)` picks an agent by keyword, embedding or hybrid selection. Call `ProcessAsync(input, agentName)` to address one directly.
- **Guardrails** — `services.AddGuardrails()` returns a `GuardrailBuilder` for input and output checks. They are opt-in.
- **Multi-agent orchestration** (`Ironbees.Ironhive`) — `IIronhiveOrchestratorFactory.CreateOrchestrator(settings, agents)` with an `OrchestratorSettings`:
  `Type` (Sequential / Parallel / HubSpoke / Handoff / GroupChat / Graph), `Middleware` (retry, circuit breaker, bulkhead, rate limit, timeout),
  `StopOnAgentFailure` and the timeouts. Every orchestrator type carries the same common options. See
  [Multi-Agent Orchestration](#multi-agent-orchestration).
- **Approval gate** — set `IronhiveOptions.ApprovalHandler` in `AddIronbeesIronhive` to be asked before each agent runs. A refusal stops the run.
- **Workflow human gates** (`Ironbees.AgentMode`) — a `type: human_gate` state in a YAML workflow stops `YamlDrivenOrchestrator.ExecuteAsync` with a
  `WaitingForApproval` state; answer with `ApproveAsync(executionId, new ApprovalDecision { Approved = … })` to move to `on_approve` / `on_reject`.
  `approval_mode: never` makes the gate pass through (e.g. in a test environment); `always_require` is the default.
- **Goals** (`Ironbees.AgentFramework`) — `IGoalExecutionBridge.ExecuteGoalAsync(goal, input, new GoalExecutionOptions { MaxIterations = …, Timeout = …, Parameters = … })`
  runs a goal's workflow template. The per-call options override the goal's own constraints and checkpoint settings. A goal that exceeds its `Timeout` (or
  `Constraints.MaxDuration`) ends with a `GoalFailed` event marked `timedOut`.
- **Autonomous execution** (`Ironbees.Autonomous`) — `AutonomousOrchestrator.Create<TRequest, TResult>().WithExecutor(…).WithRequestFactory(…).WithOracle(…).Build()`
  (executor and request factory are required), then `EnqueuePrompt(…)` and `await StartAsync()`; progress arrives on the `OnEvent` event. Context tracking is on by default;
  `WithoutContext()` turns it off. `AutonomousConfig.MaxContextLearnings` (`context.max_learnings` in settings) caps the learnings kept between iterations.
  `services.AddAutonomousContext(…)` registers the context manager for DI.
- **Token tracking and cost estimation** — see [Token Tracking & Cost Estimation](#token-tracking--cost-estimation).

## Architecture

```
                    Ironbees.Core
         (Agent loading, routing, guardrails,
          token tracking, cost estimation)
                    |
              Ironbees.AgentMode
           (YAML workflows, definitions)
                  /            \
Ironbees.AgentFramework    Ironbees.Ironhive
  (Azure OpenAI + MAF)      (IronHive multi-provider,
                             Multi-agent orchestration)

              Ironbees.Autonomous
        (Iterative execution, oracle verification)
```

Ironbees Core is **backend-agnostic**. Pick the adapter that fits your stack:

- **Ironbees.Ironhive** - Multi-provider (OpenAI, Anthropic, Google, Ollama) + **Multi-agent orchestration** via [IronHive](https://github.com/iyulab/ironhive)
- **Ironbees.AgentFramework** - Azure OpenAI + Microsoft Agent Framework

## Installation

**Option A: IronHive backend** (multi-provider)

```bash
dotnet add package Ironbees.Ironhive
```

**Option B: Azure OpenAI backend**

```bash
dotnet add package Ironbees.AgentFramework
```

## Define an Agent

```
agents/
└── coding-agent/
    ├── agent.yaml          # Agent metadata and model config
    └── system-prompt.md    # System prompt
```

**agents/coding-agent/agent.yaml:**
```yaml
name: coding-agent
description: Expert software developer
capabilities: [code-generation, code-review]
model:
  provider: openai
  deployment: gpt-4o
  temperature: 0.7
```

**agents/coding-agent/system-prompt.md:**
```markdown
You are an expert software developer specializing in C# and .NET...
```

#### Sampling parameters

`model:` accepts five sampling parameters. Support differs by backend — a parameter marked
*ignored* is accepted by configuration binding but never reaches the provider:

| Parameter | Agent Framework | IronHive |
|---|---|---|
| `temperature` | applied | applied |
| `maxTokens` | applied | applied |
| `topP` | applied | applied |
| `frequencyPenalty` | applied | **ignored** (warns at agent creation) |
| `presencePenalty` | applied | **ignored** (warns at agent creation) |

The last two have no field on the IronHive agent parameter contract, so the adapter has nothing
to forward them to; it logs a warning naming the agent rather than dropping them silently. If
you need them, run that agent on the Agent Framework backend.

`temperature` and `maxTokens` are always sent — including when you set them to the same value as
the documented default. Provider support applies on top of this: a backend marked *applied* still
depends on the selected provider honouring the parameter.

### Tools (function calling)

An agent may declare tool names it can call; each name is resolved against a pool registered once
at startup, not per agent:

**agents/coding-agent/agent.yaml:**
```yaml
name: coding-agent
description: Expert software developer
model:
  provider: openai
  deployment: gpt-4o
tools: [search-code, run-tests]
```

```csharp
using IronHive.Abstractions;          // AddOpenAIProviders
using IronHive.Core.Tools;            // ToolCollection
using IronHive.Providers.OpenAI;      // OpenAIConfig
using Ironbees.Ironhive;              // AddIronbeesIronhive

services.AddIronbeesIronhive(options =>
{
    options.AgentsDirectory = "./agents";
    options.ConfigureHive = hive => hive.AddOpenAIProviders("openai", new OpenAIConfig { ApiKey = apiKey });
    options.Tools = new ToolCollection([mySearchCodeTool, myRunTestsTool]); // your IronHive.Abstractions.Tools.ITool instances
});
```

A tool name with no match in the registered pool fails agent creation loud, at
`orchestrator.LoadAgentsAsync()` time, rather than silently running the agent without it. Tools
are a fixed per-agent property (like `model`). To give one request a different tool set — say a
tool bound to the current workspace — pass `ProcessOptions.Tools` (Microsoft.Extensions.AI `AITool`s);
it replaces the agent's tools for that call only. Tool execution surfaces on the structured stream as
`ToolCallStartChunk`/`ToolCallCompleteChunk` (see the streaming example below); the text-only
`StreamAsync` surface only sees the resulting answer text, not the intermediate tool calls.

Currently supported by the `Ironbees.Ironhive` adapter. The Agent Framework/Azure OpenAI backend
(`AddIronbees`) does not yet resolve `tools:` — an agent declaring tools on that backend runs
without them (no `ITool`/pool concept exists there today).

## Quick Start

### With IronHive

```bash
dotnet add package Ironbees.Ironhive
dotnet add package IronHive.Providers.OpenAI
```

```csharp
using IronHive.Abstractions;          // AddOpenAIProviders
using IronHive.Providers.OpenAI;      // OpenAIConfig
using Ironbees.Ironhive;              // AddIronbeesIronhive

services.AddIronbeesIronhive(options =>
{
    options.AgentsDirectory = "./agents";
    options.ConfigureHive = hive =>
    {
        hive.AddOpenAIProviders("openai", new OpenAIConfig { ApiKey = apiKey });
    };
});
```

### With Azure OpenAI

```csharp
using Ironbees.AgentFramework;        // AddIronbees

services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
});
```

### Without an API key (Ollama, LM Studio, vLLM, llama.cpp server)

The IronHive backend can talk to any OpenAI-compatible local server, and those accept requests
without a key. Register the server under a provider name and point the agent at it — nothing else
in the agent definition changes:

```bash
dotnet add package Ironbees.Ironhive
dotnet add package IronHive.Providers.OpenAI.Compatible
```

```csharp
using IronHive.Abstractions;                   // AddOpenAICompatibleProviders
using IronHive.Providers.OpenAI.Compatible;    // OpenAICompatibleConfig
using Ironbees.Ironhive;

services.AddIronbeesIronhive(options =>
{
    options.AgentsDirectory = "./agents";
    options.ConfigureHive = hive =>
    {
        // Ollama's default endpoint; LM Studio is :1234, vLLM :8000 — only BaseUrl differs.
        hive.AddOpenAICompatibleProviders("ollama", new OpenAICompatibleConfig
        {
            BaseUrl = "http://localhost:11434"
        });
    };
});
```

```yaml
# agents/coding-agent/agent.yaml
model:
  provider: ollama        # the name you registered above
  deployment: qwen2.5:7b  # a model the server has pulled
```

The server must already be running with that model available (`ollama pull qwen2.5:7b`); Ironbees
does not start or download it. For a fully in-process local model with no server at all, see
[ironhive-host](https://github.com/iyulab/ironhive-host), which embeds local inference through
LMSupply and has it on by default.

### Use the Agent

```csharp
using Ironbees.Core;
using Ironbees.Core.Streaming;        // TextChunk, ThinkingChunk, SuggestionsChunk
using Microsoft.Extensions.DependencyInjection;

var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync();

// Explicit agent selection
var response = await orchestrator.ProcessAsync(
    "Write a C# fibonacci function",
    agentName: "coding-agent");

// Automatic routing
var routed = await orchestrator.ProcessAsync(
    "fibonacci in C#"); // Routes based on keywords/embeddings

// Streaming
await foreach (var chunk in orchestrator.StreamAsync("Write a blog post"))
{
    Console.Write(chunk);
}

// Per-request system prompt override (RAG context, per-workspace instructions)
string ragContext = "...";            // e.g. the passages your retrieval step returned for `query`
await foreach (var chunk in orchestrator.StreamAsync(query, new ProcessOptions
{
    AgentName = "rag-agent",
    ConversationId = sessionId,
    SystemPromptOverride = $"Context:\n{ragContext}\n\nAnswer only from the context above.",
}))
{
    Console.Write(chunk);
}

// Per-request output cap. Null (the default) keeps the agent's configured ModelConfig.MaxTokens.
// Worth setting on a reasoning model: reasoning and answer share one budget, so a run that reasons
// without concluding can spend the whole allowance and return no text - which reads to a user as a
// request that thought and then said nothing.
await foreach (var chunk in orchestrator.StreamStructuredAsync(query, new ProcessOptions
{
    ConversationId = sessionId,
    MaxTokens = 2048,
}))
{
    // ...
}

// Structured streaming with follow-up suggestions (adapter permitting, e.g. Ironbees.Ironhive)
await foreach (var chunk in orchestrator.StreamStructuredAsync(query, new ProcessOptions
{
    ConversationId = sessionId,
    Suggestions = new SuggestionRequest { MaxCount = 1 }, // model-generated follow-up questions
}))
{
    switch (chunk)
    {
        case TextChunk text: Console.Write(text.Content); break;
        case ThinkingChunk thinking: Console.Write(thinking.Content); break; // reasoning models (extended thinking)
        case SuggestionsChunk s:
            foreach (var suggestion in s.Suggestions)
                Console.WriteLine($"{suggestion.Question}: {string.Join(" | ", suggestion.Items)}");
            break;
    }
}
// Non-streaming: var result = await orchestrator.ProcessStructuredAsync(query, options);
//                result.Text / result.Suggestions
```

Which per-request options an adapter applies. An option the adapter cannot apply throws
`NotSupportedException` at call time rather than being ignored:

| `ProcessOptions` | `Ironbees.Ironhive` | `Ironbees.AgentFramework` |
|---|---|---|
| `MaxTokens` | applied | applied |
| `ThinkingEffort` | applied | throws |
| `Tools` | applied | throws |
| `Suggestions` | applied | throws |
| `MaxToolTurns` | applied | throws |

### ASP.NET Core Integration

**Required packages** — `Ironbees.Core` is abstraction-only. An LLM backend package must also be added:

```bash
dotnet add package Ironbees.Ironhive
dotnet add package IronHive.Providers.OpenAI
```

**`ConfigureHive` is a property assignment, not a method call:**

```csharp
using IronHive.Abstractions;
using IronHive.Providers.OpenAI;
using Ironbees.Ironhive;

builder.Services.AddIronbeesIronhive(opts =>
{
    opts.AgentsDirectory = "agents";
    opts.ConfigureHive = hive =>       // ← property assignment (not a method call)
    {
        hive.AddOpenAIProviders("openai", new OpenAIConfig { ApiKey = "..." });
    };
});
```

Both registration paths (`ConfigureHive` and a pre-built `HiveService` instance) register
`IHiveService` as a **singleton**, matching the lifetime of the Ironbees services that consume it.

**`LoadAgentsAsync()` must be called after DI build, before `app.Run()`:**

```csharp
using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;   // already a global using in a web project

var app = builder.Build();

var orchestrator = app.Services.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync();

app.Run();
```

## Multi-Agent Orchestration

Ironbees.Ironhive builds an IronHive orchestrator from an `OrchestratorSettings` record and the agents' configs, and
streams the run as goal events:

```csharp
using Ironbees.Core.Orchestration;    // OrchestratorSettings, MiddlewareSettings, RetrySettings, ...
using Ironbees.Ironhive;              // IronhiveAdapter
using Microsoft.Extensions.DependencyInjection;

var adapter = serviceProvider.GetRequiredService<IronhiveAdapter>();   // registered by AddIronbeesIronhive

var settings = new OrchestratorSettings
{
    Type = OrchestratorType.Handoff,   // Sequential, Parallel, HubSpoke, Handoff, GroupChat, Graph
    InitialAgent = "triage",
    MaxTransitions = 10,
    Middleware = new MiddlewareSettings
    {
        Retry = new RetrySettings { MaxRetries = 3 },
        CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 5, BreakDuration = TimeSpan.FromSeconds(30) },
    },
};

// Which agents each agent may hand off to (Handoff only); without it no agent has a target to hand to.
var handoffs = new Dictionary<string, IReadOnlyList<HandoffTargetDefinition>>
{
    ["triage"] = [new() { AgentName = "billing", Description = "Invoices and payments" },
                  new() { AgentName = "tech-support", Description = "Technical problems" }],
};

var orchestrator = await adapter.CreateOrchestratorAsync(settings, agentConfigs, handoffs);   // agentConfigs: IReadOnlyList<AgentConfig>
await foreach (var evt in adapter.RunOrchestrationAsync(orchestrator, input, goalId, executionId))
{
    // AgentStarted, AgentCompleted, ..., GoalCompleted / GoalFailed
}
```

**Graph-based workflows** for complex pipelines:

```csharp
using Ironbees.Core.Orchestration;

var settings = new OrchestratorSettings
{
    Type = OrchestratorType.Graph,
    Graph = new GraphSettings
    {
        Nodes = [new() { Id = "analyze", Agent = "code-analyzer" }, new() { Id = "review", Agent = "reviewer" }],
        Edges = [new() { From = "analyze", To = "review", Condition = "needs_review" }],
        StartNode = "analyze",
        OutputNode = "review",
    },
};
```

There is no YAML loader for these settings — build the record in code (or bind it from your own configuration).

**Available Orchestrator Types**:
| Type | Use Case |
|------|----------|
| `Sequential` | Pipeline processing, output chains |
| `Parallel` | Concurrent analysis, fan-out tasks |
| `HubSpoke` | Central coordinator with specialists |
| `Handoff` | Context-aware agent switching |
| `GroupChat` | Multi-agent discussion with speaker selection |
| `Graph` | DAG workflows with conditional routing |

**Approval gate**: set `IronhiveOptions.ApprovalHandler` in `AddIronbeesIronhive` to be asked before each agent runs;
returning `false` stops the orchestration, which then fails.

## Token Tracking & Cost Estimation

`UseTokenTracking` adds a middleware to a Microsoft.Extensions.AI chat client pipeline. It records the token
usage each response reports (input, output and cached input tokens, as the provider counted them) and, with cost
tracking on, prices it with [TokenMeter](https://github.com/iyulab/TokenMeter)'s model catalog. A model the catalog
does not know is recorded without a cost.

```csharp
using Ironbees.Core.Middleware;       // UseTokenTracking, TokenTrackingOptions
using Microsoft.Extensions.AI;        // ChatClientBuilder, IChatClient
using TokenMeter;                     // CostCalculator

// Middleware pipeline with cost tracking; `innerClient` is your provider's IChatClient
IChatClient client = new ChatClientBuilder(innerClient)
    .UseTokenTracking(
        out InMemoryTokenUsageStore store,   // or pass your own ITokenUsageStore (e.g. FileSystemTokenUsageStore)
        new TokenTrackingOptions { EnableCostTracking = true },
        CostCalculator.Default())
    .Build();

// ... use `client` ...

// Query cost statistics
var stats = await store.GetStatisticsAsync();
Console.WriteLine($"Total cost: ${stats.TotalEstimatedCost:F4}");
Console.WriteLine($"By model: {string.Join(", ",
    stats.ByModel.Select(m => $"{m.Key}: ${m.Value.EstimatedCost:F4}"))}");
```

## Autonomous SDK

For iterative autonomous execution with oracle verification:

```csharp
using Ironbees.Autonomous;
using Ironbees.Autonomous.Abstractions;   // ITaskRequest, ITaskResult, ITaskExecutor, IOracleVerifier

var orchestrator = AutonomousOrchestrator.Create<MyRequest, MyResult>()
    .WithExecutor(executor)               // your ITaskExecutor<MyRequest, MyResult> (required)
    .WithRequestFactory((id, prompt) => new MyRequest(id, prompt))   // required
    .WithOracle(oracle)                   // your IOracleVerifier (optional)
    .Build();

orchestrator.OnEvent += evt => Console.WriteLine($"[{evt.Type}] {evt.Message}");

orchestrator.EnqueuePrompt("Find the failing test and fix it");
await orchestrator.StartAsync();          // by default returns once the queue is empty or MaxIterations is reached

public record MyRequest(string RequestId, string Prompt) : ITaskRequest;
public record MyResult(string RequestId, bool Success, string Output, string? ErrorOutput = null) : ITaskResult;
```

Settings can also come from a YAML file: `await AutonomousOrchestrator.FromSettingsFileAsync<MyRequest, MyResult>("settings.yaml")`
returns the builder with them applied. See the [Autonomous SDK guide](docs/autonomous-sdk-guide.md).

## Design Principles

- **Thin Wrapper** - Complement LLM frameworks, don't replace them
- **Convention over Configuration** - Filesystem structure defines behavior
- **Declaration vs Execution** - Ironbees declares patterns; backends execute them
- **Filesystem = Single Source of Truth** - All state observable via standard tools

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/ARCHITECTURE.md) | System design and interfaces |
| [Philosophy](docs/PHILOSOPHY.md) | Design principles and scope |
| [Autonomous SDK](docs/autonomous-sdk-guide.md) | Autonomous execution guide |
| [Agentic Patterns](docs/AGENTIC-PATTERNS.md) | HITL, sampling, confidence |
| [Providers](docs/PROVIDERS.md) | LLM provider configuration |

## Samples

| Sample | Description |
|--------|-------------|
| [OpenAISample](samples/OpenAISample/) | Basic OpenAI usage |
| [GpuStackSample](samples/GpuStackSample/) | Local GPU infrastructure |
| [EmbeddingSample](samples/EmbeddingSample/) | ONNX embedding and semantic routing |
| [TwentyQuestionsSample](samples/TwentyQuestionsSample/) | Autonomous SDK demo |

## Contributing

Issues and PRs welcome. Please maintain the thin wrapper philosophy.

## License

MIT License - See [LICENSE](LICENSE)
