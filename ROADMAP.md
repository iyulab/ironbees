# Ironbees Roadmap

> Thin wrapper 철학 기반 점진적 개선 로드맵

## 핵심 원칙

**Ironbees가 집중해야 할 영역:**
- ✅ 파일시스템 컨벤션 기반 에이전트 로딩
- ✅ 지능형 에이전트 라우팅 (키워드 → 임베딩 → 하이브리드)
- ✅ 다중 LLM 프레임워크 통합 (Adapter 패턴)
- ✅ 개발자 경험 개선 (CLI, 템플릿, 샘플)
- ✅ 보일러플레이트 제거

**Ironbees가 하지 않을 영역:**
- ❌ 복잡한 워크플로우 오케스트레이션 → 기본 프레임워크
- ❌ 대화 관리 및 메모리 → 기본 프레임워크
- ❌ 도구 통합 및 MCP → 기본 프레임워크
- ❌ RAG, 벡터 DB 구현 → 기본 프레임워크 또는 외부 라이브러리

---

## Phase 4: 핵심 안정화 (v0.1.1 - v0.1.3) 🔄 Current

**목표**: v0.1.0 핵심 기능 안정화 및 품질 개선

### 4.1 KeywordAgentSelector 개선 (v0.1.1) ✅ Completed
**우선순위**: 높음 | **완료 날짜**: 2025-11-10

- [x] **정확도 개선**
  - ✅ TF-IDF 가중치 적용 (TfidfWeightCalculator 클래스)
  - ✅ 불용어 사전 확장 (.NET 특화, 80+ 불용어)
  - ✅ 키워드 정규화 개선 (50+ 동의어, 100+ 어간 추출)

- [x] **성능 최적화**
  - ✅ 키워드 추출 캐싱 (스레드 안전, 1000 엔트리 제한)
  - ✅ TF-IDF 계산 최적화 (지연 초기화, IDF 캐싱)
  - ✅ 벤치마크 테스트 추가 (1000회 < 100ms 달성)

- [x] **선택 품질 지표**
  - ✅ 88% 정확도 달성 (50개 테스트 케이스)
  - ✅ 가중치 최적화 (Capabilities: 0.5, Tags: 0.35)
  - ✅ 상세 스코어링 이유 제공 (TF-IDF 부스트 포함)

**완료 조건**:
- ⚠️ 88% 정확도 (목표 90%, -2% 차이)
- ✅ 평균 선택 시간 < 1ms (목표 초과 달성)
- ✅ 단위 테스트 13개 추가 (목표 초과)

**결과물**:
- 3개 신규 클래스: TfidfWeightCalculator, StopwordsProvider, KeywordNormalizer
- 3개 신규 테스트 파일: Benchmark, Enhanced, Accuracy
- 총 80개 테스트 (71개 통과, 88.75% 통과율)
- 문서: claudedocs/KEYWORDSELECTOR_IMPROVEMENTS_v0.1.1.md

### 4.2 FileSystemAgentLoader 강화 (v0.1.2) ✅
**우선순위**: 중간 | **완료 날짜**: 2025-11-10

- [x] **에러 처리 개선**
  - YAML 파싱 오류 상세 메시지 (YamlParsingException with diagnostics)
  - 파일 누락 시 명확한 안내 (expected file paths)
  - 부분 로드 지원 (StopOnFirstError 옵션)

- [x] **캐싱 전략**
  - 파일 변경 감지 (FileSystemWatcher with 100ms debounce)
  - 메모리 캐싱 옵션 (ConcurrentDictionary, modification timestamp)
  - Hot reload 지원 (AgentReloaded event, EnableHotReload option)

- [x] **검증 강화**
  - agent.yaml 스키마 검증 (AgentConfigValidator with comprehensive rules)
  - system-prompt.md 필수 필드 체크 (length, content validation)
  - 중복 에이전트 이름 감지 (case-insensitive duplicate detection)

**완료 조건**:
- [x] 에러 메시지 개선 (파일명, YAML 라인 번호, 수정 가이드 포함)
- [x] Hot reload 동작 확인 (FileSystemWatcher with event notifications)
- [x] 검증 테스트 33개 추가 (20 validator + 13 loader tests)

**결과**:
- 3개 새 클래스: AgentConfigValidator, YamlParsingException, FileSystemAgentLoaderOptions
- 2개 새 테스트 파일: AgentConfigValidatorTests (20), FileSystemAgentLoaderEnhancedTests (13)
- 총 136개 테스트 (127개 통과, 93.4% 통과율)
- 문서: CHANGELOG.md 업데이트 완료

