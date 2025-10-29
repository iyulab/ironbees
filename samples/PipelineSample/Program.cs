using DotNetEnv;
using Ironbees.Core;
using Ironbees.Core.Pipeline;
using Ironbees.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("🔄 Ironbees Agent Pipeline Demo");
Console.WriteLine("=====================================\n");

// Load .env file
var envPath = Path.Combine("..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine("✅ Loaded .env file\n");
}

// Get configuration
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("❌ OPENAI_API_KEY environment variable is required");
    return;
}

Console.WriteLine($"🤖 Using model: {model}");
Console.WriteLine("=====================================\n");

// Setup DI
var services = new ServiceCollection();
var agentsPath = Path.Combine("..", "..", "agents");

services.AddSingleton<IAgentLoader>(sp => new FileSystemAgentLoader());
services.AddSingleton<IAgentRegistry, AgentRegistry>();
services.AddSingleton<IAgentSelector>(sp =>
    new KeywordAgentSelector(minimumConfidenceThreshold: 0.3));
services.AddSingleton<ILLMFrameworkAdapter>(sp =>
    new OpenAIAdapter(apiKey, model));
services.AddSingleton<IAgentOrchestrator>(sp =>
    new AgentOrchestrator(
        sp.GetRequiredService<IAgentLoader>(),
        sp.GetRequiredService<IAgentRegistry>(),
        sp.GetRequiredService<ILLMFrameworkAdapter>(),
        sp.GetRequiredService<IAgentSelector>(),
        agentsPath));

var serviceProvider = services.BuildServiceProvider();
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();

// Load agents
await orchestrator.LoadAgentsAsync();
var agents = orchestrator.ListAgents();
Console.WriteLine($"✅ Loaded {agents.Count} agents\n");

// ============================================
// SCENARIO 1: Simple Sequential Pipeline
// ============================================
Console.WriteLine("=====================================");
Console.WriteLine("SCENARIO 1: Simple Sequential Pipeline");
Console.WriteLine("=====================================\n");

Console.WriteLine("Pipeline: router-agent → rag-agent → summarization-agent\n");

var pipeline1 = orchestrator.CreatePipeline("simple-pipeline")
    .AddAgent("router-agent")
    .AddAgent("rag-agent")
    .AddAgent("summarization-agent")
    .Build();

var input1 = @"Context: Ironbees is a multi-agent orchestration framework for .NET.
It supports agent pipelines, conversation history, and built-in agents.

Question: What is Ironbees and what are its main features?";

Console.WriteLine($"Input: {input1.Substring(0, Math.Min(100, input1.Length))}...\n");

try
{
    var result1 = await pipeline1.ExecuteAsync(input1);

    Console.WriteLine($"✅ Pipeline completed successfully!");
    Console.WriteLine($"⏱️  Total time: {result1.TotalExecutionTime.TotalSeconds:F2}s");
    Console.WriteLine($"📊 Steps executed: {result1.StepsExecuted}\n");

    Console.WriteLine("Step Results:");
    foreach (var step in result1.Context.StepResults)
    {
        Console.WriteLine($"\n[{step.AgentName}] ({step.ExecutionTime.TotalMilliseconds:F0}ms)");
        Console.WriteLine($"Output: {step.Output.Substring(0, Math.Min(150, step.Output.Length))}...");
    }

    Console.WriteLine($"\n🎯 Final Output:\n{result1.Output}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Pipeline failed: {ex.Message}\n");
}

// ============================================
// SCENARIO 2: Pipeline with Transformers
// ============================================
Console.WriteLine("\n=====================================");
Console.WriteLine("SCENARIO 2: Pipeline with Input/Output Transformers");
Console.WriteLine("=====================================\n");

var pipeline2 = orchestrator.CreatePipeline("transformer-pipeline")
    .AddAgent("function-calling-agent", step => step
        .WithInputTransformer(ctx =>
        {
            // Transform input to focus on function calling
            return $"Explain step-by-step how to: {ctx.OriginalInput}";
        })
        .WithOutputTransformer((ctx, output) =>
        {
            // Add metadata to output
            return $"[Function Calling Agent Analysis]\n{output}";
        }))
    .AddAgent("summarization-agent", step => step
        .WithInputTransformer(ctx =>
        {
            // Summarize the previous step's output
            var prevResult = ctx.GetLastStepResult();
            return $"Summarize this in 2-3 sentences:\n\n{prevResult?.Output}";
        }))
    .Build();

var input2 = "Get weather data from an API and convert temperature units";

Console.WriteLine($"Input: {input2}\n");

