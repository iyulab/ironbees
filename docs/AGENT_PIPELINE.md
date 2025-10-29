# Agent Pipeline Guide

## Overview

The Agent Pipeline feature in Ironbees enables you to chain multiple agents in sequence, creating complex multi-step workflows. Each agent in the pipeline processes the output from the previous agent, with support for transformations, conditional execution, and error handling.

## Key Concepts

### Pipeline Context
The `PipelineContext` object flows through the entire pipeline, carrying:
- **Original Input**: The initial input to the pipeline
- **Current Input**: The input for the current step (updated after each step)
- **Step Results**: Collection of results from all executed steps
- **Shared State**: Dictionary for storing data between steps
- **Error Tracking**: Captures errors and manages pipeline flow control

### Pipeline Steps
Each step in a pipeline represents an agent execution with configurable:
- **Agent Name**: The agent to execute
- **Input Transformer**: Function to transform input before agent execution
- **Output Transformer**: Function to transform output after agent execution
- **Condition**: Optional condition to determine if step should execute
- **Error Handling**: Continue on error, retry logic, timeout settings
- **Metadata**: Custom key-value pairs for step configuration

### Pipeline Result
After execution, the pipeline returns a comprehensive result containing:
- **Success Status**: Whether the pipeline completed successfully
- **Total Execution Time**: Time taken for entire pipeline
- **Steps Executed**: Count of steps that ran
- **Steps Failed**: Count of steps that failed
- **Context**: Complete pipeline context with all step results
- **Final Output**: The output from the last successful step

## Basic Usage

### 1. Simple Sequential Pipeline

```csharp
var pipeline = orchestrator.CreatePipeline("simple-pipeline")
    .AddAgent("router-agent")
    .AddAgent("rag-agent")
    .AddAgent("summarization-agent")
    .Build();

var result = await pipeline.ExecuteAsync("What is Ironbees?");

Console.WriteLine($"✅ Pipeline completed in {result.TotalExecutionTime.TotalSeconds:F2}s");
Console.WriteLine($"📊 Steps executed: {result.StepsExecuted}");
Console.WriteLine($"🎯 Final Output: {result.Output}");
```

**Use case**: Process a question through routing, knowledge retrieval, and summarization.

### 2. Quick Pipeline Helper

For simple sequential pipelines, use the quick helper method:

```csharp
var result = await orchestrator.ExecutePipelineAsync(
    "Analyze user engagement metrics",
    new[] { "analysis-agent", "writing-agent", "summarization-agent" }
);
```

**Use case**: Rapidly prototype multi-agent workflows without builder configuration.

## Pipeline Scenarios

### Scenario 1: Input/Output Transformers

Transform data between pipeline steps:

```csharp
var pipeline = orchestrator.CreatePipeline("transformer-pipeline")
    .AddAgent("function-calling-agent", step => step
        .WithInputTransformer(ctx =>
        {
            // Transform input before agent execution
            return $"Explain step-by-step how to: {ctx.OriginalInput}";
        })
        .WithOutputTransformer((ctx, output) =>
        {
            // Transform output after agent execution
            return $"[Function Calling Agent Analysis]\n{output}";
        }))
    .AddAgent("summarization-agent", step => step
        .WithInputTransformer(ctx =>
        {
            // Use previous step's output
            var prevResult = ctx.GetLastStepResult();
            return $"Summarize this in 2-3 sentences:\n\n{prevResult?.Output}";
        }))
    .Build();

var result = await pipeline.ExecuteAsync("Get weather data from an API");
```

**Use case**: Format inputs/outputs, add context, or extract specific information between steps.

### Scenario 2: Conditional Execution

Execute agents based on runtime conditions:

```csharp
var pipeline = orchestrator.CreatePipeline("conditional-pipeline")
    .AddAgent("router-agent")  // Always executes
    .AddAgentIf("coding-agent", ctx =>
    {
        // Only execute if router detected coding task
        var routerResult = ctx.GetStepResult("router-agent");
        return routerResult?.Output.Contains("coding", StringComparison.OrdinalIgnoreCase) ?? false;
    })
    .AddAgentIf("summarization-agent", ctx =>
    {
        // Only execute if previous step ran
        return ctx.GetLastStepResult() != null;
    })
    .Build();

var result = await pipeline.ExecuteAsync("Write a Python function to calculate factorial");
```

