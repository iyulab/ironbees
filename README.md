# Ironbees

[![CI](https://github.com/iyulab/ironbees/actions/workflows/ci.yml/badge.svg)](https://github.com/iyulab/ironbees/actions/workflows/ci.yml)
[![NuGet - Core](https://img.shields.io/nuget/v/Ironbees.Core?label=Ironbees.Core)](https://www.nuget.org/packages/Ironbees.Core)
[![NuGet - AgentFramework](https://img.shields.io/nuget/v/Ironbees.AgentFramework?label=Ironbees.AgentFramework)](https://www.nuget.org/packages/Ironbees.AgentFramework)
[![License](https://img.shields.io/github/license/iyulab/ironbees)](LICENSE)

> Filesystem convention-based LLM agent management wrapper for .NET

Ironbees is a lightweight wrapper that simplifies **repetitive patterns** in LLM agent development. It doesn't replace frameworks like Microsoft Agent Framework or Semantic Kernel—it **complements them** by providing filesystem conventions for agent management.

## Key Features

- **Filesystem Convention**: Define agents via `agents/{name}/agent.yaml` + `system-prompt.md`
- **Intelligent Routing**: Keyword, embedding, and hybrid agent selection
- **Multi-Framework Support**: Microsoft Agent Framework, OpenAI, and custom providers
- **YAML Workflows**: Declarative workflow definitions with MAF integration
- **Guardrails**: Content validation with Azure AI and OpenAI Moderation support
- **Autonomous SDK**: Iterative autonomous execution with oracle verification

## Installation

```bash
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework  # For Azure OpenAI + MAF
```

## Quick Start

### 1. Define an Agent

```
agents/
└── coding-agent/
    ├── agent.yaml          # Agent metadata
    └── system-prompt.md    # System prompt
```

**agents/coding-agent/agent.yaml:**
```yaml
name: coding-agent
description: Expert software developer
capabilities: [code-generation, code-review]
model:
  deployment: gpt-4o
  temperature: 0.7
```

**agents/coding-agent/system-prompt.md:**
```markdown
You are an expert software developer specializing in C# and .NET...
```

### 2. Configure Services

```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
});
```

### 3. Use the Agent

```csharp
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync();

// Explicit agent selection
var response = await orchestrator.ProcessAsync(
    "Write a C# fibonacci function",
    agentName: "coding-agent");

// Automatic routing
var response = await orchestrator.ProcessAsync(
    "fibonacci in C#"); // Routes based on keywords

// Streaming
await foreach (var chunk in orchestrator.StreamAsync("Write a blog post"))
{
    Console.Write(chunk);
}
```

## Autonomous SDK

For iterative autonomous execution with oracle verification:

**settings.yaml:**
```yaml
orchestration:
  max_iterations: 10
  oracle:
    enabled: true
  confidence:
    min_threshold: 0.8
```

```csharp
var settings = await OrchestratorSettings.LoadFromFileAsync("settings.yaml");

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

## Architecture

```
┌─────────────────────────────────────────────┐
│   Ironbees (Thin Wrapper)                   │
│   - FileSystemAgentLoader                   │
│   - Agent Routing (Keyword/Embedding)       │
│   - Guardrails Pipeline                     │
├─────────────────────────────────────────────┤
│   Microsoft Agent Framework / LLM Providers │
│   - Agent execution                         │
│   - Tool integration                        │
│   - Conversation management                 │
└─────────────────────────────────────────────┘
```

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/ARCHITECTURE.md) | System design and interfaces |
| [Philosophy](docs/PHILOSOPHY.md) | Design principles and scope |
| [Autonomous SDK](docs/autonomous-sdk-guide.md) | Autonomous execution guide |
| [Agentic Patterns](docs/AGENTIC-PATTERNS.md) | HITL, sampling, confidence |
| [Providers](docs/PROVIDERS.md) | LLM provider configuration |
| [FAQ](docs/FAQ.md) | Common questions |

## Samples

| Sample | Description |
|--------|-------------|
| [OpenAISample](samples/OpenAISample/) | Basic OpenAI usage |
| [GpuStackSample](samples/GpuStackSample/) | Local GPU infrastructure |
| [EmbeddingSample](samples/EmbeddingSample/) | ONNX embedding and semantic routing |
| [TwentyQuestionsSample](samples/TwentyQuestionsSample/) | Autonomous SDK demo |

## Design Principles

- **Thin Wrapper**: Complement LLM frameworks, don't replace them
- **Convention over Configuration**: Filesystem structure defines behavior
- **Filesystem = Single Source of Truth**: All state observable via `ls`, `grep`, `cat`

## Contributing

Issues and PRs welcome. Please maintain the thin wrapper philosophy.

## License

MIT License - See [LICENSE](LICENSE)

---

**Ironbees** - Filesystem convention-based LLM agent wrapper for .NET
