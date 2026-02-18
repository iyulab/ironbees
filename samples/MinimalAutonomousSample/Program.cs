// Minimal Autonomous SDK Sample
// Demonstrates the simplest usage pattern for goal-based iterative execution
//
// This sample shows:
// - YAML-based configuration
// - Simple executor and oracle implementation
// - Auto-continue loop
// - Final iteration enforcement

using Ironbees.Autonomous;
using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Configuration;
using Ironbees.Autonomous.Models;

// ============================================================================
// STEP 1: Load configuration from YAML
// ============================================================================
var loader = new SettingsLoader();
var settings = await loader.LoadWithEnvironmentAsync("settings.yaml");

Console.WriteLine("🚀 Minimal Autonomous Sample");
Console.WriteLine("═══════════════════════════════════════════════════════");

// ============================================================================
// STEP 2: Create simple executor and oracle
// ============================================================================
var executor = new SimpleExecutor();
var oracle = new SimpleOracle();

// ============================================================================
// STEP 3: Build the orchestrator with minimal configuration
// ============================================================================
var orchestrator = AutonomousOrchestrator.Create<SimpleRequest, SimpleResult>()
    .WithSettings(settings)
    .WithExecutor(executor)
    .WithRequestFactory((id, prompt) => new SimpleRequest(id, prompt))
    .WithOracle(oracle)
    .WithFinalIterationEnforcement(ctx =>
    {
        // Force a final answer on last iteration
        Console.WriteLine("⚠️ Forcing final answer...");
        return new SimpleResult(
            RequestId: ctx.OriginalRequest.RequestId,
            Output: $"FINAL ANSWER: Based on {ctx.PreviousOutputs.Count} attempts, my best conclusion is complete.",
            Success: true);
    })
    .Build();

// ============================================================================
// STEP 4: Dump configuration (for debugging)
// ============================================================================
if (settings.Debug?.Enabled == true)
{
    Console.WriteLine("\n📋 Configuration:");
    orchestrator.DumpConfiguration(Console.Out);
    Console.WriteLine();
}

// ============================================================================
// STEP 5: Subscribe to events
// ============================================================================
orchestrator.OnEvent += evt =>
{
    var emoji = evt.Type switch
    {
        AutonomousEventType.IterationStarted => "🔄",
        AutonomousEventType.TaskOutput => "💬",
        AutonomousEventType.OracleVerified => "🔍",
        AutonomousEventType.Completed => "✅",
        AutonomousEventType.FinalIterationApproaching => "⚠️",
        AutonomousEventType.ForcedCompletion => "🎯",
        _ => "📌"
    };

    if (evt.Type == AutonomousEventType.TaskOutput)
    {
        Console.WriteLine($"{emoji} Output: {evt.Message?[..Math.Min(80, evt.Message.Length)]}...");
    }
    else if (evt.Type == AutonomousEventType.OracleVerified && evt.OracleVerdict != null)
    {
        Console.WriteLine($"{emoji} Oracle: complete={evt.OracleVerdict.IsComplete}, confidence={evt.OracleVerdict.Confidence:P0}");
    }
    else
    {
        Console.WriteLine($"{emoji} {evt.Type}: {evt.Message}");
    }
};

// ============================================================================
// STEP 6: Run the task
// ============================================================================
var task = args.Length > 0
    ? string.Join(" ", args)
    : "What is the capital of France?";

Console.WriteLine($"\n🎯 Task: {task}\n");

orchestrator.EnqueuePrompt(task);
await orchestrator.StartAsync();

Console.WriteLine("\n═══════════════════════════════════════════════════════");
Console.WriteLine($"Final State: {orchestrator.Status.State}");
Console.WriteLine($"Total Iterations: {orchestrator.Status.CurrentIteration}");

// ============================================================================
// Simple Implementations
// ============================================================================
#pragma warning disable CA1050 // Declare types in namespaces — top-level statements sample file
public record SimpleRequest(string RequestId, string Prompt) : ITaskRequest;

public record SimpleResult(string RequestId, string Output, bool Success, string? ErrorOutput = null) : ITaskResult;

/// <summary>
/// Simple executor that simulates thinking and provides answers
/// </summary>
public class SimpleExecutor : ITaskExecutor<SimpleRequest, SimpleResult>
{
    private static int _iteration;

    public async Task<SimpleResult> ExecuteAsync(
        SimpleRequest request,
        Action<TaskOutput>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        _iteration++;
        await Task.Delay(100, cancellationToken); // Simulate processing

        var output = _iteration switch
        {
            1 => "Thinking about the question...",
            2 => "Analyzing available information...",
            3 => "The capital of France is Paris.",
            _ => $"Iteration {_iteration}: Continuing analysis..."
        };

        onOutput?.Invoke(new TaskOutput
        {
            RequestId = request.RequestId,
            Content = output,
            Type = TaskOutputType.Output
        });

        return new SimpleResult(
            RequestId: request.RequestId,
            Output: output,
            Success: true);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple oracle that checks if the answer is complete
/// </summary>
public class SimpleOracle : IOracleVerifier
{
    public bool IsConfigured => true;

    public async Task<OracleVerdict> VerifyAsync(
        string originalPrompt,
        string executionOutput,
        OracleConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken); // Simulate verification

        // Check if output contains a definitive answer
        if (executionOutput.Contains("Paris") || executionOutput.Contains("FINAL ANSWER"))
        {
            return OracleVerdict.GoalAchieved(
                "Found definitive answer about Paris",
                confidence: 0.95);
        }

        // Check if still processing
        if (executionOutput.Contains("Thinking") || executionOutput.Contains("Analyzing"))
        {
            return OracleVerdict.ContinueToNextIteration(
                "Still processing, continue",
                confidence: 0.3);
        }

        // Default: continue with more analysis
        return OracleVerdict.ContinueToNextIteration(
            "Need more information",
            confidence: 0.5);
    }

    public string BuildVerificationPrompt(string originalPrompt, string executionOutput, OracleConfig? config = null)
    {
        return $"Goal: {originalPrompt}\nOutput: {executionOutput}\nVerify if the goal is achieved.";
    }
}