**Use case**: Dynamic routing based on content, intent classification, or previous results.

### Scenario 3: Error Handling and Retry

Build resilient pipelines with error handling:

```csharp
var pipeline = orchestrator.CreatePipeline("error-handling-pipeline")
    .AddAgent("rag-agent")
    .WithErrorHandling(
        continueOnError: true,    // Continue even if this step fails
        maxRetries: 2,             // Retry up to 2 times
        retryDelay: TimeSpan.FromSeconds(1))  // Wait 1s between retries
    .AddAgent("summarization-agent")
    .WithTimeout(TimeSpan.FromSeconds(30))  // Fail if step takes >30s
    .Build();

var result = await pipeline.ExecuteAsync("Process this request");

Console.WriteLine($"Steps failed: {result.StepsFailed}");
Console.WriteLine($"Pipeline success: {result.Success}");
```

**Use case**: Handle transient failures, network issues, or optional processing steps.

### Scenario 4: Shared State Between Steps

Share data across multiple steps:

```csharp
var pipeline = orchestrator.CreatePipeline("shared-state-pipeline")
    .AddAgent("analysis-agent", step => step
        .WithOutputTransformer((ctx, output) =>
        {
            // Store analysis results in shared state
            ctx.SharedState["analysis_complete"] = true;
            ctx.SharedState["key_insights"] = ExtractInsights(output);
            return output;
        }))
    .AddAgent("writing-agent", step => step
        .WithInputTransformer(ctx =>
        {
            // Use shared state from previous step
            if (ctx.SharedState.TryGetValue("key_insights", out var insights))
            {
                return $"Write a report based on these insights: {insights}";
            }
            return ctx.CurrentInput;
        }))
    .Build();
```

**Use case**: Pass structured data, maintain state, or accumulate information across steps.

### Scenario 5: Pipeline Composition

Combine multiple pipeline patterns:

```csharp
var pipeline = orchestrator.CreatePipeline("complex-pipeline")
    // Phase 1: Analysis
    .AddAgent("router-agent")
    .AddAgent("analysis-agent", step => step
        .WithErrorHandling(continueOnError: true, maxRetries: 1))

    // Phase 2: Conditional Processing
    .AddAgentIf("coding-agent", ctx =>
        ctx.GetStepResult("router-agent")?.Output.Contains("code") ?? false)
    .AddAgentIf("rag-agent", ctx =>
        ctx.GetStepResult("router-agent")?.Output.Contains("knowledge") ?? false)

    // Phase 3: Finalization
    .AddAgent("summarization-agent", step => step
        .WithInputTransformer(ctx =>
        {
            var allOutputs = string.Join("\n\n", ctx.StepResults.Select(r => r.Output));
            return $"Synthesize these results:\n{allOutputs}";
        })
        .WithTimeout(TimeSpan.FromSeconds(30)))
    .Build();
```

**Use case**: Multi-phase workflows with branching logic and synthesis.

### Scenario 6: Parallel Agent Execution

Execute multiple agents concurrently with result aggregation:

```csharp
var pipeline = orchestrator.CreatePipeline("parallel-execution")
    .AddAgent("router-agent")

    // Execute 3 agents in parallel with voting strategy
    .AddParallelAgents(
        new[] { "analysis-agent-1", "analysis-agent-2", "analysis-agent-3" },
        parallel => parallel
            .WithVoting()  // Use voting to select most common result
            .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))

    .AddAgent("summarization-agent")
    .Build();

var result = await pipeline.ExecuteAsync("Analyze market trends");
```

**Use case**: Improve result quality through consensus, speed through parallelization, or robustness through redundancy.

#### Parallel Execution Options

```csharp
.AddParallelAgents(
    new[] { "agent1", "agent2", "agent3" },
    parallel => parallel
        // Collaboration strategy (choose one)
        .WithVoting()  // Majority consensus
        .WithBestOfN(result => result.Output.Length)  // Best quality
        .WithFirstSuccess()  // Fastest result
        .WithEnsemble(async results => /* combine */)  // Synthesize all

        // Execution options
        .WithMaxDegreeOfParallelism(2)  // Limit concurrency
        .WithTimeout(TimeSpan.FromSeconds(30))  // Overall timeout
        .WithPerAgentTimeout(TimeSpan.FromSeconds(10))  // Per-agent timeout
        .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))  // Failure handling
```

