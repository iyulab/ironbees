# Ironbees FAQ (Frequently Asked Questions)

**Version**: 0.4.0 | **Updated**: 2026-01-06

## Workflow Configuration

### Q: Can I define workflows in YAML?

**A: Yes!** Ironbees supports YAML configuration in two ways:

**1. Autonomous SDK** - Use `OrchestratorSettings` for simple iterative workflows:

```yaml
# settings.yaml
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
    .Build();
```

**Example**: See `samples/MinimalAutonomousSample/settings.yaml`

**2. AgentMode** - Use YAML workflow definitions for complex state machines:

```yaml
# workflow.yaml
name: "data-processing"
states:
  - id: START
    next: PROCESS
  - id: PROCESS
    agent: processor
    next: REVIEW
```

**Example**: See `workflows/templates/agentic-loop.yaml`

**Decision Guide**: See [ARCHITECTURE.md - Workflow Decision Guide](./ARCHITECTURE.md#workflow-decision-guide)

---

### Q: How do I persist execution state?

**A:** Use checkpoint stores (available in AgentMode):

**File-Based Checkpointing** (Built-in):
```csharp
var checkpointStore = new FileSystemCheckpointStore("./checkpoints");
var orchestrator = new YamlDrivenOrchestrator(
    workflow,
    checkpointStore: checkpointStore);
```

**Custom Storage** (Implement interface):
```csharp
public class MyStateStore : ICheckpointStore
{
    public async Task SaveAsync(Checkpoint checkpoint)
    {
        // Your storage logic (SQL, Redis, etc.)
    }

    public async Task<Checkpoint?> LoadAsync(string workflowId)
    {
        // Your retrieval logic
    }
}
```

**For Autonomous SDK** (v0.4.1 planned):
- `IAutonomousStateStore` interface will be added
- See Issue #6 in triage report

**Philosophy**: Storage implementations belong in application code, not SDK core. This allows flexibility for different storage backends (file, SQL, NoSQL, cloud).

---

### Q: Can I run multiple workflows concurrently?

**A: Yes,** but this belongs in the **application layer**, not SDK.

**Pattern**: Create multiple orchestrator instances:

```csharp
public class ExecutionCoordinator
{
    private readonly ConcurrentDictionary<string, AutonomousOrchestrator<Request, Result>>
        _activeExecutions = new();

    public async Task<string> StartExecutionAsync(Request initialRequest)
    {
        var executionId = Guid.NewGuid().ToString();

        var orchestrator = AutonomousOrchestrator.Create<Request, Result>()
            .WithExecutor(_executor)
            .WithOracle(_oracle)
            .Build();

        _activeExecutions[executionId] = orchestrator;

        // Run in background
        _ = Task.Run(async () =>
        {
            await orchestrator.StartAsync(initialRequest);
            _activeExecutions.TryRemove(executionId, out _);
        });

        return executionId;
    }

    public ExecutionStatus GetStatus(string executionId)
    {
        return _activeExecutions.TryGetValue(executionId, out var orchestrator)
            ? ExecutionStatus.Running
            : ExecutionStatus.Completed;
    }
}
```

**Reference Pattern**: `samples/Patterns/MultiExecutionPattern/README.md` (planned)

**Why not built-in?**
- Different applications have different scaling needs
- Multi-tenancy patterns vary (single server, distributed, serverless)
- Resource management is application-specific
- Keeps SDK thin and focused

---

## Scope and Architecture

### Q: Should I use Ironbees or Microsoft Agent Framework directly?

**A: Both!** Ironbees **complements** MAF, not replaces it.

**Use Ironbees for**:
- Filesystem convention-based agent loading
- YAML-based configuration
- Multi-framework integration (if needed)
- Reducing boilerplate

**Use MAF directly for**:
- Complex workflow orchestration (DAG, parallel tasks)
- Tool execution and MCP integration
- Advanced conversation memory
- State machine execution

**Best Practice**: Start with Ironbees for simplicity, use MAF features when needed.

See [PHILOSOPHY.md](./PHILOSOPHY.md) for scope boundaries.

---

### Q: Can Ironbees handle DAG task scheduling?

**A: No,** but MAF can - and Ironbees integrates with it!

**Why not in Ironbees?**
- DAG execution is complex orchestration logic (violates thin wrapper philosophy)
- MAF already provides excellent workflow execution
- Reimplementing would create maintenance burden

**How to use DAG workflows**:

```csharp
// 1. Define workflow in YAML (Ironbees)
var yamlWorkflow = await YamlWorkflowLoader.LoadAsync("workflow.yaml");

// 2. Convert to MAF workflow (Ironbees)
var converter = new MafWorkflowConverter();
var mafWorkflow = converter.Convert(yamlWorkflow);

// 3. Execute via MAF
var executor = new MafWorkflowExecutor();
await executor.ExecuteAsync(mafWorkflow);
```

**Ironbees role**: Declarative YAML schema and MAF conversion
**MAF role**: Actual workflow execution

See [ARCHITECTURE.md - Workflow Decision Guide](./ARCHITECTURE.md#workflow-decision-guide)

---

### Q: What's the difference between Autonomous SDK, AgentMode, and MAF?

**Decision Tree**:

```
Need workflow?
├─ Simple iterations? → AutonomousOrchestrator (Autonomous SDK)
├─ State machine? → YamlDrivenOrchestrator (AgentMode)
└─ Complex DAG/parallel? → MAF directly (via MafWorkflowConverter)
```

**Autonomous SDK** (`Ironbees.Autonomous`):
- Simple iterative autonomous execution
- Oracle verification and auto-continuation
- YAML configuration via OrchestratorSettings
- Example: 20 Questions game

**AgentMode** (`Ironbees.AgentMode`):
- Complex multi-agent workflows
- State machines with HITL checkpoints
- Agentic patterns (sampling, confidence)
- Example: Data preprocessing with human approval

**MAF Integration**:
- Full workflow orchestration
- DAG task dependencies
- Tool execution and MCP
- Advanced state management

See detailed comparison in [ARCHITECTURE.md](./ARCHITECTURE.md#workflow-decision-guide)

---

## Configuration and Setup

### Q: How do I configure Oracle verifier?

**Basic Oracle** (IOracleVerifier):
```csharp
public class MyOracleVerifier : IOracleVerifier
{
    public async Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // Your verification logic
        var isComplete = /* check if goal achieved */;

        return isComplete
            ? OracleVerdict.GoalAchieved("Task completed successfully")
            : OracleVerdict.ContinueToNextIteration("Keep working");
    }
}
```

**Context-Aware Oracle** (v0.4.1 - new!):
```csharp
public class ContextAwareOracleVerifier : IContextAwareOracleVerifier
{
    public async Task<EnhancedOracleVerdict> VerifyWithContextAsync(
        OracleContext context,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // Access execution history
        var previousAttempts = context.History.Count;
        var previousVerdicts = context.PreviousVerdicts;

        // Context-aware decision
        var completedGoals = ExtractCompletedGoals(context, executionOutput);
        var remainingGoals = ExtractRemainingGoals(context, executionOutput);

        return EnhancedOracleVerdict.ContinueWithProgress(
            analysis: $"Completed {completedGoals.Count} of {completedGoals.Count + remainingGoals.Count} goals",
            confidence: 0.75,
            completedGoals: completedGoals,
            remainingGoals: remainingGoals
        );
    }
}
```

**YAML Configuration**:
```yaml
orchestration:
  oracle:
    enabled: true
    max_iterations: 3
  confidence:
    min_threshold: 0.8
```

---

### Q: How do I load configuration from environment variables?

**Combined YAML + Environment**:
```csharp
// Loads settings.yaml, then overrides with LLM_* environment variables
var orchestrator = await AutonomousOrchestrator
    .FromEnvironmentAsync<Request, Result>("settings.yaml");
```

**Environment Variable Mapping**:
```bash
# LLM_* prefix for orchestration settings
LLM_MAX_ITERATIONS=20
LLM_ORACLE_ENABLED=true
LLM_CONFIDENCE_MIN_THRESHOLD=0.9
```

**Production Pattern**:
```bash
# Development
dotnet run --settings settings.development.yaml

# Staging
LLM_MAX_ITERATIONS=15 dotnet run --settings settings.staging.yaml

# Production
LLM_MAX_ITERATIONS=10 LLM_CONFIDENCE_MIN_THRESHOLD=0.95 \
  dotnet run --settings settings.production.yaml
```

---

## Advanced Topics

### Q: How do I implement custom state storage?

**For AgentMode** (ICheckpointStore - already available):
```csharp
public class SqlCheckpointStore : ICheckpointStore
{
    private readonly string _connectionString;

    public async Task SaveAsync(Checkpoint checkpoint)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "INSERT INTO Checkpoints (WorkflowId, State, Timestamp) VALUES (@Id, @State, @Timestamp)",
            checkpoint);
    }

    public async Task<Checkpoint?> LoadAsync(string workflowId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Checkpoint>(
            "SELECT * FROM Checkpoints WHERE WorkflowId = @WorkflowId ORDER BY Timestamp DESC",
            new { WorkflowId = workflowId });
    }
}
```

**For Autonomous SDK** (IAutonomousStateStore - v0.4.1 planned):
```csharp
// Interface to be added in v0.4.1
public interface IAutonomousStateStore
{
    Task SaveStateAsync(ExecutionState state);
    Task<ExecutionState?> LoadStateAsync(string executionId);
}

// Your implementation
public class RedisStateStore : IAutonomousStateStore
{
    // Redis-based implementation
}
```

---

### Q: How do I integrate with external memory systems?

**Use Context Providers** (v0.4.0):

```csharp
public class MemoryIndexerContextProvider : IAutonomousContextProvider
{
    private readonly MemoryIndexerClient _memoryIndexer;

    public async Task<IEnumerable<ContextEntry>> GetRelevantContextAsync(
        string query,
        int maxResults = 10)
    {
        var memories = await _memoryIndexer.SearchAsync(query, maxResults);
        return memories.Select(m => new ContextEntry
        {
            Type = ContextEntryType.ExternalMemory,
            Content = m.Content,
            Timestamp = m.Timestamp,
            Metadata = new Dictionary<string, object> { ["source"] = "memory-indexer" }
        });
    }
}

// Configure
var orchestrator = AutonomousOrchestrator.Create<Request, Result>()
    .WithContextProvider(new MemoryIndexerContextProvider(memoryClient))
    .Build();
```

---

### Q: Can I use Rx.NET for reactive event handling?

**Yes!** While Ironbees doesn't include Rx.NET (to keep dependencies minimal), you can easily adapt events:

```csharp
// Adapter pattern (user code)
public class RxEventAdapter
{
    private readonly Subject<AutonomousEvent> _eventStream = new();

    public IObservable<TEvent> GetEvents<TEvent>() where TEvent : AutonomousEvent
    {
        return _eventStream.OfType<TEvent>();
    }

    public void PublishEvent(AutonomousEvent evt)
    {
        _eventStream.OnNext(evt);
    }
}

// Usage
var adapter = new RxEventAdapter();
orchestrator.OnEvent += adapter.PublishEvent;

// Subscribe to specific events
adapter.GetEvents<TaskCompletedEvent>()
    .Where(e => !e.Success)
    .Subscribe(async evt => await NotifyFailureAsync(evt));
```

**Reference**: `samples/Patterns/RxNetEventAdapter/` (planned)

---

## Troubleshooting

### Q: Why can't I find OrchestratorSettings YAML support in documentation?

**A:** This was a documentation gap (fixed in v0.4.0)!

The feature existed since v0.3.0 but wasn't prominently documented. Now available:
- [README.md - YAML Configuration](../README.md#⚙️-yaml-기반-설정-autonomous-sdk)
- [ARCHITECTURE.md - Autonomous SDK](./ARCHITECTURE.md#when-to-use-autonomousorchestrator-ironbeesautonomous)
- Sample files: `samples/MinimalAutonomousSample/settings.yaml`

---

### Q: I need DAG workflows - is Ironbees the wrong choice?

**A:** No! Use Ironbees **AgentMode** + MAF integration:

```csharp
// Ironbees provides YAML → MAF conversion
var converter = new MafWorkflowConverter();
var mafWorkflow = converter.Convert(yamlWorkflow);
await mafExecutor.ExecuteAsync(mafWorkflow);
```

Ironbees handles:
- ✅ YAML schema definition
- ✅ MAF workflow conversion
- ✅ Integration layer

MAF handles:
- ✅ DAG execution
- ✅ Parallel task scheduling
- ✅ State management

---

## Related Resources

- [PHILOSOPHY.md](./PHILOSOPHY.md) - Design principles and scope boundaries
- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture and decision guide
- [AGENTIC-PATTERNS.md](./AGENTIC-PATTERNS.md) - Agentic workflow patterns
- [autonomous-sdk-guide.md](./autonomous-sdk-guide.md) - Autonomous SDK deep dive
- [CLAUDE.md](../CLAUDE.md) - Development guidelines

---

**Have more questions?** Open an issue at https://github.com/iyulab/ironbees/issues
