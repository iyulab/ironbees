# Ironbees Architecture

**Version**: 0.3.0 | **Target**: .NET 10.0

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
│                                                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │              Guardrails Pipeline                  │   │
│  │  Input → [Regex|Keyword|Length|Azure|OpenAI] → ✓  │   │
│  │  Output → [Regex|Keyword|Length|Azure|OpenAI] → ✓ │   │
│  └──────────────────────────────────────────────────┘   │
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
│   ├── AgentDirectory/             # Filesystem agent loading
│   └── Guardrails/                 # Content validation (v0.3.0)
│       ├── IContentGuardrail       # Core interface
│       ├── GuardrailPipeline       # Multi-guardrail orchestrator
│       ├── RegexGuardrail          # Pattern-based filtering
│       ├── KeywordGuardrail        # Blocked word detection
│       ├── LengthGuardrail         # DoS prevention
│       ├── AzureContentSafetyGuardrail  # Azure AI adapter
│       ├── OpenAIModerationGuardrail    # OpenAI adapter
│       └── IAuditLogger            # Compliance logging
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
| `IAgentSelector` | Route requests to agents | `KeywordAgentSelector`, `EmbeddingAgentSelector`, `HybridAgentSelector` |
| `ILLMFrameworkAdapter` | Bridge to LLM frameworks | `MicrosoftAgentFrameworkAdapter` |
| `IWorkflowOrchestrator<T>` | Execute YAML workflows | `YamlDrivenOrchestrator` |
| `IWorkflowConverter` | YAML → MAF conversion | `MafWorkflowConverter` |
| `ICheckpointStore` | Workflow persistence | `FileSystemCheckpointStore` |
| `IContentGuardrail` | Content validation | `RegexGuardrail`, `KeywordGuardrail`, `LengthGuardrail`, `AzureContentSafetyGuardrail`, `OpenAIModerationGuardrail` |
| `IAuditLogger` | Compliance logging | `NullAuditLogger`, custom implementations |

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

## Guardrails System

Content validation pipeline for input/output filtering:

```
Input → GuardrailPipeline → [Regex|Keyword|Length|Azure|OpenAI] → ✓ or Violation
                                       ↓
                                  IAuditLogger
```

### Built-in Guardrails

| Guardrail | Purpose | Configuration |
|-----------|---------|---------------|
| `RegexGuardrail` | PII detection (email, SSN, credit card) | `PatternDefinition[]` |
| `KeywordGuardrail` | Blocked word filtering | `blockedKeywords`, `WholeWordOnly` |
| `LengthGuardrail` | DoS prevention | `maxInputLength`, `maxOutputLength` |

### External Adapters (v0.3.0)

| Adapter | Service | Categories |
|---------|---------|------------|
| `AzureContentSafetyGuardrail` | Azure AI Content Safety | Hate, SelfHarm, Sexual, Violence |
| `OpenAIModerationGuardrail` | OpenAI Moderation API | 11 categories with score thresholds |

### DI Configuration

```csharp
services.AddGuardrails()
    .AddLengthGuardrail(maxInputLength: 10000)
    .AddKeywordGuardrail("forbidden", "blocked")
    .AddRegexGuardrail(new PatternDefinition { Pattern = @"\d{3}-\d{2}-\d{4}", Name = "SSN" })
    .AddAzureContentSafety(endpoint: "https://...", apiKey: "...")
    .AddOpenAIModeration(apiKey: "sk-...")
    .AddAuditLogger<CustomAuditLogger>()
    .Build();
```

### GuardrailResult

```csharp
if (!result.IsAllowed)
{
    foreach (var violation in result.Violations)
    {
        Console.WriteLine($"[{violation.Severity}] {violation.GuardrailName}: {violation.Message}");
    }
}
```

## Scope Boundaries

**Ironbees Handles**:
- Filesystem convention-based agent loading
- Intelligent agent routing (keyword/embedding/hybrid)
- Multi-LLM framework integration (Adapter pattern)
- YAML workflow definition and parsing
- Token tracking / cost monitoring
- Conversation state management
- Middleware pipeline
- **Guardrails & content validation** (v0.3.0)
- **Audit logging for compliance** (v0.3.0)

**Delegated to External Services**:
- AI-based content moderation → Azure AI Content Safety, OpenAI Moderation

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
