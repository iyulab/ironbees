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
| **Cost Tracking** | Accurate token counting and cost estimation via [TokenMeter](https://github.com/iyulab/TokenMeter) |

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
  (Azure OpenAI + MAF)      (IronHive multi-provider)

              Ironbees.Autonomous
        (Iterative execution, oracle verification)
```

Ironbees Core is **backend-agnostic**. Pick the adapter that fits your stack:

- **Ironbees.Ironhive** - Multi-provider (OpenAI, Anthropic, Google, Ollama) via [IronHive](https://github.com/iyulab/ironhive)
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

## Quick Start

### With IronHive

```csharp
services.AddIronbeesIronhive(options =>
{
    options.AgentsDirectory = "./agents";
    options.ConfigureHive = hive =>
    {
        hive.AddMessageGenerator("openai", new OpenAIMessageGenerator(apiKey));
    };
});
```

### With Azure OpenAI

```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
});
```

### Use the Agent

```csharp
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync();

// Explicit agent selection
var response = await orchestrator.ProcessAsync(
    "Write a C# fibonacci function",
    agentName: "coding-agent");

// Automatic routing
var response = await orchestrator.ProcessAsync(
    "fibonacci in C#"); // Routes based on keywords/embeddings

// Streaming
await foreach (var chunk in orchestrator.StreamAsync("Write a blog post"))
{
    Console.Write(chunk);
}
```

## Token Tracking & Cost Estimation

Ironbees integrates [TokenMeter](https://github.com/iyulab/TokenMeter) for accurate tiktoken-based token counting and cost estimation across 40+ models (OpenAI, Anthropic, Google, xAI, Azure).

```csharp
// Middleware pipeline with cost tracking
var builder = new ChatClientBuilder(innerClient)
    .UseTokenTracking(
        store,
        new TokenTrackingOptions { EnableCostTracking = true },
        CostCalculator.Default());

// Query cost statistics
var stats = await store.GetStatisticsAsync();
Console.WriteLine($"Total cost: ${stats.TotalEstimatedCost:F4}");
Console.WriteLine($"By model: {string.Join(", ",
    stats.ByModel.Select(m => $"{m.Key}: ${m.Value.EstimatedCost:F4}"))}");
```

## Autonomous SDK

For iterative autonomous execution with oracle verification:

```csharp
var orchestrator = AutonomousOrchestrator.Create<Request, Result>()
    .WithSettings(settings)
    .WithExecutor(executor)
    .WithOracle(oracle)
    .Build();

await foreach (var evt in orchestrator.StartAsync(request))
{
    Console.WriteLine($"[{evt.Type}] {evt.Message}");
}
```

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
