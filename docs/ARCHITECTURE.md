# Ironbees Architecture

**Version**: 0.1.9 | **Target**: .NET 10.0

## Overview

Ironbees is a **Thin Wrapper** LLM agent framework that simplifies agent loading, routing, and multi-framework integration using filesystem conventions.

**Core Philosophy**:
- **Thin Wrapper**: Complement LLM frameworks, don't replace them
- **Convention over Configuration**: `agents/{name}/agent.yaml` = agent definition
- **Filesystem = Single Source of Truth**: All state observable via `ls`, `grep`, `cat`

## System Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Application Layer                     │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│              Ironbees Orchestration Layer                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ Agent Loader │→ │Agent Selector│→ │  Middleware  │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│           Microsoft Agent Framework (MAF)                │
│  - AIAgent execution    - Tool integration               │
│  - Workflow engine      - Context management             │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│                   LLM Providers                          │
│  Azure OpenAI | OpenAI | Anthropic | Self-hosted         │
└─────────────────────────────────────────────────────────┘
```

## Project Structure

```
src/
├── Ironbees.Core/                  # Core abstractions
│   ├── Middleware/                 # IChatClient pipeline
│   ├── Conversation/               # FileSystemConversationStore
│   ├── Streaming/                  # StreamChunk types
│   └── AgentDirectory/             # Filesystem agent loading
│
├── Ironbees.AgentFramework/        # MAF integration
│   └── Workflow/                   # Workflow converter, executor, checkpoint
│
└── Ironbees.AgentMode.Core/        # YAML workflow definitions
    └── Workflow/                   # YamlDrivenOrchestrator
```

## Key Interfaces

| Interface | Purpose | Implementation |
|-----------|---------|----------------|
| `IAgentLoader` | Load agents from filesystem | `FileSystemAgentLoader` |
| `IAgentSelector` | Route requests to agents | `KeywordAgentSelector`, `EmbeddingAgentSelector` |
| `ILLMFrameworkAdapter` | Bridge to LLM frameworks | `MicrosoftAgentFrameworkAdapter` |
| `IWorkflowOrchestrator<T>` | Execute YAML workflows | `YamlDrivenOrchestrator` |
| `IWorkflowConverter` | YAML → MAF conversion | `MafWorkflowConverter` |
| `ICheckpointStore` | Workflow persistence | `FileSystemCheckpointStore` |

## Agent Filesystem Convention

```
agents/{agent-name}/
├── agent.yaml           # Required: metadata and model config
├── system-prompt.md     # Required: system prompt
├── inbox/               # Optional: incoming messages
├── outbox/              # Optional: output messages
├── memory/              # Optional: long-term storage
└── workspace/           # Optional: temporary files
```

**agent.yaml**:
```yaml
name: coding-agent
description: Expert software developer
version: 1.0.0
model:
  deployment: gpt-4o
  temperature: 0.7
  maxTokens: 2000
capabilities: [code-generation, code-review]
tags: [coding, development]
```

## Middleware Pipeline

Request flow through IChatClient middleware:

```
Request → [Logging] → [TokenTracking] → [RateLimiting] → [Resilience] → [Caching] → [LLM]
```

Uses `DelegatingChatClient` pattern from `Microsoft.Extensions.AI`.

## Workflow System

YAML workflows are converted to MAF workflows for execution:

```
YAML Definition → YamlWorkflowLoader → MafWorkflowConverter → MAF Workflow → Executor
```

**Workflow Types**:
- **Sequential**: Agents execute one after another
- **Parallel**: Multiple agents execute concurrently
- **Mixed**: Combines sequential and parallel execution

## Scope Boundaries

**Ironbees Handles**:
- Filesystem convention-based agent loading
- Intelligent agent routing (keyword/embedding/hybrid)
- Multi-LLM framework integration (Adapter pattern)
- YAML workflow definition and parsing
- Token tracking / cost monitoring
- Conversation state management
- Middleware pipeline

**Delegated to MAF**:
- Complex workflow execution
- Tool execution engine
- Conversation memory
- MCP integration

## Dependencies

```xml
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.*" />
<PackageReference Include="Microsoft.Extensions.AI" Version="10.0.1" />
<PackageReference Include="YamlDotNet" Version="16.3.0" />
<PackageReference Include="Polly" Version="8.6.5" />
```

## Next Steps

- [Quick Start](./QUICKSTART.md)
- [Agentic Patterns](./AGENTIC-PATTERNS.md)
- [LLM Providers](./PROVIDERS.md)
- [Deployment](./DEPLOYMENT.md)
