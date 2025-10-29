# 협업 패턴 (Collaboration Patterns)

다중 에이전트를 병렬로 실행하고 결과를 집계하는 패턴 가이드입니다.

## 개요

### 사용 시나리오
- **다양한 관점**: 여러 에이전트의 서로 다른 시각을 비교
- **품질 향상**: 최고 품질의 결과 선택
- **속도 최적화**: 가장 빨리 성공하는 결과 사용
- **합의 도출**: 여러 에이전트의 투표로 신뢰도 향상
- **결과 합성**: 다양한 결과를 하나로 통합

### 기본 구조

```csharp
var pipeline = orchestrator.CreatePipeline("parallel-pipeline")
    .AddParallelAgents(
        new[] { "agent1", "agent2", "agent3" },
        parallel => parallel
            .WithBestOfN(result => result.Output.Length)  // 전략 선택
            .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))
    .Build();

var result = await pipeline.ExecuteAsync("Input text");
```

## 병렬 실행 옵션

### ParallelExecutionOptions

```csharp
var options = new ParallelExecutionOptions
{
    // 최대 동시 실행 수 (null = 제한 없음)
    MaxDegreeOfParallelism = 3,

    // 전체 타임아웃
    Timeout = TimeSpan.FromSeconds(30),

    // 에이전트별 타임아웃
    PerAgentTimeout = TimeSpan.FromSeconds(10),

    // 실패 처리 정책
    FailurePolicy = ParallelFailurePolicy.RequireMajority,

    // 실패해도 계속 진행
    ContinueOnFailure = true,

    // 실패한 에이전트 재시도
    RetryFailedAgents = true,
    MaxRetries = 2,
    RetryDelay = TimeSpan.FromSeconds(1)
};
```

### 실패 정책 (ParallelFailurePolicy)

| 정책 | 설명 | 사용 케이스 |
|------|------|------------|
| `RequireAll` | 모든 에이전트 성공 필요 | 완벽한 결과 필요 시 |
| `RequireMajority` | 과반수 성공 필요 | 신뢰도 중요 시 |
| `RequireMinimum` | 최소 N개 성공 필요 | 유연한 임계값 |
| `BestEffort` | 1개 이상 성공하면 통과 | 가용성 우선 시 |
| `FirstSuccess` | 첫 성공 즉시 반환 | 속도 우선 시 |

## 협업 전략

### 1. Best-of-N (최고 선택)

가장 우수한 결과를 선택하는 전략입니다.

#### 기본 사용

```csharp
var pipeline = orchestrator.CreatePipeline("best-of-n")
    .AddParallelAgents(
        new[] { "coder1", "coder2", "coder3" },
        parallel => parallel
            .WithBestOfN(result => result.Output.Length))
    .Build();
```

#### 사전 정의된 전략

```csharp
// 가장 긴 결과 선택
.WithBestOfN(result => result.Output.Length)

// 가장 짧은 결과 선택 (음수 점수)
.WithBestOfN(result => -result.Output.Length)

// 가장 빠른 결과 선택
.WithBestOfN(result => -result.ExecutionTime.TotalMilliseconds)

// 커스텀 품질 점수
.WithBestOfN(result =>
{
    var quality = AnalyzeQuality(result.Output);
    return quality.Score;
})
```

#### 고급 설정

```csharp
.WithBestOfN(
    result => CalculateScore(result),
    options =>
    {
        options.MinimumResults = 2;  // 최소 2개 결과 필요
        options.MaximumResults = 5;  // 최대 5개 결과만 평가
        options.IncludeFailedResults = false;
        options.ResultFilter = r => r.Output.Length > 100;
    })
```

### 2. Voting (투표)

유사한 결과들의 다수결로 선택합니다.

#### 기본 사용

```csharp
var pipeline = orchestrator.CreatePipeline("voting")
    .AddParallelAgents(
        new[] { "agent1", "agent2", "agent3", "agent4", "agent5" },
        parallel => parallel.WithVoting())
    .Build();
```

#### Fuzzy Matching

```csharp
// 유사도 기반 투표 (Levenshtein distance)
.WithVoting(options =>
{
    options.MinimumResults = 3;  // 최소 3개 투표 필요
})

// 완전 일치만 인정
var strategy = new VotingStrategy(
    (output1, output2) => output1.Equals(output2, StringComparison.Ordinal));
```

#### 사용 예시

```csharp
// 입력: "What is 2+2?"
// agent1: "4"
// agent2: "4"
// agent3: "4"
// agent4: "The answer is 4"
// agent5: "Four"

// Exact match: "4" wins (3 votes)
// Fuzzy match: "4" and "The answer is 4" cluster (4 votes)
```

### 3. Ensemble (결과 합성)

여러 결과를 하나로 합성합니다.

#### 기본 연결

```csharp
.WithEnsemble(
    async results =>
    {
        return string.Join("\n\n---\n\n",
            results.Select(r => $"[{r.AgentName}]\n{r.Output}"));
    })
```

