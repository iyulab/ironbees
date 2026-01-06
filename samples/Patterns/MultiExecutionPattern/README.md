# Multi-Execution Pattern

**Pattern Category**: Application-Layer Coordination
**Version**: 0.4.0
**Status**: Reference Pattern (Not SDK Feature)

## Overview

This pattern demonstrates how to coordinate multiple concurrent workflow executions in your application. Multi-execution coordination belongs in the **application layer**, not in Ironbees SDK, because different applications have vastly different requirements.

## Why Application Layer?

**Ironbees provides**:
- ✅ Autonomous execution abstraction
- ✅ YAML configuration
- ✅ Interface definitions

**Your application decides**:
- 🎯 Single-server vs distributed
- 🎯 In-memory vs persistent queue
- 🎯 Resource limits and throttling
- 🎯 Multi-tenancy strategy
- 🎯 Scaling approach

## Basic Pattern: In-Memory Coordinator

```csharp
using System.Collections.Concurrent;
using Ironbees.Autonomous;

public class ExecutionCoordinator<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    private readonly ConcurrentDictionary<string, OrchestratorInstance<TRequest, TResult>>
        _activeExecutions = new();
    private readonly ITaskExecutor<TRequest, TResult> _executor;
    private readonly IOracleVerifier? _oracle;
    private readonly int _maxConcurrent;

    public ExecutionCoordinator(
        ITaskExecutor<TRequest, TResult> executor,
        IOracleVerifier? oracle = null,
        int maxConcurrent = 10)
    {
        _executor = executor;
        _oracle = oracle;
        _maxConcurrent = maxConcurrent;
    }

    public async Task<string> StartExecutionAsync(
        TRequest initialRequest,
        OrchestratorSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        // Check capacity
        if (_activeExecutions.Count >= _maxConcurrent)
        {
            throw new InvalidOperationException(
                $"Maximum concurrent executions ({_maxConcurrent}) reached");
        }

        var executionId = Guid.NewGuid().ToString();

        // Build orchestrator
        var builder = AutonomousOrchestrator.Create<TRequest, TResult>()
            .WithExecutor(_executor);

        if (_oracle != null)
            builder = builder.WithOracle(_oracle);

        if (settings != null)
            builder = builder.WithSettings(settings);

        var orchestrator = builder.Build();

        // Track execution
        var instance = new OrchestratorInstance<TRequest, TResult>
        {
            Id = executionId,
            Orchestrator = orchestrator,
            StartedAt = DateTime.UtcNow,
            Status = ExecutionStatus.Running,
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };

        _activeExecutions[executionId] = instance;

        // Run in background
        _ = Task.Run(async () =>
        {
            try
            {
                await orchestrator.StartAsync(
                    initialRequest,
                    instance.CancellationTokenSource.Token);

                instance.Status = ExecutionStatus.Completed;
                instance.CompletedAt = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                instance.Status = ExecutionStatus.Cancelled;
                instance.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                instance.Status = ExecutionStatus.Failed;
                instance.Error = ex;
                instance.CompletedAt = DateTime.UtcNow;
            }
        }, cancellationToken);

        return executionId;
    }

    public ExecutionInfo? GetStatus(string executionId)
    {
        if (!_activeExecutions.TryGetValue(executionId, out var instance))
            return null;

        return new ExecutionInfo
        {
            Id = instance.Id,
            Status = instance.Status,
            StartedAt = instance.StartedAt,
            CompletedAt = instance.CompletedAt,
            Duration = instance.CompletedAt.HasValue
                ? instance.CompletedAt.Value - instance.StartedAt
                : DateTime.UtcNow - instance.StartedAt,
            ErrorMessage = instance.Error?.Message
        };
    }

    public async Task<bool> CancelAsync(string executionId)
    {
        if (!_activeExecutions.TryGetValue(executionId, out var instance))
            return false;

        instance.CancellationTokenSource.Cancel();
        return true;
    }

    public IReadOnlyList<ExecutionInfo> ListExecutions(ExecutionStatus? filterByStatus = null)
    {
        var executions = _activeExecutions.Values
            .Select(i => new ExecutionInfo
            {
                Id = i.Id,
                Status = i.Status,
                StartedAt = i.StartedAt,
                CompletedAt = i.CompletedAt,
                Duration = i.CompletedAt.HasValue
                    ? i.CompletedAt.Value - i.StartedAt
                    : DateTime.UtcNow - i.StartedAt
            });

        if (filterByStatus.HasValue)
            executions = executions.Where(e => e.Status == filterByStatus.Value);

        return executions.ToList();
    }

    public async Task CleanupCompletedAsync(TimeSpan? olderThan = null)
    {
        var cutoff = DateTime.UtcNow - (olderThan ?? TimeSpan.FromHours(1));

        var toRemove = _activeExecutions
            .Where(kvp => kvp.Value.Status != ExecutionStatus.Running &&
                          kvp.Value.CompletedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _activeExecutions.TryRemove(id, out _);
        }
    }
}

// Supporting types
public class OrchestratorInstance<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    public required string Id { get; init; }
    public required AutonomousOrchestrator<TRequest, TResult> Orchestrator { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public ExecutionStatus Status { get; set; }
    public Exception? Error { get; set; }
    public required CancellationTokenSource CancellationTokenSource { get; init; }
}

public record ExecutionInfo
{
    public required string Id { get; init; }
    public required ExecutionStatus Status { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum ExecutionStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}
```

