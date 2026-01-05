using DotNetEnv;
using Ironbees.AgentFramework.Workflow;
using Ironbees.AgentMode.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace WorkflowSample;

/// <summary>
/// Sample demonstrating MAF Workflow execution with real OpenAI API.
/// Tests the complete workflow pipeline: YAML -> Conversion -> MAF Execution.
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        🐝 Ironbees MAF Workflow Sample                    ║");
        Console.WriteLine("║      YAML → MAF Conversion → Real API Execution           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Load .env file
        var envPath = FindEnvFile();
        if (envPath != null)
        {
            Env.Load(envPath);
            Console.WriteLine($"✅ Loaded .env from: {envPath}");
        }
        else
        {
            Console.WriteLine("⚠️  .env file not found, using environment variables");
        }

        // Get OpenAI configuration
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Error: OPENAI_API_KEY not set");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"🔑 API Key: {apiKey[..15]}...");
        Console.WriteLine($"🤖 Model: {model}");
        Console.WriteLine();

        try
        {
            // Run all tests
            await RunTest1_BasicAgentOrchestration(apiKey, model);
            await RunTest2_YamlWorkflowLoading();
            await RunTest3_WorkflowValidation();
            await RunTest4_WorkflowConversion(apiKey, model);
            await RunTest5_EndToEndWorkflowExecution(apiKey, model);
            await RunTest6_CheckpointStore();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n═══════════════════════════════════════════════════════");
            Console.WriteLine("✅ All tests completed successfully!");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Test failed: {ex.Message}");
            Console.WriteLine($"   {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Test 1: Basic agent creation and execution with OpenAI.
    /// </summary>
    private static async Task RunTest1_BasicAgentOrchestration(string apiKey, string model)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Test 1: Basic Agent Creation & Execution (OpenAI)");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        // Create OpenAI client
        var openAiClient = new OpenAIClient(apiKey);
        var chatClient = openAiClient.GetChatClient(model).AsIChatClient();

        // Create AI Agent using MAF
        var agent = chatClient.CreateAIAgent(
            instructions: "You are a helpful assistant. Be concise and direct.",
            name: "test-agent");

        Console.WriteLine($"✅ Created AIAgent: {agent.Name}");

        // Test agent execution
        var input = "What is 2 + 2? Answer in one word.";
        Console.WriteLine($"💬 Input: {input}");
        Console.Write("🤖 Response: ");

        var response = await agent.RunAsync(input);
        Console.WriteLine(response.Text);
        Console.WriteLine();
    }

    /// <summary>
    /// Test 2: YAML workflow loading.
    /// </summary>
    private static async Task RunTest2_YamlWorkflowLoading()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Test 2: YAML Workflow Loading");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        var loader = new YamlWorkflowLoader();

        // Load from string (simulating YAML file)
        var yamlContent = @"
name: TestWorkflow
version: '1.0'
description: Test workflow for validation

states:
  - id: START
    type: start
    next: AGENT

  - id: AGENT
    type: agent
    executor: test-agent
    next: END

  - id: END
    type: terminal
";

        var workflow = await loader.LoadFromStringAsync(yamlContent);

        Console.WriteLine($"✅ Loaded workflow: {workflow.Name}");
        Console.WriteLine($"   Version: {workflow.Version}");
        Console.WriteLine($"   States: {workflow.States.Count}");
        foreach (var state in workflow.States)
        {
            Console.WriteLine($"   - {state.Id} ({state.Type})");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Test 3: Workflow validation.
    /// </summary>
    private static async Task RunTest3_WorkflowValidation()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Test 3: Workflow Validation");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var converter = new MafWorkflowConverter(loggerFactory.CreateLogger<MafWorkflowConverter>());
        var loader = new YamlWorkflowLoader();

        // Valid workflow
        var validWorkflow = new WorkflowDefinition
        {
            Name = "ValidWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PROCESS" },
                new WorkflowStateDefinition { Id = "PROCESS", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        var validResult = converter.Validate(validWorkflow);
        Console.WriteLine($"Valid workflow validation:");
        Console.WriteLine($"   IsValid: {validResult.IsValid}");
        Console.WriteLine($"   Errors: {validResult.Errors.Count}");
        Console.WriteLine($"   Warnings: {validResult.Warnings.Count}");

        // Invalid workflow (missing start state)
        var invalidWorkflow = new WorkflowDefinition
        {
            Name = "InvalidWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "PROCESS", Type = WorkflowStateType.Agent, Executor = "worker", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        var invalidResult = converter.Validate(invalidWorkflow);
        Console.WriteLine($"\nInvalid workflow validation:");
        Console.WriteLine($"   IsValid: {invalidResult.IsValid}");
        Console.WriteLine($"   Errors: {invalidResult.Errors.Count}");
        foreach (var error in invalidResult.Errors)
        {
            Console.WriteLine($"   - [{error.Code}] {error.Message}");
        }

        // Workflow with warning (trigger)
        var warningWorkflow = new WorkflowDefinition
        {
            Name = "WarningWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "WAIT" },
                new WorkflowStateDefinition
                {
                    Id = "WAIT",
                    Type = WorkflowStateType.Agent,
                    Executor = "worker",
                    Trigger = new TriggerDefinition { Type = TriggerType.FileExists, Path = "input.txt" },
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        var warningResult = converter.Validate(warningWorkflow);
        Console.WriteLine($"\nWorkflow with warnings:");
        Console.WriteLine($"   IsValid: {warningResult.IsValid}");
        Console.WriteLine($"   Warnings: {warningResult.Warnings.Count}");
        foreach (var warning in warningResult.Warnings)
        {
            Console.WriteLine($"   - [{warning.Code}] {warning.Message}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Test 4: Workflow conversion to MAF format.
    /// </summary>
    private static async Task RunTest4_WorkflowConversion(string apiKey, string model)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Test 4: Workflow Conversion to MAF");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var converter = new MafWorkflowConverter(loggerFactory.CreateLogger<MafWorkflowConverter>());

        // Create agent resolver
        var openAiClient = new OpenAIClient(apiKey);
        var chatClient = openAiClient.GetChatClient(model).AsIChatClient();

        Func<string, CancellationToken, Task<AIAgent>> agentResolver = (name, ct) =>
        {
            AIAgent agent = chatClient.CreateAIAgent(
                instructions: $"You are {name}. Be helpful and concise.",
                name: name);
            return Task.FromResult(agent);
        };

        // Sequential workflow
        var sequentialWorkflow = new WorkflowDefinition
        {
            Name = "SequentialWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "STEP1" },
                new WorkflowStateDefinition { Id = "STEP1", Type = WorkflowStateType.Agent, Executor = "agent1", Next = "STEP2" },
                new WorkflowStateDefinition { Id = "STEP2", Type = WorkflowStateType.Agent, Executor = "agent2", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        var mafWorkflow = await converter.ConvertAsync(sequentialWorkflow, agentResolver);
        Console.WriteLine($"✅ Converted sequential workflow: {sequentialWorkflow.Name}");
        Console.WriteLine($"   MAF Workflow created successfully");

        // Parallel workflow
        var parallelWorkflow = new WorkflowDefinition
        {
            Name = "ParallelWorkflow",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "PARALLEL" },
                new WorkflowStateDefinition
                {
                    Id = "PARALLEL",
                    Type = WorkflowStateType.Parallel,
                    Executors = ["agent1", "agent2", "agent3"],
                    Next = "END"
                },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        var mafParallel = await converter.ConvertAsync(parallelWorkflow, agentResolver);
        Console.WriteLine($"✅ Converted parallel workflow: {parallelWorkflow.Name}");
        Console.WriteLine($"   MAF Workflow created successfully");
        Console.WriteLine();
    }

    /// <summary>
    /// Test 5: End-to-end workflow execution with real API.
    /// </summary>
    private static async Task RunTest5_EndToEndWorkflowExecution(string apiKey, string model)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Test 5: End-to-End Workflow Execution (Real API)");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var converter = new MafWorkflowConverter(loggerFactory.CreateLogger<MafWorkflowConverter>());
        var executor = new MafWorkflowExecutor(converter, loggerFactory.CreateLogger<MafWorkflowExecutor>());

        // Create agent resolver with real OpenAI
        var openAiClient = new OpenAIClient(apiKey);
        var chatClient = openAiClient.GetChatClient(model).AsIChatClient();

        Func<string, CancellationToken, Task<AIAgent>> agentResolver = (name, ct) =>
        {
            var instructions = name switch
            {
                "writer" => "You are a creative writer. Write engaging content in 2-3 sentences.",
                "reviewer" => "You are a content reviewer. Provide brief feedback (1-2 sentences) on the content quality.",
                _ => $"You are {name}. Be helpful and concise."
            };

            AIAgent agent = chatClient.CreateAIAgent(instructions: instructions, name: name);
            return Task.FromResult(agent);
        };

        // Create workflow: Writer -> Reviewer
        var workflow = new WorkflowDefinition
        {
            Name = "WriteReviewWorkflow",
            Description = "Sequential workflow: Writer creates content, Reviewer evaluates it",
            States =
            [
                new WorkflowStateDefinition { Id = "START", Type = WorkflowStateType.Start, Next = "WRITE" },
                new WorkflowStateDefinition { Id = "WRITE", Type = WorkflowStateType.Agent, Executor = "writer", Next = "REVIEW" },
                new WorkflowStateDefinition { Id = "REVIEW", Type = WorkflowStateType.Agent, Executor = "reviewer", Next = "END" },
                new WorkflowStateDefinition { Id = "END", Type = WorkflowStateType.Terminal }
            ]
        };

        var input = "Write a short paragraph about the future of AI agents in software development.";
        Console.WriteLine($"💬 Input: {input}\n");
        Console.WriteLine("📋 Workflow Events:\n");

        var eventCount = 0;
        await foreach (var evt in executor.ExecuteAsync(workflow, input, agentResolver))
        {
            eventCount++;
            var agentInfo = evt.AgentName != null ? $" ({evt.AgentName})" : "";
            var contentPreview = evt.Content?.Length > 100
                ? $"{evt.Content[..100]}..."
                : evt.Content;

            Console.WriteLine($"[{evt.Type}]{agentInfo}");
            if (!string.IsNullOrEmpty(contentPreview))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"   {contentPreview}");
                Console.ResetColor();
            }
        }

        Console.WriteLine($"\n✅ Workflow completed with {eventCount} events");
        Console.WriteLine();
    }

    /// <summary>
    /// Test 6: Checkpoint store operations.
    /// </summary>
    private static async Task RunTest6_CheckpointStore()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Test 6: Checkpoint Store Operations");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        var tempDir = Path.Combine(Path.GetTempPath(), "ironbees-workflow-test");

        using var store = new FileSystemCheckpointStore(tempDir);

        // Create checkpoint
        var checkpoint = new CheckpointData
        {
            CheckpointId = "chk-001",
            ExecutionId = "exec-001",
            WorkflowName = "TestWorkflow",
            CurrentStateId = "AGENT",
            Input = "Test input",
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        // Save
        await store.SaveAsync(checkpoint);
        Console.WriteLine($"✅ Saved checkpoint: {checkpoint.CheckpointId}");

        // Exists
        var exists = await store.ExistsAsync(checkpoint.CheckpointId);
        Console.WriteLine($"   Exists: {exists}");

        // Get
        var retrieved = await store.GetAsync(checkpoint.CheckpointId);
        Console.WriteLine($"   Retrieved: {retrieved?.CheckpointId}");
        Console.WriteLine($"   WorkflowName: {retrieved?.WorkflowName}");
        Console.WriteLine($"   CurrentStateId: {retrieved?.CurrentStateId}");

        // Get latest for execution
        var latest = await store.GetLatestForExecutionAsync(checkpoint.ExecutionId);
        Console.WriteLine($"   Latest for execution: {latest?.CheckpointId}");

        // Delete
        var deleted = await store.DeleteAsync(checkpoint.CheckpointId);
        Console.WriteLine($"   Deleted: {deleted}");

        // Cleanup temp directory
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch { /* ignore cleanup errors */ }

        Console.WriteLine();
    }

    private static string? FindEnvFile()
    {
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".env"),
            @"D:\data\ironbees\.env"
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