For detailed information about collaboration strategies and parallel execution, see [Collaboration Patterns](COLLABORATION_PATTERNS.md).

## API Reference

### AgentPipelineBuilder

#### AddAgent(string agentName)
Add an agent to the pipeline with default configuration.

```csharp
builder.AddAgent("router-agent")
```

#### AddAgent(string agentName, Action<PipelineStepBuilder> configure)
Add an agent with step-level configuration.

```csharp
builder.AddAgent("rag-agent", step => step
    .WithInputTransformer(ctx => $"Context: {ctx.CurrentInput}")
    .WithTimeout(TimeSpan.FromSeconds(10)))
```

#### AddAgentIf(string agentName, Func<PipelineContext, bool> condition)
Add a conditional agent that only executes if condition is true.

```csharp
builder.AddAgentIf("coding-agent", ctx =>
    ctx.GetStepResult("router")?.Output.Contains("code") ?? false)
```

#### AddAgents(params string[] agentNames)
Add multiple agents in sequence.

```csharp
builder.AddAgents("router-agent", "rag-agent", "summarization-agent")
```

#### AddParallelAgents(string[] agentNames, Action<ParallelPipelineStepBuilder> configure)
Add multiple agents to execute in parallel with configuration.

```csharp
builder.AddParallelAgents(
    new[] { "agent1", "agent2", "agent3" },
    parallel => parallel
        .WithVoting()
        .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))
```

See [Collaboration Patterns](COLLABORATION_PATTERNS.md) for detailed parallel execution documentation.

#### TransformInput(Func<PipelineContext, string> transformer)
Add input transformer to the last added agent.

```csharp
builder.AddAgent("rag-agent")
    .TransformInput(ctx => $"Question: {ctx.OriginalInput}")
```

#### TransformOutput(Func<PipelineContext, string, string> transformer)
Add output transformer to the last added agent.

```csharp
builder.AddAgent("rag-agent")
    .TransformOutput((ctx, output) => output.ToUpper())
```

#### WithErrorHandling(bool continueOnError, int maxRetries, TimeSpan? retryDelay)
Configure error handling for the last added agent.

```csharp
builder.AddAgent("rag-agent")
    .WithErrorHandling(continueOnError: true, maxRetries: 2, retryDelay: TimeSpan.FromSeconds(1))
```

#### WithTimeout(TimeSpan timeout)
Set timeout for the last added agent.

```csharp
builder.AddAgent("summarization-agent")
    .WithTimeout(TimeSpan.FromSeconds(30))
```

#### Build()
Build the configured pipeline.

```csharp
IAgentPipeline pipeline = builder.Build();
```

### PipelineStepBuilder

Used within `AddAgent` configuration:

```csharp
builder.AddAgent("agent-name", step => step
    .WithInputTransformer(ctx => /* transform */)
    .WithOutputTransformer((ctx, output) => /* transform */)
    .WithCondition(ctx => /* condition */)
    .WithErrorHandling(continueOnError: true, maxRetries: 2)
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithMetadata("key", value))
```

### PipelineContext

#### Properties
- `OriginalInput`: The initial pipeline input (readonly)
- `CurrentInput`: Input for current/next step (mutable)
- `StepResults`: List of all step results
- `SharedState`: Dictionary for cross-step data
- `ShouldContinue`: Whether pipeline should continue
- `Error`: Last error encountered

#### Methods
- `AddStepResult(PipelineStepResult result)`: Add a step result
- `GetStepResult(string agentName)`: Get result by agent name
- `GetLastStepResult()`: Get most recent result
- `StopPipeline(string reason)`: Stop pipeline execution

### PipelineResult

#### Properties
- `PipelineName`: Name of the executed pipeline
- `Success`: Whether pipeline completed successfully
- `TotalExecutionTime`: Total time taken
- `StepsExecuted`: Count of executed steps
- `StepsFailed`: Count of failed steps
- `Context`: Full pipeline context
- `Output`: Final output (from last step)
- `Error`: Exception if pipeline failed

### Extension Methods

#### CreatePipeline(string pipelineName)
Create a new pipeline builder from orchestrator.

```csharp
var pipeline = orchestrator.CreatePipeline("my-pipeline")
    .AddAgent("agent1")
    .Build();
```

#### ExecutePipelineAsync(string input, string[] agentNames)
Quick pipeline execution without builder.

