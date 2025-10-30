# 🐝 Ironbees

> 파일시스템 컨벤션 기반 LLM 에이전트 관리 래퍼

Ironbees는 .NET 환경에서 LLM 에이전트의 **반복되는 패턴을 간소화**하는 경량 래퍼입니다. Microsoft Agent Framework, Semantic Kernel, LangChain, ironhive 등의 프레임워크를 대체하는 것이 아니라, **그 위에서 작동하며** 파일시스템 컨벤션으로 에이전트 관리를 단순화합니다.

## 🎯 핵심 가치 제안

**Ironbees가 하는 것:**
- ✅ 파일시스템 컨벤션으로 에이전트 자동 로딩 (`agents/{name}/agent.yaml`)
- ✅ 간단한 키워드 기반 에이전트 라우팅
- ✅ 다중 프레임워크 통합 (Microsoft Agent Framework, ironhive 등)
- ✅ 보일러플레이트 설정 코드 제거

**Ironbees가 하지 않는 것:**
- ❌ 복잡한 워크플로우 오케스트레이션 → 기본 프레임워크 기능 사용
- ❌ 대화 관리 및 컨텍스트 → 기본 프레임워크 기능 사용
- ❌ 도구 통합 및 MCP → 기본 프레임워크 기능 사용
- ❌ 고급 협업 패턴 → 기본 프레임워크 기능 사용

## 💡 왜 Ironbees인가?

일반적인 LLM 앱 개발 시:
```csharp
// 매번 반복되는 패턴
// 1. 에이전트 설정 파일 파싱
// 2. 프롬프트 로딩
// 3. LLM 클라이언트 초기화
// 4. 에이전트 생성
// 5. 의존성 주입 설정
```

Ironbees 사용 시:
```csharp
// 파일 구조만 맞추면 끝
services.AddIronbees(options => {
    options.AzureOpenAIEndpoint = "...";
    options.AgentsDirectory = "./agents";
});

await orchestrator.LoadAgentsAsync();
var result = await orchestrator.ProcessAsync("요청", "agent-name");
```

## 📦 설치

```bash
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework  # Azure OpenAI + Microsoft Agent Framework용
```

## 🚀 빠른 시작

### 1. 에이전트 정의 (파일시스템 컨벤션)

```
agents/
└── coding-agent/
    ├── agent.yaml          # 필수: 에이전트 메타데이터
    └── system-prompt.md    # 필수: 시스템 프롬프트
```

**agents/coding-agent/agent.yaml:**
```yaml
name: coding-agent
description: Expert software developer
capabilities: [code-generation, code-review]
tags: [programming, development]
model:
  deployment: gpt-4
  temperature: 0.7
```

**agents/coding-agent/system-prompt.md:**
```markdown
You are an expert software developer specializing in C# and .NET...
```

### 2. 서비스 구성

**기본 구성 (Azure.AI.OpenAI ChatClient):**
```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
});
```

**Microsoft Agent Framework 사용:**
```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
    options.UseMicrosoftAgentFramework = true; // 👈 프레임워크 전환
});
```

### 3. 에이전트 사용

```csharp
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();

// 에이전트 로드 (파일시스템에서 자동)
await orchestrator.LoadAgentsAsync();

// 명시적 에이전트 선택
var response = await orchestrator.ProcessAsync(
    "Write a C# method to calculate fibonacci numbers",
    agentName: "coding-agent");

// 자동 라우팅 (키워드 기반)
var response = await orchestrator.ProcessAsync(
    "fibonacci numbers in C#"); // "coding" 키워드로 자동 라우팅
```

## 🏗️ 아키텍처

```
┌─────────────────────────────────────────────┐
│   Ironbees (얇은 래퍼)                       │
│   ✅ FileSystemAgentLoader                  │
│      - agents/ 디렉터리 스캔                │
│      - agent.yaml 파싱                      │
│      - system-prompt.md 로딩                │
│   ✅ KeywordAgentSelector                   │
│      - 키워드 기반 라우팅                    │
│   ✅ ILLMFrameworkAdapter                   │
│      - 다중 프레임워크 통합                 │
├─────────────────────────────────────────────┤
│   Microsoft Agent Framework / Semantic Kernel│
│   ➡️ 실제 에이전트 실행                     │
│   ➡️ 워크플로우 오케스트레이션              │
│   ➡️ 도구 통합, MCP, 대화 관리              │
└─────────────────────────────────────────────┘
```

## 📂 프로젝트 구조

