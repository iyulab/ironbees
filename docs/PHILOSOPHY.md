# Ironbees Design Philosophy

## Core Principle: Declaration vs Execution

Ironbees follows a fundamental principle that shapes every design decision:

> **"Declaration in Ironbees, Execution in MAF"**

This means:

```
┌─────────────────────────────┐      ┌──────────────────────────────┐
│   Ironbees Responsibility   │      │    MAF Responsibility        │
├─────────────────────────────┤      ├──────────────────────────────┤
│ YAML Schema Definition      │  →   │ State Machine Execution      │
│ Event Type Definition       │  →   │ Agent Orchestration          │
│ Workflow Template Parsing   │  →   │ Tool/MCP Integration         │
│ MAF Workflow Conversion     │  →   │ Memory/Context Management    │
│ Interface Abstractions      │  →   │ Business Logic Implementation│
└─────────────────────────────┘      └──────────────────────────────┘
```

### What This Means in Practice

**✅ Ironbees SHOULD provide**:
- Declarative patterns (YAML schemas, configuration types)
- Interface abstractions (IOracleVerifier, ITaskExecutor, ICheckpointStore)
- Convention-based loading (filesystem-based agent discovery)
- Integration adapters (bridging to MAF, Semantic Kernel, etc.)
- Developer experience (boilerplate reduction, DI helpers)

**❌ Ironbees SHOULD NOT provide**:
- Workflow execution engines (state machines, task schedulers)
- Complex orchestration logic (DAG scheduling, parallel execution)
- Business logic implementations (sampling algorithms, confidence calculations)
- Framework features (conversation management, tool execution, memory)

## The Three Philosophies

### 1. Thin Wrapper Philosophy

**Principle**: Complement LLM frameworks, don't replace them.

Ironbees is intentionally **NOT**:
- A full-featured agent framework
- A workflow orchestration engine
- A conversation management system
- A tool execution platform

Ironbees **IS**:
- A convention-based configuration layer
- A multi-framework integration bridge
- A boilerplate reduction toolkit
- A declarative pattern library

**Example - What Belongs Where**:

```csharp
// ✅ GOOD: Ironbees provides the interface
public interface IOracleVerifier
{
    Task<OracleVerdict> VerifyAsync(string prompt, string output);
}

// ✅ GOOD: User implements the logic
public class OpenAIOracleVerifier : IOracleVerifier
{
    public async Task<OracleVerdict> VerifyAsync(string prompt, string output)
    {
        // User's business logic here
    }
}

// ❌ BAD: Ironbees implementing provider-specific logic
public class IronbeesBuiltInOpenAIOracleVerifier : IOracleVerifier
{
    // This couples Ironbees to OpenAI - violates thin wrapper
}
```

### 2. Convention over Configuration

**Principle**: Filesystem structure defines behavior.

Instead of complex configuration files, Ironbees uses **observable filesystem conventions**:

```
agents/{agent-name}/
├── agent.yaml           # Metadata → auto-discovered
├── system-prompt.md     # Prompt → auto-loaded
├── memory/              # State → auto-persisted
└── workspace/           # Temp files → auto-managed
```

**Benefits**:
- `ls agents/` shows all available agents
- `cat agents/*/agent.yaml` reveals all configurations
- No hidden state in databases
- Version control friendly
- Developer-friendly debugging

### 3. Filesystem = Single Source of Truth

**Principle**: All state must be observable via standard Unix tools.

```bash
# What agents exist?
ls agents/

# What are their capabilities?
grep -r "capabilities:" agents/*/agent.yaml

# What workflows are defined?
find workflows/ -name "*.yaml"

# What's the current execution state?
cat workflows/checkpoints/latest.json
```

No hidden state in:
- ❌ Databases
- ❌ In-memory caches
- ❌ Binary files
- ❌ External services

Everything is:
- ✅ Text files (YAML, Markdown, JSON)
- ✅ Filesystem-based
- ✅ Human-readable
- ✅ Version control friendly

## When to Add Features: The Decision Framework