```csharp
var result = await orchestrator.ExecutePipelineAsync(
    "input",
    new[] { "agent1", "agent2", "agent3" });
```

## Best Practices

### 1. Design for Idempotency
Each step should produce consistent results for the same input:

```csharp
// Good: Deterministic transformation
.WithInputTransformer(ctx => $"Analyze: {ctx.OriginalInput}")

// Avoid: Non-deterministic operations
.WithInputTransformer(ctx => $"Time: {DateTime.Now} - {ctx.OriginalInput}")
```

### 2. Use Descriptive Pipeline Names
Name pipelines based on their business purpose:

```csharp
// Good
orchestrator.CreatePipeline("customer-inquiry-routing")
orchestrator.CreatePipeline("document-analysis-and-summarization")

// Avoid
orchestrator.CreatePipeline("pipeline1")
orchestrator.CreatePipeline("test")
```

### 3. Handle Errors Gracefully
Set appropriate error handling based on step criticality:

```csharp
// Critical step - fail fast
.AddAgent("authentication-agent")
.WithTimeout(TimeSpan.FromSeconds(5))

// Optional step - continue on error
.AddAgent("analytics-agent")
.WithErrorHandling(continueOnError: true, maxRetries: 1)
```

### 4. Optimize for Performance
Consider execution time and dependencies:

```csharp
// Good: Fast routing first
.AddAgent("router-agent")  // <1s
.AddAgentIf("expensive-agent", ctx => /* condition */)  // Only if needed

// Avoid: Always running expensive operations
.AddAgent("expensive-agent")  // Always runs even if not needed
```

### 5. Use Shared State Sparingly
Only store data needed across multiple steps:

```csharp
// Good: Store structured data
ctx.SharedState["classification"] = new { Intent = "query", Confidence = 0.95 };

// Avoid: Storing entire outputs
ctx.SharedState["all_data"] = ctx.StepResults.Select(r => r.Output).ToList();
```

### 6. Test Individual Agents First
Verify each agent works correctly before combining in pipeline:

```csharp
// Test individual agents
var routerResult = await orchestrator.RunAgentAsync("router-agent", input);
var ragResult = await orchestrator.RunAgentAsync("rag-agent", input);

// Then combine in pipeline
var pipeline = orchestrator.CreatePipeline("tested-pipeline")
    .AddAgent("router-agent")
    .AddAgent("rag-agent")
    .Build();
```

### 7. Monitor Pipeline Performance
Track execution metrics for optimization:

```csharp
var result = await pipeline.ExecuteAsync(input);

Console.WriteLine($"Total time: {result.TotalExecutionTime.TotalSeconds}s");
foreach (var step in result.Context.StepResults)
{
    Console.WriteLine($"{step.AgentName}: {step.ExecutionTime.TotalMilliseconds}ms");
}
```

## Common Patterns

### Pattern 1: Route → Process → Summarize

```csharp
var pipeline = orchestrator.CreatePipeline("route-process-summarize")
    .AddAgent("router-agent")           // Classify intent
    .AddAgentIf("rag-agent", ctx =>     // Knowledge retrieval
        ctx.GetStepResult("router-agent")?.Output.Contains("knowledge") ?? false)
    .AddAgentIf("coding-agent", ctx =>  // Code generation
        ctx.GetStepResult("router-agent")?.Output.Contains("coding") ?? false)
    .AddAgent("summarization-agent")    // Final summary
    .Build();
```

### Pattern 2: Parallel Consensus Building

```csharp
var pipeline = orchestrator.CreatePipeline("parallel-consensus")
    .AddAgent("router-agent")
    .AddParallelAgents(
        new[] { "reviewer-1", "reviewer-2", "reviewer-3", "reviewer-4", "reviewer-5" },
        parallel => parallel
            .WithVoting()  // Use voting for consensus
            .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))
    .AddAgent("summarization-agent")
    .Build();
```

### Pattern 3: Iterative Refinement

```csharp
var pipeline = orchestrator.CreatePipeline("iterative-refinement")
    .AddAgent("draft-agent")
    .AddAgent("critique-agent", step => step
        .WithInputTransformer(ctx =>
        {
            var draft = ctx.GetLastStepResult()?.Output;
            return $"Critique this draft:\n{draft}";
        }))
    .AddAgent("refine-agent", step => step
        .WithInputTransformer(ctx =>
        {
            var draft = ctx.GetStepResult("draft-agent")?.Output;
            var critique = ctx.GetLastStepResult()?.Output;
            return $"Draft:\n{draft}\n\nCritique:\n{critique}\n\nProduce refined version";
        }))
    .Build();
```