try
{
    var result2 = await pipeline2.ExecuteAsync(input2);

    Console.WriteLine($"✅ Pipeline completed!");
    Console.WriteLine($"⏱️  Total time: {result2.TotalExecutionTime.TotalSeconds:F2}s\n");

    foreach (var step in result2.Context.StepResults)
    {
        Console.WriteLine($"\n[{step.AgentName}]");
        Console.WriteLine($"Input: {step.Input.Substring(0, Math.Min(100, step.Input.Length))}...");
        Console.WriteLine($"Output: {step.Output.Substring(0, Math.Min(200, step.Output.Length))}...");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Pipeline failed: {ex.Message}\n");
}

// ============================================
// SCENARIO 3: Conditional Pipeline
// ============================================
Console.WriteLine("\n=====================================");
Console.WriteLine("SCENARIO 3: Conditional Pipeline (Dynamic Routing)");
Console.WriteLine("=====================================\n");

var pipeline3 = orchestrator.CreatePipeline("conditional-pipeline")
    .AddAgent("router-agent")  // First, analyze the request
    .AddAgentIf("coding-agent", ctx =>
    {
        // Only run coding agent if router suggests code-related task
        var routerResult = ctx.GetStepResult("router-agent");
        return routerResult?.Output.Contains("coding", StringComparison.OrdinalIgnoreCase) ?? false;
    })
    .AddAgentIf("summarization-agent", ctx =>
    {
        // Only run summarization if previous step exists
        return ctx.GetLastStepResult() != null;
    })
    .Build();

var input3 = "Write a Python function to calculate factorial";

Console.WriteLine($"Input: {input3}\n");

try
{
    var result3 = await pipeline3.ExecuteAsync(input3);

    Console.WriteLine($"✅ Pipeline completed!");
    Console.WriteLine($"📊 Steps executed: {result3.StepsExecuted} (some may have been skipped)\n");

    foreach (var step in result3.Context.StepResults)
    {
        Console.WriteLine($"\n✓ [{step.AgentName}] executed");
    }

    Console.WriteLine($"\n🎯 Final Output:\n{result3.Output?.Substring(0, Math.Min(300, result3.Output?.Length ?? 0))}...\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Pipeline failed: {ex.Message}\n");
}

// ============================================
// SCENARIO 4: Error Handling and Retry
// ============================================
Console.WriteLine("\n=====================================");
Console.WriteLine("SCENARIO 4: Error Handling with Retry");
Console.WriteLine("=====================================\n");

var pipeline4 = orchestrator.CreatePipeline("error-handling-pipeline")
    .AddAgent("rag-agent")
    .WithErrorHandling(
        continueOnError: true,
        maxRetries: 2,
        retryDelay: TimeSpan.FromSeconds(1))
    .AddAgent("summarization-agent")
    .WithTimeout(TimeSpan.FromSeconds(30))
    .Build();

var input4 = "Context: Error handling demo. Question: How does retry logic work?";

Console.WriteLine($"Input: {input4}\n");
Console.WriteLine("Configuration:");
Console.WriteLine("- rag-agent: continueOnError=true, maxRetries=2");
Console.WriteLine("- summarization-agent: timeout=30s\n");

try
{
    var result4 = await pipeline4.ExecuteAsync(input4);

    Console.WriteLine($"✅ Pipeline completed!");
    Console.WriteLine($"⏱️  Total time: {result4.TotalExecutionTime.TotalSeconds:F2}s");
    Console.WriteLine($"📊 Steps executed: {result4.StepsExecuted}");
    Console.WriteLine($"❌ Steps failed: {result4.StepsFailed}\n");

    foreach (var step in result4.Context.StepResults)
    {
        var status = step.Success ? "✓" : "✗";
        Console.WriteLine($"{status} [{step.AgentName}] - {(step.Success ? "Success" : "Failed")}");
    }

    if (result4.Success)
    {
        Console.WriteLine($"\n🎯 Final Output:\n{result4.Output?.Substring(0, Math.Min(200, result4.Output?.Length ?? 0))}...\n");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Pipeline failed: {ex.Message}\n");
}

// ============================================
// SCENARIO 5: Quick Pipeline Helper
// ============================================
Console.WriteLine("\n=====================================");
Console.WriteLine("SCENARIO 5: Quick Pipeline (Helper Method)");
Console.WriteLine("=====================================\n");

var input5 = "Analyze: User engagement metrics show 30% increase";

Console.WriteLine($"Input: {input5}\n");
Console.WriteLine("Pipeline: analysis-agent → writing-agent → summarization-agent\n");

try
{
    // Quick pipeline without builder
    var result5 = await orchestrator.ExecutePipelineAsync(
        input5,
        new[] { "analysis-agent", "writing-agent", "summarization-agent" }
    );

    Console.WriteLine($"✅ Quick pipeline completed!");
    Console.WriteLine($"⏱️  Total time: {result5.TotalExecutionTime.TotalSeconds:F2}s\n");
    Console.WriteLine($"🎯 Final Output:\n{result5.Output?.Substring(0, Math.Min(300, result5.Output?.Length ?? 0))}...\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Pipeline failed: {ex.Message}\n");
}

Console.WriteLine("=====================================");
Console.WriteLine("🎉 All Pipeline Scenarios Completed!");
Console.WriteLine("=====================================");