Before adding any feature to Ironbees, ask these questions:

### Question 1: Declaration or Execution?

**Is this feature about DECLARING a pattern or EXECUTING logic?**

```
Declaration → Ironbees        Execution → MAF/User Code
├─ YAML schema design         ├─ State machine runtime
├─ Event type definition      ├─ Workflow orchestration
├─ Interface abstraction      ├─ Business logic
└─ Configuration model        └─ Algorithm implementation
```

**Examples**:

| Feature | Type | Belongs In |
|---------|------|------------|
| YAML workflow schema | Declaration | Ironbees ✅ |
| DAG task scheduler | Execution | MAF ❌ |
| ICheckpointStore interface | Declaration | Ironbees ✅ |
| Checkpoint storage logic | Execution | User Code ❌ |
| AgenticSettings type | Declaration | Ironbees ✅ |
| Sampling algorithm | Execution | User Code ❌ |

### Question 2: Does it belong in YAML or Code?

**Configuration vs Implementation**

```yaml
# ✅ YAML: Declarative configuration
orchestration:
  max_iterations: 10
  oracle:
    enabled: true
  confidence:
    min_threshold: 0.8
```

```csharp
// ✅ Code: Implementation logic
public async Task<OracleVerdict> VerifyAsync(string prompt, string output)
{
    // Complex verification logic that can't be declared
}
```

**Guidelines**:
- Thresholds, limits, flags → YAML
- Algorithms, business rules, complex logic → Code
- Static configuration → YAML
- Dynamic behavior → Code

### Question 3: Would this reimplement MAF/SK functionality?

**Duplication Detection**

Before implementing:
1. Check if MAF/Semantic Kernel already provides this
2. If yes, provide an **adapter/integration**, not a reimplementation
3. If no, verify it's truly a cross-framework concern

**Example - Workflow Orchestration**:

```
❌ BAD: Reimplement in Ironbees
public class IronbeesWorkflowEngine
{
    public async Task ExecuteDAG(TaskGraph graph) { ... }
}

✅ GOOD: Integrate with MAF
public class MafWorkflowConverter
{
    public MafWorkflow Convert(YamlWorkflow yaml) { ... }
}
```

## Design Patterns in Ironbees

### 1. Adapter Pattern (Multi-Framework Integration)

```csharp
// Ironbees provides the interface
public interface ILLMFrameworkAdapter { }

// User or Ironbees provides adapters
public class MicrosoftAgentFrameworkAdapter : ILLMFrameworkAdapter { }
public class SemanticKernelAdapter : ILLMFrameworkAdapter { }
```

### 2. Builder Pattern (Developer Experience)

```csharp
// Fluent API for complex configuration
var orchestrator = AutonomousOrchestrator.Create<Request, Result>()
    .WithExecutor(executor)
    .WithOracle(oracle)
    .WithMaxIterations(10)
    .Build();
```

### 3. Template Method (Declarative Patterns)

```yaml
# Template defines the pattern
name: "agentic-loop"
states:
  - id: START
    next: SAMPLE
  - id: SAMPLE
    next: ANALYZE
  # ... MAF executes the pattern
```

## Scope Boundaries by Example

### ✅ IN-SCOPE: Declarations and Abstractions

**Workflow Schema Definition**:
```yaml
# Ironbees defines the schema
workflow:
  name: data-processing
  states:
    - id: process
      agent: processor
```

**Interface Abstractions**:
```csharp
public interface IContextAwareOracleVerifier
{
    Task<EnhancedOracleVerdict> VerifyAsync(OracleContext context, string output);
}
```

**YAML Configuration Loading**:
```csharp
var settings = await OrchestratorSettings.LoadFromFileAsync("settings.yaml");
```

**Event Type System**:
```csharp
public record TaskCompletedEvent : AutonomousEvent
{
    public bool Success { get; init; }
    public TimeSpan Duration { get; init; }
}
```

### ❌ OUT-OF-SCOPE: Execution and Business Logic

