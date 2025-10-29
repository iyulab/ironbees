# 🐝 Ironbees

> 컨벤션 기반 멀티 에이전트 오케스트레이션 프레임워크

Ironbees는 .NET 환경에서 LLM 에이전트를 관리하고 조율하는 경량 프레임워크입니다. YAML 기반 설정으로 에이전트를 정의하고, 파이프라인을 통해 복잡한 워크플로우를 구성할 수 있습니다.

## 핵심 기능

- **컨벤션 기반**: 파일 구조만으로 에이전트 자동 로딩
- **지능형 선택**: 입력 분석을 통한 자동 에이전트 선택
- **파이프라인**: 순차/병렬 실행 및 조건부 워크플로우
- **협업 패턴**: 다중 에이전트 결과 집계 (Voting, BestOfN, Ensemble, FirstSuccess)
- **대화 관리**: 세션 기반 컨텍스트 및 히스토리 관리
- **내장 에이전트**: RAG, Function Calling, Router, Memory, Summarization
- **확장성**: 플러그인 가능한 Selector, Adapter, Strategy

## 빠른 시작

### 설치

```bash
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework  # Azure OpenAI용
```

### 1. 에이전트 정의

`agents/coding-agent/agent.yaml`:
```yaml
name: coding-agent
description: Expert software developer
capabilities: [code-generation, code-review]
tags: [programming, development]
model:
  deployment: gpt-4
  temperature: 0.7
```

`agents/coding-agent/system-prompt.md`:
```markdown
You are an expert software developer...
```

### 2. 서비스 구성

```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";
});

var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();
```

### 3. 에이전트 사용

```csharp
// 에이전트 로드
await orchestrator.LoadAgentsAsync();

// 단일 실행
var response = await orchestrator.ProcessAsync(
    "Write a C# method to calculate fibonacci numbers",
    agentName: "coding-agent");

// 파이프라인 실행
var pipeline = orchestrator.CreatePipeline("analysis-pipeline")
    .AddAgent("router-agent")
    .AddAgent("analysis-agent")
    .AddAgent("summarization-agent")
    .Build();

var result = await pipeline.ExecuteAsync("Analyze user engagement metrics");
```

### 4. 병렬 협업

```csharp
var pipeline = orchestrator.CreatePipeline("parallel-review")
    .AddParallelAgents(
        new[] { "coding-agent", "review-agent", "analysis-agent" },
        parallel => parallel
            .WithBestOfN(result => result.Output.Length)
            .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))
    .Build();
```

## 프로젝트 구조

```
ironbees/
├── src/
│   ├── Ironbees.Core/           # 핵심 추상화 및 파이프라인
│   └── Ironbees.AgentFramework/ # Azure OpenAI 통합
├── agents/                       # 에이전트 정의 (9개)
├── docs/                         # 상세 문서
├── samples/                      # 실행 가능한 예제
└── tests/                        # 단위 테스트
```

## 문서

- [시작 가이드](docs/GETTING_STARTED.md) - 상세한 설치 및 구성
- [에이전트 파이프라인](docs/AGENT_PIPELINE.md) - 파이프라인 패턴
- [협업 패턴](docs/COLLABORATION_PATTERNS.md) - 다중 에이전트 협업
- [내장 에이전트](agents/BUILTIN_AGENTS.md) - 5가지 내장 에이전트
- [아키텍처](docs/ARCHITECTURE.md) - 설계 및 확장성

## 예제

- [OpenAISample](samples/OpenAISample/) - OpenAI API 사용
- [WebApiSample](samples/WebApiSample/) - RESTful API 서버
- [PipelineSample](samples/PipelineSample/) - 파이프라인 시나리오

## 로드맵

### 완료 ✅
- [x] 핵심 추상화 및 파일시스템 로더
- [x] Azure OpenAI 통합
- [x] 지능형 에이전트 선택
- [x] 대화 히스토리 관리
- [x] 내장 에이전트 (RAG, Function Calling, Router, Memory, Summarization)
- [x] Agent Pipeline (순차 실행, 조건부 실행, 에러 처리)
- [x] 협업 패턴 (병렬 실행, 4가지 집계 전략)

### 계획 중 📋
- [ ] NuGet 패키지 배포
- [ ] 성능 최적화 및 벤치마크
- [ ] Embedding 기반 Selector
- [ ] 벡터 데이터베이스 통합
- [ ] CLI 도구

## 설계 원칙

**Convention over Configuration**: 파일 구조와 명명 규칙을 따르면 최소 코드로 동작
**Thin Abstraction**: LLM 프레임워크의 기능을 숨기지 않고 오케스트레이션에만 집중
**Extensibility First**: 모든 핵심 컴포넌트 교체 가능
**Type Safety**: C# 타입 시스템을 활용한 컴파일 타임 안전성

## 테스트

```bash
dotnet test  # 67개 테스트 통과
```

## 라이선스

MIT License - [LICENSE](LICENSE) 참조

## 기여

이슈와 PR을 환영합니다. [CONTRIBUTING.md](CONTRIBUTING.md) 참조.

---

**Ironbees** - Convention-based multi-agent orchestration for .NET 🐝