#### 에이전트를 사용한 합성

```csharp
var pipeline = orchestrator.CreatePipeline("ensemble-synthesis")
    .AddParallelAgents(
        new[] { "analyst1", "analyst2", "analyst3" },
        parallel => parallel.WithEnsemble(
            async results =>
            {
                var combined = string.Join("\n\n---\n\n",
                    results.Select((r, i) => $"분석 {i + 1} ({r.AgentName}):\n{r.Output}"));

                var synthesisPrompt = $@"다음 {results.Count}개의 분석 결과를 하나로 종합해주세요:

{combined}

핵심 인사이트를 통합한 종합 분석을 제공하세요.";

                // 별도의 합성 에이전트 사용
                return await orchestrator.ProcessAsync(
                    synthesisPrompt,
                    "synthesis-agent");
            }))
    .Build();
```

#### 섹션별 합성

```csharp
var sectionExtractors = new Dictionary<string, Func<string, string>>
{
    ["장점"] = output => ExtractSection(output, "## 장점"),
    ["단점"] = output => ExtractSection(output, "## 단점"),
    ["권장사항"] = output => ExtractSection(output, "## 권장사항")
};

var strategy = EnsembleStrategy.MergeSections(sectionExtractors);
```

### 4. First-Success (최초 성공)

가장 빨리 성공한 결과를 사용합니다.

#### 기본 사용

```csharp
var pipeline = orchestrator.CreatePipeline("first-success")
    .AddParallelAgents(
        new[] { "fast-agent", "accurate-agent", "reliable-agent" },
        parallel => parallel.WithFirstSuccess())
    .Build();
```

#### 검증 함수 추가

```csharp
.WithFirstSuccess(
    result =>
    {
        // 최소 품질 기준 확인
        return result.Output.Length > 100 &&
               !result.Output.Contains("ERROR");
    })
```

#### 사용 케이스

```csharp
// 속도가 중요한 검색 시나리오
var searchPipeline = orchestrator.CreatePipeline("fast-search")
    .AddParallelAgents(
        new[] { "cache-agent", "db-agent", "api-agent" },
        parallel => parallel
            .WithFirstSuccess(result => result.Output != "NOT_FOUND")
            .WithFailurePolicy(ParallelFailurePolicy.FirstSuccess))
    .Build();
```

## 실전 예제

### 코드 리뷰 시스템

```csharp
var reviewPipeline = orchestrator.CreatePipeline("code-review")
    .AddParallelAgents(
        new[] { "security-reviewer", "performance-reviewer", "style-reviewer" },
        parallel => parallel
            .WithEnsemble(async results =>
            {
                var sections = new StringBuilder();
                sections.AppendLine("# 종합 코드 리뷰");

                foreach (var result in results)
                {
                    sections.AppendLine($"\n## {result.AgentName}");
                    sections.AppendLine(result.Output);
                }

                return sections.ToString();
            })
            .WithFailurePolicy(ParallelFailurePolicy.BestEffort)
            .WithMaxDegreeOfParallelism(3))
    .Build();

var review = await reviewPipeline.ExecuteAsync(sourceCode);
```

### 번역 품질 검증

```csharp
var translationPipeline = orchestrator.CreatePipeline("translation-validation")
    .AddParallelAgents(
        new[] { "translator-1", "translator-2", "translator-3", "translator-4", "translator-5" },
        parallel => parallel
            .WithVoting(options =>
            {
                options.MinimumResults = 3;
                options.IncludeFailedResults = false;
            })
            .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))
    .Build();

var translation = await translationPipeline.ExecuteAsync("Translate: Hello World");
```

### 빠른 응답 시스템

```csharp
var quickResponsePipeline = orchestrator.CreatePipeline("quick-response")
    .AddParallelAgents(
        new[] { "cached-agent", "fast-agent", "thorough-agent" },
        parallel => parallel
            .WithFirstSuccess(result => result.Output.Length >= 50)
            .WithPerAgentTimeout(TimeSpan.FromSeconds(5))
            .WithTimeout(TimeSpan.FromSeconds(10)))
    .Build();

var response = await quickResponsePipeline.ExecuteAsync(userQuery);
```

### 품질 우선 생성

```csharp
var qualityPipeline = orchestrator.CreatePipeline("quality-generation")
    .AddParallelAgents(
        new[] { "creative-agent", "formal-agent", "concise-agent" },
        parallel => parallel
            .WithBestOfN(result =>
            {
                // 커스텀 품질 점수 계산
                var lengthScore = Math.Min(result.Output.Length / 1000.0, 1.0);
                var readabilityScore = CalculateReadability(result.Output);
                var grammarScore = CheckGrammar(result.Output);

                return (lengthScore + readabilityScore + grammarScore) / 3.0;
            },
            options =>
            {
                options.MinimumResults = 2;
                options.ResultFilter = r => r.Output.Length > 200;
            })
            .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))
    .Build();

var content = await qualityPipeline.ExecuteAsync(prompt);
```