```
ironbees/
├── src/
│   ├── Ironbees.Core/           # 파일시스템 로더, 라우팅
│   └── Ironbees.AgentFramework/ # Azure OpenAI + MS Agent Framework 어댑터
├── agents/                       # 에이전트 정의 (9개 예제)
├── docs/                         # 상세 문서
├── samples/                      # 실행 가능한 예제
└── tests/                        # 단위 테스트 (67개)
```

## 🔌 다중 프레임워크 지원

Ironbees는 `ILLMFrameworkAdapter` 인터페이스를 통해 다양한 LLM 프레임워크와 통합할 수 있습니다:

| 프레임워크 | 상태 | 패키지 |
|-----------|------|--------|
| Azure.AI.OpenAI ChatClient | ✅ 지원 | Ironbees.AgentFramework |
| Microsoft Agent Framework | ✅ 지원 | Ironbees.AgentFramework |
| Semantic Kernel | 🔄 계획됨 | - |
| LangChain | 🔄 계획됨 | - |

**프레임워크 전환은 설정 플래그 하나로:**
```csharp
options.UseMicrosoftAgentFramework = true; // or false
```

## 📚 문서

- [시작 가이드](docs/GETTING_STARTED.md) - 상세한 설치 및 구성
- [Microsoft Agent Framework 통합](docs/MICROSOFT_AGENT_FRAMEWORK.md)
- [내장 에이전트](agents/BUILTIN_AGENTS.md) - 5가지 내장 에이전트
- [아키텍처](docs/ARCHITECTURE.md) - 설계 및 확장성

## 🎯 설계 원칙

**Convention over Configuration**
- 파일 구조와 명명 규칙을 따르면 최소 코드로 동작
- `agents/{name}/agent.yaml` + `system-prompt.md` = 자동 로딩

**Thin Wrapper Philosophy**
- LLM 프레임워크의 기능을 숨기지 않고 보완
- 복잡한 오케스트레이션은 기본 프레임워크에 위임
- 보일러플레이트 제거에만 집중

**Framework Agnostic**
- Microsoft Agent Framework, Semantic Kernel, LangChain 등과 통합
- `ILLMFrameworkAdapter`로 새 프레임워크 추가 가능

**Extensibility First**
- 모든 핵심 컴포넌트 교체 가능
- `IAgentLoader`, `IAgentSelector`, `ILLMFrameworkAdapter`

## 🧪 실험적 기능

다음 기능은 **실험적**이며 향후 제거되거나 크게 변경될 수 있습니다:

- ⚠️ **Agent Pipeline**: 순차/병렬 실행 워크플로우
- ⚠️ **Collaboration Patterns**: Voting, BestOfN, Ensemble 전략
- ⚠️ **Conversation Manager**: 대화 히스토리 관리

→ 프로덕션에서는 Microsoft Agent Framework, Semantic Kernel의 네이티브 기능 사용을 권장합니다.

## 📖 예제

- [OpenAISample](samples/OpenAISample/) - 기본 사용법
- [WebApiSample](samples/WebApiSample/) - RESTful API 서버
- [PipelineSample](samples/PipelineSample/) - 파이프라인 (실험적)

## 🗺️ 로드맵

### v0.1.0 - 현재 (초기 릴리스) ✅
- [x] 파일시스템 컨벤션 기반 로더
- [x] Azure OpenAI 통합
- [x] Microsoft Agent Framework 통합
- [x] 키워드 기반 라우팅
- [x] 다중 프레임워크 어댑터

### v0.2.0 - 계획
- [ ] Semantic Kernel 어댑터
- [ ] 임베딩 기반 라우팅
- [ ] Pipeline 단순화 또는 제거
- [ ] 성능 최적화

### v0.3.0 - 계획
- [ ] LangChain 어댑터
- [ ] CLI 도구
- [ ] 벡터 DB 통합 (선택적)

## 🧪 테스트

```bash
dotnet test  # 67개 테스트 통과
```

## 🤝 기여

이슈와 PR을 환영합니다.

**핵심 철학 유지:**
- 얇은 래퍼로 유지
- 과도한 기능 추가 지양
- 파일시스템 컨벤션 중심

## 📄 라이선스

MIT License - [LICENSE](LICENSE) 참조

---

**Ironbees** - Filesystem convention-based LLM agent wrapper for .NET 🐝

**버전:** 0.1.0 (초기 베타) | **.NET:** 9.0+ | **상태:** 실험적