### Pattern 4: Multi-Stage Analysis

```csharp
var pipeline = orchestrator.CreatePipeline("multi-stage-analysis")
    .AddAgent("data-extraction-agent")
    .AddAgent("statistical-analysis-agent")
    .AddAgent("insight-generation-agent")
    .AddAgent("recommendation-agent", step => step
        .WithInputTransformer(ctx =>
        {
            var insights = string.Join("\n",
                ctx.StepResults.Select(r => $"- {r.AgentName}: {r.Output.Substring(0, 100)}..."));
            return $"Based on these insights, provide recommendations:\n{insights}";
        }))
    .Build();
```

## Advanced Topics

### Custom Step Metadata

Add custom metadata to steps for tracking or configuration:

```csharp
builder.AddAgent("custom-agent", step => step
    .WithMetadata("priority", "high")
    .WithMetadata("cost-center", "research")
    .WithMetadata("required-permission", "admin"))
```

### Pipeline Cancellation

Support cancellation tokens for long-running pipelines:

```csharp
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromMinutes(5));

try
{
    var result = await pipeline.ExecuteAsync(input, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Pipeline was cancelled");
}
```

### Dynamic Pipeline Construction

Build pipelines dynamically based on runtime conditions:

```csharp
var builder = orchestrator.CreatePipeline("dynamic-pipeline");

// Always add router
builder.AddAgent("router-agent");

// Conditionally add processing agents
if (requiresKnowledge)
    builder.AddAgent("rag-agent");

if (requiresCode)
    builder.AddAgent("coding-agent");

// Always add summarization
builder.AddAgent("summarization-agent");

var pipeline = builder.Build();
```

### Error Recovery Strategies

Implement custom error recovery logic:

```csharp
builder.AddAgent("primary-agent")
    .WithErrorHandling(continueOnError: true, maxRetries: 2);

builder.AddAgent("fallback-agent", step => step
    .WithCondition(ctx =>
    {
        // Only execute if primary failed
        var primaryResult = ctx.GetStepResult("primary-agent");
        return primaryResult?.Success == false;
    }))
```

## Troubleshooting

### Pipeline Not Executing Steps

**Issue**: Steps are being skipped unexpectedly.

**Solution**: Check condition logic and ensure `ShouldContinue` is true:

```csharp
.AddAgentIf("agent-name", ctx =>
{
    var condition = /* your logic */;
    Console.WriteLine($"Condition result: {condition}");  // Debug
    return condition;
})
```

### Timeout Issues

**Issue**: Steps timing out frequently.

**Solution**: Increase timeout or optimize agent performance:

```csharp
// Increase timeout
.WithTimeout(TimeSpan.FromMinutes(2))

// Or make timeout configurable
.WithTimeout(TimeSpan.FromSeconds(config.GetValue<int>("AgentTimeout")))
```

### Memory Pressure

**Issue**: Pipeline using excessive memory with large contexts.

**Solution**: Limit step results stored in context:

```csharp
.WithOutputTransformer((ctx, output) =>
{
    // Store only summary, not full output
    ctx.SharedState[$"{step.AgentName}_summary"] = output.Substring(0, 500);
    return output;
})
```

### Error Propagation

**Issue**: Errors not being handled correctly.

**Solution**: Use appropriate error handling strategy:

```csharp
// Fail fast (default)
.AddAgent("critical-agent")

// Continue on error
.AddAgent("optional-agent")
.WithErrorHandling(continueOnError: true)

// Retry with backoff
.AddAgent("flaky-agent")
.WithErrorHandling(continueOnError: false, maxRetries: 3, retryDelay: TimeSpan.FromSeconds(2))
```

## Next Steps

- Explore [Built-in Agents](../agents/BUILTIN_AGENTS.md) for pre-configured agents
- Review [PipelineSample](../../samples/PipelineSample/) for complete examples
- Learn about [Agent Configuration](./AGENT_CONFIGURATION.md) for customizing agents
- Check [API Reference](./API_REFERENCE.md) for detailed method documentation

## Feedback and Contributions

Found an issue or have a suggestion? Please open an issue on the Ironbees GitHub repository.
