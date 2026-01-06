# Migration Guide: ChatClientBuilder Pattern

> **Breaking Change**: v0.1.8 → v0.4.1
>
> This guide helps you migrate from `LLMProviderFactoryRegistry` to the `ChatClientBuilder` pattern introduced in v0.4.1.

**Migration Time Estimate**:
- Small projects (<5 agents): 1-2 hours
- Medium projects (5-20 agents): 2-4 hours
- Large projects (>20 agents): 4-8 hours

**Table of Contents**:
- [Overview](#overview)
- [What Changed](#what-changed)
- [Why Changed](#why-changed)
- [Provider-Specific Recipes](#provider-specific-recipes)
- [Common Errors](#common-errors)
- [Testing Strategy](#testing-strategy)
- [Migration Checklist](#migration-checklist)

---

## Overview

### What Changed

v0.4.1 removed the `LLMProviderFactoryRegistry` abstraction in favor of direct integration with `Microsoft.Extensions.AI`:

```diff
- using Ironbees.AgentMode.Providers;
+ using Microsoft.Extensions.AI;
+ using OpenAI;

- var chatClient = LLMProviderFactoryRegistry.CreateChatClient(
-     provider: "openai",
-     modelName: "gpt-4o-mini",
-     apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")
- );

+ var openAIClient = new OpenAIClient(apiKey);
+ var chatClient = new ChatClientBuilder(
+     openAIClient.GetChatClient(model).AsIChatClient())
+     .UseFunctionInvocation()
+     .Build();
```

### Why Changed

**Benefits of Microsoft.Extensions.AI Integration**:
1. ✅ **Industry Standard**: Microsoft's official LLM abstraction
2. ✅ **Better Maintenance**: Microsoft maintains provider implementations
3. ✅ **Richer Features**: Native streaming, function calling, embeddings
4. ✅ **Thin Wrapper**: ironbees focuses on agent orchestration, not provider abstraction

**ironbees Philosophy**:
> "Delegate to MS Agent Framework, not reinvent"

We removed our provider abstraction because Microsoft.Extensions.AI does it better.

---

## Provider-Specific Recipes

### Recipe 1: OpenAI (Standard)

**Before (v0.1.8)**:
```csharp
using Ironbees.AgentMode.Providers;

var chatClient = LLMProviderFactoryRegistry.CreateChatClient(
    provider: "openai",
    modelName: "gpt-4o-mini",
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")
);
```

**After (v0.4.1)**:
```csharp
using Microsoft.Extensions.AI;
using OpenAI;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
var model = "gpt-4o-mini";

var openAIClient = new OpenAIClient(apiKey);
var chatClient = new ChatClientBuilder(
    openAIClient.GetChatClient(model).AsIChatClient())
    .UseFunctionInvocation()
    .Build();
```

**Key Changes**:
- ✅ Import `OpenAI` package instead of ironbees provider
- ✅ Create `OpenAIClient` first
- ✅ Pass `IChatClient` to `ChatClientBuilder` constructor
- ✅ Use `.UseFunctionInvocation()` for tool support

**Package Reference**:
```xml
<PackageReference Include="OpenAI" Version="2.1.0" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.1.1" />
```

---

### Recipe 2: Azure OpenAI

**Before (v0.1.8)**:
```csharp
using Ironbees.AgentMode.Providers;

var chatClient = LLMProviderFactoryRegistry.CreateChatClient(
    provider: "azure-openai",
    endpoint: "https://my-resource.openai.azure.com",
    apiKey: Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY"),
    deploymentName: "gpt-4o"
);
```

**After (v0.4.1)**:
```csharp
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using OpenAI;

var azureEndpoint = "https://my-resource.openai.azure.com";
var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")!;
var deployment = "gpt-4o";

// Use OpenAI package with custom endpoint (Azure.AI.OpenAI removed in ME.AI v10.x)
var azureClient = new OpenAIClient(
    new ApiKeyCredential(azureKey),
    new OpenAIClientOptions
    {
        Endpoint = new Uri(azureEndpoint)
    });

var chatClient = new ChatClientBuilder(
    azureClient.GetChatClient(deployment).AsIChatClient())
    .UseFunctionInvocation()
    .Build();
```

**Important Note**:
> ⚠️ `Azure.AI.OpenAI` package was removed in Microsoft.Extensions.AI v10.x.
> Use `OpenAI` package with custom `Endpoint` instead.

**Package Reference**:
```xml
<PackageReference Include="OpenAI" Version="2.1.0" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.1.1" />
```

---

### Recipe 3: Custom Endpoints (GPUStack, LiteLLM, etc.)

**Before (v0.1.8)**:
```csharp
// Custom endpoints not well supported
```

**After (v0.4.1)**:
```csharp
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using OpenAI;

var customEndpoint = "https://gpustack.example.com/v1";
var apiKey = Environment.GetEnvironmentVariable("GPUSTACK_KEY")!;
var model = "claude-3-5-sonnet-20241022"; // Anthropic via proxy

var customClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions
    {
        Endpoint = new Uri(customEndpoint)
    });

var chatClient = new ChatClientBuilder(
    customClient.GetChatClient(model).AsIChatClient())
    .UseFunctionInvocation()
    .Build();
```

**Use Cases**:
- ✅ GPUStack (OpenAI-compatible)
- ✅ LiteLLM proxy
- ✅ Anthropic via proxy (workaround until native support)
- ✅ Local models (Ollama with OpenAI compatibility)

**Package Reference**: Same as Azure OpenAI

---

## Common Errors

### Error 1: No Parameterless Constructor

**Error Message**:
```
error CS1729: 'ChatClientBuilder' does not contain a constructor that takes 0 arguments
```

**Cause**:
Trying to use `new ChatClientBuilder()` without arguments.

**Solution**:
`ChatClientBuilder` requires `IChatClient` parameter:

```csharp
// ❌ Wrong
var builder = new ChatClientBuilder();

// ✅ Correct
var openAIClient = new OpenAIClient(apiKey);
var builder = new ChatClientBuilder(
    openAIClient.GetChatClient(model).AsIChatClient());
```

---

### Error 2: Namespace Not Found (Core)

**Error Message**:
```
error CS0234: The type or namespace name 'Providers' does not exist in the namespace 'Ironbees.AgentMode'
```

**Cause**:
`Ironbees.AgentMode.Providers` removed in v0.4.1.

**Solution**:
Use `Microsoft.Extensions.AI`:

```csharp
// ❌ Wrong
using Ironbees.AgentMode.Providers;

// ✅ Correct
using Microsoft.Extensions.AI;
using OpenAI;
```

---

### Error 3: Azure.AI.OpenAI Not Found

**Error Message**:
```
error CS0234: The type or namespace name 'Azure' could not be found
```

**Cause**:
`Azure.AI.OpenAI` package removed in Microsoft.Extensions.AI v10.x.

**Solution**:
Use `OpenAI` package with custom endpoint (see Recipe 2).

---

### Error 4: AsIChatClient() Missing

**Error Message**:
```
error CS1503: Argument 1: cannot convert from 'OpenAI.Chat.ChatClient' to 'Microsoft.Extensions.AI.IChatClient'
```

**Cause**:
Forgot `.AsIChatClient()` extension method.

**Solution**:
```csharp
// ❌ Wrong
var chatClient = new ChatClientBuilder(
    openAIClient.GetChatClient(model))  // Missing conversion
    .Build();

// ✅ Correct
var chatClient = new ChatClientBuilder(
    openAIClient.GetChatClient(model).AsIChatClient())
    .Build();
```

---

## Testing Strategy

### Validation Checklist

After migration, verify:

#### Build
```bash
dotnet build
# Should succeed with 0 errors
```

#### Unit Tests (if applicable)
```bash
dotnet test --filter "Category!=Integration"
# Service layer tests should pass without LLM
```

#### Integration Tests
```bash
# Requires API keys
export OPENAI_API_KEY=sk-...
dotnet test --filter "Category=Integration"
```

#### Manual Verification
1. ✅ Agent selection works (keyword/embedding/hybrid)
2. ✅ Streaming responses work
3. ✅ Function calling works (if used)
4. ✅ Error handling behaves as expected

---

## Migration Checklist

### Phase 1: Preparation
- [ ] Backup project (git commit or branch)
- [ ] Read this guide completely
- [ ] Identify all `LLMProviderFactoryRegistry` usages
- [ ] Determine provider types used (OpenAI, Azure, Custom)

### Phase 2: Package Updates
- [ ] Update `Directory.Packages.props` or `.csproj`:
  ```xml
  <PackageReference Include="Ironbees.Core" Version="0.4.1" />
  <PackageReference Include="Ironbees.AgentMode" Version="0.4.1" />
  <PackageReference Include="OpenAI" Version="2.1.0" />
  <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.1.1" />
  ```
- [ ] Remove old packages (if any):
  ```xml
  <!-- Remove these -->
  <PackageReference Include="Ironbees.AgentMode.Core" Version="0.1.8" />
  <PackageReference Include="Ironbees.AgentMode.Providers" Version="0.1.8" />
  ```

### Phase 3: Code Migration
- [ ] Update using statements:
  ```diff
  - using Ironbees.AgentMode.Providers;
  + using Microsoft.Extensions.AI;
  + using OpenAI;
  ```
- [ ] Replace `LLMProviderFactoryRegistry` calls (use recipes above)
- [ ] Add `.AsIChatClient()` where needed
- [ ] Add `.UseFunctionInvocation()` if using tools

### Phase 4: Testing
- [ ] Build succeeds (`dotnet build`)
- [ ] Unit tests pass
- [ ] Integration tests pass (with API keys)
- [ ] Manual smoke test

### Phase 5: Cleanup
- [ ] Remove unused using statements
- [ ] Remove old provider-related code
- [ ] Update documentation/comments if any

---

## Environment Variables Pattern

**Recommended Configuration**:

```bash
# .env or launchSettings.json

# OpenAI
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-4o-mini

# Azure OpenAI
AZURE_OPENAI_ENDPOINT=https://my-resource.openai.azure.com
AZURE_OPENAI_KEY=...
AZURE_OPENAI_DEPLOYMENT=gpt-4o

# Custom Endpoint (GPUStack, etc.)
GPUSTACK_ENDPOINT=https://gpustack.example.com/v1
GPUSTACK_KEY=...
GPUSTACK_MODEL=claude-3-5-sonnet-20241022
```

**Environment-Based Factory** (optional):
```csharp
public static IChatClient CreateChatClientFromEnvironment()
{
    var provider = Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "openai";

    return provider.ToLowerInvariant() switch
    {
        "openai" => CreateOpenAIChatClient(),
        "azure" => CreateAzureOpenAIChatClient(),
        "gpustack" => CreateGPUStackChatClient(),
        _ => throw new ArgumentException($"Unsupported provider: {provider}")
    };
}

private static IChatClient CreateOpenAIChatClient()
{
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
    var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

    var client = new OpenAIClient(apiKey);
    return new ChatClientBuilder(client.GetChatClient(model).AsIChatClient())
        .UseFunctionInvocation()
        .Build();
}

// Similar for Azure, GPUStack...
```

---

## Real-World Example: MLoop Migration

**MLoop Project Context**:
- 5 custom agents
- OpenAI + GPUStack providers
- Migration time: ~2 hours

**Before (IronbeesOrchestrator.cs)**:
```csharp
// v0.1.8
using Ironbees.AgentMode.Providers;

var chatClient = LLMProviderFactoryRegistry.CreateChatClient(
    provider: "openai",
    modelName: configuration["LLM:Model"]!,
    apiKey: configuration["LLM:ApiKey"]!
);
```

**After (IronbeesOrchestrator.cs)**:
```csharp
// v0.4.1
using Microsoft.Extensions.AI;
using OpenAI;

private static IChatClient CreateChatClient(IConfiguration configuration)
{
    var provider = configuration["LLM:Provider"] ?? "openai";

    return provider.ToLowerInvariant() switch
    {
        "openai" => CreateOpenAIChatClient(configuration),
        "gpustack" => CreateGPUStackChatClient(configuration),
        _ => throw new ArgumentException($"Unsupported provider: {provider}")
    };
}

private static IChatClient CreateOpenAIChatClient(IConfiguration configuration)
{
    var apiKey = configuration["LLM:ApiKey"]!;
    var model = configuration["LLM:Model"] ?? "gpt-4o-mini";

    var client = new OpenAIClient(apiKey);
    return new ChatClientBuilder(client.GetChatClient(model).AsIChatClient())
        .UseFunctionInvocation()
        .Build();
}

private static IChatClient CreateGPUStackChatClient(IConfiguration configuration)
{
    var endpoint = configuration["LLM:Endpoint"]!;
    var apiKey = configuration["LLM:ApiKey"]!;
    var model = configuration["LLM:Model"]!;

    var client = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

    return new ChatClientBuilder(client.GetChatClient(model).AsIChatClient())
        .UseFunctionInvocation()
        .Build();
}
```

**Results**:
- ✅ Build: Success
- ✅ Tests: 98 passed, 0 failed
- ✅ Both OpenAI and GPUStack working

---

## Additional Resources

### Official Documentation
- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- [OpenAI SDK for .NET](https://github.com/openai/openai-dotnet)

### Related Guides
- [Service Layer Pattern Migration](service-layer-pattern.md) (ConversationalAgent removal)
- [Namespace Migration](namespace-migration.md)

### Support
- [ironbees GitHub Issues](https://github.com/iyulab/ironbees/issues)
- [Discussions](https://github.com/iyulab/ironbees/discussions)

---

**Last Updated**: 2026-01-06
**Validated By**: MLoop Team
