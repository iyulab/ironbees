# Ironbees Architecture

**Target**: .NET 10.0

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
│   └── Guardrails/                 # Content validation
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
├── Ironbees.AgentMode/             # YAML workflow definitions
│   └── Workflow/                   # YamlDrivenOrchestrator
│
└── Ironbees.Autonomous/            # Autonomous execution SDK
    ├── Abstractions/               # Core interfaces
    │   ├── IOracleVerifier         # Completion verification
    │   ├── ITaskExecutor           # Task execution
    │   ├── IHumanInTheLoop         # HITL interface
    │   ├── IAutonomousContextProvider  # Context management
    │   └── IAutonomousMemoryStore  # Memory storage
    ├── Context/                    # Context management
    │   └── DefaultContextManager   # Built-in context tracking
    ├── Configuration/              # YAML configuration
    │   └── OrchestratorSettings    # Settings loader
    └── Models/                     # Domain models
        ├── AutonomousEvent         # Event system
        └── OracleVerdict           # Oracle responses
```

## Key Interfaces

| Interface | Purpose | Implementation |
|-----------|---------|----------------|
| `IAgentLoader` | Load agents from filesystem | `FileSystemAgentLoader` |
| `IAgentSelector` | Route requests to agents | `KeywordAgentSelector`, `EmbeddingAgentSelector`, `HybridAgentSelector` |
| `ILLMFrameworkAdapter` | Bridge to LLM frameworks | `AgentFrameworkAdapter` |
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

### External Adapters

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
- Guardrails & content validation
- Audit logging for compliance

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

## Workflow Decision Guide

Ironbees provides three layers for different workflow complexity levels:

### When to Use AutonomousOrchestrator (Ironbees.Autonomous)

**Use for**: Simple iterative autonomous execution

✅ **Good fit when**:
- Self-directed agents with oracle verification
- Sequential task execution with auto-continuation
- Iterative improvement patterns (e.g., 20 Questions game)
- Simple autonomous workflows with YAML configuration

**Configuration**:
```csharp
// Code-based configuration
var orchestrator = AutonomousOrchestrator.Create<Request, Result>()
    .WithExecutor(executor)
    .WithOracle(oracle)
    .WithMaxIterations(10)
    .Build();

// YAML-based configuration
var settings = await OrchestratorSettings.LoadFromFileAsync("settings.yaml");
var orchestrator = AutonomousOrchestrator.Create<Request, Result>()
    .WithSettings(settings)
    .WithExecutor(executor)
    .Build();
```

**YAML Configuration Example** (`settings.yaml`):
```yaml
orchestration:
  max_iterations: 10
  completion_mode: until_goal_achieved

  oracle:
    enabled: true
    max_iterations: 3

  confidence:
    min_threshold: 0.8
    human_review_threshold: 0.5

  context:
    enable_tracking: true
    max_learnings: 5

  auto_continue:
    enabled: true
    prompt_template: "Continue iteration {iteration}"
```

**Examples**:
- 20 Questions game (`samples/TwentyQuestionsSample/`)
- Automated data processing
- Simple agent conversations

### When to Use YamlDrivenOrchestrator (Ironbees.AgentMode)

**Use for**: Complex multi-agent workflows with state machines

✅ **Good fit when**:
- State machine workflows with branching logic
- Human-in-the-Loop (HITL) checkpoints
- Agentic patterns (sampling, confidence thresholds)
- Multi-agent collaboration workflows

**Configuration**:
```yaml
# workflow.yaml
name: "data-processing-workflow"
version: "1.0"

states:
  - id: START
    type: start
    next: SAMPLE

  - id: SAMPLE
    type: action
    agent: sampling-agent
    next: ANALYZE

  - id: ANALYZE
    type: action
    agent: analysis-agent
    next: HITL_CHECKPOINT

  - id: HITL_CHECKPOINT
    type: hitl
    requestType: Review
    options:
      - id: approve
        label: "Approve"
      - id: reject
        label: "Reject"
    next:
      approve: SUCCESS
      reject: FAILED
```

**Examples**:
- Data preprocessing with human approval gates (`workflows/templates/agentic-loop.yaml`)
- Multi-step validation workflows
- Complex decision trees

### When to Use MAF Directly

**Use for**: Full workflow orchestration with advanced features

✅ **Good fit when**:
- DAG task dependencies and parallel execution
- Advanced state management
- Tool execution and MCP integration
- Complex memory and context management

**Integration**:
```csharp
// Ironbees converts YAML → MAF workflow
var converter = new MafWorkflowConverter();
var mafWorkflow = converter.Convert(yamlWorkflow);

// Execute via MAF
var executor = new MafWorkflowExecutor();
await executor.ExecuteAsync(mafWorkflow);
```

**Examples**:
- Multi-step parallel data pipelines
- Complex tool orchestration
- Advanced agent collaboration

## Decision Tree

```
┌─ Need workflow orchestration?
│
├─ YES → Complex multi-step?
│        │
│        ├─ NO → Simple iterations?
│        │        │
│        │        ├─ YES → AutonomousOrchestrator ✅
│        │        │        (+ OrchestratorSettings YAML)
│        │        │
│        │        └─ NO → State machine required?
│        │                 │
│        │                 └─ YES → YamlDrivenOrchestrator ✅
│        │                          (+ MAF integration)
│        │
│        └─ YES → Parallel tasks / DAG?
│                 │
│                 └─ YES → MAF directly ✅
│                          (via MafWorkflowConverter)
│
└─ NO → Single agent interaction
         → Use IAgentOrchestrator (Ironbees.Core)
```

## Scope Boundaries

**Ironbees Handles**:
- Filesystem convention-based agent loading
- Intelligent agent routing (keyword/embedding/hybrid)
- Multi-LLM framework integration (Adapter pattern)
- YAML workflow definition and parsing
- **YAML configuration for Autonomous SDK** (OrchestratorSettings)
- **Interface abstractions** (IOracleVerifier, ITaskExecutor, IContextAwareOracleVerifier)
- Token tracking / cost monitoring
- Conversation state management
- Middleware pipeline
- Guardrails & content validation
- Audit logging for compliance
- **Context management abstractions** (DefaultContextManager)

**Delegated to MAF**:
- Complex workflow execution (state machines)
- DAG task scheduling and parallel execution
- Tool execution engine
- Conversation memory
- MCP integration
- **Business logic implementation** (sampling algorithms, confidence calculations)

**Delegated to Application Layer**:
- Multi-execution coordination
- Multi-tenancy management
- Scaling and load balancing
- Custom orchestration logic

**Delegated to External Services**:
- AI-based content moderation → Azure AI Content Safety, OpenAI Moderation

## Next Steps

- [Philosophy](./PHILOSOPHY.md) - Design principles and scope boundaries
- [README](../README.md) - Getting started and quick examples
- [Agentic Patterns](./AGENTIC-PATTERNS.md)
- [Autonomous SDK Guide](./autonomous-sdk-guide.md)
- [LLM Providers](./PROVIDERS.md)
- [Deployment](./DEPLOYMENT.md)
