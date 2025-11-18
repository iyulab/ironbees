# LLM Provider Configuration Guide

**Version**: 0.1.6
**Target**: Ironbees.AgentMode.Providers
**Audience**: Developers integrating LLM providers

---

## Overview

Ironbees AgentMode supports multiple LLM providers through a unified `IChatClient` interface from `Microsoft.Extensions.AI`. This guide covers configuration and usage for all supported providers.

### Supported Providers

| Provider | Status | Package | Use Case |
|----------|--------|---------|----------|
| **Azure OpenAI** | ✅ Supported | `Microsoft.Extensions.AI.AzureAIInference` | Enterprise, production deployments |
| **OpenAI** | ✅ Supported | `Microsoft.Extensions.AI.OpenAI` | Rapid prototyping, development |
| **Anthropic** | ✅ Supported | `Anthropic.SDK` (community) | Claude models (Sonnet 4.5, 3.5, Opus, Haiku) |
| **OpenAI-Compatible** | ✅ Supported | `Microsoft.Extensions.AI.OpenAI` | Self-hosted LLMs (GPUStack, LocalAI, Ollama, vLLM) |

---

## Quick Start

### Basic Configuration Pattern

All providers follow the same configuration pattern:

```csharp
using Ironbees.AgentMode.Configuration;
using Ironbees.AgentMode.Providers;

// 1. Create configuration
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,  // or AzureOpenAI, OpenAICompatible
    Model = "gpt-4o-mini",
    ApiKey = "your-api-key",
    Temperature = 0.0f,             // Optional (default: 0.0)
    MaxOutputTokens = 4096          // Optional (default: 4096)
};

// 2. Create chat client
var registry = new LLMProviderFactoryRegistry();
var factory = registry.GetFactory(config.Provider);
var chatClient = factory.CreateChatClient(config);

// 3. Use with Microsoft.Extensions.AI
var response = await chatClient.CompleteAsync([
    new ChatMessage(ChatRole.User, "Hello, world!")
]);
```

---

## Provider-Specific Configuration

### 1. Azure OpenAI (Enterprise)

**Use Case**: Production deployments, enterprise environments, compliance requirements

**Requirements**:
- Azure subscription
- Azure OpenAI resource created
- Model deployment configured

**Configuration**:

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.AzureOpenAI,
    Model = "gpt-4o",                    // Your deployment name
    Endpoint = "https://your-resource.openai.azure.com",
    ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY"),
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new AzureOpenAIProviderFactory();
var chatClient = factory.CreateChatClient(config);
```

**Environment Variables**:

```bash
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4o
```

**Features**:
- ✅ Private network deployment
- ✅ RBAC and managed identity support
- ✅ Enterprise SLA and support
- ✅ Data residency control
- ✅ Content filtering

**Pricing**: Pay-as-you-go, reserved capacity available

---

### 2. OpenAI (Development)

**Use Case**: Rapid prototyping, development, personal projects

**Requirements**:
- OpenAI API key from https://platform.openai.com

**Configuration**:

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o-mini",               // or gpt-4o, gpt-4-turbo, etc.
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    // Endpoint is optional (defaults to https://api.openai.com/v1)
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new OpenAIProviderFactory();
var chatClient = factory.CreateChatClient(config);
```

**Environment Variables**:

```bash
OPENAI_API_KEY=sk-proj-...
OPENAI_MODEL=gpt-4o-mini
```

**Available Models**:
- `gpt-4o` - Most capable multimodal model
- `gpt-4o-mini` - Affordable and fast
- `gpt-4-turbo` - Previous generation
- `gpt-3.5-turbo` - Legacy model

**Features**:
- ✅ Latest models available immediately
- ✅ Simple API key authentication
- ✅ Pay-per-use pricing
- ✅ No infrastructure management

**Pricing**: https://openai.com/pricing

---

### 3. Anthropic Claude

**Use Case**: Claude-specific features (extended context, vision, streaming)

**Status**: ✅ Supported (via community Anthropic.SDK)

**Configuration**:

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.Anthropic,
    Model = "claude-sonnet-4-20250514",  // or claude-3-5-sonnet, claude-3-opus, claude-3-haiku
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    // Endpoint defaults to https://api.anthropic.com
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new AnthropicProviderFactory();
var chatClient = factory.CreateChatClient(config);
```

**Environment Variables**:

```bash
ANTHROPIC_API_KEY=sk-ant-api03-...
ANTHROPIC_MODEL=claude-sonnet-4-20250514
```

**Supported Models**:
- `claude-sonnet-4-20250514` - Claude Sonnet 4.5 (latest)
- `claude-3-5-sonnet-20241022` - Claude 3.5 Sonnet
- `claude-3-opus-20240229` - Most capable Claude 3
- `claude-3-haiku-20240307` - Fastest Claude 3

**Features**:
- ✅ Extended context (200K tokens)
- ✅ Streaming responses
- ✅ System prompts
- ✅ Temperature and TopP control
- ⚠️ Vision capabilities (SDK supported, integration pending)
- ⚠️ Function calling (SDK supported, integration pending)
- ⚠️ Prompt caching (SDK supported, integration pending)

**Note**: Currently uses community Anthropic.SDK package. Migration to official Microsoft.Extensions.AI.Anthropic package planned when available

---

### 4. OpenAI-Compatible Endpoints (Self-Hosted)

**Use Case**: Self-hosted LLMs, air-gapped environments, cost optimization

**Supported Platforms**:
- ✅ **GPUStack** - Kubernetes-native GPU cluster management
- ✅ **LocalAI** - Self-hosted OpenAI alternative
- ✅ **Ollama** - Local LLM runner
- ✅ **vLLM** - High-throughput inference server
- ✅ Any OpenAI-compatible API server

**Configuration**:

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://172.30.1.53:8080/v1",  // Required: custom endpoint
    ApiKey = "gpustack_xxx",                  // Optional for some servers
    Model = "kanana-1.5",                     // Model from your deployment
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var factory = new OpenAICompatibleProviderFactory();
var chatClient = factory.CreateChatClient(config);
```