### 4.3 문서 및 예제 확장 (v0.1.3)
**우선순위**: 중간 | **예상 기간**: 1주

- [ ] **튜토리얼 작성**
  - 첫 에이전트 만들기 (5분)
  - 커스텀 프레임워크 어댑터 작성
  - 프로덕션 배포 가이드

- [ ] **API 문서 생성**
  - XML 주석 완성도 100%
  - DocFX 또는 Sandcastle 통합
  - GitHub Pages 배포

- [ ] **추가 샘플**
  - ConsoleChatSample (간단한 CLI 채팅)
  - BlazorWebAppSample (Blazor UI)
  - MinimalAPISample (ASP.NET Minimal API)

**완료 조건**:
- [ ] docs/ 디렉터리 구조 완성
- [ ] 3개 튜토리얼 문서
- [ ] 1개 이상 새 샘플

---

## Phase 5: Semantic Kernel 통합 (v0.2.0) 🚀 Next

**목표**: Semantic Kernel 프레임워크 지원 추가

### 5.1 Semantic Kernel Adapter (2주)
**우선순위**: 높음

- [ ] **어댑터 구현**
  - `ILLMFrameworkAdapter` 구현 (SemanticKernelAdapter)
  - Kernel, Plugin 통합
  - Function calling 지원

- [ ] **의존성 주입 확장**
  - `AddIronbees()` 확장 (Semantic Kernel 옵션)
  - 프레임워크 전환 플래그 (Azure/MAF/SK)

- [ ] **통합 테스트**
  - 단위 테스트 20개
  - 통합 테스트 5개
  - 샘플 프로젝트 (SemanticKernelSample)

**완료 조건**:
- [ ] 3개 프레임워크 동일 API로 사용 가능
- [ ] 테스트 커버리지 > 85%
- [ ] 문서 업데이트

### 5.2 프레임워크 비교 문서 (3일)
**우선순위**: 중간

- [ ] **프레임워크 선택 가이드**
  - Azure OpenAI vs MAF vs Semantic Kernel
  - 각 프레임워크 장단점
  - 사용 사례별 추천

- [ ] **마이그레이션 가이드**
  - 프레임워크 간 전환 방법
  - Breaking changes 및 주의사항

**완료 조건**:
- [ ] docs/FRAMEWORK_COMPARISON.md
- [ ] docs/MIGRATION_GUIDE.md

---

## Phase 6: 임베딩 기반 라우팅 (v0.2.1 - v0.2.2) 🎯

**목표**: 키워드 → 임베딩 → 하이브리드 라우팅 진화

### 6.1 임베딩 기반 Selector (2주)
**우선순위**: 높음

- [ ] **IAgentSelector 구현**
  - EmbeddingAgentSelector
  - Azure OpenAI Embeddings 통합
  - 코사인 유사도 계산

- [ ] **에이전트 임베딩 캐싱**
  - 초기화 시 에이전트 설명 임베딩
  - 디스크 캐시 (변경 감지)
  - 메모리 효율적 저장

- [ ] **벤치마크**
  - 키워드 vs 임베딩 정확도 비교
  - 성능 측정 (레이턴시, 비용)

**완료 조건**:
- [ ] 임베딩 라우팅 95% 정확도
- [ ] 평균 선택 시간 < 200ms (캐시 히트)
- [ ] 비용 분석 문서

### 6.2 하이브리드 Selector (1주)
**우선순위**: 중간

- [ ] **HybridAgentSelector 구현**
  - 키워드 + 임베딩 가중 조합
  - 동적 가중치 조정
  - Fallback 전략 (임베딩 실패 시 키워드)

- [ ] **설정 옵션**
  - 키워드 가중치 (기본: 0.3)
  - 임베딩 가중치 (기본: 0.7)
  - 신뢰도 임계값

**완료 조건**:
- [ ] 3가지 Selector 옵션 제공
- [ ] 사용자 선택 가능한 전략

---

## Phase 7: Guardrails & Audit (v0.2.3 - v0.2.4) 🛡️

**목표**: 입출력 감사 및 보안 가드레일 (프로덕션 필수)

### 7.1 Content Guardrails 인터페이스 (2주)
**우선순위**: 높음

- [ ] **IContentGuardrail 인터페이스**
  ```csharp
  public interface IContentGuardrail
  {
      Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken ct);
      Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken ct);
  }

  public class GuardrailResult
  {
      public bool IsAllowed { get; set; }
      public string Reason { get; set; }
      public GuardrailViolation[] Violations { get; set; }
      public Dictionary<string, object> Metadata { get; set; }
  }
  ```

