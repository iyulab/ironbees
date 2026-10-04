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

```csharp
// CORS 설정
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Ironbees 서비스 등록
services.AddSingleton<IAgentOrchestrator>(...)

// Swagger 설정
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ironbees Web API",
        Version = "v1.0"
    });
});
```

## 배포 고려사항

### Production 설정

1. **HTTPS 활성화**
```csharp
app.UseHttpsRedirection();
```

2. **CORS 정책 제한**
```csharp
policy.WithOrigins("https://yourdomain.com")
      .AllowAnyMethod()
      .AllowAnyHeader();
```

3. **환경 변수 보안**
```bash
# Azure App Service
az webapp config appsettings set --settings OPENAI_API_KEY=xxx

# Docker
docker run -e OPENAI_API_KEY=xxx
```

4. **Rate Limiting 추가**
```csharp
builder.Services.AddRateLimiter(options => ...);
```

### Docker 배포

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => ...);

[Authorize]
public class AgentsController : ControllerBase { }
```

### 2. 스트리밍 응답 지원

```csharp
[HttpPost("stream")]
public async IAsyncEnumerable<string> StreamChat([FromBody] ChatRequest request)
{
    await foreach (var chunk in _orchestrator.StreamAsync(request.Message, request.AgentName))
    {
        yield return chunk;
    }
}
```

### 3. 웹소켓 지원

```csharp
app.MapHub<ChatHub>("/chat");

public class ChatHub : Hub
{
    public async IAsyncEnumerable<string> StreamMessage(string message)
    {
        await foreach (var chunk in _orchestrator.StreamAsync(message))
        {
            yield return chunk;
        }
    }
}
```

## 문제 해결

### 포트 충돌
```bash
# 다른 포트 사용
dotnet run --urls "http://localhost:5002"
```

### CORS 오류
```csharp
// 특정 도메인 허용
policy.WithOrigins("http://localhost:3000")
```

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

1. **응답 캐싱**
```csharp
[ResponseCache(Duration = 60)]
public ActionResult<List<AgentInfo>> GetAgents()
```

2. **압축 활성화**
```csharp
builder.Services.AddResponseCompression();
```

3. **비동기 처리**
모든 API 메서드는 async/await 패턴 사용

4. **Connection Pooling**
OpenAI API 클라이언트 재사용

## 모니터링

### Application Insights
```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

### Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<AgentHealthCheck>("agents");
```

### Logging
```csharp
_logger.LogInformation("Processing chat request for agent: {AgentName}", agentName);
```

## 참고 자료

- [ASP.NET Core Web API](https://learn.microsoft.com/aspnet/core/web-api)
- [Swagger/OpenAPI](https://swagger.io/docs/)
- [Ironbees Framework](../../README.md)

---

**Ironbees Web API** - RESTful multi-agent orchestration 🐝
