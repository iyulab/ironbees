# Ironbees Release Notes

## Version 0.1.0 (2025-01-30) - Initial Beta Release

### 🎉 첫 공개 릴리스

Ironbees의 첫 공개 베타 버전입니다. 이 프로젝트는 **LLM 프레임워크를 대체하는 것이 아니라**, 파일시스템 컨벤션을 통해 반복되는 에이전트 관리 패턴을 간소화하는 **얇은 래퍼**입니다.

### 🎯 핵심 철학

- **Thin Wrapper**: Microsoft Agent Framework, Semantic Kernel, LangChain 위에서 작동
- **Convention over Configuration**: 파일시스템 구조로 에이전트 자동 로딩
- **Boilerplate Reduction**: 반복되는 설정 코드 제거에 집중
- **Framework Delegation**: 복잡한 오케스트레이션은 기본 프레임워크에 위임

### ✨ 주요 기능

#### 1. 파일시스템 컨벤션 기반 에이전트 로딩
```
agents/{agent-name}/
  ├── agent.yaml         # 에이전트 메타데이터
  └── system-prompt.md   # 시스템 프롬프트
```

단순히 파일 구조를 맞추면 자동으로 로딩됩니다:
```csharp
await orchestrator.LoadAgentsAsync(); // agents/ 디렉터리 자동 스캔
```

#### 2. 다중 LLM 프레임워크 지원

`ILLMFrameworkAdapter` 인터페이스로 다양한 프레임워크 통합:

**Azure.AI.OpenAI ChatClient (기본)**:
```csharp
services.AddIronbees(options => {
    options.AzureOpenAIEndpoint = "...";
    options.AzureOpenAIKey = "...";
});
```

**Microsoft Agent Framework**:
```csharp
services.AddIronbees(options => {
    options.AzureOpenAIEndpoint = "...";
    options.AzureOpenAIKey = "...";
    options.UseMicrosoftAgentFramework = true; // 플래그 하나로 전환
});
```

#### 3. 간단한 에이전트 라우팅

**명시적 선택**:
```csharp
var response = await orchestrator.ProcessAsync(input, agentName: "coding-agent");
```

**자동 라우팅** (키워드 기반):
```csharp
var response = await orchestrator.ProcessAsync("Write C# code"); // "coding" 키워드 자동 매칭
```

#### 4. ASP.NET Core 통합

```csharp
services.AddIronbees(options => { /* 설정 */ });
var orchestrator = app.Services.GetRequiredService<IAgentOrchestrator>();
```

### 📦 패키지 구조

- **Ironbees.Core** (0.1.0)
  - 파일시스템 로더 (`FileSystemAgentLoader`)
  - 키워드 라우팅 (`KeywordAgentSelector`)
  - 프레임워크 어댑터 인터페이스 (`ILLMFrameworkAdapter`)
  - 오케스트레이터 (`AgentOrchestrator`)

- **Ironbees.AgentFramework** (0.1.0-preview)
  - Azure.AI.OpenAI ChatClient 어댑터
  - Microsoft Agent Framework 어댑터
  - 의존성 주입 확장 (`AddIronbees`)

### ⚠️ 실험적 기능 (향후 변경 가능)

다음 기능은 현재 포함되어 있지만 **실험적**이며, 향후 버전에서 제거되거나 크게 변경될 수 있습니다:

- **Agent Pipeline**: 순차/병렬 실행 워크플로우
- **Collaboration Patterns**: Voting, BestOfN, Ensemble, FirstSuccess 전략
- **Conversation Manager**: 대화 히스토리 및 세션 관리

→ **권장사항**: 프로덕션 환경에서는 이러한 기능 대신 Microsoft Agent Framework, Semantic Kernel의 네이티브 워크플로우 기능을 사용하세요.

### 📚 내장 에이전트 예제

9개의 예제 에이전트 포함:
- `coding-agent` - 소프트웨어 개발
- `writing-agent` - 기술 문서 작성
- `analysis-agent` - 데이터 분석
- `review-agent` - 코드 리뷰
- `rag-agent` - RAG 패턴
- `function-calling-agent` - 함수 호출
- `router-agent` - 요청 라우팅
- `memory-agent` - 컨텍스트 유지
- `summarization-agent` - 요약

### 🧪 테스트 커버리지

- **67개 단위 테스트** 통과
  - Ironbees.Core: 36개 테스트
  - Ironbees.AgentFramework: 31개 테스트
- **빌드**: 0 warnings, 0 errors

### 📋 Dependencies

**Ironbees.Core**:
- YamlDotNet 16.3.0

**Ironbees.AgentFramework**:
- Azure.AI.OpenAI 2.5.0-beta.1
- Azure.Identity 1.17.0
- Microsoft.Agents.AI.OpenAI 1.0.0-preview.251028.1 (선택적)
- Microsoft.Extensions.DependencyInjection.Abstractions 9.0.10
- Microsoft.Extensions.Logging.Abstractions 9.0.10

### 🔄 향후 계획

#### v0.2.0 (계획)
- Semantic Kernel 어댑터 추가
- 임베딩 기반 라우팅
- Pipeline 단순화 또는 제거 검토
- 성능 최적화

#### v0.3.0 (계획)
- LangChain 어댑터 추가
- CLI 도구
- 벡터 데이터베이스 통합 (선택적)

### ⚠️ 알려진 제한사항

1. **초기 베타 버전**: API가 안정화되지 않았으며 Breaking Changes 가능
2. **실험적 기능**: Pipeline, Collaboration 패턴은 향후 제거될 수 있음
3. **프레임워크 의존성**: Azure OpenAI 또는 Microsoft Agent Framework 필요
4. **테스트 범위**: 실제 프로덕션 시나리오 검증 필요

### 📖 문서

- [README.md](README.md) - 시작 가이드
- [docs/GETTING_STARTED.md](docs/GETTING_STARTED.md) - 상세 설치
- [docs/MICROSOFT_AGENT_FRAMEWORK.md](docs/MICROSOFT_AGENT_FRAMEWORK.md) - MS Agent Framework 통합
- [agents/BUILTIN_AGENTS.md](agents/BUILTIN_AGENTS.md) - 내장 에이전트

### 🤝 기여

Ironbees는 오픈소스 프로젝트입니다. 이슈와 PR을 환영합니다!

**핵심 철학 유지 부탁드립니다:**
- 얇은 래퍼로 유지 (과도한 기능 추가 지양)
- 파일시스템 컨벤션 중심
- 프레임워크 기능은 위임

### 📄 라이선스

MIT License

---

## Support

- **Documentation**: [README.md](README.md)
- **Issues**: [GitHub Issues](https://github.com/iyulab/ironbees/issues)
- **License**: MIT License

---

**Ironbees** v0.1.0 - Filesystem convention-based LLM agent wrapper for .NET 🐝

**Status**: Experimental Beta | **.NET**: 9.0+ | **Released**: 2025-01-30
