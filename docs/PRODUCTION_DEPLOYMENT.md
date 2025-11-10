# 🚀 Production Deployment Guide

**목표**: Ironbees를 프로덕션 환경에 안전하고 효율적으로 배포하기

## 📋 체크리스트

### 배포 전 필수 사항
- [ ] 환경 변수 구성 완료
- [ ] API 키 보안 설정 (Azure Key Vault 등)
- [ ] 로깅 및 모니터링 구성
- [ ] 에이전트 검증 활성화 (StrictValidation)
- [ ] 캐싱 전략 설정
- [ ] 에러 핸들링 및 복원력 구성
- [ ] 부하 테스트 완료
- [ ] 백업 및 복구 계획 수립

---

## 🔐 보안 설정

### 1. API 키 관리

**❌ 절대 하지 말 것:**
```csharp
// NEVER hardcode API keys in code!
options.AzureOpenAIKey = "your-api-key-here"; // ❌ DANGEROUS
```

**✅ Azure Key Vault 사용 (권장):**
```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
var credential = new DefaultAzureCredential();
var client = new SecretClient(keyVaultUri, credential);

var apiKeySecret = await client.GetSecretAsync("AzureOpenAIKey");

services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
    options.AzureOpenAIKey = apiKeySecret.Value.Value;
    options.AgentsDirectory = "./agents";
});
```

**✅ 환경 변수 사용 (단순한 환경):**
```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
        ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT not configured");
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")
        ?? throw new InvalidOperationException("AZURE_OPENAI_KEY not configured");
});
```

**✅ ASP.NET Core 구성 (appsettings.json + User Secrets):**

**appsettings.json** (버전 관리에 커밋):
```json
{
  "Ironbees": {
    "AzureOpenAIEndpoint": "https://your-resource.openai.azure.com",
    "AgentsDirectory": "./agents",
    "ConfidenceThreshold": 0.7
  }
}
```

**secrets.json** (로컬 개발 전용, 커밋하지 않음):
```bash
dotnet user-secrets init
dotnet user-secrets set "Ironbees:AzureOpenAIKey" "your-local-dev-key"
```

**Program.cs**:
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIronbees(options =>
{
    var config = builder.Configuration.GetSection("Ironbees");
    options.AzureOpenAIEndpoint = config["AzureOpenAIEndpoint"]!;
    options.AzureOpenAIKey = config["AzureOpenAIKey"]!;
    options.AgentsDirectory = config["AgentsDirectory"] ?? "./agents";
    options.ConfidenceThreshold = double.Parse(config["ConfidenceThreshold"] ?? "0.7");
});
```

### 2. 에이전트 검증 강화

**프로덕션 환경에서는 StrictValidation 활성화:**
```csharp
var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions
{
    EnableValidation = true,      // 필수
    StrictValidation = true,      // 프로덕션 권장
    StopOnFirstError = true,      // 빠른 실패
    EnableCaching = true,         // 성능
    EnableHotReload = false,      // 프로덕션에서는 비활성화
    LogWarnings = true
});

// 또는 DI를 통해 구성
services.Configure<FileSystemAgentLoaderOptions>(options =>
{
    options.EnableValidation = true;
    options.StrictValidation = true;
    options.StopOnFirstError = true;
    options.EnableHotReload = false;
});
```

### 3. 입력 검증

**사용자 입력 검증 및 제한:**
```csharp
public class InputValidator
{
    private const int MaxInputLength = 4000; // Token limit consideration
    private static readonly string[] ProhibitedPatterns = { "<script", "DROP TABLE", "'; DELETE" };

    public static (bool IsValid, string? Error) ValidateInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, "Input cannot be empty");

        if (input.Length > MaxInputLength)
            return (false, $"Input exceeds maximum length of {MaxInputLength}");

        foreach (var pattern in ProhibitedPatterns)
        {
            if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return (false, "Input contains prohibited content");
        }

        return (true, null);
    }
}

// 사용 예시
var (isValid, error) = InputValidator.ValidateInput(userInput);
if (!isValid)
{
    return BadRequest(error);
}

var response = await orchestrator.ProcessAsync(userInput);
```

---

## 📊 로깅 및 모니터링

### 1. 구조화된 로깅 (Serilog)

**NuGet 패키지 설치:**
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.ApplicationInsights
```

**Program.cs 구성:**
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Ironbees")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/ironbees-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.ApplicationInsights(
        telemetryConfiguration: TelemetryConfiguration.Active,
        telemetryConverter: TelemetryConverter.Traces)
    .CreateLogger();

