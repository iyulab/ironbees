# Deployment Guide

Deploy Ironbees to production environments safely and efficiently.

## Pre-Deployment Checklist

- [ ] API keys in secure storage (Key Vault / Environment variables)
- [ ] StrictValidation enabled
- [ ] Logging and monitoring configured
- [ ] Health check endpoints implemented
- [ ] Resilience policies (retry, circuit breaker) configured

## Security Configuration

### API Key Management

```csharp
// ❌ NEVER hardcode
options.AzureOpenAIKey = "your-key"; // DANGEROUS

// ✅ Environment variables
options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")
    ?? throw new InvalidOperationException("AZURE_OPENAI_KEY not configured");

// ✅ Azure Key Vault (recommended)
var client = new SecretClient(
    new Uri("https://your-vault.vault.azure.net/"),
    new DefaultAzureCredential());
var secret = await client.GetSecretAsync("AzureOpenAIKey");
```

### Agent Validation

```csharp
var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions
{
    EnableValidation = true,
    StrictValidation = true,   // Production
    StopOnFirstError = true,
    EnableHotReload = false    // Disable in production
});
```

## Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY agents/ agents/
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'
services:
  ironbees-app:
    build: .
    ports:
      - "8080:8080"
    environment:
      - AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT}
      - AZURE_OPENAI_KEY=${AZURE_OPENAI_KEY}
    volumes:
      - ./agents:/app/agents:ro
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

## Azure Deployment

### Container Apps

```bash
# Create environment
az containerapp env create \
  --name ironbees-env \
  --resource-group ironbees-rg

# Deploy
az containerapp create \
  --name ironbees-app \
  --resource-group ironbees-rg \
  --environment ironbees-env \
  --image your-registry.azurecr.io/ironbees:latest \
  --target-port 8080 \
  --ingress external \
  --env-vars AZURE_OPENAI_ENDPOINT=$ENDPOINT \
  --min-replicas 1 \
  --max-replicas 5
```

### Key Vault Integration

```bash
# Create and configure
az keyvault create --name ironbees-kv --resource-group ironbees-rg
az keyvault secret set --vault-name ironbees-kv --name AzureOpenAIKey --value $KEY

# Enable Managed Identity
az webapp identity assign --name ironbees-app --resource-group ironbees-rg
```

## Resilience Patterns

```csharp
// Polly retry with circuit breaker
services.AddHttpClient("AzureOpenAI")
    .AddPolicyHandler(Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))))
    .AddPolicyHandler(Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1)));
```

## Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<AgentHealthCheck>("agents")
    .AddCheck<LLMHealthCheck>("llm");

app.MapHealthChecks("/health");

public class AgentHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var agents = _registry.GetAllAgents();
        return Task.FromResult(agents.Count > 0
            ? HealthCheckResult.Healthy($"{agents.Count} agents loaded")
            : HealthCheckResult.Unhealthy("No agents"));
    }
}
```

## Monitoring

### Serilog Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/ironbees-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.ApplicationInsights(TelemetryConfiguration.Active)
    .CreateLogger();
```

### Application Insights

```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = config["ApplicationInsights:ConnectionString"];
});
```

## Post-Deployment

- [ ] Verify health check endpoints
- [ ] Run load tests (k6, JMeter)
- [ ] Configure alerts and notifications
- [ ] Test backup and recovery procedures

## Next Steps

- [Quick Start](./QUICKSTART.md)
- [Architecture](./ARCHITECTURE.md)
- [LLM Providers](./PROVIDERS.md)
