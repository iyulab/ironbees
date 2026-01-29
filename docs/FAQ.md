# Ironbees FAQ

## Workflow Configuration

### Q: Can I define workflows in YAML?

**Yes!** Ironbees supports YAML configuration in two ways:

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

See [ARCHITECTURE.md - Workflow Decision Guide](./ARCHITECTURE.md#workflow-decision-guide)

---

### Q: How do I persist execution state?

Use checkpoint stores (available in AgentMode):

```csharp
var checkpointStore = new FileSystemCheckpointStore("./checkpoints");
var orchestrator = new YamlDrivenOrchestrator(
    workflow,
    checkpointStore: checkpointStore);
```

**Custom Storage**:
```csharp
public class MyStateStore : ICheckpointStore
{
    public async Task SaveAsync(Checkpoint checkpoint) { /* Your logic */ }
    public async Task<Checkpoint?> LoadAsync(string workflowId) { /* Your logic */ }
}
```

---

### Q: Can I run multiple workflows concurrently?

**Yes,** but this belongs in the **application layer**.

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
            .Build();

        _activeExecutions[executionId] = orchestrator;
        _ = Task.Run(async () =>
        {
            await orchestrator.StartAsync(initialRequest);
            _activeExecutions.TryRemove(executionId, out _);
        });

        return executionId;
    }
}
```

---

## Scope and Architecture

### Q: Should I use Ironbees or Microsoft Agent Framework directly?

**Both!** Ironbees **complements** MAF, not replaces it.

**Use Ironbees for**:
- Filesystem convention-based agent loading
- YAML-based configuration
- Multi-framework integration
- Reducing boilerplate

**Use MAF directly for**:
- Complex workflow orchestration (DAG, parallel tasks)
- Tool execution and MCP integration
- Advanced conversation memory

See [PHILOSOPHY.md](./PHILOSOPHY.md) for scope boundaries.

---

### Q: What's the difference between Autonomous SDK, AgentMode, and MAF?

**Decision Tree**:

```
Need workflow?
├─ Simple iterations? → AutonomousOrchestrator (Autonomous SDK)
├─ State machine? → YamlDrivenOrchestrator (AgentMode)
└─ Complex DAG/parallel? → MAF directly (via MafWorkflowConverter)
```

---

## Configuration

### Q: How do I configure Oracle verifier?

```csharp
public class MyOracleVerifier : IOracleVerifier
{
    public async Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var isComplete = /* check if goal achieved */;
        return isComplete
            ? OracleVerdict.GoalAchieved("Task completed")
            : OracleVerdict.ContinueToNextIteration("Keep working");
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

```csharp
// Loads settings.yaml, then overrides with LLM_* environment variables
var orchestrator = await AutonomousOrchestrator
    .FromEnvironmentAsync<Request, Result>("settings.yaml");
```

**Environment Variable Mapping**:
```bash
LLM_MAX_ITERATIONS=20
LLM_ORACLE_ENABLED=true
LLM_CONFIDENCE_MIN_THRESHOLD=0.9
```

---

### Q: Can I use Anthropic Claude?

Yes, via **OpenAI-compatible proxy** (GPUStack, LiteLLM):

```csharp
var customClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri("https://proxy.example.com/v1") });

var chatClient = new ChatClientBuilder(
    customClient.GetChatClient(model).AsIChatClient())
    .UseFunctionInvocation()
    .Build();
```

See [PROVIDERS.md](./PROVIDERS.md) for more provider configurations.

---

## Related Resources

- [PHILOSOPHY.md](./PHILOSOPHY.md) - Design principles
- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [AGENTIC-PATTERNS.md](./AGENTIC-PATTERNS.md) - Agentic workflow patterns
- [autonomous-sdk-guide.md](./autonomous-sdk-guide.md) - Autonomous SDK guide

---

**Questions?** Open an issue at https://github.com/iyulab/ironbees/issues
