# 시작 가이드

Ironbees 프레임워크를 사용하여 멀티 에이전트 시스템을 구축하는 방법을 단계별로 안내합니다.

## 설치

### NuGet 패키지

```bash
# Core library
dotnet add package Ironbees.Core

# Azure OpenAI integration
dotnet add package Ironbees.AgentFramework
```

### 소스 코드

```bash
git clone https://github.com/iyulab/ironbees.git
cd ironbees
dotnet restore
dotnet build
```

## 에이전트 정의

### 디렉토리 구조

```
agents/
├── coding-agent/
│   ├── agent.yaml
│   └── system-prompt.md
├── writing-agent/
│   ├── agent.yaml
│   └── system-prompt.md
└── ...
```

### agent.yaml 형식

```yaml
# 필수 필드
name: coding-agent              # 고유 식별자
description: Expert coder       # 간단한 설명
version: 1.0.0                  # 시맨틱 버전

# 선택 기준
capabilities:                   # 에이전트가 할 수 있는 일
  - code-generation
  - code-review
  - debugging

tags:                           # 검색 키워드
  - programming
  - development
  - coding

# LLM 모델 설정
model:
  deployment: gpt-4             # Azure OpenAI 배포 이름
  temperature: 0.7              # 0.0 ~ 2.0
  max_tokens: 2000              # 최대 토큰 수
  top_p: 0.95                   # 선택적
  frequency_penalty: 0.0        # 선택적
  presence_penalty: 0.0         # 선택적

# 선택적 메타데이터
metadata:
  author: Your Name
  created: 2025-01-15
```

### system-prompt.md 형식

```markdown
You are an expert software developer.

## Your Capabilities
- Write clean, maintainable code
- Review code for potential issues
- Debug and fix problems

## Guidelines
- Follow best practices
- Provide clear explanations
- Use appropriate design patterns
```

## 서비스 구성

### ASP.NET Core

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIronbees(options =>
{
    // Azure OpenAI 설정
    options.AzureOpenAIEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
    options.AzureOpenAIKey = builder.Configuration["AzureOpenAI:Key"];

    // 선택적 설정
    options.AgentsDirectory = "./agents";
    options.MinimumConfidenceThreshold = 0.3;
});

var app = builder.Build();

// 에이전트 로드
var orchestrator = app.Services.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync();

app.Run();
```

### Console Application

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
});

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

await orchestrator.LoadAgentsAsync();
```

### OpenAI API (non-Azure)

```csharp
// 커스텀 어댑터 사용
services.AddSingleton<ILLMFrameworkAdapter>(sp =>
    new OpenAIAdapter(apiKey, model: "gpt-4"));

services.AddSingleton<IAgentLoader>(sp => new FileSystemAgentLoader());
services.AddSingleton<IAgentRegistry, AgentRegistry>();
services.AddSingleton<IAgentSelector>(sp =>
    new KeywordAgentSelector(minimumConfidenceThreshold: 0.3));
services.AddSingleton<IAgentOrchestrator>(sp =>
    new AgentOrchestrator(
        sp.GetRequiredService<IAgentLoader>(),
        sp.GetRequiredService<IAgentRegistry>(),
        sp.GetRequiredService<ILLMFrameworkAdapter>(),
        sp.GetRequiredService<IAgentSelector>(),
        agentsPath));
```

## 기본 사용법

### 명시적 에이전트 지정

```csharp
var response = await orchestrator.ProcessAsync(
    "Write a C# method to calculate fibonacci numbers",
    agentName: "coding-agent");

Console.WriteLine(response);
```

### 자동 에이전트 선택

```csharp
var response = await orchestrator.ProcessAsync(
    "Help me write some Python code");
// → 자동으로 coding-agent 선택

Console.WriteLine(response);
```

### 스트리밍 응답

```csharp
await foreach (var chunk in orchestrator.StreamAsync(
    "Explain SOLID principles",
    "coding-agent"))
{
    Console.Write(chunk);
}
```

### 에이전트 선택 정보 확인

```csharp
var selection = await orchestrator.SelectAgentAsync(
    "Debug this code for me");

Console.WriteLine($"Selected: {selection.SelectedAgent?.Name}");
Console.WriteLine($"Confidence: {selection.ConfidenceScore:P0}");
Console.WriteLine($"Reason: {selection.SelectionReason}");

// 모든 에이전트 점수 확인
foreach (var score in selection.AllScores)
{
    Console.WriteLine($"  {score.Agent.Name}: {score.Score:P0}");
}
```

## 에이전트 선택 알고리즘

`KeywordAgentSelector`는 다중 요소 점수 계산을 사용:

| 요소 | 가중치 | 설명 |
|------|--------|------|
| Capabilities | 40% | 입력과 capabilities 키워드 매칭 |
| Tags | 30% | 입력과 tags 키워드 매칭 |
| Description | 20% | 설명 텍스트 매칭 |
| Name | 10% | 에이전트 이름 매칭 |

### 선택 과정

1. 입력 텍스트에서 키워드 추출 (불용어 제거)
2. 각 에이전트에 대해 점수 계산
3. 점수를 정규화 (0.0 ~ 1.0)
4. 최소 신뢰도 임계값과 비교
5. 가장 높은 점수의 에이전트 선택

## 고급 설정

### 커스텀 Selector

```csharp
public class LLMBasedSelector : IAgentSelector
{
    public async Task<AgentSelectionResult> SelectAgentAsync(
        string input,
        IReadOnlyCollection<IAgent> availableAgents,
        CancellationToken cancellationToken = default)
    {
        // LLM을 사용하여 에이전트 선택
        // ...
    }
}

services.AddSingleton<IAgentSelector, LLMBasedSelector>();
```

### 커스텀 Adapter

```csharp
public class CustomLLMAdapter : ILLMFrameworkAdapter
{
    public Task<IAgent> CreateAgentAsync(
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        // 커스텀 LLM 통합
        // ...
    }
}

services.AddSingleton<ILLMFrameworkAdapter, CustomLLMAdapter>();
```

## 문제 해결

### 에이전트가 로드되지 않음

```bash
# 디렉토리 구조 확인
ls -la agents/*/

# YAML 구문 검증
# agent.yaml이 올바른 YAML 형식인지 확인

# 필수 필드 확인
# name, description, version, model이 모두 있는지 확인
```

### 에이전트 선택이 예상과 다름

```csharp
// 선택 과정 디버깅
var result = await orchestrator.SelectAgentAsync(input);

Console.WriteLine($"Selected: {result.SelectedAgent?.Name}");
Console.WriteLine($"Confidence: {result.ConfidenceScore:P0}");

foreach (var score in result.AllScores)
{
    Console.WriteLine($"  {score.Agent.Name}: {score.Score:P0}");
    Console.WriteLine($"    {string.Join(", ", score.Reasons)}");
}
```

### Azure OpenAI 연결 실패

```csharp
// 1. 엔드포인트 형식 확인
// 올바름: https://your-resource.openai.azure.com
// 잘못됨: https://your-resource.openai.azure.com/

// 2. API 키 확인
// Azure Portal에서 키를 다시 확인

// 3. 배포 이름 확인
// agent.yaml의 model.deployment가 실제 배포 이름과 일치하는지 확인
```

## 다음 단계

- [내장 에이전트](../agents/BUILTIN_AGENTS.md) - 즉시 사용 가능한 에이전트
- [아키텍처](ARCHITECTURE.md) - 프레임워크 설계 이해
- [Microsoft Agent Framework 통합](MICROSOFT_AGENT_FRAMEWORK.md) - 고급 기능 활용
