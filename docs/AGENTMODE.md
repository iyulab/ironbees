# Ironbees AgentMode - Overview

**Version**: 0.1.6
**Status**: Production Ready
**Target Framework**: .NET 10.0

---

## What is AgentMode?

AgentMode is Ironbees' **multi-provider LLM agent system** that extends the core Ironbees framework with:

- 🔌 **Multi-Provider Support**: Azure OpenAI, OpenAI, Anthropic (planned), Self-Hosted LLMs
- 🤖 **Agent Abstractions**: ICodingAgent, ConversationalAgent base classes
- 🛠️ **Tool Integration**: MCP (Model Context Protocol) server support
- 📊 **Unified Configuration**: Single `LLMConfiguration` for all providers

AgentMode builds on Ironbees.Core's filesystem convention patterns but adds **provider abstraction** and **agent lifecycle management**.

---

## Architecture

### Project Structure

```
src/Ironbees.AgentMode.*
├── Core/                    # Configuration, interfaces, base types
├── Providers/               # LLM provider factories (OpenAI, Azure, etc.)
├── Agents/                  # Agent implementations (ICodingAgent, etc.)
├── MCP/                     # Model Context Protocol integration
├── Tools/                   # Tool definitions and handlers
└── Providers/               # Provider-specific implementations
```

### Core Components

```
┌─────────────────────────────────────────────────────┐
│                  Your Application                    │
└────────────────────┬────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────┐
│         Ironbees.AgentMode.Agents                   │
│  - ICodingAgent (planned)                           │
│  - ConversationalAgent (base class)                 │
│  - CustomerSupportAgent, DataAnalystAgent (samples) │
└────────────────────┬────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────┐
│      Ironbees.AgentMode.Providers                   │
│  - LLMProviderFactoryRegistry                       │
│  - OpenAIProviderFactory                            │
│  - AzureOpenAIProviderFactory                       │
│  - AnthropicProviderFactory (planned)               │
│  - OpenAICompatibleProviderFactory                  │
└────────────────────┬────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────┐
│        Microsoft.Extensions.AI.IChatClient          │
│  - Unified abstraction for all LLM providers        │
└─────────────────────────────────────────────────────┘
```

---

## Quick Start

### 1. Installation

```bash
# Core packages
dotnet add package Ironbees.AgentMode.Core
dotnet add package Ironbees.AgentMode.Providers

# Optional: For specific agents
dotnet add package Ironbees.AgentMode.Agents
```

### 2. Basic Usage

```csharp
using Ironbees.AgentMode.Configuration;
using Ironbees.AgentMode.Providers;
using Microsoft.Extensions.AI;

// Configure provider
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o-mini",
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

// Create chat client
var registry = new LLMProviderFactoryRegistry();
var factory = registry.GetFactory(config.Provider);
var chatClient = factory.CreateChatClient(config);

// Use IChatClient directly
var response = await chatClient.CompleteAsync([
    new ChatMessage(ChatRole.User, "Write a C# hello world")
]);

Console.WriteLine(response.Text);
```

### 3. Using ConversationalAgent

```csharp
using Ironbees.AgentMode.Agents;

// Create custom agent
public class MyAgent : ConversationalAgent
{
    public MyAgent(IChatClient chatClient)
        : base(chatClient, "You are a helpful C# coding assistant.")
    {
    }
}

// Use agent
var agent = new MyAgent(chatClient);

// Single-turn response
var response = await agent.RespondAsync("How do I create a class in C#?");
Console.WriteLine(response);

// Streaming response
await foreach (var chunk in agent.StreamResponseAsync("Explain async/await"))
{
    Console.Write(chunk);
}
```

---

## Core Projects

### Ironbees.AgentMode.Core

**Purpose**: Configuration, interfaces, and base types

**Key Types**:
- `LLMConfiguration` - Universal provider configuration
- `LLMProvider` enum - Provider selection (OpenAI, Azure, Anthropic, etc.)
- `ILLMProviderFactory` - Factory interface for creating chat clients

**Usage**:
```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o-mini",
    ApiKey = apiKey,
    Temperature = 0.0f,
    MaxOutputTokens = 4096,
    EnablePromptCaching = false  // Provider-specific features
};
```

---

### Ironbees.AgentMode.Providers

**Purpose**: LLM provider implementations

**Key Components**:

#### 1. LLMProviderFactoryRegistry

Centralized registry for all provider factories:

```csharp
var registry = new LLMProviderFactoryRegistry();

// Get factory for specific provider
var factory = registry.GetFactory(LLMProvider.OpenAI);

// Or let it select based on configuration
var factory = registry.GetFactory(config.Provider);
```

#### 2. Provider Factories

| Factory | Provider | Status |
|---------|----------|--------|
| `OpenAIProviderFactory` | OpenAI | ✅ Ready |
| `AzureOpenAIProviderFactory` | Azure OpenAI | ✅ Ready |
| `OpenAICompatibleProviderFactory` | GPUStack, Ollama, etc. | ✅ Ready |
| `AnthropicProviderFactory` | Anthropic Claude | 🔄 Planned v0.2.0 |

