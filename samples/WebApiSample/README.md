# Ironbees Web API Sample

RESTful API로 Ironbees 멀티 에이전트 시스템을 제공하는 ASP.NET Core Web API 샘플입니다.

## 특징

- ✅ **RESTful API**: 표준 HTTP 메서드를 사용한 API
- ✅ **Swagger/OpenAPI**: 자동 생성된 API 문서
- ✅ **CORS 지원**: 크로스 오리진 요청 허용
- ✅ **실시간 처리**: 에이전트와의 실시간 채팅
- ✅ **자동 선택**: 지능형 에이전트 자동 선택
- ✅ **Health Check**: 시스템 상태 모니터링

## 빠른 시작

### 1. 환경 설정

프로젝트 루트에 `.env` 파일이 있는지 확인:

```env
OPENAI_API_KEY=your-api-key-here
OPENAI_MODEL=gpt-5-nano
```

### 2. 서버 실행

```bash
cd samples/WebApiSample/Ironbees.WebApi
dotnet run --urls "http://localhost:5001"
```

### 3. Swagger UI 접속

브라우저에서 http://localhost:5001 접속

## API 엔드포인트

### 1. Health Check
**GET** `/api/agents/health`

시스템 상태 확인

**Response**:
```json
{
  "status": "healthy",
  "agentsLoaded": 4,
  "timestamp": "2025-10-29T07:04:30.1136078Z"
}
```

### 2. List Agents
**GET** `/api/agents`

사용 가능한 모든 에이전트 목록 조회

**Response**:
```json
[
  {
    "name": "coding-agent",
    "description": "Expert software developer for code generation and review",
    "capabilities": [
      "code-generation",
      "code-review",
      "debugging",
      "refactoring"
    ],
    "tags": [
      "coding",
      "development",
      "csharp"
    ],
    "model": {
      "deployment": "gpt-4o",
      "temperature": 0.7,
      "maxTokens": 4000
    }
  }
]
```

### 3. Get Agent
**GET** `/api/agents/{name}`

특정 에이전트 정보 조회

**Parameters**:
- `name` (path): 에이전트 이름

**Response**:
```json
{
  "name": "writing-agent",
  "description": "Professional content writer and editor",
  "capabilities": ["content-writing", "editing"],
  "tags": ["writing", "content"],
  "model": {
    "deployment": "gpt-4o",
    "temperature": 0.8,
    "maxTokens": 3000
  }
}
```

### 4. Chat (Explicit Agent)
**POST** `/api/agents/chat`

특정 에이전트와 채팅

**Request Body**:
```json
{
  "message": "Write a hello world function in C#",
  "agentName": "coding-agent"
}
```

**Response**:
```json
{
  "message": "Here's a simple hello world function...",
  "agentName": "coding-agent",
  "confidenceScore": null,
  "processingTimeMs": 1523
}
```

### 5. Chat (Auto Selection)
**POST** `/api/agents/chat`

자동으로 에이전트를 선택하여 채팅

**Request Body**:
```json
{
  "message": "Write a blog post about AI"
}
```

**Response**:
```json
{
  "message": "AI is transforming industries...",
  "agentName": "writing-agent",
  "confidenceScore": 0.65,
  "processingTimeMs": 2134
}
```

### 6. Select Agent
**POST** `/api/agents/select`

메시지에 가장 적합한 에이전트 선택 (실행 없이)

**Request Body**:
```json
{
  "input": "Help me analyze sales data"
}
```

**Response**:
```json
{
  "selectedAgent": "analysis-agent",
  "confidenceScore": 0.75,
  "selectionReason": "Matched on: capability matches: data-analysis...",
  "allScores": [
    {
      "agentName": "analysis-agent",
      "score": 0.75,
      "reasons": ["capability matches: data-analysis", "tag matches: data"]
    },
    {
      "agentName": "coding-agent",
      "score": 0.15,
      "reasons": ["description keyword matches: 1"]
    }
  ]
}
```

## Swagger UI 사용법

1. 브라우저에서 http://localhost:5001 접속
2. 각 엔드포인트를 클릭하여 상세 정보 확인
3. "Try it out" 버튼 클릭
4. 파라미터 입력
5. "Execute" 버튼 클릭하여 API 호출

## 예제 시나리오

### 시나리오 1: 코드 생성

```bash
curl -X POST http://localhost:5001/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Write a function to calculate fibonacci numbers",
    "agentName": "coding-agent"
  }'
```

### 시나리오 2: 자동 선택

```bash
curl -X POST http://localhost:5001/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Analyze customer feedback data"
  }'
```

### 시나리오 3: 에이전트 선택 확인

```bash
curl -X POST http://localhost:5001/api/agents/select \
  -H "Content-Type: application/json" \
  -d '{
    "input": "Review this code quality"
  }'
```

## 프로젝트 구조

```
samples/WebApiSample/Ironbees.WebApi/
├── Controllers/
│   └── AgentsController.cs      # API 컨트롤러
├── Models/
│   ├── ChatRequest.cs           # 채팅 요청 모델
│   ├── ChatResponse.cs          # 채팅 응답 모델
│   ├── AgentInfo.cs             # 에이전트 정보 모델
│   ├── SelectionRequest.cs      # 선택 요청 모델
│   └── SelectionResponse.cs     # 선택 응답 모델
├── OpenAIAdapter.cs             # OpenAI 어댑터
├── Program.cs                   # 애플리케이션 진입점
└── Ironbees.WebApi.csproj       # 프로젝트 파일
```

## 기술 스택

