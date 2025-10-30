# Phase 4.1: KeywordAgentSelector 개선

**버전**: v0.1.1
**우선순위**: 높음
**예상 기간**: 1주 (2025-02-01 ~ 2025-02-07)
**담당**: Core Team

## 목표

KeywordAgentSelector의 정확도와 성능을 개선하여 에이전트 선택 품질 향상

**목표 지표**:
- 정확도: 90% 이상 (50개 테스트 케이스)
- 평균 선택 시간: < 50ms
- 테스트 커버리지: 단위 테스트 10개 추가

## 현재 상태 분석

### KeywordAgentSelector.cs 구조
```csharp
// 현재 구현 (154줄)
public class KeywordAgentSelector : IAgentSelector
{
    // 가중치 (하드코딩)
    private const double CapabilitiesWeight = 0.4;  // 40%
    private const double TagsWeight = 0.3;          // 30%
    private const double DescriptionWeight = 0.2;   // 20%
    private const double NameWeight = 0.1;          // 10%

    // 불용어 (영어만)
    private static readonly HashSet<string> StopWords = new()
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for"
    };

    // 단순 키워드 매칭 (대소문자 무시)
    private double CalculateScore(string input, IAgent agent)
    {
        var inputKeywords = ExtractKeywords(input);
        // 단순 교집합 카운트
    }
}
```

### 문제점
1. **낮은 정확도**
   - TF-IDF 가중치 없음 (모든 키워드 동일 가중치)
   - 불용어 사전 부족 (영어만, .NET 용어 없음)
   - 동의어/어간 미처리 (code ≠ coding ≠ development)

2. **성능 이슈**
   - 매번 키워드 추출 (캐싱 없음)
   - 에이전트 메타데이터 매번 파싱
   - O(n*m) 복잡도 (n=에이전트 수, m=키워드 수)

3. **확장성 제한**
   - 가중치 하드코딩 (사용자 설정 불가)
   - 신뢰도 임계값만 설정 가능
   - 디버깅 정보 부족

## 개선 계획

### Day 1-2: TF-IDF 가중치 적용

**Task 1.1: TF-IDF 계산 로직 구현**
```csharp
// src/Ironbees.Core/Selection/TfIdfCalculator.cs (새 파일)
public class TfIdfCalculator
{
    // Term Frequency: 문서 내 키워드 빈도
    public double CalculateTF(string term, List<string> document);

    // Inverse Document Frequency: 전체 문서 중 키워드 희소성
    public double CalculateIDF(string term, List<List<string>> allDocuments);

    // TF-IDF 점수
    public double CalculateTfIdf(string term, List<string> document, List<List<string>> corpus);

    // 문서 벡터 생성
    public Dictionary<string, double> CreateDocumentVector(List<string> keywords, List<List<string>> corpus);
}
```

**Task 1.2: KeywordAgentSelector에 통합**
```csharp
// 기존 KeywordAgentSelector 수정
public class KeywordAgentSelector : IAgentSelector
{
    private readonly TfIdfCalculator _tfidfCalculator;
    private Dictionary<string, Dictionary<string, double>> _agentVectors; // 캐싱

    public async Task LoadAgentsAsync(IEnumerable<IAgent> agents)
    {
        // 초기화 시 모든 에이전트 벡터 계산 및 캐싱
        var corpus = BuildCorpus(agents);
        foreach (var agent in agents)
        {
            var keywords = ExtractKeywords(agent);
            _agentVectors[agent.Name] = _tfidfCalculator.CreateDocumentVector(keywords, corpus);
        }
    }

    private double CalculateScore(string input, IAgent agent)
    {
        var inputVector = _tfidfCalculator.CreateDocumentVector(ExtractKeywords(input), _corpus);
        var agentVector = _agentVectors[agent.Name];

        // 코사인 유사도 계산
        return CosineSimilarity(inputVector, agentVector);
    }
}
```

**Task 1.3: 테스트 및 벤치마크**
```csharp
// tests/Ironbees.Core.Tests/Selection/TfIdfCalculatorTests.cs
[Fact]
public void CalculateTF_SimpleDocument_ReturnsCorrectFrequency()
[Fact]
public void CalculateIDF_MultipleDocuments_ReturnsCorrectScore()
[Fact]
public void CalculateTfIdf_RealExample_ImprovedAccuracy()

// tests/Ironbees.Core.Tests/KeywordAgentSelectorBenchmarkTests.cs
[Fact]
public void SelectAgent_WithTfIdf_ImprovedAccuracy()
{
    // Before: 75% accuracy
    // After: 90% accuracy (목표)
}
```

### Day 3-4: 정확도 개선

