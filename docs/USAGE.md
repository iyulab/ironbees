# Ironbees Usage Guide

Comprehensive guide for using the Ironbees multi-agent orchestration framework.

## Table of Contents

1. [Basic Setup](#basic-setup)
2. [Agent Usage Patterns](#agent-usage-patterns)
3. [Agent Selection](#agent-selection)
4. [Advanced Scenarios](#advanced-scenarios)
5. [ASP.NET Core Integration](#aspnet-core-integration)
6. [Custom Implementations](#custom-implementations)
7. [Best Practices](#best-practices)

## Basic Setup

### 1. Installation

```bash
# Install both packages
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework
```

### 2. Configuration

#### Console Application

```csharp
using Ironbees.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Add Ironbees services
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
    options.MinimumConfidenceThreshold = 0.3;
});

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

// Load agents
await orchestrator.LoadAgentsAsync();
```

#### ASP.NET Core Application

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
    options.AzureOpenAIKey = builder.Configuration["AzureOpenAI:Key"];
    options.AgentsDirectory = "./agents";
});

var app = builder.Build();

// Load agents on startup
var orchestrator = app.Services.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync();

app.Run();
```

## Agent Usage Patterns

### Pattern 1: Explicit Agent Selection

Use when you know exactly which agent should handle the request.

```csharp
// Directly specify the agent by name
var response = await orchestrator.ProcessAsync(
    "Write a C# method to calculate factorial",
    agentName: "coding-agent"
);

Console.WriteLine(response);
```

**When to use:**
- You know the exact agent needed
- Building specialized workflows
- Testing specific agents

### Pattern 2: Automatic Agent Selection

Let the framework choose the best agent based on input analysis.

```csharp
// Framework analyzes input and selects appropriate agent
var response = await orchestrator.ProcessAsync(
    "Write a blog post about cloud computing"
);

// Framework selects writing-agent based on keywords
Console.WriteLine(response);
```

**When to use:**
- Building general-purpose applications
- User input is varied
- Want to leverage intelligent routing

### Pattern 3: Selection with Inspection

Check which agent would be selected before processing.

```csharp
// First, see what agent would be selected
var selection = await orchestrator.SelectAgentAsync(
    "Analyze customer feedback data"
);

Console.WriteLine($"Selected: {selection.SelectedAgent?.Name}");
Console.WriteLine($"Confidence: {selection.ConfidenceScore:P0}");
Console.WriteLine($"Reason: {selection.SelectionReason}");

// Then decide whether to proceed
if (selection.ConfidenceScore > 0.5)
{
    var response = await orchestrator.ProcessAsync(
        "Analyze customer feedback data",
        agentName: selection.SelectedAgent.Name
    );
}
```

**When to use:**
- Need to validate agent selection
- Want to show user which agent will be used
- Building debugging/monitoring tools

### Pattern 4: Streaming Responses

Get real-time streaming output for better user experience.

```csharp
// Stream response chunks as they arrive
await foreach (var chunk in orchestrator.StreamAsync(
    "Explain the SOLID principles in detail",
    "coding-agent"))
{
    Console.Write(chunk);
    // Update UI in real-time
}
```

**When to use:**
- Long-running responses
- Want to show progress to users
- Building chat interfaces

## Agent Selection

### Understanding Selection Scores

The `KeywordAgentSelector` uses weighted scoring:

| Factor | Weight | Purpose |
|--------|--------|---------|
| Capabilities | 40% | Core functionality matching |
| Tags | 30% | Domain and topic matching |
| Description | 20% | Contextual relevance |
| Name | 10% | Direct agent name references |

### Improving Selection Accuracy

#### 1. Use Descriptive Capabilities

```yaml
# Good - specific and descriptive
capabilities:
  - code-generation
  - code-review
  - debugging
  - refactoring

# Less effective - too generic
capabilities:
  - programming
  - help
```

#### 2. Add Relevant Tags

```yaml
# Good - covers various search terms
tags:
  - coding
  - development
  - csharp
  - dotnet
  - programming

# Less effective - too narrow
tags:
  - code
```

#### 3. Write Clear Descriptions

```yaml
# Good - clear and keyword-rich
description: Expert software developer for code generation, review, and debugging

# Less effective - vague
description: Helps with programming
```

### Testing Agent Selection

```csharp
// Test different inputs to see selection behavior
var testInputs = new[]
{
    "Write Python code",
    "Analyze sales data",
    "Review this document",
    "Create a blog post"
};

foreach (var input in testInputs)
{
    var result = await orchestrator.SelectAgentAsync(input);

    Console.WriteLine($"\nInput: {input}");
    Console.WriteLine($"Selected: {result.SelectedAgent?.Name ?? "None"}");
    Console.WriteLine($"Confidence: {result.ConfidenceScore:P0}");

    // Show all agent scores
    Console.WriteLine("All scores:");
    foreach (var score in result.AllScores)
    {
        Console.WriteLine($"  {score.Agent.Name}: {score.Score:P2}");
    }
}
```

## Advanced Scenarios

### Scenario 1: Multi-Step Workflow

Chain multiple agents for complex tasks.

```csharp
// Step 1: Writing agent creates content
var content = await orchestrator.ProcessAsync(
    "Write a technical article about microservices",
    "writing-agent"
);

// Step 2: Review agent checks quality
var review = await orchestrator.ProcessAsync(
    $"Review this article for technical accuracy:\n\n{content}",
    "review-agent"
);

// Step 3: Writing agent revises based on feedback
var revised = await orchestrator.ProcessAsync(
    $"Revise this article based on feedback:\n\nOriginal:\n{content}\n\nFeedback:\n{review}",
    "writing-agent"
);
```

### Scenario 2: Batch Processing

Process multiple requests efficiently.

```csharp
var tasks = new[]
{
    "Write a function to sort arrays",
    "Create a README for my project",
    "Analyze user engagement metrics"
};

var results = await Task.WhenAll(
    tasks.Select(task => orchestrator.ProcessAsync(task))
);

for (int i = 0; i < tasks.Length; i++)
{
    Console.WriteLine($"\nTask: {tasks[i]}");
    Console.WriteLine($"Result: {results[i]}");
}
```

### Scenario 3: Conditional Agent Selection

Select agents based on custom logic.

```csharp
async Task<string> ProcessWithFallback(string input)
{
    // Try automatic selection first
    var selection = await orchestrator.SelectAgentAsync(input);

    if (selection.ConfidenceScore < 0.3)
    {
        // Low confidence - use a general-purpose fallback
        Console.WriteLine("Low confidence, using fallback agent");
        return await orchestrator.ProcessAsync(input, "coding-agent");
    }
    else if (selection.ConfidenceScore < 0.6)
    {
        // Medium confidence - ask user to confirm
        Console.WriteLine($"Selected: {selection.SelectedAgent.Name}");
        Console.Write("Proceed? (y/n): ");

        if (Console.ReadLine()?.ToLower() == "y")
        {
            return await orchestrator.ProcessAsync(input, selection.SelectedAgent.Name);
        }
        else
        {
            Console.Write("Which agent to use? ");
            var agentName = Console.ReadLine();
            return await orchestrator.ProcessAsync(input, agentName);
        }
    }
    else
    {
        // High confidence - proceed automatically
        return await orchestrator.ProcessAsync(input, selection.SelectedAgent.Name);
    }
}
```

### Scenario 4: Context-Aware Processing

Maintain context across multiple interactions.

```csharp
public class ConversationHandler
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly List<string> _history = new();

    public ConversationHandler(IAgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<string> ProcessAsync(string input, string? agentName = null)
    {
        // Build context from history
        var context = string.Join("\n", _history.TakeLast(3));
        var prompt = string.IsNullOrEmpty(context)
            ? input
            : $"Previous context:\n{context}\n\nCurrent request:\n{input}";

        // Process with context
        var response = await _orchestrator.ProcessAsync(prompt, agentName);

        // Update history
        _history.Add($"User: {input}");
        _history.Add($"Agent: {response}");

        return response;
    }
}
```

## ASP.NET Core Integration

### REST API Example

```csharp
// Models/ChatRequest.cs
public record ChatRequest(string Message, string? AgentName = null);
public record ChatResponse(string Message, string AgentUsed, double Confidence);

// Controllers/ChatController.cs
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;

    public ChatController(IAgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat(ChatRequest request)
    {
        try
        {
            string response;
            string agentUsed;
            double confidence;

            if (request.AgentName != null)
            {
                // Explicit agent selection
                response = await _orchestrator.ProcessAsync(
                    request.Message,
                    request.AgentName
                );
                agentUsed = request.AgentName;
                confidence = 1.0;
            }
            else
            {
                // Automatic selection
                var selection = await _orchestrator.SelectAgentAsync(request.Message);

                if (selection.SelectedAgent == null)
                {
                    return BadRequest("No suitable agent found");
                }

                response = await _orchestrator.ProcessAsync(
                    request.Message,
                    selection.SelectedAgent.Name
                );
                agentUsed = selection.SelectedAgent.Name;
                confidence = selection.ConfidenceScore;
            }

            return Ok(new ChatResponse(response, agentUsed, confidence));
        }
        catch (AgentNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("agents")]
    public ActionResult<IReadOnlyCollection<string>> ListAgents()
    {
        return Ok(_orchestrator.ListAgents());
    }

    [HttpPost("select")]
    public async Task<ActionResult<AgentSelectionResult>> SelectAgent(
        [FromBody] string input)
    {
        var result = await _orchestrator.SelectAgentAsync(input);
        return Ok(result);
    }
}
```

### SignalR Streaming Example

```csharp
// Hubs/ChatHub.cs
public class ChatHub : Hub
{
    private readonly IAgentOrchestrator _orchestrator;

    public ChatHub(IAgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async IAsyncEnumerable<string> StreamResponse(
        string message,
        string? agentName = null)
    {
        string selectedAgent = agentName;

        if (selectedAgent == null)
        {
            var selection = await _orchestrator.SelectAgentAsync(message);
            selectedAgent = selection.SelectedAgent?.Name;

            if (selectedAgent == null)
            {
                yield return "Error: No suitable agent found";
                yield break;
            }

            // Send agent selection info
            yield return $"[Using {selectedAgent}]\n\n";
        }

        await foreach (var chunk in _orchestrator.StreamAsync(message, selectedAgent))
        {
            yield return chunk;
        }
    }
}

// Program.cs
builder.Services.AddSignalR();
app.MapHub<ChatHub>("/chat");
```

## Custom Implementations

### Custom Agent Selector

Create a selector that uses embeddings for semantic matching.

```csharp
public class SemanticAgentSelector : IAgentSelector
{
    private readonly AzureOpenAIClient _client;

    public async Task<AgentSelectionResult> SelectAgentAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default)
    {
        // Get embedding for input
        var inputEmbedding = await GetEmbeddingAsync(input);

        // Score each agent
        var scores = new List<AgentScore>();
        foreach (var agent in availableAgents)
        {
            var agentText = $"{agent.Description} {string.Join(" ", agent.Config.Capabilities)}";
            var agentEmbedding = await GetEmbeddingAsync(agentText);

            var similarity = CosineSimilarity(inputEmbedding, agentEmbedding);

            scores.Add(new AgentScore
            {
                Agent = agent,
                Score = similarity,
                Reasons = new[] { $"Semantic similarity: {similarity:P0}" }
            });
        }

        var bestAgent = scores.OrderByDescending(s => s.Score).First();

        return new AgentSelectionResult
        {
            SelectedAgent = bestAgent.Agent,
            ConfidenceScore = bestAgent.Score,
            SelectionReason = "Selected by semantic similarity",
            AllScores = scores
        };
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        // Implementation using Azure OpenAI embeddings
        throw new NotImplementedException();
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        // Calculate cosine similarity between vectors
        throw new NotImplementedException();
    }
}

// Registration
services.AddSingleton<IAgentSelector, SemanticAgentSelector>();
```

### Custom LLM Adapter

Integrate with different LLM frameworks.

```csharp
public class CustomLLMAdapter : ILLMFrameworkAdapter
{
    public async Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        // Create agent with your custom LLM implementation
        return new CustomAgent(config);
    }

    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        // Execute with your LLM
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IAgent agent,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stream responses from your LLM
        yield break;
    }
}
```

## Best Practices

### 1. Agent Design

**DO:**
- Give agents specific, focused responsibilities
- Use descriptive capabilities and tags
- Set appropriate temperature for agent purpose
- Include comprehensive system prompts

**DON'T:**
- Create generic "do everything" agents
- Overlap capabilities between agents too much
- Use extreme temperature values without testing

### 2. Error Handling

```csharp
try
{
    var response = await orchestrator.ProcessAsync(input, agentName);
    return response;
}
catch (AgentNotFoundException ex)
{
    // Agent doesn't exist
    _logger.LogError(ex, "Agent {AgentName} not found", agentName);
    return "Agent not available";
}
catch (InvalidOperationException ex)
{
    // No agents loaded or other operational issues
    _logger.LogError(ex, "Orchestration error");
    return "Service temporarily unavailable";
}
catch (Exception ex)
{
    // Unexpected errors (network, API issues, etc.)
    _logger.LogError(ex, "Unexpected error");
    return "An error occurred processing your request";
}
```

### 3. Configuration Management

```csharp
// appsettings.json
{
  "Ironbees": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com",
      "Key": "your-key-here"
    },
    "AgentsDirectory": "./agents",
    "MinimumConfidenceThreshold": 0.3
  }
}

// Strongly-typed configuration
public class IronbeesSettings
{
    public AzureOpenAISettings AzureOpenAI { get; set; }
    public string AgentsDirectory { get; set; } = "./agents";
    public double MinimumConfidenceThreshold { get; set; } = 0.3;
}

public class AzureOpenAISettings
{
    public string Endpoint { get; set; }
    public string Key { get; set; }
}

// Program.cs
builder.Services.Configure<IronbeesSettings>(
    builder.Configuration.GetSection("Ironbees"));

builder.Services.AddIronbees(options =>
{
    var settings = builder.Configuration
        .GetSection("Ironbees")
        .Get<IronbeesSettings>();

    options.AzureOpenAIEndpoint = settings.AzureOpenAI.Endpoint;
    options.AzureOpenAIKey = settings.AzureOpenAI.Key;
    options.AgentsDirectory = settings.AgentsDirectory;
    options.MinimumConfidenceThreshold = settings.MinimumConfidenceThreshold;
});
```

### 4. Testing

```csharp
public class AgentOrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_WithValidAgent_ReturnsResponse()
    {
        // Arrange
        var mockAdapter = new Mock<ILLMFrameworkAdapter>();
        mockAdapter
            .Setup(a => a.RunAsync(It.IsAny<IAgent>(), It.IsAny<string>(), default))
            .ReturnsAsync("Test response");

        var orchestrator = CreateOrchestrator(mockAdapter.Object);
        await orchestrator.LoadAgentsAsync();

        // Act
        var response = await orchestrator.ProcessAsync(
            "test input",
            "coding-agent"
        );

        // Assert
        Assert.Equal("Test response", response);
    }
}
```

### 5. Performance Optimization

```csharp
// Cache agent loading results
public class CachedAgentLoader : IAgentLoader
{
    private readonly FileSystemAgentLoader _inner;
    private IReadOnlyCollection<AgentConfig>? _cached;

    public async Task<IReadOnlyCollection<AgentConfig>> LoadAllConfigsAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        if (_cached != null)
            return _cached;

        _cached = await _inner.LoadAllConfigsAsync(directory, cancellationToken);
        return _cached;
    }
}