- [ ] **GuardrailPipeline 통합**
  ```csharp
  public class AgentOrchestrator : IAgentOrchestrator
  {
      private readonly IContentGuardrail[] _inputGuardrails;
      private readonly IContentGuardrail[] _outputGuardrails;

      public async Task<string> ProcessAsync(string input, ...)
      {
          // 1. Input guardrails
          foreach (var guardrail in _inputGuardrails)
          {
              var result = await guardrail.ValidateInputAsync(input, ct);
              if (!result.IsAllowed)
                  throw new GuardrailViolationException(result);
          }

          // 2. Agent processing
          var response = await _frameworkAdapter.RunAsync(agent, input, ct);

          // 3. Output guardrails
          foreach (var guardrail in _outputGuardrails)
          {
              var result = await guardrail.ValidateOutputAsync(response, ct);
              if (!result.IsAllowed)
                  throw new GuardrailViolationException(result);
          }

          return response;
      }
  }
  ```

- [ ] **의존성 주입 확장**
  ```csharp
  services.AddIronbees(options => {
      options.AddInputGuardrail<ProfanityGuardrail>();
      options.AddInputGuardrail<PIIDetectionGuardrail>();
      options.AddOutputGuardrail<PolicyComplianceGuardrail>();
  });
  ```

**완료 조건**:
- [ ] IContentGuardrail 인터페이스 정의
- [ ] AgentOrchestrator 통합
- [ ] DI 확장 메서드
- [ ] 단위 테스트 15개

### 7.2 기본 Guardrail 구현체 (1주)
**우선순위**: 높음

- [ ] **RegexGuardrail (패턴 매칭)**
  ```csharp
  public class RegexGuardrail : IContentGuardrail
  {
      private readonly Regex[] _blockedPatterns;

      // 금지어 패턴 매칭
      // 이메일, 전화번호, 신용카드 등
  }
  ```

- [ ] **KeywordGuardrail (키워드 필터)**
  ```csharp
  public class KeywordGuardrail : IContentGuardrail
  {
      private readonly HashSet<string> _blockedKeywords;

      // 금지어 목록 체크
      // 욕설, 혐오 표현 등
  }
  ```

- [ ] **LengthGuardrail (길이 제한)**
  ```csharp
  public class LengthGuardrail : IContentGuardrail
  {
      private readonly int _maxLength;

      // DoS 방지, 비용 제어
  }
  ```

**완료 조건**:
- [ ] 3가지 기본 구현체
- [ ] 설정 가능한 옵션
- [ ] 테스트 케이스 20개

### 7.3 외부 서비스 어댑터 (2주)
**우선순위**: 중간

- [ ] **Azure AI Content Safety Adapter**
  ```csharp
  public class AzureContentSafetyGuardrail : IContentGuardrail
  {
      private readonly ContentSafetyClient _client;

      // Azure AI Content Safety API 연동
      // Hate, Violence, Sexual, Self-harm 카테고리
  }
  ```

- [ ] **OpenAI Moderation Adapter**
  ```csharp
  public class OpenAIModerationGuardrail : IContentGuardrail
  {
      private readonly OpenAIClient _client;

      // OpenAI Moderation API 연동
  }
  ```

- [ ] **Custom Service Adapter Template**
  ```csharp
  // 사용자 정의 감사 서비스 연동 예제
  public class CustomAuditServiceGuardrail : IContentGuardrail
  {
      // 기업 내부 감사 API 연동 템플릿
  }
  ```

**완료 조건**:
- [ ] Azure Content Safety 통합
- [ ] OpenAI Moderation 통합
- [ ] 커스텀 어댑터 예제
- [ ] 샘플 프로젝트 (GuardrailsSample)

### 7.4 감사 로깅 및 모니터링 (1주)
**우선순위**: 중간

- [ ] **IAuditLogger 인터페이스**
  ```csharp
  public interface IAuditLogger
  {
      Task LogInputAsync(string input, GuardrailResult result);
      Task LogOutputAsync(string output, GuardrailResult result);
      Task LogViolationAsync(GuardrailViolation violation);
  }
  ```

- [ ] **구조화된 로깅**
  ```csharp
  public class StructuredAuditLogger : IAuditLogger
  {
      private readonly ILogger _logger;

      public Task LogViolationAsync(GuardrailViolation violation)
      {
          _logger.LogWarning(
              "Guardrail violation detected. Type: {Type}, Severity: {Severity}, Agent: {Agent}",
              violation.Type,
              violation.Severity,
              violation.AgentName
          );
      }
  }
  ```