**Task 2.1: 불용어 사전 확장**
```csharp
// src/Ironbees.Core/Selection/StopWords.cs (새 파일)
public static class StopWords
{
    // 영어 불용어
    public static readonly HashSet<string> English = new()
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did",
        "will", "would", "should", "could", "may", "might", "must",
        "this", "that", "these", "those", "i", "you", "he", "she", "it", "we", "they"
    };

    // .NET 및 프로그래밍 관련 (의미 있는 키워드)
    public static readonly HashSet<string> ProgrammingExceptions = new()
    {
        // 이것들은 불용어에서 제외 (중요한 키워드)
        "api", "code", "data", "file", "test", "async", "agent", "model"
    };

    // 최종 불용어 (English - ProgrammingExceptions)
    public static HashSet<string> GetStopWords() =>
        English.Except(ProgrammingExceptions).ToHashSet();
}
```

**Task 2.2: 동의어 매핑**
```csharp
// src/Ironbees.Core/Selection/SynonymMapper.cs (새 파일)
public class SynonymMapper
{
    private static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["code"] = new[] { "coding", "programming", "development", "script" },
        ["test"] = new[] { "testing", "validation", "verification" },
        ["api"] = new[] { "endpoint", "interface", "service" },
        ["data"] = new[] { "information", "dataset", "records" },
        ["analyze"] = new[] { "analysis", "examine", "investigate" },
        ["write"] = new[] { "create", "generate", "compose" },
        ["fix"] = new[] { "repair", "correct", "debug" },
        ["review"] = new[] { "check", "inspect", "evaluate" }
    };

    public string Normalize(string word)
    {
        // 동의어를 대표 키워드로 정규화
        foreach (var (canonical, synonyms) in Synonyms)
        {
            if (synonyms.Contains(word.ToLowerInvariant()))
                return canonical;
        }
        return word.ToLowerInvariant();
    }
}
```

**Task 2.3: 어간 추출 (Stemming)**
```csharp
// NuGet: Porter2StemmerStandard 추가
using Porter2StemmerStandard;

public class KeywordExtractor
{
    private readonly EnglishPorter2Stemmer _stemmer = new();
    private readonly SynonymMapper _synonymMapper = new();

    public List<string> ExtractKeywords(string text)
    {
        return text
            .Split(new[] { ' ', ',', '.', ';', ':', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => !StopWords.GetStopWords().Contains(w))
            .Select(w => _synonymMapper.Normalize(w))  // 동의어 정규화
            .Select(w => _stemmer.Stem(w).Value)       // 어간 추출
            .Distinct()
            .ToList();
    }
}
```

**Task 2.4: 테스트**
```csharp
[Theory]
[InlineData("I need code review", "code,review")]
[InlineData("Write coding tests", "code,test")]  // coding → code (어간)
[InlineData("API development", "api,code")]       // development → code (동의어)
public void ExtractKeywords_WithNormalization_ReturnsCanonicalTerms(string input, string expected)
```

### Day 5: 성능 최적화

**Task 3.1: 키워드 추출 캐싱**
```csharp
public class KeywordAgentSelector : IAgentSelector
{
    private readonly MemoryCache _keywordCache;
    private readonly MemoryCacheOptions _cacheOptions = new()
    {
        SizeLimit = 1000,
        ExpirationScanFrequency = TimeSpan.FromMinutes(5)
    };

    public async Task<AgentSelectionResult> SelectAgentAsync(string input, ...)
    {
        var inputKeywords = _keywordCache.GetOrCreate(input, entry =>
        {
            entry.Size = 1;
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);
            return _keywordExtractor.ExtractKeywords(input);
        });
        // ...
    }
}
```

**Task 3.2: 에이전트 메타데이터 인덱싱**
```csharp
public class AgentIndex
{
    private readonly Dictionary<string, AgentMetadata> _index = new();

    public class AgentMetadata
    {
        public string Name { get; set; }
        public List<string> Keywords { get; set; }
        public Dictionary<string, double> TfIdfVector { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public void IndexAgent(IAgent agent)
    {
        _index[agent.Name] = new AgentMetadata
        {
            Name = agent.Name,
            Keywords = ExtractAllKeywords(agent),
            TfIdfVector = CalculateVector(agent),
            LastUpdated = DateTime.UtcNow
        };
    }
}
```

**Task 3.3: 벤치마크**
```csharp
[Benchmark]
public void SelectAgent_1000Times()
{
    for (int i = 0; i < 1000; i++)
    {
        _selector.SelectAgentAsync("Write C# code").GetAwaiter().GetResult();
    }
}

// 목표: 평균 < 50ms, 총 < 50초
```

### Day 6-7: 테스트 및 문서

**Task 4.1: 단위 테스트 추가 (10개)**
```csharp
// tests/Ironbees.Core.Tests/Selection/KeywordAgentSelectorTests.cs

1. SelectAgent_ExactMatch_ReturnsHighConfidence()
2. SelectAgent_PartialMatch_ReturnsMediumConfidence()
3. SelectAgent_NoMatch_ReturnsLowConfidence()
4. SelectAgent_Synonyms_RecognizesAsMatch()
5. SelectAgent_Stemming_NormalizesVariants()
6. SelectAgent_TfIdf_PrioritizesRareTerms()
7. SelectAgent_WithCache_ImprovesPerformance()
8. SelectAgent_MultipleAgents_SelectsBest()
9. SelectAgent_EdgeCase_EmptyInput()
10. SelectAgent_EdgeCase_AllStopWords()
```

