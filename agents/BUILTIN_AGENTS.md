# Built-in Agents

일반적인 LLM 응용 프로그램에서 사용되는 내장 에이전트 모음입니다.

## 📚 사용 가능한 에이전트

### 1. RAG Agent (Retrieval-Augmented Generation)
**파일**: `rag-agent.yaml`
**용도**: 문서 검색 기반 질의응답

**주요 기능**:
- 컨텍스트 기반 정보 검색
- 문서 출처 인용
- 여러 문서 정보 종합
- 지식 베이스 활용

**사용 사례**:
- FAQ 시스템
- 기술 문서 질의응답
- 지식 베이스 검색
- 컨텍스트 기반 추론

```bash
# 사용 예시
curl -X POST http://localhost:5001/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What is authentication in this framework? [context: framework docs]",
    "agentName": "rag-agent"
  }'
```

### 2. Function Calling Agent (Tool Integration)
**파일**: `function-calling-agent.yaml`
**용도**: 외부 도구 및 API 통합

**주요 기능**:
- 외부 API 호출
- 다중 도구 오케스트레이션
- 파라미터 추출 및 검증
- 결과 종합 및 처리

**사용 사례**:
- 날씨 조회, 뉴스 검색 등 외부 데이터
- 계산 및 변환 작업
- 다단계 워크플로우
- 서비스 간 통합

```bash
# 사용 예시
curl -X POST http://localhost:5001/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Get weather in Seoul and convert to Fahrenheit",
    "agentName": "function-calling-agent"
  }'
```

### 3. Router Agent (Intent Classification)
**파일**: `router-agent.yaml`
**용도**: 의도 분류 및 요청 라우팅

**주요 기능**:
- 사용자 의도 분석
- 적절한 에이전트/서비스 선택
- 신뢰도 점수 제공
- 다중 의도 감지

**사용 사례**:
- 멀티 에이전트 시스템 게이트웨이
- 요청 분류 및 우선순위 지정
- 워크플로우 오케스트레이션
- 지능형 티켓 라우팅

```bash
# 사용 예시
curl -X POST http://localhost:5001/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Analyze this request and route it: Write Python code to scrape websites",
    "agentName": "router-agent"
  }'
```

### 4. Memory Agent (Context Persistence)
**파일**: `memory-agent.yaml`
**용도**: 대화 컨텍스트 및 사용자 선호도 관리

**주요 기능**:
- 대화 히스토리 추적
- 사용자 선호도 저장/조회
- 세션 상태 관리
- 장기 메모리 유지

**사용 사례**:
- 개인화된 대화
- 세션 연속성
- 사용자 프로파일링
- 컨텍스트 유지

```bash
# 사용 예시
curl -X POST http://localhost:5001/api/agents/conversation/chat \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "user-123",
    "message": "What did we discuss last time?",
    "agentName": "memory-agent"
  }'
```

### 5. Summarization Agent
**파일**: `summarization-agent.yaml`
**용도**: 텍스트 요약 및 핵심 정보 추출

**주요 기능**:
- 문서 요약 (다양한 길이)
- 핵심 포인트 추출
- 회의록 생성
- 다중 형식 요약

**사용 사례**:
- 긴 문서 요약
- 회의 노트 생성
- 뉴스 다이제스트
- 컨텐츠 압축

```bash
# 사용 예시
curl -X POST http://localhost:5001/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Summarize: [long article text here...]",
    "agentName": "summarization-agent"
  }'
```

## 🚀 빠른 시작

### 1. 에이전트 로드 확인

```bash
# Web API 실행
cd samples/WebApiSample/Ironbees.WebApi
dotnet run --urls "http://localhost:5001"

# 에이전트 목록 확인
curl http://localhost:5001/api/agents
```

### 2. 특정 에이전트 테스트

```csharp
// C# 예제
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();
await orchestrator.LoadAgentsAsync("agents/builtin");

var response = await orchestrator.ProcessAsync(
    "Summarize the key points of this meeting",
    "summarization-agent"
);
```

### 3. 자동 선택 테스트

```bash
# 자동으로 적절한 에이전트 선택
curl -X POST http://localhost:5001/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Search for information about quantum computing and summarize it"
  }'
```

## 📊 에이전트 선택 가이드

| 작업 유형 | 추천 에이전트 | 이유 |
|-----------|---------------|------|
| 문서 기반 질문 | RAG Agent | 출처 기반 정확한 답변 |
| API/도구 사용 | Function Calling | 외부 서비스 통합 |
| 요청 분류 | Router Agent | 의도 파악 및 라우팅 |
| 대화 연속성 | Memory Agent | 컨텍스트 유지 |
| 긴 텍스트 압축 | Summarization | 핵심 정보 추출 |

