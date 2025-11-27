# 🔌 커스텀 프레임워크 어댑터 작성 가이드

**목표**: 다른 LLM 프레임워크를 Ironbees와 통합하기

Ironbees는 `ILLMFrameworkAdapter` 인터페이스를 통해 다양한 LLM 프레임워크와 통합할 수 있습니다. 이 가이드에서는 새로운 프레임워크 어댑터를 작성하는 방법을 단계별로 설명합니다.

## 📋 지원하는 프레임워크

| 프레임워크 | 상태 | 패키지 |
|-----------|------|--------|
| Azure.AI.OpenAI ChatClient | ✅ 내장 | Ironbees.AgentFramework |
| Microsoft Agent Framework | ✅ 내장 | Ironbees.AgentFramework |
| Semantic Kernel | 🔄 커스텀 | (이 가이드 참조) |
| LangChain.NET | 🔄 커스텀 | (이 가이드 참조) |
| Ollama | 🔄 커스텀 | (이 가이드 참조) |

## 🏗️ 아키텍처 개요

```
┌─────────────────────────────────────┐
│   Ironbees Core                     │
│   (Framework Agnostic)              │
├─────────────────────────────────────┤
│   ILLMFrameworkAdapter              │ ← 여기를 구현
│   (추상화 계층)                      │
├─────────────────────────────────────┤
│   Your Custom Adapter               │ ← 새로 작성
│   (SemanticKernelAdapter 등)        │
├─────────────────────────────────────┤
│   Underlying Framework              │
│   (Semantic Kernel, LangChain, etc.)│
└─────────────────────────────────────┘
```

## 1단계: 인터페이스 이해

**ILLMFrameworkAdapter 인터페이스:**

```csharp
public interface ILLMFrameworkAdapter
{
    /// <summary>
    /// 에이전트를 동기적으로 실행
    /// </summary>
    Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 에이전트를 스트리밍 방식으로 실행
    /// </summary>
    IAsyncEnumerable<string> RunStreamingAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default);
}
```

## 2단계: 프로젝트 구조 생성

```bash
# 새 프로젝트 생성
dotnet new classlib -n Ironbees.SemanticKernel
cd Ironbees.SemanticKernel

# 필요한 패키지 설치
dotnet add package Ironbees.Core
dotnet add package Microsoft.SemanticKernel
```

## 3단계: 어댑터 구현

### 예제: Semantic Kernel 어댑터

```csharp
using Ironbees.Core;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace Ironbees.SemanticKernel;

public class SemanticKernelAdapter : ILLMFrameworkAdapter
{
    private readonly Kernel _kernel;

    public SemanticKernelAdapter(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        // 1. ChatCompletion 서비스 가져오기
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // 2. Chat history 구성
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(agent.Config.SystemPrompt);
        chatHistory.AddUserMessage(input);

        // 3. 설정 구성
        var settings = new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = agent.Config.Model.Temperature,
                ["max_tokens"] = agent.Config.Model.MaxTokens,
                ["top_p"] = agent.Config.Model.TopP
            }
        };

        // 4. 응답 생성
        var response = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            settings,
            _kernel,
            cancellationToken);

        return response.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> RunStreamingAsync(
        IAgent agent,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. ChatCompletion 서비스 가져오기
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // 2. Chat history 구성
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(agent.Config.SystemPrompt);
        chatHistory.AddUserMessage(input);

        // 3. 설정 구성
        var settings = new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = agent.Config.Model.Temperature,
                ["max_tokens"] = agent.Config.Model.MaxTokens,
                ["top_p"] = agent.Config.Model.TopP
            }
        };

        // 4. 스트리밍 응답 생성
        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(
            chatHistory,
            settings,
            _kernel,
            cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }
}
```

## 4단계: 의존성 주입 확장 작성