- [ ] **메트릭 수집**
  - 위반 횟수 (by type, by agent)
  - 차단률 (blocked/total)
  - 평균 검증 시간

**완료 조건**:
- [ ] IAuditLogger 구현
- [ ] 구조화된 로깅
- [ ] 메트릭 수집 (선택적)
- [ ] 문서: docs/GUARDRAILS.md

**Phase 7 전체 완료 조건**:
- [ ] Guardrail 파이프라인 동작
- [ ] 3개 기본 구현체 + 2개 외부 어댑터
- [ ] 샘플 프로젝트 동작
- [ ] 문서 및 튜토리얼 완성
- [ ] 테스트 50개 이상

---

## Phase 8: 개발자 경험 (v0.3.0) 🛠️

**목표**: CLI 도구 및 템플릿으로 생산성 향상

### 8.1 CLI 도구 (3주)
**우선순위**: 중간

- [ ] **ironbees-cli 패키지**
  - `dotnet tool install -g ironbees-cli`
  - .NET Tool로 배포

- [ ] **명령어 구현**
  ```bash
  # 프로젝트 초기화
  ironbees init --framework AspNetCore

  # 에이전트 생성
  ironbees agent create coding-agent \
    --description "Expert C# developer" \
    --capabilities "code-generation,code-review"

  # 에이전트 테스트
  ironbees agent test coding-agent \
    --input "Write fibonacci function"

  # 에이전트 목록
  ironbees agent list

  # 벤치마크
  ironbees benchmark --selector keyword,embedding,hybrid
  ```

- [ ] **템플릿 생성**
  - `dotnet new ironbees-web` (ASP.NET Core API)
  - `dotnet new ironbees-console` (Console App)
  - `dotnet new ironbees-agent` (Agent 템플릿)

**완료 조건**:
- [ ] 5개 이상 CLI 명령어
- [ ] 3개 dotnet new 템플릿
- [ ] CLI 문서 및 튜토리얼

### 8.2 개발자 도구 (1주)
**우선순위**: 낮음

- [ ] **Visual Studio 확장** (선택적)
  - Agent YAML 스키마 IntelliSense
  - 파일 템플릿

- [ ] **VS Code 확장** (선택적)
  - Agent 생성 스니펫
  - YAML 검증

**완료 조건**:
- [ ] 1개 이상 IDE 확장

---

## Phase 9: LangChain 통합 (v0.3.1) 🔗

**목표**: LangChain.NET 프레임워크 지원

### 9.1 LangChain Adapter (2주)
**우선순위**: 중간

- [ ] **어댑터 구현**
  - `ILLMFrameworkAdapter` 구현
  - LangChain.NET 통합
  - Chain, Agent 지원

- [ ] **샘플 및 문서**
  - LangChainSample 프로젝트
  - 통합 테스트

**완료 조건**:
- [ ] 4개 프레임워크 지원
- [ ] 테스트 및 문서

---

## Phase 10: 선택적 기능 (v0.4.0+) 🌟

**우선순위**: 낮음 | **필요 시 추가**

### 10.1 벡터 DB 통합 (선택적)
- Qdrant, Milvus, Chroma 어댑터
- 에이전트 임베딩 저장소
- **주의**: Thin wrapper 철학 유지 (기본 프레임워크 기능 우선)

### 10.2 성능 최적화
- 에이전트 병렬 로딩
- 선택 알고리즘 최적화
- 메모리 프로파일링

### 10.3 모니터링 및 관찰성
- OpenTelemetry 통합
- 구조화된 로깅
- 메트릭 수집 (선택 정확도, 레이턴시)

---

## 릴리스 일정 (예상)

| 버전 | 목표 | 주요 기능 | 예상 일정 |
|------|------|-----------|-----------|
| v0.1.0 | ✅ 완료 | 초기 릴리스 (Thin wrapper) | 2025-01-30 |
| v0.1.1 | 안정화 | KeywordSelector 개선 | 2025-02-15 |
| v0.1.2 | 안정화 | FileSystemLoader 강화 | 2025-02-28 |
| v0.1.3 | 문서 | 튜토리얼 및 샘플 확장 | 2025-03-15 |
| v0.2.0 | 확장 | Semantic Kernel 통합 | 2025-04-15 |
| v0.2.1 | 라우팅 | 임베딩 기반 Selector | 2025-05-15 |
| v0.2.2 | 라우팅 | 하이브리드 Selector | 2025-06-01 |
| v0.2.3 | 보안 | Guardrails 인터페이스 | 2025-06-22 |
| v0.2.4 | 보안 | Guardrails 구현체 및 어댑터 | 2025-07-13 |
| v0.3.0 | DX | CLI 도구 및 템플릿 | 2025-08-15 |
| v0.3.1 | 확장 | LangChain 통합 | 2025-09-15 |
| v0.4.0 | 선택적 | 고급 기능 (필요 시) | TBD |

