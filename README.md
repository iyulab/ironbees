# 🐝 Ironbees

[![CI](https://github.com/iyulab/ironbees/actions/workflows/ci.yml/badge.svg)](https://github.com/iyulab/ironbees/actions/workflows/ci.yml)
[![NuGet - Core](https://img.shields.io/nuget/v/Ironbees.Core?label=Ironbees.Core)](https://www.nuget.org/packages/Ironbees.Core)
[![NuGet - AgentFramework](https://img.shields.io/nuget/v/Ironbees.AgentFramework?label=Ironbees.AgentFramework)](https://www.nuget.org/packages/Ironbees.AgentFramework)
[![License](https://img.shields.io/github/license/iyulab/ironbees)](LICENSE)

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

// 방법 1: 명시적 에이전트 선택
var response = await orchestrator.ProcessAsync(
    "Write a C# method to calculate fibonacci numbers",
    agentName: "coding-agent");

// 방법 2: 자동 라우팅 (키워드 기반)
var response = await orchestrator.ProcessAsync(
    "fibonacci numbers in C#"); // "coding" 키워드로 자동 라우팅

// 방법 3: 스트리밍 응답 (명시적 에이전트) 🆕
await foreach (var chunk in orchestrator.StreamAsync(
    "Write a blog post about AI",
    agentName: "writing-agent"))
{
    Console.Write(chunk); // 실시간 스트리밍
}

// 방법 4: 스트리밍 + 자동 라우팅 (v0.1.6+) 🆕
await foreach (var chunk in orchestrator.StreamAsync(
    "fibonacci in Python")) // 자동으로 coding-agent 선택
{
    Console.Write(chunk); // 실시간 스트리밍
}
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
| OpenAI API | ✅ 지원 | Ironbees.Samples.Shared |
| GPU-Stack (OpenAI Compatible) | ✅ 지원 | Ironbees.Samples.Shared |
| Anthropic Claude | 🔄 계획됨 | - |
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

## 📖 예제

- [OpenAISample](samples/OpenAISample/) - 기본 사용법 (OpenAI API)
- [GpuStackSample](samples/GpuStackSample/) - 로컬 GPU 인프라 (GPU-Stack) 🆕
- [WebApiSample](samples/WebApiSample/) - RESTful API 서버
- [EmbeddingSample](samples/EmbeddingSample/) - 로컬 ONNX 임베딩 및 시맨틱 라우팅

## ✨ 최신 기능

### v0.1.6 - StreamAsync 자동 라우팅 🆕
실시간 스트리밍과 자동 에이전트 선택을 결합! API 일관성 개선.

**주요 기능:**
- **스트리밍 + 자동 선택**: `ProcessAsync`와 동일한 패턴으로 `StreamAsync` 자동 라우팅 지원
- **API 일관성**: 모든 주요 메서드에서 명시적/자동 선택 오버로드 제공
- **간소화된 코드**: 2단계 호출(선택 → 스트리밍)을 1단계로 통합

```csharp
// 이전: 수동 선택 필요
var selection = await orchestrator.SelectAgentAsync(input);
await foreach (var chunk in orchestrator.StreamAsync(input, selection.SelectedAgent.Name))
{
    Console.Write(chunk);
}

// 이제: 자동 선택 통합 (v0.1.6+)
await foreach (var chunk in orchestrator.StreamAsync(input))
{
    Console.Write(chunk); // 자동으로 최적 에이전트 선택 후 스트리밍
}
```

**기술 상세:**
- 내부적으로 `SelectAgentAsync` 재사용으로 일관된 선택 로직
- 에이전트를 찾지 못한 경우 명확한 에러 메시지 스트리밍
- `[EnumeratorCancellation]` 속성으로 적절한 취소 처리

### v0.1.5 - Local ONNX Embeddings
로컬 ONNX 모델로 완전 무료 임베딩 지원! API 키 불필요, 완전히 오프라인 동작.

**주요 기능:**
- **자동 모델 다운로드**: 첫 실행 시 Hugging Face에서 자동 다운로드 (~23-45MB)
- **2가지 모델 지원**:
  - `all-MiniLM-L6-v2`: 빠른 속도 (기본값, ~14K sent/sec, 84-85% 정확도)
  - `all-MiniLM-L12-v2`: 높은 정확도 (~4K sent/sec, 87-88% 정확도)
- **크로스 플랫폼**: Windows, Linux, macOS 지원
- **시맨틱 에이전트 선택**: EmbeddingAgentSelector로 의미 기반 라우팅
- **하이브리드 선택**: 키워드(40%) + 임베딩(60%) 결합

```csharp
// 로컬 ONNX 임베딩 프로바이더 생성 (첫 실행 시 자동 다운로드)
var provider = await OnnxEmbeddingProvider.CreateAsync(
    OnnxEmbeddingProvider.ModelType.MiniLML6V2);

// 텍스트를 384차원 벡터로 변환
var embedding = await provider.GenerateEmbeddingAsync("Write Python code");

// 임베딩 기반 에이전트 선택
var selector = new EmbeddingAgentSelector(provider);
var result = await selector.SelectAgentAsync("secure my web app", agents);
// → Security Specialist 선택 (키워드 없이도 시맨틱 매칭)

// 하이브리드 선택 (키워드 + 임베딩)
var hybridSelector = new HybridAgentSelector(
    new KeywordAgentSelector(),
    new EmbeddingAgentSelector(provider));
var result = await hybridSelector.SelectAgentAsync("python security", agents);
// → 키워드와 의미를 모두 고려한 최적 선택
```

**모델 비교:**
| 모델 | 크기 | 속도 | 정확도 | 용도 |
|------|------|------|--------|------|
| L6-v2 (기본값) | ~23MB | ~14K sent/sec | 84-85% | 실시간 앱, 리소스 제한 환경 |
| L12-v2 | ~45MB | ~4K sent/sec | 87-88% | 법률 문서, 학술 논문, 고품질 요구 |

샘플 코드: [EmbeddingSample](samples/EmbeddingSample/)

### v0.1.1 - 향상된 KeywordAgentSelector
- **TF-IDF 가중치**: 용어 관련성 기반 스코어링으로 정확도 향상
- **스마트 정규화**: 50+ 동의어 그룹, 100+ 어간 추출 규칙 (code↔programming, db↔database)
- **성능 캐싱**: 반복 쿼리 ~50% 속도 향상
- **확장된 불용어**: 80+ 불용어, .NET 기술 용어 보존
- **정확도**: 88% (50개 테스트 케이스)
- **속도**: < 1ms 단일 선택, 1000회 < 100ms

```csharp
// 동일한 API, 향상된 성능과 정확도
var result = await orchestrator.ProcessAsync("Write C# code", "coding-agent");
// 이제 "code", "coding", "programming" 모두 매칭
// TF-IDF로 더 관련성 높은 에이전트 선택
```

## 🗺️ 로드맵

### v0.1.6 - 현재 ✅
- [x] StreamAsync 자동 라우팅
- [x] API 일관성 개선
- [x] GpuStackAdapter 완성

### v0.1.5 - ONNX Embeddings ✅
- [x] 로컬 ONNX 임베딩 프로바이더 (all-MiniLM-L6-v2, L12-v2)
- [x] 자동 모델 다운로드 및 캐싱
- [x] EmbeddingAgentSelector (시맨틱 에이전트 선택)
- [x] HybridAgentSelector (키워드 + 임베딩)
- [x] 완전 무료, API 키 불필요

### v0.1.4 - 임베딩 기반 라우팅 ✅
- [x] IEmbeddingProvider 인터페이스
- [x] VectorSimilarity 유틸리티
- [x] 코사인 유사도 계산
- [x] 임베딩 캐싱 최적화

### v0.1.1 - TF-IDF 키워드 선택 ✅
- [x] TF-IDF 가중치 알고리즘
- [x] 키워드 정규화 (동의어, 어간 추출)
- [x] 성능 캐싱
- [x] 확장된 불용어 사전
- [x] 88% 선택 정확도

### v0.1.0 - 초기 릴리스 ✅
- [x] 파일시스템 컨벤션 기반 로더
- [x] Azure OpenAI 통합
- [x] Microsoft Agent Framework 통합
- [x] 키워드 기반 라우팅
- [x] 다중 프레임워크 어댑터

### v0.2.0 - 계획
- [ ] Semantic Kernel 어댑터
- [ ] OpenAI/Azure OpenAI 임베딩 프로바이더
- [ ] 성능 최적화
- [ ] 추가 예제 및 문서

### v0.3.0 - 계획
- [ ] LangChain 어댑터
- [ ] CLI 도구
- [ ] 벡터 DB 통합 (선택적)

## 🧪 테스트

### 테스트 카테고리

Ironbees는 테스트를 카테고리로 구분하여 효율적인 테스트 실행을 지원합니다:

| 카테고리 | 설명 | CI 실행 | 로컬 실행 |
|---------|------|--------|----------|
| **Unit** | 빠른 단위 테스트 (mock 사용) | ✅ 항상 | ✅ 권장 |
| **Performance** | 메모리/성능 테스트 (GC, 동시성) | ❌ 제외 | ✅ 권장 |
| **Integration** | 외부 서비스 테스트 (API 키 필요) | ⏸️ 선택적 | ⚠️ 환경 필요 |

### 빠른 실행

```bash
# 모든 테스트 (로컬 권장)
dotnet test

# CI 테스트만 (Performance 제외)
dotnet test --filter "Category!=Performance"

# Unit 테스트만
dotnet test --filter "Category!=Performance&Category!=Integration"
```

### 테스트 스크립트 사용

**Windows (PowerShell)**:
```powershell
# 전체 테스트 (Performance 포함)
.\run-tests.ps1 -Category all

# CI 테스트 (Performance 제외)
.\run-tests.ps1 -Category ci

# Unit 테스트만
.\run-tests.ps1 -Category unit

# Performance 테스트만
.\run-tests.ps1 -Category performance

# 커버리지 포함
.\run-tests.ps1 -Category all -Coverage
```

**Linux/macOS (Bash)**:
```bash
# 스크립트 실행 권한 부여
chmod +x run-tests.sh

# 전체 테스트
./run-tests.sh --category all

# CI 테스트
./run-tests.sh --category ci

# Unit 테스트만
./run-tests.sh --category unit

# 커버리지 포함
./run-tests.sh --category all --coverage
```

### 테스트 통계 (v0.1.6)

```
Total: 169 tests
├─ Unit: 166 tests ✅
├─ Performance: 3 tests ✅ (로컬 전용)
└─ Integration: 3 tests ⏸️ (환경 필요)

CI Status: 166/166 passed (100%)
Local Status: 169/169 passed (100%)
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

**버전:** 0.1.6 | **.NET:** 9.0+ | **상태:** 실험적