## 순차 + 병렬 혼합

```csharp
var complexPipeline = orchestrator.CreatePipeline("complex-workflow")
    // 1단계: 순차 - 입력 분석
    .AddAgent("input-analyzer")

    // 2단계: 병렬 - 다양한 분석
    .AddParallelAgents(
        new[] { "sentiment-agent", "entity-agent", "intent-agent" },
        parallel => parallel
            .WithEnsemble(CombineAnalysis)
            .WithFailurePolicy(ParallelFailurePolicy.BestEffort))

    // 3단계: 순차 - 결과 요약
    .AddAgent("summarization-agent")

    // 4단계: 병렬 - 품질 검증
    .AddParallelAgents(
        new[] { "quality-checker-1", "quality-checker-2" },
        parallel => parallel
            .WithVoting()
            .WithFailurePolicy(ParallelFailurePolicy.RequireMajority))

    .Build();

var result = await complexPipeline.ExecuteAsync(input);
```

## CollaborationResult 구조

```csharp
public class CollaborationResult
{
    public string Output { get; set; }              // 최종 집계된 결과
    public string Strategy { get; set; }             // 사용된 전략 이름
    public int ResultCount { get; set; }             // 집계에 사용된 결과 수
    public double ConfidenceScore { get; set; }      // 신뢰도 점수 (0.0 ~ 1.0)
    public List<PipelineStepResult>? IndividualResults { get; set; }  // 개별 결과들
    public Dictionary<string, object> Metadata { get; set; }  // 추가 메타데이터
}
```

## 성능 최적화

### 타임아웃 설정

```csharp
.AddParallelAgents(
    agents,
    parallel => parallel
        .WithTimeout(TimeSpan.FromSeconds(30))       // 전체 타임아웃
        .WithPerAgentTimeout(TimeSpan.FromSeconds(10)))  // 개별 타임아웃
```

### 동시 실행 제한

```csharp
.AddParallelAgents(
    agents,
    parallel => parallel
        .WithMaxDegreeOfParallelism(3))  // 최대 3개만 동시 실행
```

### 재시도 설정

```csharp
.WithExecutionOptions(options =>
{
    options.RetryFailedAgents = true;
    options.MaxRetries = 2;
    options.RetryDelay = TimeSpan.FromSeconds(1);
})
```

## 에러 처리

### 실패한 에이전트 처리

```csharp
var result = await pipeline.ExecuteAsync(input);

if (result.Success)
{
    // 개별 결과 확인
    foreach (var stepResult in result.Context.StepResults)
    {
        if (stepResult.Success)
        {
            Console.WriteLine($"✅ {stepResult.AgentName}: {stepResult.Output}");
        }
        else
        {
            Console.WriteLine($"❌ {stepResult.AgentName}: {stepResult.Error?.Message}");
        }
    }
}
```

### 부분 실패 시 처리

```csharp
.WithFailurePolicy(ParallelFailurePolicy.BestEffort)
.WithExecutionOptions(options =>
{
    options.ContinueOnFailure = true;  // 일부 실패해도 계속 진행
    options.MinimumSuccessfulResults = 2;  // 최소 2개는 성공해야 함
})
```

## 모범 사례

### 1. 적절한 전략 선택

| 상황 | 권장 전략 | 이유 |
|------|----------|------|
| 품질이 중요 | Best-of-N | 최고 품질 선택 |
| 합의가 중요 | Voting | 다수결 신뢰도 |
| 다양성 필요 | Ensemble | 여러 관점 통합 |
| 속도가 중요 | First-Success | 빠른 응답 |

### 2. 타임아웃 설정

```csharp
// 나쁜 예: 무한 대기
.WithTimeout(null)

// 좋은 예: 적절한 타임아웃
.WithTimeout(TimeSpan.FromSeconds(30))
.WithPerAgentTimeout(TimeSpan.FromSeconds(10))
```

### 3. 실패 정책 선택

```csharp
// 중요한 작업: 모든 에이전트 성공 필요
.WithFailurePolicy(ParallelFailurePolicy.RequireAll)

// 일반적인 작업: 과반수 성공
.WithFailurePolicy(ParallelFailurePolicy.RequireMajority)

// 빠른 응답 필요: 1개만 성공하면 됨
.WithFailurePolicy(ParallelFailurePolicy.BestEffort)
```

### 4. 리소스 관리

```csharp
// 동시 실행 제한으로 리소스 절약
.WithMaxDegreeOfParallelism(Environment.ProcessorCount)
.WithPerAgentTimeout(TimeSpan.FromSeconds(10))
```

## 다음 단계

- [에이전트 파이프라인](AGENT_PIPELINE.md) - 순차 실행 및 조건부 워크플로우
- [시작 가이드](GETTING_STARTED.md) - 기본 에이전트 정의 및 사용법
- [아키텍처](ARCHITECTURE.md) - 프레임워크 설계 및 확장성