**Platform-Specific Examples**:

#### GPUStack

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://gpu-cluster.local:8080/v1",
    ApiKey = "gpustack_8ef8f2d1e0537fb8_9f99ccb2699267880f8a5787deab1cf1",
    Model = "llama-3.1-70b-instruct"
};
```

**Environment Variables**:
```bash
GPUSTACK_ENDPOINT=http://172.30.1.53:8080
GPUSTACK_API_KEY=gpustack_xxx
GPUSTACK_MODEL=kanana-1.5
```

#### LocalAI

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:8080/v1",
    ApiKey = "optional-key",              // LocalAI may not require auth
    Model = "llama-2-7b"
};
```

#### Ollama

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:11434/v1",
    ApiKey = "dummy",                     // Ollama doesn't require auth
    Model = "mistral:7b-instruct"
};
```

#### vLLM

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:8000/v1",
    ApiKey = "optional-key",
    Model = "meta-llama/Llama-3.1-8B-Instruct"
};
```

**Benefits**:
- 🔐 **Privacy**: Keep sensitive data on-premise
- 💰 **Cost**: Eliminate API usage costs
- 🚀 **Performance**: Lower latency for local deployments
- 🌐 **Offline**: Air-gapped environment support
- 🔧 **Flexibility**: Use any OpenAI-compatible LLM server

**See Also**: [Self-Hosted LLMs Guide](SELF_HOSTED_LLMS.md) for detailed setup instructions

---

## Advanced Configuration

### Temperature and Sampling

```csharp
var config = new LLMConfiguration
{
    // ... provider settings ...
    Temperature = 0.0f,      // 0.0 = deterministic, 2.0 = very creative
    MaxOutputTokens = 4096,  // Maximum response length
};
```

**Temperature Guidelines**:
- `0.0` - Code generation, factual answers (deterministic)
- `0.3-0.7` - Balanced creativity and coherence
- `1.0-2.0` - Creative writing, brainstorming

### Prompt Caching (Anthropic-Specific)

> ⚠️ **Status**: SDK supported, integration pending in future release

```csharp
// Future API (not yet implemented)
var config = new LLMConfiguration
{
    Provider = LLMProvider.Anthropic,
    // ... other settings ...
    EnablePromptCaching = true  // Reduces cost for repeated prompts
};
```

**When to Use** (when available):
- Long system prompts (>1000 tokens)
- Repeated context across requests
- Document analysis workflows

**Cost Savings**: Up to 90% for cached portions

### Additional Options

```csharp
var config = new LLMConfiguration
{
    // ... provider settings ...
    AdditionalOptions = new Dictionary<string, string>
    {
        ["top_p"] = "0.9",
        ["frequency_penalty"] = "0.0",
        ["presence_penalty"] = "0.0"
    }
};
```

---

## Provider Selection Guide

### Decision Matrix

| Requirement | Recommended Provider |
|-------------|---------------------|
| Enterprise compliance, SLA | **Azure OpenAI** |
| Latest models immediately | **OpenAI** |
| Extended context (200K tokens) | **Anthropic** |
| Cost optimization, high volume | **OpenAI-Compatible** (self-hosted) |
| Air-gapped environment | **OpenAI-Compatible** (self-hosted) |
| Data privacy requirements | **Azure OpenAI** or **Self-Hosted** |
| Rapid prototyping | **OpenAI** |

### Cost Comparison

| Provider | Pricing Model | Cost Range (1M tokens) |
|----------|---------------|------------------------|
| Azure OpenAI | Pay-as-you-go or reserved | $2-$60 depending on model |
| OpenAI | Pay-per-use | $0.15-$60 depending on model |
| Anthropic | Pay-per-use | $3-$15 (Claude 3), prompt caching available |
| Self-Hosted | Infrastructure + electricity | Variable (GPU costs) |

### Performance Characteristics

| Provider | Latency | Throughput | Reliability |
|----------|---------|------------|-------------|
| Azure OpenAI | Low (regional) | High | 99.9% SLA |
| OpenAI | Medium | High | Best-effort |
| Anthropic | Low | High | Best-effort |
| Self-Hosted | Very Low | Variable | User-managed |