## 🔧 커스터마이징

### 에이전트 설정 변경

```yaml
# 예: rag-agent.yaml 수정
model:
  deployment: gpt-4o         # 모델 변경
  temperature: 0.3           # 창의성 조절 (0.0-1.0)
  max_tokens: 3000          # 최대 토큰 수

capabilities:
  - retrieval-augmented-generation
  - custom-capability         # 새로운 능력 추가
```

### 시스템 프롬프트 커스터마이징

```yaml
system_prompt: |
  Your custom instructions here...

  Additional guidelines:
  - Custom rule 1
  - Custom rule 2
```

## 🎯 고급 사용법

### 1. 멀티 에이전트 워크플로우

```bash
# 1단계: Router로 의도 분류
POST /api/agents/chat
{
  "message": "Analyze news and create summary",
  "agentName": "router-agent"
}

# 2단계: Function Calling으로 뉴스 수집
POST /api/agents/chat
{
  "message": "Fetch latest AI news",
  "agentName": "function-calling-agent"
}

# 3단계: Summarization으로 요약
POST /api/agents/chat
{
  "message": "Summarize: [news content]",
  "agentName": "summarization-agent"
}
```

### 2. 대화 컨텍스트 활용

```bash
# 세션 시작
POST /api/agents/conversation/chat
{
  "message": "I prefer Python",
  "agentName": "memory-agent"
}

# 컨텍스트 활용
POST /api/agents/conversation/chat
{
  "sessionId": "[from previous response]",
  "message": "Recommend a web framework for me"
}
# → Memory Agent가 Python 선호도를 기억하여 추천
```

### 3. RAG + Summarization 조합

```bash
# RAG로 관련 문서 검색
POST /api/agents/chat
{
  "message": "Find documentation about authentication [context: docs]",
  "agentName": "rag-agent"
}

# Summarization으로 핵심 요약
POST /api/agents/chat
{
  "message": "Summarize: [RAG output]",
  "agentName": "summarization-agent"
}
```

## 📈 성능 최적화

### Temperature 설정 가이드

| 에이전트 | 기본값 | 용도 |
|----------|--------|------|
| RAG | 0.3 | 정확성 우선 |
| Function Calling | 0.5 | 균형 |
| Router | 0.2 | 일관성 우선 |
| Memory | 0.4 | 안정성 |
| Summarization | 0.3 | 정확한 요약 |

### Max Tokens 가이드

- **짧은 응답** (1000-1500): Router, Memory
- **중간 응답** (2000-2500): Function Calling
- **긴 응답** (3000-4000): RAG, Summarization

## 🧪 테스트

### 단위 테스트

```bash
# 전체 테스트 실행
cd tests/Ironbees.Core.Tests
dotnet test

# 특정 에이전트 테스트
dotnet test --filter "Category=BuiltinAgents"
```

### 통합 테스트

```bash
# Web API 통합 테스트
cd samples/WebApiSample/Ironbees.WebApi.Tests
dotnet test
```

## 📝 모범 사례

### 1. 적절한 에이전트 선택
- 명확한 의도 → 특정 에이전트 직접 호출
- 불명확한 의도 → Router Agent 사용
- 복잡한 작업 → 여러 에이전트 조합

### 2. 컨텍스트 관리
- 대화형 앱 → Memory Agent + Conversation API
- 단발성 질의 → 개별 Chat API
- 장기 세션 → 주기적 세션 정리

### 3. 에러 처리
- 항상 confidence score 확인
- 낮은 신뢰도 → 사용자에게 재확인
- 적절한 fallback 에이전트 설정

## 🔗 관련 문서

- [Main README](../../README.md)
- [Web API Sample](../../samples/WebApiSample/README.md)
- [Usage Guide](../../docs/USAGE.md)
- [Architecture](../../docs/ARCHITECTURE.md)

## 💡 추가 에이전트 아이디어

프로젝트 요구사항에 따라 다음 에이전트들을 추가로 구현할 수 있습니다:

- **Translation Agent**: 다국어 번역
- **Code Execution Agent**: 코드 실행 및 검증
- **Search Agent**: 웹/문서 검색
- **Data Analysis Agent**: 데이터 분석 및 시각화
- **Validation Agent**: 입력 검증 및 데이터 품질
- **Monitoring Agent**: 시스템 모니터링 및 알림

---

**Ironbees Built-in Agents** - 실전 LLM 애플리케이션을 위한 필수 에이전트 🐝