---

## 우선순위 매트릭스

### 높은 우선순위 (당장 시작)
1. ⭐ KeywordAgentSelector 개선 (v0.1.1) - Phase 4.1
2. ⭐ Semantic Kernel Adapter (v0.2.0) - Phase 5.1
3. ⭐ 임베딩 기반 Selector (v0.2.1) - Phase 6.1
4. ⭐ Guardrails 인터페이스 (v0.2.3) - Phase 7.1

### 중간 우선순위 (순차 진행)
5. FileSystemAgentLoader 강화 (v0.1.2) - Phase 4.2
6. 문서 및 예제 확장 (v0.1.3) - Phase 4.3
7. 하이브리드 Selector (v0.2.2) - Phase 6.2
8. Guardrails 구현체 (v0.2.4) - Phase 7.2-7.3
9. CLI 도구 (v0.3.0) - Phase 8.1

### 낮은 우선순위 (선택적)
10. LangChain Adapter (v0.3.1) - Phase 9.1
11. 벡터 DB 통합 (v0.4.0+) - Phase 10.1
12. IDE 확장 (v0.3.0+) - Phase 8.2

---

## Phase 4 상세 태스크 (즉시 시작 가능)

### Sprint 1: KeywordAgentSelector 개선 (1주)

**Day 1-2: TF-IDF 가중치**
- [ ] TF-IDF 계산 로직 구현
- [ ] 가중치 적용 테스트
- [ ] 성능 벤치마크

**Day 3-4: 정확도 개선**
- [ ] 불용어 사전 확장 (.NET, C#, programming 용어)
- [ ] 동의어 매핑 (code = programming = development)
- [ ] 어간 추출 (coding = code, developer = develop)

**Day 5: 성능 최적화**
- [ ] 키워드 추출 캐싱
- [ ] 에이전트 메타데이터 인덱싱
- [ ] 벤치마크 테스트 (1000회 < 100ms)

**Day 6-7: 테스트 및 문서**
- [ ] 단위 테스트 10개 추가
- [ ] 정확도 테스트 케이스 50개
- [ ] 문서 업데이트

---

## 커뮤니티 기여 영역

**Good First Issues:**
- 불용어 사전 확장
- 추가 샘플 프로젝트
- 문서 번역 (영어 ↔ 한국어)

**Advanced Issues:**
- 새로운 프레임워크 어댑터 (Ollama, LocalAI)
- 임베딩 기반 라우팅 구현
- CLI 도구 기능 추가

---

## 제외 사항 (Thin Wrapper 철학 위배)

다음은 **절대 추가하지 않을** 기능:

- ❌ Pipeline 엔진 재구현
- ❌ Conversation Manager 재구현
- ❌ 도구 호출 프레임워크 (MCP 대체)
- ❌ RAG 엔진 구현 (벡터 검색, 청킹)
- ❌ 프롬프트 엔지니어링 라이브러리
- ❌ LLM 응답 캐싱 시스템
- ❌ AI 기반 감사 엔진 자체 구현 (Azure AI Content Safety, OpenAI Moderation 사용)
- ❌ PII 감지 엔진 구현 (Microsoft Presidio, Azure Text Analytics 사용)
- ❌ 프롬프트 인젝션 탐지 AI 모델 (외부 서비스 사용)

→ 이러한 기능은 Microsoft Agent Framework, Semantic Kernel, LangChain 등의 기본 프레임워크를 사용하세요.

**Guardrails 예외 (Ironbees가 제공하는 것)**:
- ✅ `IContentGuardrail` 인터페이스 및 파이프라인 통합
- ✅ 간단한 패턴 매칭 기반 구현체 (Regex, Keyword, Length)
- ✅ 외부 감사 서비스 어댑터 (Azure Content Safety, OpenAI Moderation)
- ✅ 감사 로깅 및 모니터링 인터페이스

→ 복잡한 AI 기반 감사는 외부 서비스에 위임

---

**Ironbees Roadmap** - Focused, lightweight, and developer-friendly 🐝

**Last Updated**: 2025-01-30
**Current Version**: v0.1.0
**Next Release**: v0.1.1 (KeywordSelector 개선)
