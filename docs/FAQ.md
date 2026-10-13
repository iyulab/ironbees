# Ironbees FAQ

## Workflow Configuration

### Q: Can I define workflows in YAML?

**Yes!** Ironbees supports YAML configuration in two ways:

**1. Autonomous SDK** - Load the orchestrator's settings from YAML for simple iterative workflows:

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
var orchestrator = (await AutonomousOrchestrator.FromSettingsFileAsync<Request, Result>("settings.yaml"))
    .WithExecutor(executor)
    .WithRequestFactory((id, prompt) => new Request(id, prompt))
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

Give the AgentMode orchestrator a checkpoint store (`Ironbees.Core.Orchestration.ICheckpointStore`); Ironbees.Ironhive
ships a file-system one:

```csharp
var orchestrator = new YamlDrivenOrchestrator(
    new YamlWorkflowLoader(),
    new TriggerEvaluatorFactory(),
    agentExecutorFactory,
    checkpointStore: new FileSystemIronhiveCheckpointStore(
        "./checkpoints", NullLogger<FileSystemIronhiveCheckpointStore>.Instance));
```

**Custom Storage**:
```csharp
public class MyStateStore : Ironbees.Core.Orchestration.ICheckpointStore
{
    public Task SaveCheckpointAsync(string orchestrationId, OrchestrationCheckpoint checkpoint, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // your storage

    public Task<OrchestrationCheckpoint?> LoadCheckpointAsync(string orchestrationId, string? checkpointId = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // the given checkpoint, or the latest when checkpointId is null

    public Task<IReadOnlyList<OrchestrationCheckpoint>> ListCheckpointsAsync(string orchestrationId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteCheckpointAsync(string orchestrationId, string checkpointId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAllCheckpointsAsync(string orchestrationId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
```

---

### Q: Can I run multiple workflows concurrently?

**Yes,** but this belongs in the **application layer**.

```csharp
public class ExecutionCoordinator(ITaskExecutor<Request, Result> executor)
{
    private readonly ConcurrentDictionary<string, AutonomousOrchestrator<Request, Result>>
        _activeExecutions = new();

    public string StartExecution(string prompt)
    {
        var executionId = Guid.NewGuid().ToString();
        var orchestrator = AutonomousOrchestrator.Create<Request, Result>()
            .WithExecutor(executor)
            .WithRequestFactory((id, p) => new Request(id, p))
            .Build();

        _activeExecutions[executionId] = orchestrator;
        orchestrator.EnqueuePrompt(prompt);
        _ = Task.Run(async () =>
        {
            await orchestrator.StartAsync();
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
    public bool IsConfigured => true;

    public Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var isComplete = executionOutput.Contains("DONE"); // check whether the goal is achieved
        return Task.FromResult(isComplete
            ? OracleVerdict.GoalAchieved("Task completed")
            : OracleVerdict.ContinueToNextIteration("Keep working"));
    }

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null)
        => $"Goal: {originalPrompt}\nOutput: {executionOutput}\nIs the goal achieved?";
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
var orchestrator = (await AutonomousOrchestrator.FromEnvironmentAsync<Request, Result>("settings.yaml"))
    .WithExecutor(executor)
    .WithRequestFactory((id, prompt) => new Request(id, prompt))
    .Build();
```

**Environment Variable Mapping**:
```bash
LLM_MAX_ITERATIONS=20
LLM_ORACLE_ENABLED=true
LLM_CONFIDENCE_MIN_THRESHOLD=0.9
```

---

### Q: Can I use Anthropic Claude?

Yes — on the IronHive backend, with `IronHive.Providers.Anthropic`:

```csharp
services.AddIronbeesIronhive(options =>
{
    options.AgentsDirectory = "./agents";
    options.ConfigureHive = hive => hive.AddAnthropicProviders("anthropic", new AnthropicConfig
    {
        ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    });
});
```

An agent then names `provider: anthropic` in its `agent.yaml`. See [PROVIDERS.md](./PROVIDERS.md) for the other providers.

---

## Related Resources

- [PHILOSOPHY.md](./PHILOSOPHY.md) - Design principles
- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [autonomous-sdk-guide.md](./autonomous-sdk-guide.md) - Autonomous SDK guide

---

**Questions?** Open an issue at https://github.com/iyulab/ironbees/issues