try
{
    Log.Information("Starting Ironbees application");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ... rest of configuration
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
```

### 2. 에이전트 실행 로깅

**커스텀 로깅 래퍼:**
```csharp
public class LoggingAgentOrchestrator : IAgentOrchestrator
{
    private readonly IAgentOrchestrator _inner;
    private readonly ILogger<LoggingAgentOrchestrator> _logger;

    public LoggingAgentOrchestrator(
        IAgentOrchestrator inner,
        ILogger<LoggingAgentOrchestrator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<string> ProcessAsync(
        string input,
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Processing request with agent: {AgentName}, InputLength: {Length}",
                agentName ?? "auto-select",
                input.Length);

            var response = await _inner.ProcessAsync(input, agentName, cancellationToken);

            sw.Stop();
            _logger.LogInformation(
                "Request completed successfully in {ElapsedMs}ms, ResponseLength: {Length}",
                sw.ElapsedMilliseconds,
                response.Length);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Request failed after {ElapsedMs}ms with agent: {AgentName}",
                sw.ElapsedMilliseconds,
                agentName ?? "auto-select");
            throw;
        }
    }

    // Implement other IAgentOrchestrator methods similarly...
}

// DI 등록
services.AddIronbees(options => { /* ... */ });
services.Decorate<IAgentOrchestrator, LoggingAgentOrchestrator>();
```

### 3. Application Insights 통합

**NuGet 패키지:**
```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

**Program.cs:**
```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

// 커스텀 메트릭 추적
public class TelemetryAgentOrchestrator : IAgentOrchestrator
{
    private readonly IAgentOrchestrator _inner;
    private readonly TelemetryClient _telemetry;

    public async Task<string> ProcessAsync(string input, string? agentName = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await _inner.ProcessAsync(input, agentName, cancellationToken);
            sw.Stop();

            _telemetry.TrackMetric("AgentExecutionTime", sw.ElapsedMilliseconds);
            _telemetry.TrackMetric("InputLength", input.Length);
            _telemetry.TrackMetric("OutputLength", response.Length);

            return response;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex);
            throw;
        }
    }
}
```

---

## ⚡ 성능 최적화

### 1. 캐싱 전략

**에이전트 구성 캐싱 (기본 활성화):**
```csharp
var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions
{
    EnableCaching = true,  // 기본값, 성능 향상
    EnableHotReload = false // 프로덕션에서는 비활성화
});
```

**응답 캐싱 (동일 입력에 대해):**
```csharp
using Microsoft.Extensions.Caching.Memory;

public class CachingAgentOrchestrator : IAgentOrchestrator
{
    private readonly IAgentOrchestrator _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

    public async Task<string> ProcessAsync(
        string input,
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"agent:{agentName ?? "auto"}:input:{input.GetHashCode()}";

        if (_cache.TryGetValue<string>(cacheKey, out var cachedResponse))
        {
            return cachedResponse!;
        }

        var response = await _inner.ProcessAsync(input, agentName, cancellationToken);

        _cache.Set(cacheKey, response, _cacheDuration);

        return response;
    }
}

// DI 등록
services.AddMemoryCache();
services.Decorate<IAgentOrchestrator, CachingAgentOrchestrator>();
```

⚠️ **주의**: 캐싱은 deterministic 응답에만 사용하세요. LLM은 비결정적일 수 있습니다.

### 2. 연결 풀링

**HttpClient 팩토리 사용 (Azure OpenAI 연결):**
```csharp
// Microsoft Agent Framework를 사용하는 경우 내부적으로 HttpClient 관리
// 별도 설정 필요 없음

// 하지만 커스텀 어댑터를 만드는 경우:
services.AddHttpClient("AzureOpenAI", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Ironbees/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 20
});
```

### 3. 병렬 처리

**여러 에이전트 동시 실행:**
```csharp
// 여러 에이전트에게 동일 질문을 병렬로 보내고 가장 빠른 응답 선택
public async Task<string> ProcessWithMultipleAgentsAsync(
    string input,
    string[] agentNames,
    CancellationToken cancellationToken = default)
{
    var tasks = agentNames.Select(name =>
        orchestrator.ProcessAsync(input, name, cancellationToken)
    );

    var firstCompleted = await Task.WhenAny(tasks);
    return await firstCompleted;
}

// 또는 모든 응답을 수집하고 가장 좋은 것 선택
public async Task<string[]> GetAllAgentResponsesAsync(
    string input,
    string[] agentNames,
    CancellationToken cancellationToken = default)
{
    var tasks = agentNames.Select(name =>
        orchestrator.ProcessAsync(input, name, cancellationToken)
    );

    return await Task.WhenAll(tasks);
}
```

---

## 🔄 에러 핸들링 및 복원력

### 1. Polly를 사용한 재시도 정책

**NuGet 패키지:**
```bash
dotnet add package Polly
dotnet add package Polly.Extensions.Http
```