```csharp
using Ironbees.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace Ironbees.SemanticKernel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIronbeesWithSemanticKernel(
        this IServiceCollection services,
        Action<SemanticKernelOptions> configure)
    {
        // 1. 옵션 구성
        var options = new SemanticKernelOptions();
        configure(options);

        // 2. Semantic Kernel 빌더 생성
        var kernelBuilder = Kernel.CreateBuilder();

        // 3. OpenAI 서비스 추가
        if (!string.IsNullOrEmpty(options.AzureOpenAIEndpoint))
        {
            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: options.DefaultDeployment,
                endpoint: options.AzureOpenAIEndpoint,
                apiKey: options.AzureOpenAIKey);
        }
        else if (!string.IsNullOrEmpty(options.OpenAIApiKey))
        {
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: options.DefaultDeployment,
                apiKey: options.OpenAIApiKey);
        }

        var kernel = kernelBuilder.Build();

        // 4. Ironbees 서비스 등록
        services.AddSingleton(kernel);
        services.AddSingleton<ILLMFrameworkAdapter, SemanticKernelAdapter>();
        services.AddSingleton<IAgentLoader, FileSystemAgentLoader>();
        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<IAgentSelector>(sp =>
            new KeywordAgentSelector(
                threshold: options.ConfidenceThreshold,
                fallbackAgentName: options.FallbackAgentName));
        services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}

public class SemanticKernelOptions
{
    public string? AzureOpenAIEndpoint { get; set; }
    public string? AzureOpenAIKey { get; set; }
    public string? OpenAIApiKey { get; set; }
    public string DefaultDeployment { get; set; } = "gpt-4";
    public string AgentsDirectory { get; set; } = "./agents";
    public double ConfidenceThreshold { get; set; } = 0.6;
    public string? FallbackAgentName { get; set; }
}
```

## 5단계: 사용 예제

```csharp
using Ironbees.Core;
using Ironbees.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Semantic Kernel을 사용하는 Ironbees 구성
services.AddIronbeesWithSemanticKernel(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.DefaultDeployment = "gpt-4";
    options.AgentsDirectory = "./agents";
});

var serviceProvider = services.BuildServiceProvider();
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();

// 에이전트 로드 및 실행
await orchestrator.LoadAgentsAsync();
var response = await orchestrator.ProcessAsync("Hello!");

Console.WriteLine(response);
```

## 6단계: 테스트 작성

```csharp
using Ironbees.Core;
using Ironbees.SemanticKernel;
using Microsoft.SemanticKernel;
using Xunit;

public class SemanticKernelAdapterTests
{
    [Fact]
    public async Task RunAsync_WithValidAgent_ReturnsResponse()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion("gpt-4", "test-api-key")
            .Build();

        var adapter = new SemanticKernelAdapter(kernel);
        var agent = CreateTestAgent();

        // Act
        var response = await adapter.RunAsync(agent, "Hello!");

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response);
    }

    [Fact]
    public async Task RunStreamingAsync_WithValidAgent_ReturnsChunks()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion("gpt-4", "test-api-key")
            .Build();

        var adapter = new SemanticKernelAdapter(kernel);
        var agent = CreateTestAgent();

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in adapter.RunStreamingAsync(agent, "Hello!"))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.NotEmpty(chunks);
    }

    private IAgent CreateTestAgent()
    {
        var config = new AgentConfig
        {
            Name = "test-agent",
            Description = "Test agent",
            Version = "1.0.0",
            SystemPrompt = "You are a helpful assistant.",
            Model = new ModelConfig
            {
                Deployment = "gpt-4",
                Temperature = 0.7,
                MaxTokens = 1000,
                TopP = 1.0
            }
        };

        return new Agent(config);
    }
}
```

## 📦 패키징 및 배포

### NuGet 패키지 생성

**Ironbees.SemanticKernel.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>Ironbees.SemanticKernel</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Semantic Kernel adapter for Ironbees</Description>
    <PackageTags>ironbees;semantic-kernel;llm;agent</PackageTags>
    <RepositoryUrl>https://github.com/iyulab/ironbees-semantickernel</RepositoryUrl>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Ironbees.Core" Version="0.1.2" />
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### 빌드 및 배포

```bash
# 패키지 빌드
dotnet pack -c Release

# NuGet에 배포
dotnet nuget push bin/Release/Ironbees.SemanticKernel.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## 🎨 고급 패턴

### 1. Agent Wrapper 구현

```csharp
public class SemanticKernelAgentWrapper : IAgent
{
    private readonly AgentConfig _config;
    private readonly Kernel _kernel;

    public SemanticKernelAgentWrapper(AgentConfig config, Kernel kernel)
    {
        _config = config;
        _kernel = kernel;
    }

    public string Name => _config.Name;
    public string Description => _config.Description;
    public AgentConfig Config => _config;

