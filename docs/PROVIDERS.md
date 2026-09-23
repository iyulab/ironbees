# LLM Providers

Ironbees does not create chat clients itself. A backend adapter does, and each backend has its own way
of being told which provider to use:

| Backend | Package | Providers | Where the provider is chosen |
|---------|---------|-----------|------------------------------|
| IronHive | `Ironbees.Ironhive` | OpenAI, Anthropic, Google AI, any OpenAI-compatible server (Ollama, LM Studio, vLLM, GPUStack, llama.cpp) | Register providers by name in `ConfigureHive`; an agent picks one with `model.provider` |
| Microsoft Agent Framework | `Ironbees.AgentFramework` | OpenAI, Azure OpenAI | `IronbeesOptions` in `AddIronbees` — one client for every agent |

The provider packages belong to IronHive; add the ones you register.

## IronHive backend

Each `Add…Providers` call registers a provider under a name you choose. An agent refers to that name in
`agents/<name>/agent.yaml`, so several providers can live side by side and each agent can use a
different one.

```bash
dotnet add package Ironbees.Ironhive
dotnet add package IronHive.Providers.OpenAI
```

```csharp
services.AddIronbeesIronhive(options =>
{
    options.AgentsDirectory = "./agents";
    options.ConfigureHive = hive =>
    {
        hive.AddOpenAIProviders("openai", new OpenAIConfig
        {
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
        });
    };
});
```

```yaml
# agents/coding-agent/agent.yaml
model:
  provider: openai     # the name registered above
  deployment: gpt-4o-mini
  temperature: 0.0
  maxTokens: 4096
```

### Anthropic

```bash
dotnet add package IronHive.Providers.Anthropic
```

```csharp
hive.AddAnthropicProviders("anthropic", new AnthropicConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
});
```

### Google AI

```bash
dotnet add package IronHive.Providers.GoogleAI
```

```csharp
hive.AddGoogleAIProviders("google", new GoogleAIConfig
{
    ApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
});
```

### Self-hosted (OpenAI-compatible)

Local servers usually accept requests without a key. Only `BaseUrl` differs between them.

```bash
dotnet add package IronHive.Providers.OpenAI.Compatible
```

```csharp
// Ollama's default endpoint; LM Studio is :1234, vLLM :8000.
hive.AddOpenAICompatibleProviders("ollama", new OpenAICompatibleConfig
{
    BaseUrl = "http://localhost:11434"
});
```

```yaml
model:
  provider: ollama
  deployment: qwen2.5:7b   # a model the server has already pulled
```

The server must already be running with that model available; Ironbees does not start or download it.

## Microsoft Agent Framework backend

```csharp
// OpenAI
services.AddIronbees(options =>
{
    options.OpenAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    options.AgentsDirectory = "./agents";
});

// Azure OpenAI — deployment names go in model.deployment
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
});
```

## Custom adapter

To run agents on another framework, implement `ILLMFrameworkAdapter`:

```csharp
public class CustomAdapter : ILLMFrameworkAdapter
{
    public Task<IAgent> CreateAgentAsync(
        AgentConfig config, CancellationToken ct = default)
    {
        // Create your framework's agent and wrap it as an Ironbees IAgent
    }

    public Task<string> RunAsync(
        IAgent agent, string input, CancellationToken ct = default)
    {
        // Your implementation
    }

    public IAsyncEnumerable<string> StreamAsync(
        IAgent agent, string input, CancellationToken ct = default)
    {
        // Your streaming implementation
    }

    // Optional: conversation-history overloads (default to the base methods) and
    // structured surfaces RunStructuredAsync/StreamStructuredAsync. The structured
    // defaults delegate to your text methods and fail loud (NotSupportedException)
    // when AgentRunOptions requests a feature (e.g. Suggestions) you haven't
    // implemented — override them to support structured output.
}

// Register
services.AddSingleton<ILLMFrameworkAdapter, CustomAdapter>();
```

## Next steps

- [README](../README.md) — the `model:` keys and which backend applies each of them
- [Architecture](./ARCHITECTURE.md)
- [Deployment](./DEPLOYMENT.md)