// Register cached loader
services.AddSingleton<IAgentLoader, CachedAgentLoader>();
```

### 6. Monitoring and Logging

```csharp
public class LoggingAgentOrchestrator : IAgentOrchestrator
{
    private readonly IAgentOrchestrator _inner;
    private readonly ILogger<LoggingAgentOrchestrator> _logger;

    public async Task<string> ProcessAsync(
        string input,
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Processing request with agent {AgentName}",
                agentName ?? "auto"
            );

            var result = await _inner.ProcessAsync(input, agentName, cancellationToken);

            _logger.LogInformation(
                "Request completed in {ElapsedMs}ms",
                sw.ElapsedMilliseconds
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Request failed after {ElapsedMs}ms",
                sw.ElapsedMilliseconds
            );
            throw;
        }
    }
}
```

## Troubleshooting

### Common Issues

1. **"No agents available" error**
   - Check agents directory path
   - Verify agent.yaml files are valid
   - Ensure LoadAgentsAsync was called

2. **Agent not selected correctly**
   - Review agent capabilities and tags
   - Test with SelectAgentAsync to see scores
   - Adjust MinimumConfidenceThreshold if needed

3. **Azure OpenAI errors**
   - Verify endpoint URL format
   - Check API key validity
   - Ensure deployment names in agent.yaml match Azure

4. **Performance issues**
   - Use streaming for long responses
   - Consider caching agent configurations
   - Implement request timeouts

For more help, see the [main README](../README.md) troubleshooting section.
