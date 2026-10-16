# Multi-Execution Pattern

**Pattern Category**: Application-Layer Coordination
**Status**: Reference Pattern (Not SDK Feature)

## Overview

This pattern shows how to run several autonomous executions at once in your application. Multi-execution coordination belongs in the **application layer**, not in the Ironbees SDK, because applications need very different things from it.

## Why Application Layer?

**Ironbees provides**:
- ✅ Autonomous execution abstraction (`AutonomousOrchestrator`)
- ✅ YAML configuration (`AutonomousSettings`)
- ✅ Interface definitions (`ITaskExecutor`, `IOracleVerifier`)

**Your application decides**:
- 🎯 Single-server vs distributed
- 🎯 In-memory vs persistent queue
- 🎯 Resource limits and throttling
- 🎯 Multi-tenancy strategy
- 🎯 Scaling approach

## Basic Pattern: In-Memory Coordinator

One orchestrator per execution, each given its prompt with `EnqueuePrompt` and run in the background. `StartAsync` returns when
the queue is done or the run stops, and records how it ended in `Status.State`; it rethrows a failure, so a faulted run task is
a failed execution. A slot is taken before the orchestrator is built: checking a count and then adding lets two callers pass
the limit at once.

```csharp
public sealed class ExecutionCoordinator<TRequest, TResult>(
    ITaskExecutor<TRequest, TResult> executor,
    Func<string, string, TRequest> requestFactory,
    IOracleVerifier? oracle = null,
    int maxConcurrent = 10) : IDisposable
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    private readonly ConcurrentDictionary<string, Execution> _executions = new();
    private readonly SemaphoreSlim _slots = new(maxConcurrent, maxConcurrent);

    public string Start(string prompt, AutonomousSettings? settings = null, CancellationToken cancellationToken = default)
    {
        if (!_slots.Wait(0, cancellationToken))
            throw new InvalidOperationException($"{maxConcurrent} executions are already running.");

        var builder = AutonomousOrchestrator.Create<TRequest, TResult>()
            .WithExecutor(executor)
            .WithRequestFactory(requestFactory);
        if (oracle is not null)
            builder = builder.WithOracle(oracle);
        if (settings is not null)
            builder = builder.WithSettings(settings);

        var orchestrator = builder.Build();
        orchestrator.EnqueuePrompt(prompt);

        var execution = new Execution(
            Guid.NewGuid().ToString("N"),
            orchestrator,
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
            DateTimeOffset.UtcNow);
        _executions[execution.Id] = execution;

        execution.Run = Task.Run(async () =>
        {
            try
            {
                await orchestrator.StartAsync(cancellationToken: execution.Cancellation.Token);
            }
            finally
            {
                execution.MarkCompleted();
                _slots.Release();
            }
        }, CancellationToken.None);

        return execution.Id;
    }

    public ExecutionInfo? GetStatus(string executionId) =>
        _executions.TryGetValue(executionId, out var execution) ? execution.Describe() : null;

    public IReadOnlyList<ExecutionInfo> List(AutonomousState? state = null) =>
        [.. _executions.Values.Select(e => e.Describe()).Where(info => state is null || info.State == state)];

    public bool Cancel(string executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            return false;

        execution.Cancellation.Cancel();
        return true;
    }

    public void RemoveFinished(TimeSpan olderThan)
    {
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        foreach (var (id, execution) in _executions)
        {
            if (execution.CompletedAt is { } completed && completed < cutoff && _executions.TryRemove(id, out _))
                execution.Cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var execution in _executions.Values)
            execution.Cancellation.Cancel();
    }

    private sealed class Execution(
        string id,
        AutonomousOrchestrator<TRequest, TResult> orchestrator,
        CancellationTokenSource cancellation,
        DateTimeOffset startedAt)
    {
        private long _completedTicks;

        public string Id { get; } = id;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public Task Run { get; set; } = Task.CompletedTask;

        public DateTimeOffset? CompletedAt =>
            Volatile.Read(ref _completedTicks) is var ticks and not 0 ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

        public void MarkCompleted() => Volatile.Write(ref _completedTicks, DateTimeOffset.UtcNow.UtcTicks);

        public ExecutionInfo Describe()
        {
            var status = orchestrator.Status;
            var error = Run.IsFaulted ? Run.Exception?.InnerException?.Message : status.LastError;
            return new ExecutionInfo(Id, status.State, startedAt, CompletedAt, error);
        }
    }
}

public sealed record ExecutionInfo(
    string Id,
    AutonomousState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);
```

