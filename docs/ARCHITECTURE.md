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
│        LLM framework adapter (ILLMFrameworkAdapter)      │
│  - Ironbees.Ironhive: IronHive agents, tools,            │
│    multi-agent orchestration                             │
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
│   ├── Orchestration/              # Multi-agent orchestration models
│   │   ├── OrchestratorSettings    # Declarative orchestrator config
│   │   ├── GraphSettings           # DAG workflow configuration
│   │   └── MiddlewareSettings      # Resilience middleware config
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
├── Ironbees.AgentMode/             # YAML workflow definitions
│   └── Workflow/                   # YamlDrivenOrchestrator
│
├── Ironbees.Ironhive/              # IronHive multi-provider integration
│   ├── Orchestration/              # Multi-agent orchestration
│   │   ├── IronhiveOrchestratorFactory   # Creates orchestrators from settings
│   │   ├── IronhiveOrchestratorWrapper   # IronHive → Ironbees adapter
│   │   ├── IronhiveEventAdapter          # Event type conversion
│   │   └── IronhiveMiddlewareFactory     # Middleware from settings
│   └── Checkpoint/                 # Checkpoint store adapter
│       └── IronhiveCheckpointStoreAdapter
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
    │   └── AutonomousSettings      # Settings and their loader
    └── Models/                     # Domain models
        ├── AutonomousEvent         # Event system
        └── OracleVerdict           # Oracle responses
```

## Key Interfaces

| Interface | Purpose | Implementation |
|-----------|---------|----------------|
| `IAgentLoader` | Load agents from filesystem | `FileSystemAgentLoader` |
| `IAgentSelector` | Route requests to agents | `KeywordAgentSelector`, `EmbeddingAgentSelector`, `HybridAgentSelector` |
| `ILLMFrameworkAdapter` | Bridge to LLM frameworks | `IronhiveAdapter` |
| `IMultiAgentOrchestrator` | Multi-agent orchestration | `IronhiveOrchestratorWrapper` |
| `IIronhiveOrchestratorFactory` | Create orchestrators from settings | `IronhiveOrchestratorFactory` |
| `IWorkflowOrchestrator<T>` | Execute YAML workflows | `YamlDrivenOrchestrator` |
| `ICheckpointStore` (Core) | Orchestration persistence | `FileSystemIronhiveCheckpointStore`, `IronhiveCheckpointStoreAdapter` |
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

## Multi-Agent Orchestration (IronHive)

Ironbees.Ironhive provides declarative multi-agent orchestration via IronHive SDK:

```
OrchestratorSettings (YAML) → IronhiveOrchestratorFactory → IronHive Orchestrator → IronhiveOrchestratorWrapper
```

### Orchestrator Types

| Type | Builder | Description |
|------|---------|-------------|
| `Sequential` | `SequentialOrchestrator` | Agents execute in order, output chains |
| `Parallel` | `ParallelOrchestrator` | Concurrent execution, aggregated results |
| `HubSpoke` | `HubSpokeOrchestrator` | Central hub coordinates specialists |
| `Handoff` | `HandoffOrchestratorBuilder` | Context-aware agent switching |
| `GroupChat` | `GroupChatOrchestratorBuilder` | Multi-agent discussion |
| `Graph` | `GraphOrchestratorBuilder` | DAG with conditional edges |

### Middleware Stack

Resilience middleware applied to orchestrators (order matters):

```
Request → [Logging] → [RateLimit] → [Bulkhead] → [CircuitBreaker] → [Retry] → [Timeout] → Agent
```

**Configuration Example**:
```yaml
orchestrator:
  type: Handoff
  initialAgent: triage
  middleware:
    enableLogging: true
    rateLimit:
      maxRequests: 60
      window: 1m
    bulkhead:
      maxConcurrency: 10
    circuitBreaker:
      failureThreshold: 5
      breakDuration: 30s
    retry:
      maxRetries: 3
    timeout:
      duration: 30s
