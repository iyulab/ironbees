# Microsoft Agent Framework 통합

## 개요

Ironbees v1.1부터 [Microsoft Agent Framework](https://aka.ms/agent-framework)와 통합되어 강력한 에이전트 실행 엔진을 활용할 수 있습니다.

Microsoft Agent Framework는 Semantic Kernel과 AutoGen을 통합한 Microsoft의 공식 에이전트 프레임워크로, 다음과 같은 이점을 제공합니다:

- **공식 지원**: Microsoft의 장기 지원 및 업데이트
- **고급 기능**: Workflow, Tool 통합, MCP 네이티브 지원
- **성능 최적화**: 최신 Azure OpenAI 기능 활용
- **표준화**: Microsoft.Extensions.AI 기반 표준 인터페이스

## 아키텍처

```
┌─────────────────────────────────────────────┐
│   Ironbees Orchestrator                     │
│   - Agent loading (filesystem)              │
│   - Agent selection (routing)               │
│   - Pipeline (pre/post processing)          │
├─────────────────────────────────────────────┤
│   Microsoft Agent Framework                 │
│   - AIAgent execution                       │
│   - Model clients (Azure/OpenAI)            │
│   - Thread/context management               │
│   - Middleware & tools                      │
└─────────────────────────────────────────────┘
```

**역할 분담**:
- **Ironbees**: 컨벤션 기반 에이전트 로딩, 선택, 오케스트레이션
- **Agent Framework**: 에이전트 실행, LLM 통신, 도구 호출, 컨텍스트 관리

## 사용 방법

### 1. 패키지 설치

Microsoft Agent Framework 통합은 `Ironbees.AgentFramework` 패키지에 포함되어 있습니다.

```bash
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework
```

### 2. 서비스 구성

`AddIronbees` 구성에서 `UseMicrosoftAgentFramework` 옵션을 활성화합니다:

```csharp
using Ironbees.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
    options.UseMicrosoftAgentFramework = true; // 👈 Microsoft Agent Framework 활성화
});

var serviceProvider = services.BuildServiceProvider();
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();
```

### 3. 에이전트 사용

에이전트 사용 방법은 기존과 동일합니다:

```csharp
// 에이전트 로드
await orchestrator.LoadAgentsAsync();

// 단일 실행
var response = await orchestrator.ProcessAsync(
    "Write a C# method to calculate fibonacci numbers",
    agentName: "coding-agent");

Console.WriteLine(response);
```

## 기능 비교

| 기능 | Azure.AI.OpenAI ChatClient | Microsoft Agent Framework |
|------|----------------------------|---------------------------|
| 기본 채팅 완료 | ✅ | ✅ |
| 스트리밍 응답 | ✅ | ✅ |
| 시스템 프롬프트 | ✅ | ✅ |
| 모델 파라미터 설정 | ✅ | ✅ |
| Workflow 지원 | ❌ | ✅ |
| MCP 네이티브 지원 | ❌ | ✅ |
| 도구 통합 | 수동 | 자동 |
| 컨텍스트 관리 | 수동 | 자동 |
| 공식 장기 지원 | ✅ | ✅ |

## 마이그레이션 가이드

### 기존 코드에서 변경 없음

**좋은 소식**: 기존 Ironbees 코드를 변경할 필요가 없습니다!

```csharp
// 기존 코드 - 그대로 작동
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "...";
    options.AzureOpenAIKey = "...";
    options.AgentsDirectory = "./agents";
    // UseMicrosoftAgentFramework를 설정하지 않으면 기본 ChatClient 사용
});
```

### 점진적 마이그레이션

1. **테스트 환경에서 먼저 활성화**:
   ```csharp
   options.UseMicrosoftAgentFramework = Environment.GetEnvironmentVariable("USE_AGENT_FRAMEWORK") == "true";
   ```

2. **에이전트별로 테스트**: 하나씩 검증 후 전체 마이그레이션

3. **프로덕션 배포**: 검증 완료 후 전환

## 고급 기능

### 1. 스트리밍 응답

Microsoft Agent Framework는 `RunStreamingAsync`를 통해 스트리밍을 지원합니다:

```csharp
await foreach (var chunk in orchestrator.StreamAsync("Tell me a story", "writing-agent"))
{
    Console.Write(chunk);
}
```

### 2. Workflow 통합 (향후 지원 예정)

Microsoft Agent Framework의 Workflow 기능을 활용한 복잡한 멀티 에이전트 오케스트레이션이 향후 버전에서 지원될 예정입니다.

### 3. MCP 도구 통합 (향후 지원 예정)

Model Context Protocol (MCP) 서버와의 네이티브 통합이 향후 버전에서 지원될 예정입니다.

## 문제 해결

### 빌드 오류

**증상**: `AIAgent` 또는 관련 타입을 찾을 수 없음

**해결책**:
```bash
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package Azure.Identity
```

### 런타임 오류

**증상**: "Agent must be created by MicrosoftAgentFrameworkAdapter"

**원인**: `UseMicrosoftAgentFramework` 설정과 실제 사용 어댑터 불일치

**해결책**: 구성 확인 및 애플리케이션 재시작

## 참고 자료

- [Microsoft Agent Framework 공식 문서](https://learn.microsoft.com/agent-framework/)
- [GitHub 저장소](https://github.com/microsoft/agent-framework)
- [Ironbees 연구 문서](../claudedocs/agent-framework-research.md)

## 버전 호환성

| Ironbees | Agent Framework | .NET |
|----------|----------------|------|
| 1.1.0+ | 1.0.0-preview.251028.1+ | 9.0+ |

## 라이선스

Microsoft Agent Framework는 MIT 라이선스를 따릅니다.