**Example**:
```csharp
// OpenAI
var openaiFactory = new OpenAIProviderFactory();
var openaiClient = openaiFactory.CreateChatClient(new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o-mini",
    ApiKey = openaiKey
});

// Self-hosted (GPUStack)
var gpustackFactory = new OpenAICompatibleProviderFactory();
var gpustackClient = gpustackFactory.CreateChatClient(new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://gpu-cluster:8080/v1",
    ApiKey = gpustackKey,
    Model = "llama-3.1-8b"
});
```

---

### Ironbees.AgentMode.Agents

**Purpose**: Agent implementations and base classes

**Key Components**:

#### ConversationalAgent (Base Class)

Abstract base for request-response agents:

```csharp
public abstract class ConversationalAgent
{
    protected ConversationalAgent(IChatClient chatClient, string systemPrompt);

    // Core methods
    public virtual Task<string> RespondAsync(
        string userMessage,
        CancellationToken cancellationToken = default);

    public virtual IAsyncEnumerable<string> StreamResponseAsync(
        string userMessage,
        CancellationToken cancellationToken = default);

    // Override for conversation history
    protected virtual IList<ChatMessage> BuildMessages(string userMessage);
}
```

**Sample Agents**:
- `CustomerSupportAgent` - Empathetic customer support
- `DataAnalystAgent` - Data science expertise (SQL, Python, ML)

**Custom Agent Example**:
```csharp
public class CSharpExpertAgent : ConversationalAgent
{
    public CSharpExpertAgent(IChatClient chatClient)
        : base(chatClient, @"
You are a C# expert with deep knowledge of:
- .NET Framework, .NET Core, .NET 8+
- LINQ, async/await, pattern matching
- Design patterns and SOLID principles
- Performance optimization and best practices
")
    {
    }

    // Optional: Add conversation history
    protected override IList<ChatMessage> BuildMessages(string userMessage)
    {
        var messages = base.BuildMessages(userMessage);
        // Add conversation context if needed
        return messages;
    }
}
```

---

### Ironbees.AgentMode.MCP

**Purpose**: Model Context Protocol (MCP) integration

**Status**: Planned for v0.2.0+

**Planned Features**:
- MCP server discovery
- Tool registration and invocation
- JSON-RPC 2.0 protocol handling
- stdio/HTTP transport support

---

### Ironbees.AgentMode.Tools

**Purpose**: Tool definitions and handlers

**Status**: Planned for v0.2.0+

**Planned Tools**:
- File system operations
- Git operations
- Build and test execution
- Code analysis (Roslyn-based)

---

## Integration Patterns

### Pattern 1: Direct IChatClient Usage

**Best for**: Simple chat interactions, prototyping

```csharp
var config = new LLMConfiguration { /* ... */ };
var factory = new OpenAIProviderFactory();
var chatClient = factory.CreateChatClient(config);

var response = await chatClient.CompleteAsync([
    new ChatMessage(ChatRole.System, "You are helpful assistant"),
    new ChatMessage(ChatRole.User, "Hello!")
]);
```

### Pattern 2: ConversationalAgent

**Best for**: Domain-specific chatbots, Q&A systems

```csharp
public class SupportAgent : ConversationalAgent
{
    public SupportAgent(IChatClient chatClient)
        : base(chatClient, "You are a customer support agent...")
    {
    }
}

var agent = new SupportAgent(chatClient);
var response = await agent.RespondAsync("How do I reset my password?");
```

### Pattern 3: ICodingAgent (Planned)

**Best for**: Code generation, autonomous coding tasks

```csharp
// Planned for v0.2.0
public interface ICodingAgent
{
    Task<CodeGenerationResult> GenerateAsync(string instruction);
    Task<ValidationResult> ValidateAsync(string code);
    Task<RefineResult> RefineAsync(string code, ValidationResult validation);
}
```

---

## Configuration Strategies

### Environment-Based Configuration

```csharp
public static LLMConfiguration CreateFromEnvironment()
{
    var provider = Enum.Parse<LLMProvider>(
        Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "OpenAI");

    return provider switch
    {
        LLMProvider.OpenAI => new LLMConfiguration
        {
            Provider = LLMProvider.OpenAI,
            Model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        },
        LLMProvider.AzureOpenAI => new LLMConfiguration
        {
            Provider = LLMProvider.AzureOpenAI,
            Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
            Model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"),
            ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")
        },
        LLMProvider.OpenAICompatible => new LLMConfiguration
        {
            Provider = LLMProvider.OpenAICompatible,
            Endpoint = Environment.GetEnvironmentVariable("GPUSTACK_ENDPOINT"),
            ApiKey = Environment.GetEnvironmentVariable("GPUSTACK_API_KEY"),
            Model = Environment.GetEnvironmentVariable("GPUSTACK_MODEL")
        },
        _ => throw new NotSupportedException($"Provider {provider} not supported")
    };
}
```

### Dependency Injection