```

### Event Streaming

IronHive events are converted to Ironbees events via `IronhiveEventAdapter`:

| IronHive Event | Ironbees Event |
|----------------|----------------|
| `Started` | `OrchestrationStartedEvent` |
| `AgentStarted` | `AgentStartedEvent` |
| `MessageDelta` | `MessageDeltaEvent` |
| `AgentCompleted` | `AgentCompletedEvent` |
| `Completed` | `OrchestrationCompletedEvent` |
| `Failed` | `OrchestrationFailedEvent` |
| `Handoff` | `HandoffEvent` |
| `SpeakerSelected` | `SpeakerSelectedEvent` |

## Middleware Pipeline

Request flow through IChatClient middleware:

```
Request → [Logging] → [TokenTracking] → [RateLimiting] → [Resilience] → [Caching] → [LLM]
```

Uses `DelegatingChatClient` pattern from `Microsoft.Extensions.AI`.

## Workflow System

YAML workflows are state machines that `YamlDrivenOrchestrator` executes; each agent state runs through the
`IAgentExecutorFactory` the application supplies:

```
YAML Definition → YamlWorkflowLoader (parse + validate) → YamlDrivenOrchestrator → IAgentExecutorFactory → agents
```

**State Types**:
- **start** / **terminal**: Entry and end of the workflow
- **agent**: One agent runs, then the workflow moves to `next`
- **parallel**: Several agents run concurrently
- **human_gate**: The workflow waits for `ApproveAsync`
- **escalation**: Error handling and escalation

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
GuardrailResult result = await guardrail.ValidateInputAsync(userInput);
if (!result.IsAllowed)
{
    foreach (var violation in result.Violations)
    {
        Console.WriteLine($"[{violation.Severity}] {result.GuardrailName}: {violation.ViolationType} - {violation.Description}");
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

**Delegated to the LLM backend** (IronHive through `Ironbees.Ironhive`, or the framework behind your own adapter):
- Tool execution engine
- Conversation memory
- MCP integration

## Dependencies

The packages build on Microsoft.Extensions.AI, YamlDotNet and Polly; the orchestration layer builds on IronHive
(`IronHive.Abstractions`, `IronHive.Core`). The versions in use are the ones pinned in
[`Directory.Packages.props`](../Directory.Packages.props) — this page does not repeat them.

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
    .WithRequestFactory((id, prompt) => new Request(id, prompt))
    .WithOracle(oracle)
    .WithMaxIterations(10)
    .Build();

// YAML-based configuration
var fromYaml = (await AutonomousOrchestrator.FromSettingsFileAsync<Request, Result>("settings.yaml"))
    .WithExecutor(executor)
    .WithRequestFactory((id, prompt) => new Request(id, prompt))
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
- Data preprocessing with human approval gates (`IronhiveOptions.ApprovalHandler`)
- Multi-step validation workflows
- Complex decision trees

### When to Use IronHive Orchestration (Ironbees.Ironhive)

**Use for**: Multi-agent orchestration patterns

✅ **Good fit when**:
- DAG task dependencies (`OrchestratorType.Graph`) and parallel execution (`OrchestratorType.Parallel`)
- Hub-and-spoke delegation, handoff between agents, group discussion
- Resilience middleware around the agents (retry, circuit breaker, bulkhead, rate limit, timeout)

**Integration**: `IIronhiveOrchestratorFactory.CreateOrchestrator(settings, agents)` with an `OrchestratorSettings` - see
[Multi-Agent Orchestration](../README.md#multi-agent-orchestration) in the README.

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
│        │        │        (+ AutonomousSettings YAML)
│        │        │
│        │        └─ NO → State machine required?
│        │                 │
│        │                 └─ YES → YamlDrivenOrchestrator ✅
│        │                          (Ironbees.AgentMode)
│        │
│        └─ YES → Parallel tasks / DAG?
│                 │
│                 └─ YES → IronHive orchestration ✅
│                          (IIronhiveOrchestratorFactory, Ironbees.Ironhive)
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
- **YAML configuration for Autonomous SDK** (AutonomousSettings)
- **Interface abstractions** (IOracleVerifier, ITaskExecutor, IContextAwareOracleVerifier)
- Token tracking / cost monitoring
- Conversation state management
- Middleware pipeline
- Guardrails & content validation
- Audit logging for compliance
- **Context management abstractions** (DefaultContextManager)

**Delegated to the LLM backend** (IronHive through `Ironbees.Ironhive`, or the framework behind your own adapter):
- DAG task scheduling and parallel execution (IronHive orchestrators)
- Tool execution engine
- Conversation memory
- MCP integration

**Delegated to Application Layer**:
- **Business logic implementation** (sampling algorithms, confidence calculations)
- Multi-execution coordination
- Multi-tenancy management
- Scaling and load balancing
- Custom orchestration logic

**Delegated to External Services**:
- AI-based content moderation → Azure AI Content Safety, OpenAI Moderation

## Next Steps

- [Philosophy](./PHILOSOPHY.md) - Design principles and scope boundaries
- [README](../README.md) - Getting started and quick examples
- [Autonomous SDK Guide](./autonomous-sdk-guide.md)
- [LLM Providers](./PROVIDERS.md)
- [Deployment](./DEPLOYMENT.md)