- **ASP.NET Core 9.0**: 웹 API 프레임워크
- **Swashbuckle.AspNetCore**: Swagger/OpenAPI 문서
- **OpenAI SDK**: OpenAI API 통합
- **Ironbees.Core**: 멀티 에이전트 오케스트레이션
- **DotNetEnv**: 환경 변수 관리

## 설정 옵션

### Program.cs 주요 설정

CORS 를 열고, Ironbees 의 다섯 서비스를 등록한다 — `agents` 디렉터리에서 에이전트를 읽는 로더, 레지스트리, 키워드로
에이전트를 고르는 선택기, 모델을 부르는 어댑터, 그리고 이들을 묶는 오케스트레이터. 샘플의 어댑터는
`Ironbees.Samples.Shared` 의 `OpenAIAdapter(apiKey, model)` 이다. Swagger 는 Swashbuckle(`AddSwaggerGen`)로 켠다(`Program.cs`).

```csharp
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddSingleton<IAgentLoader>(_ => new FileSystemAgentLoader());
builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
builder.Services.AddSingleton<IAgentSelector>(_ => new KeywordAgentSelector(minimumConfidenceThreshold: 0.3));
builder.Services.AddSingleton<ILLMFrameworkAdapter>(adapter);
builder.Services.AddSingleton<IAgentOrchestrator>(sp => new AgentOrchestrator(
    sp.GetRequiredService<IAgentLoader>(),
    sp.GetRequiredService<IAgentRegistry>(),
    sp.GetRequiredService<ILLMFrameworkAdapter>(),
    sp.GetRequiredService<IAgentSelector>(),
    agentsPath));
```

## 배포 고려사항

### Production 설정

1. **HTTPS 활성화와 CORS 정책 제한** — 개발용 «모든 출처 허용» 대신 서비스할 도메인만 연다.

```csharp
app.UseHttpsRedirection();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.WithOrigins("https://yourdomain.com").AllowAnyMethod().AllowAnyHeader()));
```

2. **환경 변수 보안** — API 키는 저장소가 아니라 호스트의 설정에 둔다.
```bash
# Azure App Service
az webapp config appsettings set --settings OPENAI_API_KEY=xxx

# Docker
docker run -e OPENAI_API_KEY=xxx
```

3. **Rate Limiting** — ASP.NET Core 의 `AddRateLimiter`(`Microsoft.AspNetCore.RateLimiting`)로 채팅 엔드포인트에
   고정 창 제한을 건다. 모델 호출이 요청마다 비용을 만들기 때문이다.

### Docker 배포

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Ironbees.WebApi.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ironbees.WebApi.dll"]
```

## 확장 아이디어

### 1. 인증/인가 추가

JWT Bearer 인증(`Microsoft.AspNetCore.Authentication.JwtBearer`)을 등록하고 `AgentsController` 에 `[Authorize]` 를 단다.
샘플은 이 패키지를 참조하지 않는다.

### 2. 스트리밍 응답 지원

`IAgentOrchestrator.StreamAsync` 는 응답을 조각 단위로 돌려준다. 최소 API 엔드포인트 하나로 그대로 흘려보낼 수 있다 —
ASP.NET Core 는 `IAsyncEnumerable<string>` 을 JSON 배열로 스트리밍한다.

```csharp
app.MapPost("/api/agents/chat/stream", (ChatRequest request, IAgentOrchestrator orchestrator, CancellationToken cancellationToken) =>
    request.AgentName is { } agentName
        ? orchestrator.StreamAsync(request.Message, agentName, cancellationToken)
        : orchestrator.StreamAsync(request.Message, cancellationToken));
```

### 3. 웹소켓 지원

SignalR 허브의 스트리밍 메서드(`IAsyncEnumerable<string>` 반환)에서 같은 `StreamAsync` 를 돌려주면 된다
(`app.MapHub<ChatHub>("/chat")`).

## 문제 해결

### 포트 충돌
```bash
# 다른 포트 사용
dotnet run --urls "http://localhost:5002"
```

### CORS 오류
위 «CORS 정책 제한» 의 `WithOrigins(...)` 에 프런트엔드의 출처(예: `http://localhost:3000`)를 넣는다.

### Swagger 404
```
브라우저에서 http://localhost:5001 접속
(루트 경로가 Swagger UI로 설정됨)
```

### 에이전트 로드 실패
```
1. agents 디렉토리 경로 확인
2. agent.yaml 파일 유효성 검증
3. .env 파일 위치 확인
```

## 성능 최적화

1. **응답 캐싱** — 에이전트 목록(`GetAgents`)은 자주 바뀌지 않으므로 `[ResponseCache(Duration = 60)]` 를 달 수 있다.
2. **압축 활성화** — `builder.Services.AddResponseCompression()` 과 `app.UseResponseCompression()`.
3. **비동기 처리** — 모든 API 메서드는 async/await 패턴을 쓴다.
4. **Connection Pooling** — 어댑터(모델 클라이언트)는 싱글턴으로 등록해 재사용한다.

## 모니터링

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("agents", () => HealthCheckResult.Healthy("agents loaded"));
```

### Application Insights 와 로깅

Application Insights(`Microsoft.ApplicationInsights.AspNetCore`)는 샘플이 참조하지 않는다. 컨트롤러는
`ILogger<AgentsController>` 로 요청마다 에이전트 이름을 구조화된 로그로 남긴다.

## 참고 자료

- [ASP.NET Core Web API](https://learn.microsoft.com/aspnet/core/web-api)
- [Swagger/OpenAPI](https://swagger.io/docs/)
- [Ironbees Framework](../../README.md)

---

**Ironbees Web API** - RESTful multi-agent orchestration 🐝
