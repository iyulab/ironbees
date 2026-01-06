# LLM Providers

Configure LLM providers for Ironbees via unified `LLMConfiguration`.

## Supported Providers

| Provider | Status | Best For |
|----------|--------|----------|
| Azure OpenAI | ✅ | Enterprise, production |
| OpenAI | ✅ | Development, prototyping |
| Anthropic | ✅ | Claude models |
| OpenAI-Compatible | ✅ | Self-hosted (GPUStack, Ollama, vLLM) |

## Quick Setup

```csharp
using Ironbees.AgentMode.Configuration;
using Ironbees.AgentMode.Providers;

var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    Model = "gpt-4o-mini",
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    Temperature = 0.0f,
    MaxOutputTokens = 4096
};

var registry = new LLMProviderFactoryRegistry();
var factory = registry.GetFactory(config.Provider);
var chatClient = factory.CreateChatClient(config);
```

## Provider Configuration

### Azure OpenAI

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.AzureOpenAI,
    Endpoint = "https://your-resource.openai.azure.com",
    ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY"),
    Model = "gpt-4o"  // deployment name
};
```

```bash
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_KEY=your-key
```

### OpenAI

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAI,
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    Model = "gpt-4o-mini"
};
```

**Models**: `gpt-4o`, `gpt-4o-mini`, `gpt-4-turbo`

### Anthropic

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.Anthropic,
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    Model = "claude-sonnet-4-20250514"
};
```

**Models**: `claude-sonnet-4-20250514`, `claude-3-5-sonnet-20241022`, `claude-3-opus-20240229`, `claude-3-haiku-20240307`

### Self-Hosted (OpenAI-Compatible)

#### GPUStack

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://gpu-cluster:8080/v1",
    ApiKey = "gpustack_xxx",
    Model = "llama-3.1-8b-instruct"
};
```

#### Ollama

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:11434/v1",
    ApiKey = "dummy",  // Ollama doesn't require auth
    Model = "mistral:7b-instruct"
};
```

#### vLLM

```csharp
var config = new LLMConfiguration
{
    Provider = LLMProvider.OpenAICompatible,
    Endpoint = "http://localhost:8000/v1",
    Model = "meta-llama/Llama-3.1-8B-Instruct"
};
```

## Custom Adapter

Implement `ILLMFrameworkAdapter` for custom integrations:

```csharp
public class CustomAdapter : ILLMFrameworkAdapter
{
    public Task<string> RunAsync(
        IAgent agent, string input, CancellationToken ct = default)
    {
        // Your implementation
    }

    public IAsyncEnumerable<string> RunStreamingAsync(
        IAgent agent, string input, CancellationToken ct = default)
    {
        // Your streaming implementation
    }
}

// Register
services.AddSingleton<ILLMFrameworkAdapter, CustomAdapter>();
```

## Provider Selection Guide

| Use Case | Provider |
|----------|----------|
| Enterprise compliance | Azure OpenAI |
| Latest models | OpenAI |
| Extended context (200K) | Anthropic |
| Cost optimization | Self-hosted |
| Air-gapped environment | Self-hosted |

## Temperature Guidelines

| Value | Use Case |
|-------|----------|
| 0.0 | Code generation, factual answers |
| 0.3-0.7 | Balanced creativity |
| 1.0+ | Creative writing |

## Error Handling

```csharp
try
{
    var response = await chatClient.CompleteAsync(messages);
}
catch (ArgumentException) { /* Invalid config */ }
catch (HttpRequestException) { /* Network error */ }
catch (UnauthorizedAccessException) { /* Invalid API key */ }
```

## Next Steps

- [README](../README.md) - Quick start examples
- [Architecture](./ARCHITECTURE.md)
- [Deployment](./DEPLOYMENT.md)
