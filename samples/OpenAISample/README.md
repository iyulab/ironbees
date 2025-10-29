# OpenAI Sample

OpenAI API를 사용하는 Ironbees 샘플 프로젝트입니다. Azure OpenAI 대신 일반 OpenAI API를 사용합니다.

## 특징

- ✅ **OpenAI API 통합**: Azure가 아닌 일반 OpenAI API 사용
- ✅ **gpt-5-nano 모델**: 최신 비용 효율적인 모델 지원
- ✅ **.env 설정**: 환경 변수 기반 간편한 설정
- ✅ **모든 에이전트 테스트**: 4개 에이전트 모두 데모
- ✅ **스트리밍 지원**: 실시간 응답 스트리밍 데모

## 설정

### 1. .env 파일 생성

프로젝트 루트에 `.env` 파일을 생성하고 OpenAI API 키를 설정하세요:

```env
OPENAI_API_KEY=your-api-key-here
OPENAI_MODEL=gpt-5-nano
```

### 2. 의존성 설치

```bash
cd samples/OpenAISample
dotnet restore
```

### 3. 실행

```bash
dotnet run
```

## 테스트 시나리오

### Test 1: Coding Agent
C# 문자열 역전 함수 생성 데모

**입력**: "Write a simple C# function to reverse a string."

**결과**: 완전한 함수 구현 + 설명 + 사용 예제

### Test 2: 자동 에이전트 선택
다양한 프롬프트에 대한 자동 에이전트 선택 데모

**테스트 케이스**:
- "Write a blog post about AI" → writing-agent
- "Analyze this sales data..." → analysis-agent (20% 신뢰도)
- "Review the quality of this code..." → review-agent (59% 신뢰도)
- "Help me debug this Python code" → coding-agent (35% 신뢰도)

### Test 3: 스트리밍 응답
Writing Agent를 사용한 실시간 스트리밍 데모

**입력**: "Write a short paragraph about the benefits of multi-agent systems."

**결과**: 단락이 실시간으로 스트리밍됨

### Test 4: Analysis Agent
판매 데이터 분석 데모

**입력**: 지역별 판매 데이터 (North, South, East, West)

**결과**:
- Executive Summary
- 상세 분석
- 시각화 제안
- 실행 가능한 권장사항
- 주의사항

### Test 5: Review Agent
코드 품질 검토 데모

**입력**: 간단한 Calculate 함수

**결과**:
- 전체 평가
- 중요 이슈 (없음)
- 개선 사항 (명명 규칙, 문서화)
- 긍정적 측면
- 우선순위 권장사항

### Test 6: 에이전트 점수 비교
모든 에이전트의 점수 비교 데모

**입력**: "Help me with software testing"

**결과**: 각 에이전트의 점수와 매칭 이유 표시

## OpenAI Adapter 구현

`OpenAIAdapter.cs`는 OpenAI API를 위한 커스텀 어댑터입니다:

```csharp
public class OpenAIAdapter : ILLMFrameworkAdapter
{
    private readonly string _apiKey;
    private readonly string _defaultModel;

    public OpenAIAdapter(string apiKey, string defaultModel = "gpt-4")
    {
        _apiKey = apiKey;
        _defaultModel = defaultModel;
    }

    // ILLMFrameworkAdapter 구현...
}
```

**주요 기능**:
- OpenAI ChatClient 사용
- 동기 및 스트리밍 응답 지원
- 모델 파라미터 설정 (temperature, max_tokens)
- 에이전트 래핑 및 실행

## gpt-5-nano 모델

gpt-5-nano는 OpenAI의 최신 비용 효율적인 모델입니다:

**특징**:
- 💰 낮은 비용
- ⚡ 빠른 응답 속도
- 🎯 뛰어난 정확도
- 🔄 스트리밍 지원

**테스트 결과**: 모든 에이전트가 예상대로 작동하며 고품질 응답을 생성했습니다!

## 디렉토리 구조

```
samples/OpenAISample/
├── OpenAISample.csproj    # 프로젝트 파일
├── Program.cs             # 메인 프로그램
├── OpenAIAdapter.cs       # OpenAI API 어댑터
├── .env                   # 환경 변수 (git에서 제외)
└── README.md              # 이 파일
```

## 문제 해결

### "OPENAI_API_KEY not set" 오류
`.env` 파일이 프로젝트 루트에 있고 API 키가 설정되어 있는지 확인하세요.

### "No agents found" 오류
agents 디렉토리가 올바른 위치에 있는지 확인하세요:
```
ironbees/
├── agents/
│   ├── coding-agent/
│   ├── writing-agent/
│   ├── analysis-agent/
│   └── review-agent/
└── samples/OpenAISample/
```

### API 오류
- API 키가 유효한지 확인
- 모델 이름이 정확한지 확인 (gpt-5-nano)
- 네트워크 연결 확인

## 비교: Azure OpenAI vs OpenAI API

| 항목 | Azure OpenAI | OpenAI API |
|------|--------------|------------|
| 설정 | Endpoint + Key + Deployment | API Key + Model |
| 어댑터 | AgentFrameworkAdapter | OpenAIAdapter |
| 패키지 | Azure.AI.OpenAI | OpenAI |
| 위치 | examples/BasicUsage | samples/OpenAISample |

## 다음 단계

1. 다른 OpenAI 모델 시도 (gpt-4, gpt-4-turbo)
2. 커스텀 에이전트 추가
3. 웹 API로 확장
4. 대화 히스토리 추가

## 참고

- [OpenAI API 문서](https://platform.openai.com/docs)
- [Ironbees README](../../README.md)
- [Usage Guide](../../docs/USAGE.md)