**Task 4.2: 정확도 테스트 케이스 (50개)**
```csharp
// tests/Ironbees.Core.Tests/Selection/AccuracyTests.cs

[Theory]
[MemberData(nameof(TestCases))]
public void SelectAgent_TestCase_MeetsAccuracyTarget(string input, string expectedAgent, double minConfidence)

public static IEnumerable<object[]> TestCases()
{
    // 코딩 관련 (10개)
    yield return new[] { "Write C# code", "coding-agent", 0.8 };
    yield return new[] { "Need code review", "review-agent", 0.8 };
    // ...

    // 분석 관련 (10개)
    yield return new[] { "Analyze data", "analysis-agent", 0.8 };
    // ...

    // 문서 관련 (10개)
    yield return new[] { "Write documentation", "writing-agent", 0.8 };
    // ...

    // 엣지 케이스 (10개)
    yield return new[] { "Hello", "any-agent", 0.3 };  // 낮은 신뢰도 허용
    // ...

    // 혼합 (10개)
    yield return new[] { "Write tests for API", "coding-agent", 0.7 };
    // ...
}
```

**Task 4.3: 문서 업데이트**
```markdown
# docs/AGENT_SELECTION.md (새 파일)

## 에이전트 선택 알고리즘

### KeywordAgentSelector

**TF-IDF 가중치**:
- TF (Term Frequency): 키워드 빈도
- IDF (Inverse Document Frequency): 희소성
- 코사인 유사도로 최종 점수 계산

**키워드 정규화**:
1. 불용어 제거 (영어 + .NET 예외)
2. 동의어 매핑 (code = coding = programming)
3. 어간 추출 (coding → code)

**성능**:
- 평균 선택 시간: < 50ms
- 캐싱: 최근 1000개 입력
- 인덱싱: 에이전트 메타데이터

**정확도**:
- 테스트 케이스: 50개
- 목표 정확도: 90%
- 실제 정확도: [테스트 후 기록]
```

## 테스트 계획

### 단위 테스트
- TfIdfCalculator: 5개 테스트
- SynonymMapper: 3개 테스트
- KeywordExtractor: 5개 테스트
- KeywordAgentSelector: 10개 테스트 (기존 + 추가)

**총**: 23개 테스트

### 정확도 테스트
- 50개 실제 시나리오 테스트 케이스
- 목표: 90% 정확도 (45/50 성공)

### 성능 테스트
- 1000회 선택 벤치마크
- 목표: 총 < 50초 (평균 < 50ms)

### 통합 테스트
- OpenAISample에서 실제 에이전트 선택
- 다양한 입력으로 검증

## 파일 변경 목록

### 새 파일
- `src/Ironbees.Core/Selection/TfIdfCalculator.cs`
- `src/Ironbees.Core/Selection/SynonymMapper.cs`
- `src/Ironbees.Core/Selection/StopWords.cs`
- `src/Ironbees.Core/Selection/KeywordExtractor.cs`
- `src/Ironbees.Core/Selection/AgentIndex.cs`
- `tests/Ironbees.Core.Tests/Selection/TfIdfCalculatorTests.cs`
- `tests/Ironbees.Core.Tests/Selection/AccuracyTests.cs`
- `tests/Ironbees.Core.Tests/Selection/BenchmarkTests.cs`
- `docs/AGENT_SELECTION.md`

### 수정 파일
- `src/Ironbees.Core/KeywordAgentSelector.cs`
- `src/Ironbees.Core/Ironbees.Core.csproj` (NuGet 추가)
- `tests/Ironbees.Core.Tests/KeywordAgentSelectorTests.cs`

### NuGet 패키지 추가
```xml
<PackageReference Include="Porter2StemmerStandard" Version="1.0.2" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.0" />
```

## 성공 조건

- [ ] 정확도 90% 이상 (50개 테스트 케이스)
- [ ] 평균 선택 시간 < 50ms (1000회 벤치마크)
- [ ] 단위 테스트 23개 통과
- [ ] 코드 리뷰 승인
- [ ] 문서 업데이트 완료
- [ ] CI/CD 통과 (67 + 23 = 90개 테스트)

## 다음 단계 (Phase 4.2)

KeywordAgentSelector 개선 완료 후:
- FileSystemAgentLoader 강화
- Hot reload 지원
- 에러 메시지 개선

---

**Phase 4.1 Plan** - KeywordAgentSelector 개선 🎯

**Status**: 📋 계획 수립 완료
**Ready to Start**: ✅ Yes
**Start Date**: TBD
**Target Completion**: 1주 후