```csharp
// Startup.cs or Program.cs
services.AddSingleton<LLMConfiguration>(sp =>
{
    return new LLMConfiguration
    {
        Provider = LLMProvider.OpenAI,
        Model = "gpt-4o-mini",
        ApiKey = configuration["OpenAI:ApiKey"]
    };
});

services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<LLMConfiguration>();
    var registry = new LLMProviderFactoryRegistry();
    var factory = registry.GetFactory(config.Provider);
    return factory.CreateChatClient(config);
});

services.AddScoped<CustomerSupportAgent>();
```

---

## Samples

### ConversationalAgentSample

**Location**: `samples/ConversationalAgentSample/`

Demonstrates:
- CustomerSupportAgent usage
- DataAnalystAgent usage
- Single-turn vs streaming responses
- Multi-provider configuration

```bash
cd samples/ConversationalAgentSample
dotnet run
```

### GpuStackSample

**Location**: `samples/GpuStackSample/`

Demonstrates:
- GPUStack integration
- Self-hosted LLM usage
- Environment variable configuration

```bash
# Configure .env
GPUSTACK_ENDPOINT=http://172.30.1.53:8080
GPUSTACK_API_KEY=gpustack_xxx
GPUSTACK_MODEL=kanana-1.5

cd samples/GpuStackSample
dotnet run
```

---

## Testing

### Unit Testing

```csharp
using Moq;
using Microsoft.Extensions.AI;

[Fact]
public async Task TestAgent()
{
    // Mock IChatClient
    var mockClient = new Mock<IChatClient>();
    mockClient
        .Setup(x => x.CompleteAsync(
            It.IsAny<IList<ChatMessage>>(),
            null,
            default))
        .ReturnsAsync(new ChatCompletion(
            new ChatMessage(ChatRole.Assistant, "Test response")));

    // Test agent
    var agent = new CustomerSupportAgent(mockClient.Object);
    var response = await agent.RespondAsync("Test question");

    Assert.Equal("Test response", response);
}
```

### Integration Testing

```csharp
[Fact(Skip = "Integration test - requires API key")]
public async Task TestRealProvider()
{
    var config = new LLMConfiguration
    {
        Provider = LLMProvider.OpenAI,
        Model = "gpt-4o-mini",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    };

    var factory = new OpenAIProviderFactory();
    var chatClient = factory.CreateChatClient(config);

    var response = await chatClient.CompleteAsync([
        new ChatMessage(ChatRole.User, "Say 'test passed'")
    ]);

    Assert.Contains("test passed", response.Text, StringComparison.OrdinalIgnoreCase);
}
```

---

## Roadmap

### v0.1.6 (Current) ✅
- ✅ OpenAI, Azure OpenAI, OpenAI-Compatible providers
- ✅ ConversationalAgent base class
- ✅ Sample agents (CustomerSupport, DataAnalyst)
- ✅ GPUStack integration

### v0.2.0 (Planned - 3-4 weeks)
- 🔄 Anthropic Claude provider
- 🔄 ICodingAgent interface and implementation
- 🔄 MCP server integration
- 🔄 Tool framework

### v0.3.0 (Future)
- 🔮 Advanced agent orchestration
- 🔮 Multi-agent collaboration
- 🔮 Agent marketplace/registry

---

## Relationship to Ironbees.Core

### Ironbees.Core (Original)

- **Focus**: Filesystem convention-based agent loading
- **Pattern**: `agents/{name}/agent.yaml` auto-discovery
- **Framework**: Microsoft Agent Framework, Azure OpenAI only
- **Use Case**: Simple agent management with minimal code

### Ironbees.AgentMode (New)

- **Focus**: Multi-provider LLM integration
- **Pattern**: Explicit configuration with `LLMConfiguration`
- **Framework**: Any provider via `Microsoft.Extensions.AI`
- **Use Case**: Flexible provider selection, custom agents

### When to Use Which?

| Scenario | Use |
|----------|-----|
| Azure OpenAI + filesystem agents | **Ironbees.Core** |
| Multiple providers (OpenAI, Anthropic, self-hosted) | **AgentMode** |
| Custom agent logic (ConversationalAgent, ICodingAgent) | **AgentMode** |
| MCP tool integration | **AgentMode** (v0.2.0+) |
| Legacy compatibility | **Ironbees.Core** |

**Note**: You can use both! AgentMode complements Core, doesn't replace it.

---

## See Also

- **[Providers Guide](PROVIDERS.md)** - Detailed provider configuration
- **[Self-Hosted LLMs](SELF_HOSTED_LLMS.md)** - GPUStack, Ollama, LocalAI setup
- **[Architecture](architecture/agent-mode-architecture.md)** - Technical architecture
- **[API Reference](api/agent-mode-api-specification.md)** - Complete API docs

---

## Support

- **Issues**: [GitHub Issues](https://github.com/iyulab/ironbees/issues)
- **Discussions**: [GitHub Discussions](https://github.com/iyulab/ironbees/discussions)
- **Samples**: `samples/` directory in repository