    // 추가 Semantic Kernel 특화 기능
    public async Task<string> InvokePluginAsync(string pluginName, string functionName)
    {
        var function = _kernel.Plugins[pluginName][functionName];
        var result = await _kernel.InvokeAsync(function);
        return result.ToString();
    }
}
```

### 2. 플러그인 통합

```csharp
public class SemanticKernelAdapter : ILLMFrameworkAdapter
{
    private readonly Kernel _kernel;

    public SemanticKernelAdapter(Kernel kernel)
    {
        _kernel = kernel;

        // 플러그인 자동 로드
        LoadPlugins();
    }

    private void LoadPlugins()
    {
        // 에이전트별 플러그인 로드 로직
        // 예: agents/{agent-name}/plugins/ 디렉터리 스캔
    }

    // ... RunAsync, RunStreamingAsync 구현
}
```

### 3. 메모리/컨텍스트 관리

```csharp
public class SemanticKernelAdapter : ILLMFrameworkAdapter
{
    private readonly Dictionary<string, ChatHistory> _conversationHistory = new();

    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        // 대화 기록 가져오기 또는 생성
        if (!_conversationHistory.TryGetValue(agent.Name, out var history))
        {
            history = new ChatHistory();
            history.AddSystemMessage(agent.Config.SystemPrompt);
            _conversationHistory[agent.Name] = history;
        }

        // 사용자 메시지 추가
        history.AddUserMessage(input);

        // 응답 생성
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chatCompletion.GetChatMessageContentAsync(
            history,
            cancellationToken: cancellationToken);

        // 응답을 기록에 추가
        history.AddAssistantMessage(response.Content);

        return response.Content ?? string.Empty;
    }

    public void ClearHistory(string agentName)
    {
        _conversationHistory.Remove(agentName);
    }
}
```

## 🔍 다른 프레임워크 예제

### Ollama 어댑터

```csharp
public class OllamaAdapter : ILLMFrameworkAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OllamaAdapter(string baseUrl = "http://localhost:11434")
    {
        _httpClient = new HttpClient();
        _baseUrl = baseUrl;
    }

    public async Task<string> RunAsync(
        IAgent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = agent.Config.Model.Deployment,
            prompt = $"{agent.Config.SystemPrompt}\n\nUser: {input}\nAssistant:",
            temperature = agent.Config.Model.Temperature,
            max_tokens = agent.Config.Model.MaxTokens
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/generate",
            request,
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(
            cancellationToken: cancellationToken);

        return result?.Response ?? string.Empty;
    }

    // ... RunStreamingAsync 구현
}
```

## ✅ 체크리스트

- [ ] `ILLMFrameworkAdapter` 인터페이스 구현
- [ ] `RunAsync` 메서드 구현 (동기 실행)
- [ ] `RunStreamingAsync` 메서드 구현 (스트리밍)
- [ ] 의존성 주입 확장 메서드 작성
- [ ] 단위 테스트 작성 (최소 2개)
- [ ] 통합 테스트 작성
- [ ] README 및 문서 작성
- [ ] 샘플 프로젝트 작성
- [ ] NuGet 패키지 구성
- [ ] 라이선스 파일 추가

## 📚 참고 자료

- [Ironbees 아키텍처](ARCHITECTURE.md)
- [Microsoft Agent Framework 어댑터 소스](../src/Ironbees.AgentFramework/MicrosoftAgentFrameworkAdapter.cs)
- [Azure OpenAI 어댑터 소스](../src/Ironbees.AgentFramework/AgentFrameworkAdapter.cs)

## 💡 모범 사례

1. **에러 처리**: 프레임워크별 예외를 Ironbees 예외로 변환
2. **설정 검증**: 옵션 클래스에서 필수 설정 검증
3. **리소스 관리**: IDisposable 구현 (필요시)
4. **스레드 안전성**: 멀티스레드 환경 고려
5. **성능**: 불필요한 할당 최소화
6. **로깅**: 구조화된 로깅 지원
7. **문서화**: XML 주석으로 API 문서화
8. **테스트**: 높은 테스트 커버리지 유지

## 🤝 커뮤니티 기여

어댑터를 작성했다면 커뮤니티와 공유해주세요!

1. GitHub 저장소 생성
2. NuGet 패키지 배포
3. Ironbees README에 링크 추가 (PR)
4. 샘플 및 문서 제공

---

**다음 읽기**: [프로덕션 배포 가이드](PRODUCTION_DEPLOYMENT.md) →