**재시도 및 Circuit Breaker:**
```csharp
using Polly;
using Polly.Extensions.Http;

public class ResilientAgentOrchestrator : IAgentOrchestrator
{
    private readonly IAgentOrchestrator _inner;
    private readonly IAsyncPolicy<string> _policy;

    public ResilientAgentOrchestrator(IAgentOrchestrator inner)
    {
        _inner = inner;
        _policy = Policy<string>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Log.Warning(
                        "Retry {RetryCount} after {Delay}s due to {Exception}",
                        retryCount,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message);
                })
            .WrapAsync(Policy<string>
                .Handle<HttpRequestException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (outcome, duration) =>
                    {
                        Log.Error("Circuit breaker opened for {Duration}", duration);
                    },
                    onReset: () =>
                    {
                        Log.Information("Circuit breaker reset");
                    }));
    }

    public async Task<string> ProcessAsync(
        string input,
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        return await _policy.ExecuteAsync(
            async () => await _inner.ProcessAsync(input, agentName, cancellationToken));
    }
}
```

### 2. Fallback 에이전트

**신뢰도가 낮을 때 Fallback 에이전트 사용:**
```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "...";
    options.AzureOpenAIKey = "...";
    options.ConfidenceThreshold = 0.7;
    options.FallbackAgentName = "general-assistant"; // 범용 에이전트
});
```

### 3. Timeout 설정

**과도한 대기 시간 방지:**
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var response = await orchestrator.ProcessAsync(input, agentName, cts.Token);
}
catch (OperationCanceledException)
{
    return "요청 처리 시간이 초과되었습니다. 나중에 다시 시도해주세요.";
}
```

---

## 🐳 Docker 배포

### Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/YourApp/YourApp.csproj", "src/YourApp/"]
RUN dotnet restore "src/YourApp/YourApp.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/src/YourApp"
RUN dotnet build "YourApp.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "YourApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy agents directory
COPY --from=publish /app/publish .
COPY ["agents/", "agents/"]

# Environment variables (override in docker-compose or runtime)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'

services:
  ironbees-app:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT}
      - AZURE_OPENAI_KEY=${AZURE_OPENAI_KEY}
      - ASPNETCORE_ENVIRONMENT=Production
    volumes:
      - ./agents:/app/agents:ro  # Read-only mount
      - ./logs:/app/logs
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

### .env 파일 (버전 관리에 포함하지 않음)

```bash
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_KEY=your-api-key-here
```

### 배포

```bash
# Build and run
docker-compose up --build -d

# View logs
docker-compose logs -f

# Stop
docker-compose down
```

---

## ☁️ Azure 배포

### Azure Container Apps

**Azure CLI로 배포:**

```bash
# 1. 리소스 그룹 생성
az group create --name ironbees-rg --location koreacentral

# 2. Container Apps 환경 생성
az containerapp env create \
  --name ironbees-env \
  --resource-group ironbees-rg \
  --location koreacentral

# 3. Container App 배포
az containerapp create \
  --name ironbees-app \
  --resource-group ironbees-rg \
  --environment ironbees-env \
  --image your-registry.azurecr.io/ironbees-app:latest \
  --target-port 8080 \
  --ingress external \
  --secrets \
    azure-openai-key=$AZURE_OPENAI_KEY \
  --env-vars \
    AZURE_OPENAI_ENDPOINT=$AZURE_OPENAI_ENDPOINT \
    AZURE_OPENAI_KEY=secretref:azure-openai-key \
  --cpu 1.0 \
  --memory 2.0Gi \
  --min-replicas 1 \
  --max-replicas 5
```

### Azure App Service (Web App for Containers)

```bash
# 1. App Service Plan 생성
az appservice plan create \
  --name ironbees-plan \
  --resource-group ironbees-rg \
  --is-linux \
  --sku P1V3

# 2. Web App 생성
az webapp create \
  --name ironbees-webapp \
  --resource-group ironbees-rg \
  --plan ironbees-plan \
  --deployment-container-image-name your-registry.azurecr.io/ironbees-app:latest

# 3. 환경 변수 설정
az webapp config appsettings set \
  --name ironbees-webapp \
  --resource-group ironbees-rg \
  --settings \
    AZURE_OPENAI_ENDPOINT=$AZURE_OPENAI_ENDPOINT \
    AZURE_OPENAI_KEY=$AZURE_OPENAI_KEY
```

### Azure Key Vault 통합

```bash
# 1. Key Vault 생성
az keyvault create \
  --name ironbees-kv \
  --resource-group ironbees-rg \
  --location koreacentral

# 2. Secret 추가
az keyvault secret set \
  --vault-name ironbees-kv \
  --name AzureOpenAIKey \
  --value $AZURE_OPENAI_KEY

# 3. Managed Identity 활성화 (App Service)
az webapp identity assign \
  --name ironbees-webapp \
  --resource-group ironbees-rg

