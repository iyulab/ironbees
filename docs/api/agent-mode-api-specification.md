# Ironbees Agent Mode - API Specification v1.0

**Version**: 1.0.0
**Status**: Draft
**Date**: 2025-11-11
**Target Release**: Phase 1 (v0.1.0)

---

## Table of Contents

1. [Overview](#overview)
2. [Design Principles](#design-principles)
3. [Core Interfaces](#core-interfaces)
4. [Data Models](#data-models)
5. [Configuration Schema](#configuration-schema)
6. [Usage Examples](#usage-examples)
7. [Error Handling](#error-handling)
8. [Versioning Strategy](#versioning-strategy)
9. [Migration Guide](#migration-guide)

---

## 1. Overview

### 1.1 Purpose

This document specifies the public API contract for Ironbees Agent Mode, a .NET-native autonomous coding agent framework. It defines interfaces, data models, configuration schema, and usage patterns for:

- **Application Developers**: Integrate Agent Mode into applications
- **Agent Developers**: Create custom agents using the framework
- **Tool Developers**: Implement MCP-compatible tools
- **Contributors**: Extend the framework capabilities

### 1.2 Scope

**In Scope**:
- Public interfaces for orchestration, agents, and tools
- Data models for state management and execution flow
- Configuration schema for convention-based setup
- Error handling and exception types
- Extension points for customization

**Out of Scope**:
- Internal implementation details
- Private helper classes and utilities
- Specific LLM provider APIs (abstracted behind IChatClient)
- MCP server implementation specifics (defined by MCP protocol)

### 1.3 Package Structure

```
Ironbees.AgentMode.Core (v1.0.0)
├── Ironbees.AgentMode.Abstractions  (Public interfaces)
├── Ironbees.AgentMode.Models        (Data models)
└── Ironbees.AgentMode.Configuration (Configuration)

Ironbees.AgentMode.Agents (v1.0.0)
└── Built-in agents (PlannerAgent, CoderAgent, etc.)

Ironbees.AgentMode.MCP (v1.0.0)
└── MCP protocol client implementation

Ironbees.AgentMode.Tools (v1.0.0)
└── Built-in tools (RoslynTool, MSBuildTool, etc.)
```

---

## 2. Design Principles

### 2.1 Convention over Configuration

```csharp
// Agents discovered automatically from /agents directory
/agents
  /planner
    agent.yaml          // Convention-based discovery
    prompt.md
  /coder
    agent.yaml
    prompt.md
```

### 2.2 Immutable State Management

```csharp
// All state transitions create new immutable instances
var newState = currentState with
{
    Plan = executionPlan,
    CurrentNode = "CODE"
};
```

### 2.3 Async Streaming

```csharp
// All orchestration returns IAsyncEnumerable for real-time updates
await foreach (var state in orchestrator.ExecuteAsync(request))
{
    Console.WriteLine($"Node: {state.CurrentNode}");
}
```

### 2.4 Dependency Injection

```csharp
// All components registered via DI
services.AddIronbeesAgentMode(config =>
{
    config.UseConventionBasedAgents();
    config.AddMcpServers();
});
```

---

## 3. Core Interfaces

### 3.1 IStatefulOrchestrator

**Purpose**: Main entry point for executing coding workflows with stateful graph orchestration.

**Package**: `Ironbees.AgentMode.Abstractions`

```csharp
namespace Ironbees.AgentMode;

/// <summary>
/// Stateful graph orchestrator for autonomous coding workflows.
/// Manages state transitions, agent coordination, and human-in-the-loop approvals.
/// </summary>
public interface IStatefulOrchestrator
{
    /// <summary>
    /// Executes a coding workflow from user request to completion.
    /// Streams state updates as workflow progresses through nodes.
    /// </summary>
    /// <param name="request">User's natural language request (e.g., "Add authentication to UserController")</param>
    /// <param name="context">Optional context (solution path, project references, etc.)</param>
    /// <param name="cancellationToken">Cancellation token for workflow interruption</param>
    /// <returns>Async stream of CodingState updates for real-time monitoring</returns>
    /// <exception cref="ArgumentNullException">When request is null or empty</exception>
    /// <exception cref="OrchestratorException">When workflow execution fails</exception>
    IAsyncEnumerable<CodingState> ExecuteAsync(
        string request,
        WorkflowContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves or rejects a workflow waiting for human decision.
    /// Used for HITL (Human-in-the-Loop) approval gates.
    /// </summary>
    /// <param name="stateId">Unique identifier of the workflow state</param>
    /// <param name="decision">Approval decision with optional feedback</param>
    /// <returns>Task that completes when approval is processed</returns>
    /// <exception cref="StateNotFoundException">When stateId doesn't exist</exception>
    /// <exception cref="InvalidStateException">When state is not awaiting approval</exception>
    Task ApproveAsync(string stateId, ApprovalDecision decision);

    /// <summary>
    /// Cancels an active workflow execution.
    /// </summary>
    /// <param name="stateId">Unique identifier of the workflow state</param>
    /// <returns>Task that completes when cancellation is processed</returns>
    /// <exception cref="StateNotFoundException">When stateId doesn't exist</exception>
    Task CancelAsync(string stateId);

    /// <summary>
    /// Retrieves the current state of a workflow.
    /// </summary>
    /// <param name="stateId">Unique identifier of the workflow state</param>
    /// <returns>Current CodingState snapshot</returns>
    /// <exception cref="StateNotFoundException">When stateId doesn't exist</exception>
    Task<CodingState> GetStateAsync(string stateId);
}
```

**Usage**:

```csharp
// Inject orchestrator
public class MyService
{
    private readonly IStatefulOrchestrator _orchestrator;

    public MyService(IStatefulOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task ProcessRequestAsync(string userRequest)
    {
        var context = new WorkflowContext
        {
            SolutionPath = @"D:\MyProject\MyProject.sln",
            TargetProject = "MyProject.Api"
        };

        await foreach (var state in _orchestrator.ExecuteAsync(userRequest, context))
        {
            Console.WriteLine($"[{state.CurrentNode}] Iteration {state.IterationCount}");

            if (state.CurrentNode == "WAIT_PLAN_APPROVAL")
            {
                // HITL: Present plan to user
                Console.WriteLine(state.Plan?.ToString());
                var userApproves = GetUserApproval();

                await _orchestrator.ApproveAsync(state.StateId, new ApprovalDecision
                {
                    Approved = userApproves,
                    Feedback = userApproves ? null : "Please simplify the approach"
                });
            }

            if (state.CurrentNode == "END")
            {
                Console.WriteLine("Workflow completed successfully!");
                break;
            }
        }
    }
}
```

---

### 3.2 ICodingAgent

**Purpose**: Defines contract for coding agents that execute specific tasks within the workflow.

**Package**: `Ironbees.AgentMode.Abstractions`

```csharp
namespace Ironbees.AgentMode.Agents;

/// <summary>
/// Base interface for coding agents that execute specific tasks in the workflow.
/// Agents are stateless and receive CodingState as input.
/// </summary>
public interface ICodingAgent
{
    /// <summary>
    /// Unique name of the agent (e.g., "planner", "coder", "validator").
    /// Used for logging, telemetry, and agent selection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Agent description for documentation and debugging.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// List of tools (MCP servers) this agent requires.
    /// Used by orchestrator to validate tool availability.
    /// </summary>
    IReadOnlyList<string> RequiredTools { get; }

    /// <summary>
    /// Executes the agent's task based on current CodingState.
    /// </summary>
    /// <param name="state">Current workflow state with all context</param>
    /// <param name="cancellationToken">Cancellation token for interruption</param>
    /// <returns>Agent's response with updated state fields</returns>
    /// <exception cref="AgentExecutionException">When agent execution fails</exception>
    Task<AgentResponse> ExecuteAsync(
        CodingState state,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent response with updated state fields and metadata.
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// Updated state fields (partial update, not full state).
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Updates { get; init; }

    /// <summary>
    /// Next node to transition to (e.g., "CODE", "VALIDATE", "END").
    /// If null, orchestrator decides based on workflow graph.
    /// </summary>
    public string? NextNode { get; init; }

    /// <summary>
    /// Optional metadata for logging, telemetry, and debugging.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
```

**Usage**:

```csharp
public class PlannerAgent : ICodingAgent
{
    private readonly IChatClient _chatClient;
    private readonly IToolRegistry _toolRegistry;

    public string Name => "planner";
    public string Description => "Analyzes user request and creates execution plan";
    public IReadOnlyList<string> RequiredTools => new[] { "roslyn" };

    public PlannerAgent(IChatClient chatClient, IToolRegistry toolRegistry)
    {
        _chatClient = chatClient;
        _toolRegistry = toolRegistry;
    }

    public async Task<AgentResponse> ExecuteAsync(
        CodingState state,
        CancellationToken cancellationToken = default)
    {
        // Load prompt template
        var prompt = LoadPromptTemplate("planner");

        // Create AI agent with tools
        var agent = _chatClient.CreateAIAgent(
            instructions: prompt,
            tools: _toolRegistry.GetToolsForAgent("planner")
        );

        // Execute with context
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, $"User request: {state.UserRequest}"),
            new ChatMessage(ChatRole.System, $"Solution path: {state.Metadata["SolutionPath"]}")
        };

        var response = await agent.CompleteAsync(messages, cancellationToken: cancellationToken);

        // Parse execution plan from response
        var plan = ParseExecutionPlan(response.Content);

        return new AgentResponse
        {
            Updates = new Dictionary<string, object?>
            {
                ["Plan"] = plan,
                ["Spec"] = response.Metadata?["spec"] as string
            },
            NextNode = "WAIT_PLAN_APPROVAL",
            Metadata = new Dictionary<string, string>
            {
                ["model"] = response.Model,
                ["tokens"] = response.TokenUsage.ToString()
            }
        };
    }

    private ExecutionPlan ParseExecutionPlan(string content)
    {
        // Parse structured plan from LLM response
        // ...
    }
}
```

---

### 3.3 IMcpServer

**Purpose**: Interface for MCP-compatible tool servers that provide capabilities to agents.

**Package**: `Ironbees.AgentMode.Abstractions`

```csharp
namespace Ironbees.AgentMode.MCP;

/// <summary>
/// Interface for MCP (Model Context Protocol) servers that provide tools to agents.
/// Implements the MCP protocol specification (JSON-RPC 2.0).
/// </summary>
public interface IMcpServer
{
    /// <summary>
    /// Unique name of the MCP server (e.g., "roslyn", "msbuild", "git").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Server version following semantic versioning (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// List of tools provided by this server.
    /// Each tool has a name, description, and input schema.
    /// </summary>
    IReadOnlyList<ToolDefinition> Tools { get; }

    /// <summary>
    /// Initializes the MCP server with configuration.
    /// Called once during application startup.
    /// </summary>
    /// <param name="configuration">Server-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when server is ready</returns>
    Task InitializeAsync(
        IReadOnlyDictionary<string, object> configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a tool with provided arguments.
    /// </summary>
    /// <param name="toolName">Name of the tool to execute</param>
    /// <param name="arguments">Tool arguments as key-value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    /// <exception cref="ToolNotFoundException">When toolName doesn't exist</exception>
    /// <exception cref="ToolExecutionException">When tool execution fails</exception>
    Task<ToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disposes server resources.
    /// </summary>
    ValueTask DisposeAsync();
}

/// <summary>
/// Tool definition following MCP specification.
/// </summary>
public record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonSchema InputSchema { get; init; }
}

/// <summary>
/// Tool execution result.
/// </summary>
public record ToolResult
{
    public bool Success { get; init; }
    public object? Content { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
```

**Usage**:

```csharp
public class RoslynMcpServer : IMcpServer
{
    private Solution? _solution;

    public string Name => "roslyn";
    public string Version => "1.0.0";

    public IReadOnlyList<ToolDefinition> Tools => new[]
    {
        new ToolDefinition
        {
            Name = "load_solution",
            Description = "Load a .NET solution for analysis",
            InputSchema = new JsonSchema
            {
                Type = "object",
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["solutionPath"] = new JsonSchema { Type = "string" }
                },
                Required = new[] { "solutionPath" }
            }
        },
        new ToolDefinition
        {
            Name = "find_references",
            Description = "Find all references to a symbol",
            InputSchema = new JsonSchema
            {
                Type = "object",
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["symbol"] = new JsonSchema { Type = "string" }
                },
                Required = new[] { "symbol" }
            }
        }
    };

    public async Task InitializeAsync(
        IReadOnlyDictionary<string, object> configuration,
        CancellationToken cancellationToken = default)
    {
        // Initialize Roslyn workspace
        var solutionPath = (string)configuration["solutionPath"];
        var workspace = MSBuildWorkspace.Create();
        _solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken);
    }

    public async Task<ToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "load_solution" => await LoadSolutionAsync(arguments, cancellationToken),
            "find_references" => await FindReferencesAsync(arguments, cancellationToken),
            _ => throw new ToolNotFoundException($"Tool '{toolName}' not found in server '{Name}'")
        };
    }

    private async Task<ToolResult> FindReferencesAsync(
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        var symbolName = (string)arguments["symbol"];
        var references = new List<string>();

        foreach (var project in _solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var symbol = compilation.GetTypeByMetadataName(symbolName);

            if (symbol == null) continue;

            var referencedSymbols = await SymbolFinder.FindReferencesAsync(
                symbol, _solution, cancellationToken);

            foreach (var referencedSymbol in referencedSymbols)
            {
                foreach (var location in referencedSymbol.Locations)
                {
                    var lineSpan = location.Location.GetLineSpan();
                    references.Add(
                        $"{location.Document.FilePath}:{lineSpan.StartLinePosition.Line + 1}");
                }
            }
        }

        return new ToolResult
        {
            Success = true,
            Content = new
            {
                symbol = symbolName,
                count = references.Count,
                references = references
            }
        };
    }

    public ValueTask DisposeAsync()
    {
        _solution = null;
        return ValueTask.CompletedTask;
    }
}
```

---

### 3.4 IToolRegistry

**Purpose**: Registry for managing MCP servers and their tools.

**Package**: `Ironbees.AgentMode.Abstractions`

```csharp
namespace Ironbees.AgentMode.MCP;

/// <summary>
/// Registry for managing MCP servers and providing tools to agents.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers an MCP server with the registry.
    /// </summary>
    /// <param name="server">MCP server instance</param>
    void RegisterServer(IMcpServer server);

    /// <summary>
    /// Gets all tools available for a specific agent.
    /// </summary>
    /// <param name="agentName">Name of the agent</param>
    /// <returns>List of tools the agent can use</returns>
    IReadOnlyList<ToolDefinition> GetToolsForAgent(string agentName);

    /// <summary>
    /// Executes a tool from any registered server.
    /// </summary>
    /// <param name="toolName">Name of the tool</param>
    /// <param name="arguments">Tool arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    Task<ToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default);
}
```

---

## 4. Data Models

### 4.1 CodingState

**Purpose**: Immutable state object representing the current workflow state.

**Package**: `Ironbees.AgentMode.Models`

```csharp
namespace Ironbees.AgentMode.Models;

/// <summary>
/// Immutable state object for coding workflow.
/// Represents all information needed for workflow execution and recovery.
/// </summary>
public record CodingState
{
    /// <summary>
    /// Unique identifier for this workflow instance (UUID).
    /// Used for approval operations, state retrieval, and telemetry correlation.
    /// </summary>
    public required string StateId { get; init; }

    /// <summary>
    /// Original user request in natural language.
    /// Example: "Add JWT authentication to UserController"
    /// </summary>
    public required string UserRequest { get; init; }

    /// <summary>
    /// Structured specification parsed from user request.
    /// Generated by PlannerAgent, refined during workflow execution.
    /// </summary>
    public string? Spec { get; init; }

    /// <summary>
    /// Execution plan with step-by-step actions.
    /// Generated by PlannerAgent, approved by user in HITL mode.
    /// </summary>
    public ExecutionPlan? Plan { get; init; }

    /// <summary>
    /// List of file edits (diffs) to be applied.
    /// Generated by CoderAgent, validated by ValidatorAgent.
    /// </summary>
    public ImmutableList<FileEdit> CodeDiffs { get; init; } = ImmutableList<FileEdit>.Empty;

    /// <summary>
    /// Build result from MSBuild compilation.
    /// Used to detect compilation errors and guide refinement.
    /// </summary>
    public BuildResult? BuildResult { get; init; }

    /// <summary>
    /// Test result from dotnet test execution.
    /// Used to validate code correctness and guide refinement.
    /// </summary>
    public TestResult? TestResult { get; init; }

    /// <summary>
    /// Error context when workflow encounters failures.
    /// Contains error messages, stack traces, and diagnostic information.
    /// </summary>
    public string? ErrorContext { get; init; }

    /// <summary>
    /// Current iteration count in generate-validate-refine loop.
    /// Incremented after each validation failure.
    /// </summary>
    public int IterationCount { get; init; }

    /// <summary>
    /// Maximum iterations allowed before giving up.
    /// Default: 5
    /// </summary>
    public int MaxIterations { get; init; } = 5;

    /// <summary>
    /// Current node in the state graph (e.g., "PLAN", "CODE", "VALIDATE").
    /// Used by orchestrator to determine next agent to execute.
    /// </summary>
    public required string CurrentNode { get; init; }

    /// <summary>
    /// Timestamp of this state snapshot.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata for extensibility.
    /// Example: SolutionPath, TargetProject, UserPreferences, etc.
    /// </summary>
    public ImmutableDictionary<string, string> Metadata { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}
```

---

### 4.2 ExecutionPlan

**Purpose**: Structured plan with step-by-step actions for code generation.

**Package**: `Ironbees.AgentMode.Models`

```csharp
namespace Ironbees.AgentMode.Models;

/// <summary>
/// Structured execution plan with step-by-step actions.
/// </summary>
public record ExecutionPlan
{
    /// <summary>
    /// Human-readable summary of the plan.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// List of action steps to execute sequentially.
    /// </summary>
    public required ImmutableList<PlanStep> Steps { get; init; }

    /// <summary>
    /// List of files that will be modified.
    /// </summary>
    public ImmutableList<string> AffectedFiles { get; init; } = ImmutableList<string>.Empty;

    /// <summary>
    /// Estimated complexity (LOW, MEDIUM, HIGH).
    /// </summary>
    public string Complexity { get; init; } = "MEDIUM";
}

/// <summary>
/// Single step in the execution plan.
/// </summary>
public record PlanStep
{
    /// <summary>
    /// Step number (1-based).
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Description of the action to perform.
    /// Example: "Add JwtBearer authentication middleware to Startup.cs"
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Type of action (CREATE, MODIFY, DELETE, REFACTOR).
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// File path affected by this step.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Rationale for this step (for HITL transparency).
    /// </summary>
    public string? Rationale { get; init; }
}
```

---

### 4.3 FileEdit

**Purpose**: Represents a single file edit (diff) to be applied.

**Package**: `Ironbees.AgentMode.Models`

```csharp
namespace Ironbees.AgentMode.Models;

/// <summary>
/// Represents a file edit (diff) to be applied.
/// </summary>
public record FileEdit
{
    /// <summary>
    /// File path relative to solution root.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Edit type (CREATE, MODIFY, DELETE).
    /// </summary>
    public required EditType Type { get; init; }

    /// <summary>
    /// Original file content (for MODIFY) or null (for CREATE).
    /// </summary>
    public string? OriginalContent { get; init; }

    /// <summary>
    /// New file content after edit.
    /// </summary>
    public required string NewContent { get; init; }

    /// <summary>
    /// Unified diff format for display to user.
    /// </summary>
    public string? DiffText { get; init; }
}

/// <summary>
/// Type of file edit.
/// </summary>
public enum EditType
{
    Create,
    Modify,
    Delete
}
```

---

### 4.4 BuildResult

**Purpose**: Result of MSBuild compilation.

**Package**: `Ironbees.AgentMode.Models`

```csharp
namespace Ironbees.AgentMode.Models;

/// <summary>
/// Result of MSBuild compilation.
/// </summary>
public record BuildResult
{
    /// <summary>
    /// Whether build succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// List of compilation errors.
    /// </summary>
    public ImmutableList<CompilationError> Errors { get; init; }
        = ImmutableList<CompilationError>.Empty;

    /// <summary>
    /// List of compilation warnings.
    /// </summary>
    public ImmutableList<CompilationWarning> Warnings { get; init; }
        = ImmutableList<CompilationWarning>.Empty;

    /// <summary>
    /// Build duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Compilation error from MSBuild.
/// </summary>
public record CompilationError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
}

/// <summary>
/// Compilation warning from MSBuild.
/// </summary>
public record CompilationWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
}
```

---

### 4.5 TestResult

**Purpose**: Result of dotnet test execution (parsed from TRX).

**Package**: `Ironbees.AgentMode.Models`

```csharp
namespace Ironbees.AgentMode.Models;

/// <summary>
/// Result of dotnet test execution.
/// </summary>
public record TestResult
{
    /// <summary>
    /// Whether all tests passed.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Total number of tests executed.
    /// </summary>
    public required int Total { get; init; }

    /// <summary>
    /// Number of tests passed.
    /// </summary>
    public required int Passed { get; init; }

    /// <summary>
    /// Number of tests failed.
    /// </summary>
    public required int Failed { get; init; }

    /// <summary>
    /// Number of tests skipped.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// List of failed test details.
    /// </summary>
    public ImmutableList<TestFailure> Failures { get; init; }
        = ImmutableList<TestFailure>.Empty;

    /// <summary>
    /// Test execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Details of a single test failure.
/// </summary>
public record TestFailure
{
    public required string TestName { get; init; }
    public required string Message { get; init; }
    public string? StackTrace { get; init; }
}
```

---

### 4.6 WorkflowContext

**Purpose**: Optional context for workflow execution.

**Package**: `Ironbees.AgentMode.Models`

```csharp
namespace Ironbees.AgentMode.Models;

/// <summary>
/// Optional context for workflow execution.
/// </summary>
public record WorkflowContext
{
    /// <summary>
    /// Path to .NET solution file.
    /// </summary>
    public string? SolutionPath { get; init; }

    /// <summary>
    /// Target project name (if solution has multiple projects).
    /// </summary>
    public string? TargetProject { get; init; }

    /// <summary>
    /// Additional context files (e.g., related code, documentation).
    /// </summary>
    public ImmutableList<string> ContextFiles { get; init; }
        = ImmutableList<string>.Empty;

    /// <summary>
    /// User preferences for code generation.
    /// </summary>
    public ImmutableDictionary<string, string> Preferences { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}
```

---

### 4.7 ApprovalDecision

**Purpose**: User's approval decision for HITL gates.

**Package**: `Ironbees.AgentMode.Models`

```csharp
namespace Ironbees.AgentMode.Models;

/// <summary>
/// User's approval decision for HITL gates.
/// </summary>
public record ApprovalDecision
{
    /// <summary>
    /// Whether user approved the action.
    /// </summary>
    public required bool Approved { get; init; }

    /// <summary>
    /// Optional feedback for rejection (used to refine plan).
    /// Example: "Please use async/await instead of Task.Run"
    /// </summary>
    public string? Feedback { get; init; }
}
```

---

## 5. Configuration Schema

### 5.1 appsettings.json Schema

```json
{
  "IronbeesAgentMode": {
    "Orchestration": {
      "MaxIterations": 5,
      "TimeoutSeconds": 300,
      "EnableHumanInTheLoop": true,
      "ParallelAgentExecution": false
    },
    "Agents": {
      "ConventionBasedDiscovery": true,
      "AgentsDirectory": "./agents",
      "EnabledAgents": ["planner", "coder", "validator"]
    },
    "MCP": {
      "Servers": [
        {
          "Name": "roslyn",
          "Type": "Ironbees.AgentMode.Tools.RoslynMcpServer",
          "Configuration": {
            "EnableSemanticAnalysis": true,
            "CacheSize": 100
          }
        },
        {
          "Name": "msbuild",
          "Type": "Ironbees.AgentMode.Tools.MSBuildMcpServer",
          "Configuration": {
            "MSBuildPath": "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe"
          }
        }
      ]
    },
    "LLM": {
      "PrimaryProvider": "Anthropic",
      "FallbackProvider": "OpenAI",
      "Anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-sonnet-4-20250514",
        "MaxTokens": 8192,
        "Temperature": 0.0,
        "EnablePromptCaching": true
      },
      "OpenAI": {
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o-2024-08-06",
        "MaxTokens": 16384,
        "Temperature": 0.0
      }
    },
    "Observability": {
      "OpenTelemetry": {
        "Enabled": true,
        "ServiceName": "ironbees-agent-mode",
        "ExporterEndpoint": "http://localhost:4317"
      },
      "Logging": {
        "MinimumLevel": "Information",
        "EnableStructuredLogging": true
      }
    }
  }
}
```

---

### 5.2 Convention-Based Agent Configuration

**Location**: `/agents/{agent-name}/agent.yaml`

**Example**: `/agents/planner/agent.yaml`

```yaml
name: planner
description: Analyzes user request and creates execution plan
version: 1.0.0

# Required tools (MCP server names)
requiredTools:
  - roslyn

# Prompt template file (relative to agent directory)
promptTemplate: prompt.md

# LLM configuration overrides
llmConfig:
  model: claude-sonnet-4-20250514
  maxTokens: 8192
  temperature: 0.0

# Tool-specific configuration
toolConfig:
  roslyn:
    enableSemanticAnalysis: true
    maxReferencesPerSymbol: 100
```

**Example**: `/agents/planner/prompt.md`

```markdown
# Planner Agent

You are a senior .NET architect analyzing user requests to create detailed execution plans.

## Your Task

Analyze the user's request and create a structured execution plan with:
1. Clear specification of what needs to be done
2. Step-by-step action items
3. List of affected files
4. Complexity assessment (LOW/MEDIUM/HIGH)

## Available Tools

- **roslyn**: Use to analyze existing codebase structure
  - `load_solution`: Load the solution for analysis
  - `find_references`: Find all references to a symbol
  - `get_type_hierarchy`: Get inheritance hierarchy

## Output Format

Provide a JSON response:

\`\`\`json
{
  "spec": "Detailed specification...",
  "plan": {
    "summary": "High-level summary...",
    "steps": [
      {
        "number": 1,
        "description": "Action description...",
        "actionType": "MODIFY",
        "filePath": "path/to/file.cs",
        "rationale": "Why this step..."
      }
    ],
    "affectedFiles": ["file1.cs", "file2.cs"],
    "complexity": "MEDIUM"
  }
}
\`\`\`
```

---

## 6. Usage Examples

### 6.1 Basic Usage with DI

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Ironbees Agent Mode
builder.Services.AddIronbeesAgentMode(config =>
{
    // Enable convention-based agent discovery
    config.UseConventionBasedAgents("./agents");

    // Register MCP servers
    config.AddMcpServer<RoslynMcpServer>();
    config.AddMcpServer<MSBuildMcpServer>();
    config.AddMcpServer<DotNetTestMcpServer>();

    // Configure LLM providers
    config.UseClaude(builder.Configuration.GetSection("IronbeesAgentMode:LLM:Anthropic"));
    config.UseOpenAI(builder.Configuration.GetSection("IronbeesAgentMode:LLM:OpenAI"));

    // Enable observability
    config.UseOpenTelemetry();
});

var app = builder.Build();
app.Run();
```

---

### 6.2 Console Application

```csharp
using Ironbees.AgentMode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddIronbeesAgentMode(config =>
        {
            config.UseConventionBasedAgents();
            config.AddMcpServers();
        });
    })
    .Build();

var orchestrator = host.Services.GetRequiredService<IStatefulOrchestrator>();

Console.WriteLine("Enter your request:");
var request = Console.ReadLine();

await foreach (var state in orchestrator.ExecuteAsync(request))
{
    Console.WriteLine($"[{state.CurrentNode}] Iteration {state.IterationCount}");

    if (state.CurrentNode == "WAIT_PLAN_APPROVAL")
    {
        Console.WriteLine("\n=== Execution Plan ===");
        Console.WriteLine(state.Plan?.Summary);
        Console.WriteLine("\nSteps:");
        foreach (var step in state.Plan?.Steps ?? [])
        {
            Console.WriteLine($"{step.Number}. {step.Description}");
        }

        Console.Write("\nApprove? (y/n): ");
        var approval = Console.ReadLine()?.ToLower() == "y";

        await orchestrator.ApproveAsync(state.StateId, new ApprovalDecision
        {
            Approved = approval,
            Feedback = approval ? null : "Please revise"
        });
    }

    if (state.CurrentNode == "END")
    {
        Console.WriteLine("\n=== Workflow Completed ===");
        Console.WriteLine($"Files modified: {state.CodeDiffs.Count}");
        foreach (var diff in state.CodeDiffs)
        {
            Console.WriteLine($"  - {diff.FilePath} ({diff.Type})");
        }
        break;
    }
}
```

---

### 6.3 Web API Integration

```csharp
// Controllers/AgentController.cs
[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly IStatefulOrchestrator _orchestrator;

    public AgentController(IStatefulOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("execute")]
    public async IAsyncEnumerable<StateUpdate> ExecuteAsync(
        [FromBody] AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var context = new WorkflowContext
        {
            SolutionPath = request.SolutionPath,
            TargetProject = request.TargetProject
        };

        await foreach (var state in _orchestrator.ExecuteAsync(
            request.UserRequest, context, cancellationToken))
        {
            yield return new StateUpdate
            {
                StateId = state.StateId,
                CurrentNode = state.CurrentNode,
                Iteration = state.IterationCount,
                Plan = state.Plan,
                Diffs = state.CodeDiffs.Select(d => new DiffSummary
                {
                    FilePath = d.FilePath,
                    Type = d.Type.ToString()
                }).ToList()
            };
        }
    }

    [HttpPost("approve")]
    public async Task<IActionResult> ApproveAsync(
        [FromBody] ApprovalRequest request)
    {
        await _orchestrator.ApproveAsync(request.StateId, new ApprovalDecision
        {
            Approved = request.Approved,
            Feedback = request.Feedback
        });

        return Ok();
    }

    [HttpGet("state/{stateId}")]
    public async Task<ActionResult<CodingState>> GetStateAsync(string stateId)
    {
        var state = await _orchestrator.GetStateAsync(stateId);
        return Ok(state);
    }
}

public record AgentRequest
{
    public required string UserRequest { get; init; }
    public string? SolutionPath { get; init; }
    public string? TargetProject { get; init; }
}

public record ApprovalRequest
{
    public required string StateId { get; init; }
    public required bool Approved { get; init; }
    public string? Feedback { get; init; }
}

public record StateUpdate
{
    public required string StateId { get; init; }
    public required string CurrentNode { get; init; }
    public required int Iteration { get; init; }
    public ExecutionPlan? Plan { get; init; }
    public List<DiffSummary>? Diffs { get; init; }
}

public record DiffSummary
{
    public required string FilePath { get; init; }
    public required string Type { get; init; }
}
```

---

### 6.4 Custom Agent Implementation

```csharp
public class TestGeneratorAgent : ICodingAgent
{
    private readonly IChatClient _chatClient;
    private readonly IToolRegistry _toolRegistry;

    public string Name => "test-generator";
    public string Description => "Generates xUnit tests for code changes";
    public IReadOnlyList<string> RequiredTools => new[] { "roslyn", "dotnet-test" };

    public TestGeneratorAgent(IChatClient chatClient, IToolRegistry toolRegistry)
    {
        _chatClient = chatClient;
        _toolRegistry = toolRegistry;
    }

    public async Task<AgentResponse> ExecuteAsync(
        CodingState state,
        CancellationToken cancellationToken = default)
    {
        // Analyze code diffs to identify methods needing tests
        var methodsToTest = await AnalyzeCodeDiffs(state.CodeDiffs);

        // Generate test code using LLM
        var prompt = $"""
            Generate xUnit tests for the following methods:
            {string.Join("\n", methodsToTest.Select(m => $"- {m.FullName}"))}

            Use FluentAssertions for assertions.
            Follow AAA (Arrange-Act-Assert) pattern.
            """;

        var agent = _chatClient.CreateAIAgent(
            instructions: prompt,
            tools: _toolRegistry.GetToolsForAgent(Name)
        );

        var response = await agent.CompleteAsync(
            new[] { new ChatMessage(ChatRole.User, prompt) },
            cancellationToken: cancellationToken);

        // Parse generated test code
        var testCode = response.Content;

        // Create test file edit
        var testFileEdit = new FileEdit
        {
            FilePath = $"{state.Metadata["TargetProject"]}.Tests/GeneratedTests.cs",
            Type = EditType.Create,
            NewContent = testCode,
            DiffText = $"+++ GeneratedTests.cs\n{testCode}"
        };

        return new AgentResponse
        {
            Updates = new Dictionary<string, object?>
            {
                ["CodeDiffs"] = state.CodeDiffs.Add(testFileEdit)
            },
            NextNode = "VALIDATE",
            Metadata = new Dictionary<string, string>
            {
                ["testsGenerated"] = "1",
                ["model"] = response.Model
            }
        };
    }

    private async Task<List<MethodInfo>> AnalyzeCodeDiffs(ImmutableList<FileEdit> diffs)
    {
        // Use Roslyn tool to analyze code diffs
        // ...
        return new List<MethodInfo>();
    }
}

// Register custom agent
services.AddSingleton<ICodingAgent, TestGeneratorAgent>();
```

---

### 6.5 Custom MCP Server

```csharp
public class GitMcpServer : IMcpServer
{
    private readonly ILogger<GitMcpServer> _logger;
    private string? _repositoryPath;

    public string Name => "git";
    public string Version => "1.0.0";

    public IReadOnlyList<ToolDefinition> Tools => new[]
    {
        new ToolDefinition
        {
            Name = "commit",
            Description = "Create a git commit with changes",
            InputSchema = new JsonSchema
            {
                Type = "object",
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["message"] = new JsonSchema { Type = "string" },
                    ["files"] = new JsonSchema
                    {
                        Type = "array",
                        Items = new JsonSchema { Type = "string" }
                    }
                },
                Required = new[] { "message", "files" }
            }
        },
        new ToolDefinition
        {
            Name = "create_branch",
            Description = "Create a new git branch",
            InputSchema = new JsonSchema
            {
                Type = "object",
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["branchName"] = new JsonSchema { Type = "string" }
                },
                Required = new[] { "branchName" }
            }
        }
    };

    public GitMcpServer(ILogger<GitMcpServer> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(
        IReadOnlyDictionary<string, object> configuration,
        CancellationToken cancellationToken = default)
    {
        _repositoryPath = (string)configuration["repositoryPath"];
        _logger.LogInformation("Git MCP server initialized for repository: {Path}", _repositoryPath);
        return Task.CompletedTask;
    }

    public async Task<ToolResult> ExecuteToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "commit" => await CommitAsync(arguments, cancellationToken),
            "create_branch" => await CreateBranchAsync(arguments, cancellationToken),
            _ => throw new ToolNotFoundException($"Tool '{toolName}' not found")
        };
    }

    private async Task<ToolResult> CommitAsync(
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        var message = (string)arguments["message"];
        var files = (List<object>)arguments["files"];

        using var repo = new Repository(_repositoryPath);

        // Stage files
        foreach (var file in files.Cast<string>())
        {
            Commands.Stage(repo, file);
        }

        // Create commit
        var author = repo.Config.BuildSignature(DateTimeOffset.Now);
        var commit = repo.Commit(message, author, author);

        _logger.LogInformation("Created commit {Sha}: {Message}", commit.Sha, message);

        return new ToolResult
        {
            Success = true,
            Content = new { commitSha = commit.Sha, message = message }
        };
    }

    private Task<ToolResult> CreateBranchAsync(
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        var branchName = (string)arguments["branchName"];

        using var repo = new Repository(_repositoryPath);
        var branch = repo.CreateBranch(branchName);

        _logger.LogInformation("Created branch: {Branch}", branchName);

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = new { branchName = branchName }
        });
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

// Register in DI
services.AddSingleton<IMcpServer, GitMcpServer>();
```

---

## 7. Error Handling

### 7.1 Exception Hierarchy

```csharp
namespace Ironbees.AgentMode;

/// <summary>
/// Base exception for all Agent Mode errors.
/// </summary>
public class AgentModeException : Exception
{
    public AgentModeException(string message) : base(message) { }
    public AgentModeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when orchestrator execution fails.
/// </summary>
public class OrchestratorException : AgentModeException
{
    public string? StateId { get; init; }
    public string? CurrentNode { get; init; }

    public OrchestratorException(string message, string? stateId = null, string? currentNode = null)
        : base(message)
    {
        StateId = stateId;
        CurrentNode = currentNode;
    }
}

/// <summary>
/// Exception thrown when agent execution fails.
/// </summary>
public class AgentExecutionException : AgentModeException
{
    public string AgentName { get; init; }

    public AgentExecutionException(string agentName, string message, Exception? inner = null)
        : base($"Agent '{agentName}' execution failed: {message}", inner!)
    {
        AgentName = agentName;
    }
}

/// <summary>
/// Exception thrown when tool execution fails.
/// </summary>
public class ToolExecutionException : AgentModeException
{
    public string ToolName { get; init; }
    public string ServerName { get; init; }

    public ToolExecutionException(string serverName, string toolName, string message, Exception? inner = null)
        : base($"Tool '{toolName}' from server '{serverName}' execution failed: {message}", inner!)
    {
        ServerName = serverName;
        ToolName = toolName;
    }
}

/// <summary>
/// Exception thrown when state is not found.
/// </summary>
public class StateNotFoundException : AgentModeException
{
    public string StateId { get; init; }

    public StateNotFoundException(string stateId)
        : base($"State '{stateId}' not found")
    {
        StateId = stateId;
    }
}

/// <summary>
/// Exception thrown when state is in invalid state for operation.
/// </summary>
public class InvalidStateException : AgentModeException
{
    public string StateId { get; init; }
    public string CurrentNode { get; init; }

    public InvalidStateException(string stateId, string currentNode, string message)
        : base(message)
    {
        StateId = stateId;
        CurrentNode = currentNode;
    }
}

/// <summary>
/// Exception thrown when tool is not found.
/// </summary>
public class ToolNotFoundException : AgentModeException
{
    public string ToolName { get; init; }

    public ToolNotFoundException(string toolName)
        : base($"Tool '{toolName}' not found in any registered MCP server")
    {
        ToolName = toolName;
    }
}
```

---

### 7.2 Error Handling Examples

```csharp
try
{
    await foreach (var state in orchestrator.ExecuteAsync(request))
    {
        // Process state updates
    }
}
catch (AgentExecutionException ex)
{
    _logger.LogError(ex, "Agent {AgentName} failed", ex.AgentName);
    // Handle agent failure (retry, fallback, etc.)
}
catch (ToolExecutionException ex)
{
    _logger.LogError(ex, "Tool {ToolName} from {ServerName} failed",
        ex.ToolName, ex.ServerName);
    // Handle tool failure
}
catch (StateNotFoundException ex)
{
    _logger.LogError(ex, "State {StateId} not found", ex.StateId);
    // Handle missing state
}
catch (OrchestratorException ex)
{
    _logger.LogError(ex, "Orchestrator failed at node {CurrentNode}", ex.CurrentNode);
    // Handle orchestrator failure
}
catch (AgentModeException ex)
{
    _logger.LogError(ex, "Agent Mode error: {Message}", ex.Message);
    // Handle generic Agent Mode error
}
```

---

## 8. Versioning Strategy

### 8.1 Semantic Versioning

Ironbees Agent Mode follows **Semantic Versioning 2.0.0** (https://semver.org/):

- **Major version** (X.0.0): Breaking API changes
- **Minor version** (0.X.0): Backward-compatible new features
- **Patch version** (0.0.X): Backward-compatible bug fixes

**Examples**:
- `1.0.0` → `1.0.1`: Bug fix (no API changes)
- `1.0.0` → `1.1.0`: New agent added (backward-compatible)
- `1.0.0` → `2.0.0`: IStatefulOrchestrator interface changed (breaking)

---

### 8.2 API Stability Guarantees

**Stable APIs** (Guaranteed backward compatibility within major version):
- `IStatefulOrchestrator`
- `ICodingAgent`
- `IMcpServer`
- `CodingState`
- `ExecutionPlan`
- `FileEdit`

**Unstable APIs** (May change in minor versions):
- Internal orchestrator implementation
- Agent prompt templates
- Tool-specific configurations

**Deprecated APIs**:
- Marked with `[Obsolete]` attribute
- Supported for one major version
- Removed in next major version

**Example**:
```csharp
[Obsolete("Use ExecuteAsync with WorkflowContext instead. Will be removed in v2.0.0")]
public IAsyncEnumerable<CodingState> ExecuteAsync(string request);
```

---

### 8.3 Breaking Change Policy

**Breaking changes** require major version bump:
- Interface signature changes
- Data model property removal
- Exception type changes
- Configuration schema breaking changes

**Non-breaking changes** (minor version):
- New interfaces
- New properties (with defaults)
- New agents
- New tools
- Performance improvements

---

## 9. Migration Guide

### 9.1 Upgrading from v0.x to v1.0

**Step 1**: Update package references

```xml
<PackageReference Include="Ironbees.AgentMode.Core" Version="1.0.0" />
<PackageReference Include="Ironbees.AgentMode.Agents" Version="1.0.0" />
<PackageReference Include="Ironbees.AgentMode.Tools" Version="1.0.0" />
```

**Step 2**: Update DI configuration

```csharp
// Before (v0.x)
services.AddIronbeesAgentMode();

// After (v1.0)
services.AddIronbeesAgentMode(config =>
{
    config.UseConventionBasedAgents();
    config.AddMcpServers();
});
```

**Step 3**: Update orchestrator usage

```csharp
// Before (v0.x)
await orchestrator.ExecuteAsync(request);

// After (v1.0)
var context = new WorkflowContext { SolutionPath = "..." };
await orchestrator.ExecuteAsync(request, context);
```

**Step 4**: Update agent implementations

```csharp
// Before (v0.x)
public class MyAgent : IAgent
{
    public Task<string> ExecuteAsync(string input) { ... }
}

// After (v1.0)
public class MyAgent : ICodingAgent
{
    public string Name => "my-agent";
    public string Description => "...";
    public IReadOnlyList<string> RequiredTools => new[] { "roslyn" };

    public Task<AgentResponse> ExecuteAsync(CodingState state, ...) { ... }
}
```

---

### 9.2 Deprecation Timeline

**v0.9.0** (Phase 1 - MVP):
- Deprecated: `IAgent` interface → Use `ICodingAgent`
- Deprecated: `Orchestrator.Execute()` → Use `ExecuteAsync()`

**v1.0.0** (Phase 2):
- Removed: All v0.x deprecated APIs
- Stable: `IStatefulOrchestrator`, `ICodingAgent`, `IMcpServer`

**v2.0.0** (Future):
- TBD based on Phase 3/4 feedback

---

## Appendix A: Complete Interface Reference

### A.1 Core Interfaces

```csharp
// Ironbees.AgentMode.Abstractions
public interface IStatefulOrchestrator { ... }
public interface ICodingAgent { ... }
public interface IMcpServer { ... }
public interface IToolRegistry { ... }

// Ironbees.AgentMode.Models
public record CodingState { ... }
public record ExecutionPlan { ... }
public record PlanStep { ... }
public record FileEdit { ... }
public record BuildResult { ... }
public record TestResult { ... }
public record WorkflowContext { ... }
public record ApprovalDecision { ... }
public record AgentResponse { ... }
public record ToolDefinition { ... }
public record ToolResult { ... }

// Ironbees.AgentMode
public class AgentModeException : Exception { ... }
public class OrchestratorException : AgentModeException { ... }
public class AgentExecutionException : AgentModeException { ... }
public class ToolExecutionException : AgentModeException { ... }
public class StateNotFoundException : AgentModeException { ... }
public class InvalidStateException : AgentModeException { ... }
public class ToolNotFoundException : AgentModeException { ... }
```

---

## Appendix B: Configuration Examples

### B.1 Minimal Configuration

```json
{
  "IronbeesAgentMode": {
    "LLM": {
      "PrimaryProvider": "Anthropic",
      "Anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-sonnet-4-20250514"
      }
    }
  }
}
```

### B.2 Production Configuration

```json
{
  "IronbeesAgentMode": {
    "Orchestration": {
      "MaxIterations": 5,
      "TimeoutSeconds": 300,
      "EnableHumanInTheLoop": true
    },
    "Agents": {
      "ConventionBasedDiscovery": true,
      "AgentsDirectory": "./agents"
    },
    "MCP": {
      "Servers": [
        { "Name": "roslyn", "Type": "Ironbees.AgentMode.Tools.RoslynMcpServer" },
        { "Name": "msbuild", "Type": "Ironbees.AgentMode.Tools.MSBuildMcpServer" },
        { "Name": "dotnet-test", "Type": "Ironbees.AgentMode.Tools.DotNetTestMcpServer" },
        { "Name": "git", "Type": "Ironbees.AgentMode.Tools.GitMcpServer" }
      ]
    },
    "LLM": {
      "PrimaryProvider": "Anthropic",
      "FallbackProvider": "OpenAI",
      "Anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-sonnet-4-20250514",
        "MaxTokens": 8192,
        "Temperature": 0.0,
        "EnablePromptCaching": true
      },
      "OpenAI": {
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o-2024-08-06",
        "MaxTokens": 16384,
        "Temperature": 0.0
      }
    },
    "Observability": {
      "OpenTelemetry": {
        "Enabled": true,
        "ServiceName": "ironbees-agent-mode",
        "ExporterEndpoint": "http://localhost:4317"
      },
      "Logging": {
        "MinimumLevel": "Information",
        "EnableStructuredLogging": true,
        "Serilog": {
          "WriteTo": [
            { "Name": "Console" },
            { "Name": "File", "Args": { "path": "logs/agent-mode-.log", "rollingInterval": "Day" } }
          ]
        }
      }
    }
  }
}
```

---

## Document History

| Version | Date       | Changes                                      |
|---------|------------|----------------------------------------------|
| 1.0.0   | 2025-11-11 | Initial API Specification for Phase 1 (MVP)  |

---

## References

- [MCP Protocol Specification](https://modelcontextprotocol.io/docs)
- [Microsoft.Agents.AI Documentation](https://github.com/microsoft/agents)
- [Roslyn API Documentation](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/)
- [MSBuild API Documentation](https://learn.microsoft.com/visualstudio/msbuild/msbuild-api)
- [Semantic Versioning 2.0.0](https://semver.org/)

---

**End of API Specification v1.0**