**DAG Task Scheduling**:
```csharp
// This belongs in MAF, not Ironbees
public class TaskScheduler
{
    public async Task ExecuteDAG(TaskGraph graph) { ... }
}
```

**Multi-Execution Coordination**:
```csharp
// This belongs in application layer
public class ExecutionCoordinator
{
    private Dictionary<string, Orchestrator> _activeExecutions;
    public Task<string> StartExecutionAsync(Workflow workflow) { ... }
}
```

**Oracle Verification Logic**:
```csharp
// Interface in Ironbees, implementation in user code
public class CustomOracleVerifier : IOracleVerifier
{
    public async Task<OracleVerdict> VerifyAsync(string prompt, string output)
    {
        // User's complex verification logic
    }
}
```

**Sampling Algorithms**:
```csharp
// Configuration in YAML, logic in user code
public class ProgressiveSamplingExecutor : ITaskExecutor<Request, Result>
{
    public async Task<Result> ExecuteAsync(Request request)
    {
        // User's sampling strategy implementation
    }
}
```

## Why These Boundaries Matter

### 1. Maintainability

**With Clear Boundaries**:
- Small, focused codebase
- Easy to understand
- Quick to debug
- Minimal breaking changes

**Without Boundaries**:
- Ever-growing complexity
- Unclear responsibilities
- High maintenance burden
- Frequent breaking changes

### 2. Flexibility

**Thin Wrapper Approach**:
```csharp
// User can easily swap implementations
.WithOracle(new OpenAIOracleVerifier())   // Today
.WithOracle(new AnthropicOracleVerifier()) // Tomorrow
.WithOracle(new CustomOracleVerifier())    // Next week
```

**Monolithic Framework Approach**:
```csharp
// Locked into framework's implementation
.WithBuiltInOracle(BuiltInOracleType.OpenAI) // Can't customize
```

### 3. Ecosystem Compatibility

Ironbees plays well with:
- Microsoft Agent Framework
- Semantic Kernel
- LangChain
- Custom LLM frameworks

Because it doesn't try to replace them.

## Common Questions

### "Why not make Ironbees a full-featured framework?"

**Short Answer**: That's what MAF/SK are for.

**Long Answer**:
- LLM framework space is rapidly evolving
- Microsoft Agent Framework is the strategic choice for .NET
- Ironbees adds value by **simplifying**, not **replacing**
- Thin wrapper = sustainable, full framework = unsustainable

### "Why can't Ironbees handle DAG task scheduling?"

**Answer**: It can, via MAF integration.

Ironbees provides:
- YAML schema for declaring task dependencies
- Conversion to MAF workflow format
- Integration layer for MAF execution

MAF handles:
- Actual DAG execution
- State machine management
- Parallel task scheduling

This separation keeps Ironbees maintainable while providing full workflow capabilities.

### "Where should multi-execution coordination live?"

**Answer**: Application layer.

Different applications have different needs:
- Single-server in-memory coordination
- Distributed coordination with external queue
- Cloud-native serverless execution

Ironbees provides:
- Reference pattern in `samples/Patterns/MultiExecutionPattern/`
- Documentation on coordination strategies
- Interface abstractions for state management

Application implements:
- Specific coordination logic
- Scaling strategy
- Resource management

### "Can I still use Ironbees for complex workflows?"

**Absolutely! Use the right layer:**

```
Simple Iterative        → AutonomousOrchestrator
Complex YAML Workflows  → YamlDrivenOrchestrator + MAF
Full Orchestration      → MAF directly
```

Ironbees provides **integration and abstraction** for all these scenarios.

## Related Documents

- [Architecture](./ARCHITECTURE.md) - System architecture and layer diagram
- [Agentic Patterns](./AGENTIC-PATTERNS.md) - Declarative agentic pattern examples
- [README](../README.md) - Getting started guide
- [CLAUDE.md](../CLAUDE.md) - Development guidelines

---

**Remember**: When in doubt, ask:
1. Is this declaration or execution?
2. Does this belong in YAML or code?
3. Would this reimplement framework functionality?

If the answer to #3 is yes, it doesn't belong in Ironbees.