## Advanced Pattern: Persistent Queue

For production scenarios with process restarts:

```csharp
public class PersistentExecutionCoordinator<TRequest, TResult>
    where TRequest : ITaskRequest
    where TResult : ITaskResult
{
    private readonly IExecutionQueue _queue; // Your queue implementation
    private readonly IExecutionStateStore _stateStore; // Your state store

    public async Task<string> StartExecutionAsync(TRequest request)
    {
        var executionId = Guid.NewGuid().ToString();

        // Persist initial state
        await _stateStore.SaveAsync(new ExecutionState
        {
            Id = executionId,
            Request = request,
            Status = ExecutionStatus.Queued,
            QueuedAt = DateTime.UtcNow
        });

        // Enqueue for processing
        await _queue.EnqueueAsync(executionId);

        return executionId;
    }

    // Background worker processes queue
    public async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var executionId = await _queue.DequeueAsync(cancellationToken);
            if (executionId == null)
                continue;

            var state = await _stateStore.LoadAsync(executionId);
            if (state == null)
                continue;

            // Build and execute orchestrator
            // Update state store on completion
        }
    }
}
```

## Cloud-Native Pattern: Serverless

For Azure Functions / AWS Lambda:

```csharp
[Function("ExecuteWorkflow")]
public async Task<HttpResponseData> ExecuteWorkflow(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
    [QueueOutput("workflow-executions")] IAsyncCollector<string> queue)
{
    var request = await req.ReadFromJsonAsync<TaskRequest>();

    var executionId = Guid.NewGuid().ToString();

    // Enqueue for async processing
    await queue.AddAsync(JsonSerializer.Serialize(new
    {
        ExecutionId = executionId,
        Request = request
    }));

    var response = req.CreateResponse(HttpStatusCode.Accepted);
    await response.WriteAsJsonAsync(new { ExecutionId = executionId });
    return response;
}

[Function("ProcessWorkflow")]
public async Task ProcessWorkflow(
    [QueueTrigger("workflow-executions")] string message,
    [Table("WorkflowState")] TableClient tableClient)
{
    var data = JsonSerializer.Deserialize<WorkflowMessage>(message);

    var orchestrator = AutonomousOrchestrator.Create<TaskRequest, TaskResult>()
        .WithExecutor(_executor)
        .Build();

    await orchestrator.StartAsync(data.Request);

    // Save state to Table Storage
}
```

## Usage Examples

### Basic Multi-Execution

```csharp
var coordinator = new ExecutionCoordinator<TaskRequest, TaskResult>(
    executor: myExecutor,
    oracle: myOracle,
    maxConcurrent: 5);

// Start multiple executions
var execution1 = await coordinator.StartExecutionAsync(new TaskRequest("Task 1"));
var execution2 = await coordinator.StartExecutionAsync(new TaskRequest("Task 2"));
var execution3 = await coordinator.StartExecutionAsync(new TaskRequest("Task 3"));

// Monitor status
var status1 = coordinator.GetStatus(execution1);
Console.WriteLine($"Execution 1: {status1.Status}");

// List all running executions
var running = coordinator.ListExecutions(ExecutionStatus.Running);
Console.WriteLine($"Running: {running.Count}");

// Cancel specific execution
await coordinator.CancelAsync(execution2);

// Cleanup old executions
await coordinator.CleanupCompletedAsync(olderThan: TimeSpan.FromMinutes(30));
```

### With Different Settings Per Execution

```csharp
var settingsA = await OrchestratorSettings.LoadFromFileAsync("high-priority.yaml");
var settingsB = await OrchestratorSettings.LoadFromFileAsync("low-priority.yaml");

var executionA = await coordinator.StartExecutionAsync(requestA, settingsA);
var executionB = await coordinator.StartExecutionAsync(requestB, settingsB);
```

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
3. **Flexibility**: Choose pattern that fits your scaling/reliability requirements
4. **Resource Management**: Application controls limits, queuing, and priority
5. **State Storage**: Choose appropriate persistence based on fault tolerance needs

---

**Questions?** See [FAQ.md - Multi-Execution](../../../docs/FAQ.md#q-can-i-run-multiple-workflows-concurrently)