---

## Error Handling

### Common Errors

```csharp
try
{
    var chatClient = factory.CreateChatClient(config);
    var response = await chatClient.CompleteAsync(messages);
}
catch (ArgumentException ex)
{
    // Invalid configuration (missing API key, endpoint, etc.)
    Console.WriteLine($"Configuration error: {ex.Message}");
}
catch (HttpRequestException ex)
{
    // Network errors, endpoint unreachable
    Console.WriteLine($"Connection error: {ex.Message}");
}
catch (UnauthorizedAccessException ex)
{
    // Invalid API key
    Console.WriteLine($"Authentication error: {ex.Message}");
}
```

### Rate Limiting

```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>(ex => ex.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

await retryPolicy.ExecuteAsync(async () =>
{
    var response = await chatClient.CompleteAsync(messages);
    return response;
});
```

---

## Testing and Validation

### Unit Testing with Mocks

```csharp
// Use Microsoft.Extensions.AI abstractions for testability
var mockChatClient = new Mock<IChatClient>();
mockChatClient
    .Setup(x => x.CompleteAsync(It.IsAny<IList<ChatMessage>>(), null, default))
    .ReturnsAsync(new ChatCompletion(new ChatMessage(ChatRole.Assistant, "Test response")));

// Test your agent logic with mocked client
var agent = new MyAgent(mockChatClient.Object);
var result = await agent.ProcessAsync("test input");
```

### Integration Testing

```csharp
[Fact(Skip = "Integration test - requires API key")]
public async Task TestRealOpenAI()
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

    Assert.Contains("test passed", response.Text);
}
```

---

## Migration Guide

### From Azure OpenAI to OpenAI

```csharp
// Before (Azure OpenAI)
var config = new LLMConfiguration
{
    Provider = LLMProvider.AzureOpenAI,
    Model = "gpt-4o",  // deployment name
    Endpoint = "https://your-resource.openai.azure.com",
    ApiKey = azureApiKey
};

// After (OpenAI)
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o",  // same model name
    ApiKey = openaiApiKey
    // No endpoint needed (uses default)
};
```

### From Cloud to Self-Hosted

```csharp
// Before (OpenAI)
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o-mini",
    ApiKey = openaiApiKey
};

// After (LocalAI)
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:8080/v1",
    Model = "llama-3.1-8b-instruct",
    ApiKey = "optional"
};
```

---

## Troubleshooting

### Provider Creation Fails

**Problem**: `ArgumentException: Invalid configuration`

**Solution**: Verify required fields
```csharp
// All providers require:
- Provider (enum value)
- Model (string, not empty)
- ApiKey (string, not empty)

// Azure OpenAI additionally requires:
- Endpoint (Azure resource URL)

// OpenAI-Compatible additionally requires:
- Endpoint (custom server URL)
```

### Authentication Errors

**Problem**: `UnauthorizedAccessException` or 401 errors

**Solutions**:
1. Verify API key is correct
2. Check key has not expired
3. Confirm key has necessary permissions
4. For Azure: Ensure RBAC roles are assigned

### Endpoint Connection Issues

**Problem**: `HttpRequestException: Connection refused`

**Solutions**:
1. Verify endpoint URL is correct
2. Check firewall/network configuration
3. For self-hosted: Ensure server is running
4. Test endpoint with `curl` or Postman first

---

## Best Practices

### 1. Use Environment Variables

```csharp
// ✅ Good - Secure, configurable
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("API key not configured");

// ❌ Bad - Hardcoded secrets
var apiKey = "sk-proj-hardcoded-key";
```

### 2. Implement Retry Logic

```csharp
// ✅ Good - Handle transient failures
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
```

### 3. Monitor Costs

```csharp
// Track token usage
var response = await chatClient.CompleteAsync(messages);
var usage = response.Usage;  // Prompt tokens, completion tokens, total tokens
Console.WriteLine($"Tokens used: {usage.TotalTokens}");
```

### 4. Use Appropriate Temperature

```csharp
// ✅ Good - Temperature matches use case
var codeGenConfig = new LLMConfiguration
{
    Temperature = 0.0f  // Deterministic for code
};

var creativeConfig = new LLMConfiguration
{
    Temperature = 0.7f  // Creative for writing
};
```

---

## See Also

- [Self-Hosted LLMs Guide](SELF_HOSTED_LLMS.md) - Detailed setup for GPUStack, Ollama, etc.
- [AgentMode Overview](AGENTMODE.md) - Complete AgentMode architecture
- [API Reference](api/agent-mode-api-specification.md) - Detailed API documentation
- [Architecture](architecture/agent-mode-architecture.md) - System design and architecture

---

## Support

- **Issues**: [GitHub Issues](https://github.com/iyulab/ironbees/issues)
- **Discussions**: [GitHub Discussions](https://github.com/iyulab/ironbees/discussions)
- **Documentation**: [Ironbees Docs](https://github.com/iyulab/ironbees/tree/main/docs)