## Usage Examples

### Basic Multi-Execution

```csharp
using var coordinator = new ExecutionCoordinator<Request, Result>(
    executor,
    requestFactory: (id, prompt) => new Request(id, prompt),
    oracle: oracle,
    maxConcurrent: 5);

// Start several executions
var first = coordinator.Start("Summarise the release notes");
var second = coordinator.Start("Draft the migration checklist");
var third = coordinator.Start("List the open questions");

// Monitor status
var status = coordinator.GetStatus(first);
Console.WriteLine($"First: {status?.State}");

// List the running executions
var running = coordinator.List(AutonomousState.Running);
Console.WriteLine($"Running: {running.Count}");

// Cancel one execution
coordinator.Cancel(second);

// Forget executions that finished more than 30 minutes ago
coordinator.RemoveFinished(TimeSpan.FromMinutes(30));
```

### With Different Settings Per Execution

```csharp
var loader = new SettingsLoader();
var highPriority = await loader.LoadFromFileAsync("high-priority.yaml");
var lowPriority = await loader.LoadFromFileAsync("low-priority.yaml");

using var coordinator = new ExecutionCoordinator<Request, Result>(executor, (id, prompt) => new Request(id, prompt));
var urgent = coordinator.Start("Investigate the failing build", highPriority);
var routine = coordinator.Start("Tidy the changelog", lowPriority);
```

## Beyond One Process

The in-memory coordinator loses its executions when the process restarts. The same shape carries over to infrastructure
Ironbees does not reference:

- **Persistent queue** — store each request with its state (queued, running, finished) in a database or Redis, and run a
  background worker that dequeues an id, builds the orchestrator as above, and writes the outcome back. Checkpoints
  (`GetCheckpoints`, `StartFromCheckpointAsync`) let a restarted worker resume a long run.
- **Serverless** — an HTTP-triggered function enqueues the request and answers 202 with an execution id; a queue-triggered
  function builds the orchestrator, runs it, and saves the outcome to table or document storage.
- **Distributed** — a message broker (Service Bus, Kafka, RabbitMQ) routes requests to a worker pool; each worker is a
  coordinator like the one above with its own `maxConcurrent`.

## Scaling Strategies

| Strategy | When to Use | Implementation |
|----------|-------------|----------------|
| **In-Memory** | Single server, low volume | `ExecutionCoordinator` (above) |
| **Persistent Queue** | Multi-instance, fault tolerance | SQL/Redis + worker pool |
| **Serverless** | Variable load, cloud-native | Azure Functions, AWS Lambda |
| **Distributed** | High scale, complex routing | Service Bus, Kafka, RabbitMQ |

## Related Patterns

- [Autonomous SDK Guide](../../../docs/autonomous-sdk-guide.md)
- [ARCHITECTURE.md - Scope Boundaries](../../../docs/ARCHITECTURE.md#scope-boundaries-updated-v040)
- [PHILOSOPHY.md - Application Layer](../../../docs/PHILOSOPHY.md#out-of-scope-execution-and-business-logic)

## Key Takeaways

1. **Application-Specific**: Multi-execution patterns vary widely by application needs
2. **Not SDK Concern**: Ironbees provides building blocks, not orchestration logic
3. **Flexibility**: Choose the pattern that fits your scaling and reliability requirements
4. **Resource Management**: The application controls limits, queuing, and priority
5. **State Storage**: Choose persistence based on the fault tolerance you need

---

**Questions?** See [FAQ.md - Multi-Execution](../../../docs/FAQ.md#q-can-i-run-multiple-workflows-concurrently)