# 4. Key Vault 액세스 권한 부여
PRINCIPAL_ID=$(az webapp identity show \
  --name ironbees-webapp \
  --resource-group ironbees-rg \
  --query principalId -o tsv)

az keyvault set-policy \
  --name ironbees-kv \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

**코드에서 Key Vault 사용:**
```csharp
// DefaultAzureCredential은 Managed Identity를 자동으로 사용
var credential = new DefaultAzureCredential();
var client = new SecretClient(
    new Uri("https://ironbees-kv.vault.azure.net/"),
    credential);
```

---

## 📈 확장 및 부하 분산

### 1. 수평 확장

**Azure Container Apps 자동 스케일링:**
```bash
az containerapp update \
  --name ironbees-app \
  --resource-group ironbees-rg \
  --min-replicas 2 \
  --max-replicas 10 \
  --scale-rule-name http-scaling \
  --scale-rule-type http \
  --scale-rule-http-concurrency 50
```

### 2. 부하 테스트

**k6를 사용한 부하 테스트:**

```javascript
// load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 10 },  // Ramp up
    { duration: '5m', target: 10 },  // Stay at 10 users
    { duration: '2m', target: 50 },  // Ramp up to 50
    { duration: '5m', target: 50 },  // Stay at 50
    { duration: '2m', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // 95% under 2s
    http_req_failed: ['rate<0.01'],    // <1% errors
  },
};

export default function () {
  const url = 'https://your-app.azurecontainerapps.io/api/agent';
  const payload = JSON.stringify({
    input: 'What is the weather like today?',
    agentName: 'general-assistant'
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };

  const res = http.post(url, payload, params);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 2s': (r) => r.timings.duration < 2000,
  });

  sleep(1);
}
```

**실행:**
```bash
k6 run load-test.js
```

---

## 🔍 헬스 체크

**ASP.NET Core Health Checks:**

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<AgentHealthCheck>("agents")
    .AddCheck<AzureOpenAIHealthCheck>("azure-openai");

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// AgentHealthCheck.cs
public class AgentHealthCheck : IHealthCheck
{
    private readonly IAgentRegistry _registry;

    public AgentHealthCheck(IAgentRegistry registry)
    {
        _registry = registry;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var agents = _registry.GetAllAgents();

        if (agents.Count == 0)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("No agents loaded"));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy($"{agents.Count} agents loaded"));
    }
}

// AzureOpenAIHealthCheck.cs
public class AzureOpenAIHealthCheck : IHealthCheck
{
    private readonly ILLMFrameworkAdapter _adapter;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple test request
            var testAgent = new AgentConfig
            {
                Name = "health-check",
                Description = "Health check agent",
                Version = "1.0.0",
                SystemPrompt = "Reply with 'OK'",
                Model = new ModelConfig
                {
                    Deployment = "gpt-4",
                    Temperature = 0,
                    MaxTokens = 10
                }
            };

            var response = await _adapter.RunAsync(
                new AgentWrapper(testAgent),
                "Health check",
                cancellationToken);

            return HealthCheckResult.Healthy("Azure OpenAI is responding");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Azure OpenAI is not responding",
                ex);
        }
    }
}
```

---

## 📝 체크리스트 요약

### 배포 전
- [ ] API 키를 Azure Key Vault 또는 환경 변수로 이동
- [ ] StrictValidation 및 StopOnFirstError 활성화
- [ ] 로깅 및 모니터링 구성 (Serilog, Application Insights)
- [ ] 에러 핸들링 및 재시도 정책 추가 (Polly)
- [ ] 입력 검증 및 제한 구현
- [ ] Timeout 설정
- [ ] 캐싱 전략 검토
- [ ] Dockerfile 및 docker-compose.yml 작성
- [ ] Health check 엔드포인트 구현

### 배포 후
- [ ] 헬스 체크 모니터링 설정
- [ ] 부하 테스트 실행 (k6, JMeter 등)
- [ ] 로그 및 메트릭 검토
- [ ] 알람 및 알림 구성 (Application Insights Alerts)
- [ ] 백업 및 복구 절차 테스트
- [ ] 문서화 (운영 가이드, 장애 대응 절차)

---

## 🔗 관련 문서

- [Quick Start Guide](QUICK_START.md) - 5분 빠른 시작
- [Getting Started](GETTING_STARTED.md) - 상세 가이드
- [Architecture](ARCHITECTURE.md) - 아키텍처 이해
- [Custom Adapter](CUSTOM_ADAPTER.md) - 커스텀 어댑터 개발
- [Microsoft Agent Framework](MICROSOFT_AGENT_FRAMEWORK.md) - MAF 통합

---

**Ironbees** - Production-ready multi-agent orchestration for .NET 🐝
