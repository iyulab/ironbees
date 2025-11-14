# ironbees Architecture

**Version**: 1.0.0
**Target Framework**: .NET 9.0
**Last Updated**: 2025-10-29

---

## Executive Summary

ironbees는 **convention-based, file-system driven orchestration layer**로, LLM 프레임워크와 애플리케이션 사이에서 에이전트 관리를 단순화합니다. 복잡한 설정 코드 없이 파일 구조만으로 다중 에이전트 시스템을 구성할 수 있습니다.

**핵심 설계 원칙**:
- Convention over Configuration
- Thin Layer (최소 추상화)
- Delegate Complexity (복잡성을 에이전트에 위임)
- File-based Visibility (버전 관리 가능한 설정)

**핵심 가치 제안**:
- **반복되는 패턴 간소화**: System Agents로 일반적인 작업 즉시 사용
- **즉시 사용 가능**: 설치 후 바로 활용 가능한 검증된 에이전트
- **확장 가능**: User Agents로 도메인 특화 기능 추가

---

## Agent Types

ironbees는 두 가지 유형의 에이전트를 제공합니다:

### System Agents (시스템 에이전트)

**정의**: ironbees가 기본 제공하는 **내장 에이전트**

**특징**:
- ✅ **자동 구성**: 설치하면 바로 사용 가능
- ✅ **검증된 패턴**: Production-ready 프롬프트
- ✅ **범용 기능**: 요약, 검색, 번역, 코드 리뷰 등
- ✅ **오버라이드 가능**: 필요시 커스터마이징

**위치**: `Ironbees.SystemAgents` NuGet 패키지 (Embedded Resources)

**예시**:
- `summarizer`: 텍스트 요약
- `web-search`: 웹 검색 (Tavily MCP)
- `file-explorer`: 파일 탐색
- `translator`: 다국어 번역
- `code-reviewer`: 코드 리뷰

### User Agents (사용자 에이전트)

**정의**: 사용자가 **도메인 특화**로 만드는 에이전트

**특징**:
- ✅ **완전 커스터마이즈**: 프롬프트, 도구, 설정 자유
- ✅ **도메인 전문성**: 의료, 법률, 금융 등 특화
- ✅ **회사 지식**: 내부 프로세스, 정책 반영
- ✅ **우선순위 높음**: System Agent 오버라이드 가능

**위치**: 사용자 프로젝트 `/agents/` 디렉토리

**예시**:
- `medical-diagnosis`: 의료 진단 지원
- `legal-contract-reviewer`: 법률 계약서 검토
- `customer-support`: 고객 지원 챗봇
- `coding-agent`: 회사 코딩 스타일 준수

### Loading Priority

```
1. User Agents (/agents/)           → 최우선
2. System Agents (Embedded)         → Fallback
```

사용자가 `/agents/summarizer/`를 만들면, 시스템의 기본 `summarizer`를 **오버라이드**합니다.

---

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│  - Business Logic                                            │
│  - UI/API Endpoints                                          │
│  - Authentication & Authorization                            │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│              ironbees Orchestration Layer                    │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │   Agent Registry                                      │  │
│  │   ┌─────────────────┐  ┌─────────────────┐          │  │
│  │   │ System Agents   │  │  User Agents    │          │  │
│  │   │ (Built-in)      │  │  (Custom)       │          │  │
│  │   └─────────────────┘  └─────────────────┘          │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Agent      │  │   Agent      │  │   Pipeline   │     │
│  │   Loader     │→ │   Selector   │→ │   Manager    │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│         │                  │                  │              │
│         │    CompositeAgentLoader             │              │
│  ┌──────┴─────┐    (Priority: User → System) │              │
│  │ System  │  User                            │              │
│  │ Agents  │  Agents                          │              │
│  └─────────┴──────┘                           │              │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│              LLM Framework Layer                             │
│  - Microsoft Agent Framework (primary)                       │
│  - Semantic Kernel (supported)                               │
│  - Custom Framework Adapters                                 │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│                 LLM Provider Layer                           │
│  - Azure OpenAI                                              │
│  - OpenAI                                                    │
│  - Other OpenAI-compatible providers                         │
└─────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. Agent Loader

**Responsibility**: Filesystem에서 에이전트 구성을 로드하고 파싱

**Key Features**:
- Convention-based directory scanning
- Hierarchical configuration discovery (enterprise → user → project → local)
- Hot-reload support for development
- Validation and schema checking

**File Structure Convention**:
```
/agents/
  /{agent-name}/
    agent.yaml           # Agent metadata (required)
    system-prompt.md     # System instructions (required)
    tools.md             # Tool definitions (optional)
    mcp-config.json      # MCP server config (optional)
    examples/            # Few-shot examples (optional)
      example-1.md
      example-2.md
```

**Agent Metadata Schema** (`agent.yaml`):
```yaml
name: coding-agent
description: Expert software developer for code generation and review
version: 1.0.0
model:
  provider: azure-openai
  deployment: gpt-4o
  temperature: 0.7
  max_tokens: 4000
capabilities:
  - code-generation
  - code-review
  - debugging
tags:
  - coding
  - development
  - technical
```

---

### 2. Agent Selector

**Responsibility**: 사용자 입력을 분석하여 적절한 에이전트 선택

**Selection Strategies** (Research Insight: 40-85% cost reduction):

#### Strategy 1: Semantic Routing (Default)
- **Mechanism**: Embedding-based similarity in vector space
- **Latency**: 10-100ms
- **Cost**: ~$107.9/month per 50K queries
- **Use Case**: Well-separated intent clusters

```csharp
public class SemanticRouter : IAgentSelector
{
    // Embedding model for semantic similarity
    private IEmbeddingGenerator _embedder;

    // Pre-computed agent capability embeddings
    private Dictionary<string, float[]> _agentEmbeddings;

    public async Task<Agent> SelectAgentAsync(string input)
    {
        var inputEmbedding = await _embedder.GenerateAsync(input);
        var bestMatch = FindMostSimilar(inputEmbedding, _agentEmbeddings);
        return _agentRegistry[bestMatch];
    }
}
```

#### Strategy 2: Classifier-Based Routing
- **Mechanism**: ML model trained on labeled data
- **Requirements**: 100-1000+ examples per class
- **Use Case**: In-domain tasks with training data

#### Strategy 3: LLM-as-Router
- **Mechanism**: Generative model for context-aware decisions
- **Latency**: ~500ms
- **Cost**: ~$188.9/month per 50K queries
- **Use Case**: Nuanced, context-dependent routing

#### Hybrid Approach (Recommended)
```
Input → Semantic Router (broad categorization)
      → Classifier LLM (fine-grained routing within domain)
      → Selected Agent
```

**Implementation Priority**: Phase 3 (Semantic routing first, LLM fallback later)

---

### 3. Pipeline Manager

**Responsibility**: 입력 전처리 및 출력 후처리

**Pipeline Architecture**:
```
User Input
    │
    ▼
┌────────────────────┐
│  Preprocessors     │
│  - PII Masking     │
│  - Input Filter    │
│  - Context Inject  │
└────────┬───────────┘
         │
         ▼
┌────────────────────┐
│  Agent Execution   │
│  (LLM Framework)   │
└────────┬───────────┘
         │
         ▼
┌────────────────────┐
│  Postprocessors    │
│  - Output Validate │
│  - Format Convert  │
│  - Compliance Check│
└────────┬───────────┘
         │
         ▼
    Final Output
```

**Preprocessor Interface**:
```csharp
public interface IPreprocessor
{
    Task<string> ProcessAsync(string input, Dictionary<string, object> context);
    int Priority { get; } // Execution order
}
```

**Postprocessor Interface**:
```csharp
public interface IPostprocessor
{
    Task<string> ProcessAsync(string output, Dictionary<string, object> context);
    int Priority { get; }
}
```

**Guardrails Integration** (Research Insight: 95%+ defense rates):
- Training-time defenses (future)
- Pattern matching (regex filters)
- Context-aware detection
- Structured queries (StruQ pattern)

---

### 4. Agent Registry

**Responsibility**: 로드된 에이전트 저장 및 관리

**Features**:
- Thread-safe agent storage
- Agent lifecycle management
- Version tracking
- Health checks

```csharp
public interface IAgentRegistry
{
    void Register(string name, IAgent agent);
    IAgent Get(string name);
    IReadOnlyCollection<string> ListAgents();
    bool Contains(string name);
    void Unregister(string name);
}
```

---

## Framework Integration Layer

### Microsoft Agent Framework Adapter (Primary)

**Integration Pattern**:
```csharp
public class AgentFrameworkAdapter : ILLMFrameworkAdapter
{
    private readonly AzureOpenAIClient _client;

    public async Task<IAgent> CreateAgentAsync(AgentConfig config)
    {
        // ironbees config → Agent Framework
        var chatClient = _client.GetChatClient(config.Model.Deployment);

        var agent = chatClient.CreateAIAgent(
            instructions: config.SystemPrompt,     // from system-prompt.md
            tools: config.Tools,                   // from tools.md
            mcpConfig: config.McpConfig            // from mcp-config.json
        );

        return new AgentFrameworkAgentWrapper(agent);
    }

    public async Task<string> RunAsync(IAgent agent, string input)
    {
        var frameworkAgent = (AgentFrameworkAgentWrapper)agent;
        return await frameworkAgent.InnerAgent.RunAsync(input);
    }
}
```

**Responsibilities**:
- ironbees config → Agent Framework mapping
- Agent lifecycle management
- Tool registration
- MCP client setup

---

### Semantic Kernel Adapter (Secondary)

**Purpose**: Support for existing Semantic Kernel deployments

```csharp
public class SemanticKernelAdapter : ILLMFrameworkAdapter
{
    private readonly Kernel _kernel;

    public async Task<IAgent> CreateAgentAsync(AgentConfig config)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        // Map configuration
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = config.Model.Temperature,
            MaxTokens = config.Model.MaxTokens
        };

        return new SemanticKernelAgentWrapper(chatService, config, settings);
    }
}
```

---

## Data Flow

### Request Processing Flow

```
1. User Request
   └─→ "Python으로 API 서버 코드 작성해줘"

2. Preprocessors (순차 실행)
   ├─→ PII Masking: Check for sensitive data
   ├─→ Input Validation: Length, format checks
   └─→ Context Injection: Add user_id, timestamp

3. Agent Selection
   ├─→ Semantic Router: Analyze input embedding
   ├─→ Find best match: "coding-agent" (0.89 similarity)
   └─→ Retrieve agent from registry

4. Agent Execution
   ├─→ Load agent config: system-prompt.md
   ├─→ Setup tools: tools.md
   ├─→ Configure MCP: mcp-config.json
   └─→ Execute via framework: Agent.RunAsync()

5. Postprocessors (순차 실행)
   ├─→ Output Validation: Check compliance
   ├─→ Format Conversion: Markdown formatting
   └─→ Guardrails: Check for violations

6. Response
   └─→ Return processed output to user
```

---

## Configuration Management

### Hierarchical Configuration Discovery

**Precedence Order** (highest to lowest):
```
1. Project Local:  ./.ironbees/config.yaml (gitignored)
2. Project Root:   ./ironbees.config.yaml (version controlled)
3. User Global:    ~/.ironbees/config.yaml
4. Enterprise:     /etc/ironbees/config.yaml (Linux) or C:\ProgramData\ironbees\config.yaml (Windows)
```

**Configuration Schema**:
```yaml
# ironbees.config.yaml
version: 1.0.0

framework:
  provider: agent-framework  # agent-framework | semantic-kernel | custom

model:
  default_provider: azure-openai
  azure_openai:
    endpoint: https://your-resource.openai.azure.com/
    api_version: 2024-08-01-preview
    authentication: azure-cli  # azure-cli | managed-identity | api-key

agents:
  directory: ./agents
  hot_reload: true
  validation: strict  # strict | lenient | off

routing:
  strategy: semantic  # semantic | classifier | llm | hybrid
  semantic:
    embedding_model: text-embedding-3-small
    similarity_threshold: 0.7
  llm_fallback: true

pipeline:
  preprocessors:
    - pii-masking
    - input-validation
  postprocessors:
    - output-validation
    - format-conversion

observability:
  enabled: true
  provider: opentelemetry
  export:
    traces: true
    metrics: true
    logs: true

caching:
  enabled: true
  provider: memory  # memory | redis | distributed
  ttl_seconds: 3600
```

---

## Performance Optimizations

### 1. Caching Strategy (Research Insight: 50-70% cost reduction)

**Two-Layer Caching**:
```csharp
public class HybridCache
{
    private IMemoryCache _exactCache;      // Exact match (fast)
    private ISemanticCache _semanticCache; // Similarity-based (slower, higher hit rate)

    public async Task<string?> GetAsync(string input)
    {
        // Layer 1: Exact match
        if (_exactCache.TryGetValue(input, out var cached))
            return cached;

        // Layer 2: Semantic similarity
        return await _semanticCache.FindSimilarAsync(input, threshold: 0.95);
    }
}
```

**KV Cache Optimization**:
- Anchor-based caching: 70% size reduction
- Quantization: 4-bit/8-bit representations
- Adaptive compression based on attention scores

### 2. Prompt Caching

**Mechanism**: Reuse attention states for frequently occurring segments
```
System Message (cached) + User Input (dynamic) → LLM
```

**Profitability**: After just one reuse

### 3. Dynamic Model Routing

**Cost Optimization**:
- Simple tasks → smaller models (gpt-4o-mini)
- Complex reasoning → larger models (gpt-4o)
- **Expected savings**: 40-85% operational cost

---

## Observability Architecture

### MELT + LLM Pillars

**Traditional MELT**:
- **M**etrics: Latency, throughput, error rates
- **E**vents: Agent selection, pipeline execution
- **L**ogs: Structured logging with context
- **T**races: Distributed tracing across components

**LLM-Specific Pillars**:
- **Prompts**: Input/output logging with versions
- **Evaluations**: Quality metrics (hallucination, relevance, toxicity)
- **Retrieval**: RAG performance analysis
- **Feedback**: User ratings and corrections
- **Costs**: Token usage and pricing tracking

### OpenTelemetry Integration

```csharp
public class IronbeesOrchestrator
{
    private readonly ActivitySource _activitySource = new("ironbees");

    public async Task<string> ProcessAsync(string input)
    {
        using var activity = _activitySource.StartActivity("Process Request");
        activity?.SetTag("input.length", input.Length);

        // Agent selection span
        using var selectionActivity = _activitySource.StartActivity("Agent Selection");
        var agent = await _selector.SelectAgentAsync(input);
        selectionActivity?.SetTag("agent.name", agent.Name);

        // Agent execution span
        using var executionActivity = _activitySource.StartActivity("Agent Execution");
        var output = await agent.RunAsync(input);
        executionActivity?.SetTag("output.length", output.Length);

        return output;
    }
}
```

### Metrics Collection

```csharp
public class IronbeesMetrics
{
    private readonly Meter _meter = new("ironbees");

    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _tokenUsage;

    public IronbeesMetrics()
    {
        _requestCounter = _meter.CreateCounter<long>("ironbees.requests");
        _requestDuration = _meter.CreateHistogram<double>("ironbees.request.duration");
        _tokenUsage = _meter.CreateCounter<long>("ironbees.tokens.used");
    }
}
```

---

## Security Architecture

### Input Validation Pipeline

**Five-Step Process**:
1. **Input Filtering**: Remove/flag suspicious patterns
2. **Input Encoding**: Prevent code injection
3. **Length Restrictions**: 1K-10K characters
4. **PII Detection**: Redact sensitive information
5. **Invisible Text Detection**: Hidden Unicode characters

### Guardrails Integration

**NeMo Guardrails Pattern**:
```yaml
# guardrails.yaml
rails:
  input:
    - check_pii
    - check_jailbreak
    - check_injection

  dialog:
    - enforce_topic_boundaries

  output:
    - check_hallucination
    - check_toxicity
    - verify_compliance
```

### Constitutional AI Approach

**Training-Time Defenses** (Future):
- DefensiveTokens: 0.24% attack success rate
- SecAlign: ~1% ASR on strongest attacks
- Minimal over-refusal increase: 0.38%

---

## Testing Strategy

### Test Pyramid

```
              ┌──────────────┐
              │   E2E Tests  │  (10% - Integration scenarios)
              └──────────────┘
            ┌────────────────────┐
            │  Integration Tests │  (20% - Component interaction)
            └────────────────────┘
        ┌──────────────────────────────┐
        │       Unit Tests             │  (70% - Component logic)
        └──────────────────────────────┘
```

### LLM-Specific Evaluation

**DeepEval Framework Integration**:
```csharp
[Test]
public async Task Agent_Should_Produce_Faithful_Response()
{
    var agent = _registry.Get("coding-agent");
    var context = LoadContext("test-cases/context-1.txt");
    var response = await agent.RunAsync("Explain the algorithm", context);

    // Faithfulness metric: Can claims be inferred from context?
    var faithfulness = await _evaluator.EvaluateFaithfulness(response, context);
    Assert.That(faithfulness.Score, Is.GreaterThan(0.9));
}
```

**Metrics**:
- Faithfulness (0-1)
- Answer Relevancy
- Hallucination Detection
- Toxicity
- Bias
- Contextual Relevancy (RAG)

---

## Deployment Architecture

### Development Environment

```
Developer Machine
    ├─→ Visual Studio 2022 / Rider
    ├─→ .NET 9.0 SDK
    ├─→ Azure CLI (authentication)
    └─→ ironbees CLI (agent management)
```

### Production Environment

```
┌─────────────────────────────────────┐
│         Load Balancer               │
└────────────┬────────────────────────┘
             │
    ┌────────┴────────┐
    │                 │
┌───▼────┐      ┌────▼───┐
│ App 1  │      │ App 2  │
│ ironbees│     │ ironbees│
└───┬────┘      └────┬───┘
    │                 │
    └────────┬────────┘
             │
┌────────────▼────────────────────────┐
│      Azure OpenAI Service           │
└─────────────────────────────────────┘
```

### Container Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Ironbees/Ironbees.csproj", "Ironbees/"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY agents/ ./agents/
ENTRYPOINT ["dotnet", "Ironbees.dll"]
```

---

## Package Structure

ironbees는 3개의 NuGet 패키지로 구성됩니다:

```
┌─────────────────────────────────────────┐
│  Ironbees.Core                          │
│  - 핵심 추상화 (인터페이스)            │
│  - 프레임워크 독립적 로직              │
│  - AgentRegistry, PipelineManager       │
└─────────────────────────────────────────┘
              ▲
              │ depends on
    ┌─────────┴──────────┐
    │                    │
┌───┴─────────────────┐  ┌┴──────────────────────┐
│ Ironbees.           │  │ Ironbees.SystemAgents │
│ AgentFramework      │  │ - 내장 에이전트 (5개) │
│ - MS Agent Framework│  │ - Embedded Resources  │
│   구현              │  │ - SystemAgentLoader   │
└─────────────────────┘  └───────────────────────┘
```

### Package Details

#### **Ironbees.Core** (필수)
- **목적**: 프레임워크 독립적 핵심 로직
- **의존성**: Microsoft.Extensions.* 만

#### **Ironbees.AgentFramework** (권장)
- **목적**: MS Agent Framework 구체적 구현
- **의존성**: Ironbees.Core + Microsoft.Agents.AI.OpenAI

#### **Ironbees.SystemAgents** (선택적, v1.1+)
- **목적**: 내장 시스템 에이전트 제공
- **의존성**: Ironbees.Core
- **포함**: summarizer, web-search, file-explorer, translator, code-reviewer

### Installation

```bash
# 기본 설치
dotnet add package Ironbees.AgentFramework

# System Agents 추가 (v1.1+)
dotnet add package Ironbees.SystemAgents
```

```csharp
// Startup configuration
services.AddIronbees(options =>
{
    options.AgentsDirectory = "./agents";  // User agents
})
.AddSystemAgents();  // Enable built-in agents (v1.1+)
```

---

## Technology Stack

### Core Dependencies (.NET 9.0)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Microsoft Agent Framework -->
    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="*-preview" />

    <!-- Azure OpenAI -->
    <PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
    <PackageReference Include="Azure.Identity" Version="1.*" />

    <!-- Semantic Kernel (optional) -->
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />

    <!-- Configuration -->
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Yaml" Version="9.*" />

    <!-- Dependency Injection -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.*" />

    <!-- Observability -->
    <PackageReference Include="System.Diagnostics.DiagnosticSource" Version="9.*" />
    <PackageReference Include="OpenTelemetry" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />

    <!-- Caching -->
    <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.*" />

    <!-- Testing -->
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="Moq" Version="4.*" />
  </ItemGroup>
</Project>
```

---

## API Surface

### Primary API: Orchestrator

```csharp
public interface IAgentOrchestrator
{
    // Agent management
    Task LoadAgentsAsync(string directory);
    IReadOnlyCollection<string> ListAgents();

    // Pipeline configuration
    IAgentOrchestrator AddPreprocessor(IPreprocessor preprocessor);
    IAgentOrchestrator AddPostprocessor(IPostprocessor postprocessor);

    // Execution
    Task<string> ProcessAsync(string input, string? agentName = null);
    IAsyncEnumerable<string> StreamAsync(string input, string? agentName = null);

    // Selection strategy
    void SetSelectionStrategy(IAgentSelector selector);
}
```

### Usage Example

```csharp
// Startup configuration
var orchestrator = new AgentOrchestrator("./agents");

// Add pipeline components
orchestrator
    .AddPreprocessor(new PiiMaskingPreprocessor())
    .AddPreprocessor(new InputValidationPreprocessor())
    .AddPostprocessor(new OutputValidationPostprocessor())
    .AddPostprocessor(new FormatConversionPostprocessor());

// Load agents from filesystem
await orchestrator.LoadAgentsAsync();

// Process request (auto-select agent)
var response = await orchestrator.ProcessAsync("Python으로 API 서버 코드 작성해줘");

// Or explicitly specify agent
var response2 = await orchestrator.ProcessAsync(
    "코드 리뷰해줘",
    agentName: "coding-agent"
);

// Streaming support
await foreach (var token in orchestrator.StreamAsync("긴 문서 요약해줘"))
{
    Console.Write(token);
}
```

---

## Extension Points

### Custom Framework Adapter

```csharp
public interface ILLMFrameworkAdapter
{
    Task<IAgent> CreateAgentAsync(AgentConfig config);
    Task<string> RunAsync(IAgent agent, string input);
    IAsyncEnumerable<string> StreamAsync(IAgent agent, string input);
}

// Example: LangChain adapter
public class LangChainAdapter : ILLMFrameworkAdapter
{
    public async Task<IAgent> CreateAgentAsync(AgentConfig config)
    {
        // Custom implementation
    }
}
```

### Custom Agent Selector

```csharp
public interface IAgentSelector
{
    Task<IAgent> SelectAgentAsync(string input);
}

// Example: Rule-based selector
public class RuleBasedSelector : IAgentSelector
{
    public async Task<IAgent> SelectAgentAsync(string input)
    {
        if (input.Contains("코드")) return _registry.Get("coding-agent");
        if (input.Contains("분석")) return _registry.Get("analysis-agent");
        return _registry.Get("general-agent");
    }
}
```

---

## Migration Path

### From Direct Framework Usage

**Before** (Direct Agent Framework):
```csharp
var client = new AzureOpenAIClient(endpoint, credential);
var chatClient = client.GetChatClient("gpt-4o");
var agent = chatClient.CreateAIAgent("You are a coding expert.");
var response = await agent.RunAsync(userInput);
```

**After** (With ironbees):
```csharp
var orchestrator = new AgentOrchestrator("./agents");
await orchestrator.LoadAgentsAsync();
var response = await orchestrator.ProcessAsync(userInput);
```

### From Semantic Kernel

**Before**:
```csharp
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deployment, endpoint, credential)
    .Build();
var response = await kernel.InvokePromptAsync(prompt);
```

**After**:
```csharp
var orchestrator = new AgentOrchestrator("./agents",
    framework: new SemanticKernelAdapter(kernel));
await orchestrator.LoadAgentsAsync();
var response = await orchestrator.ProcessAsync(userInput);
```

---

## Future Considerations

### Potential Enhancements (Post v1.0)

1. **Workflow Orchestration**: Multi-agent workflows (sequential, concurrent, handoff)
2. **Reinforcement Learning**: Puppeteer-style dynamic orchestration
3. **Agent Communication Protocols**: MCP, ACP, A2A integration
4. **Distributed Tracing**: Cross-service agent collaboration
5. **A/B Testing**: Agent version comparison
6. **Prompt Versioning**: Git-like version control for prompts
7. **Multi-language Support**: Python implementation

### Scalability Targets

- **Throughput**: 1000+ requests/second per instance
- **Latency**: p50 < 100ms (agent selection), p99 < 500ms
- **Cost Efficiency**: 50-70% reduction through caching
- **Reliability**: 99.9% uptime with circuit breakers

---

## Conclusion

ironbees 아키텍처는 **convention-based simplicity**와 **production-grade capabilities**를 균형있게 제공합니다. 파일시스템 기반 규칙으로 설정 오버헤드를 제거하면서도, 인텔리전트 라우팅, 파이프라인 처리, 포괄적인 관찰성을 통해 엔터프라이즈 요구사항을 충족합니다.

**핵심 차별화 요소**:
- Next.js 스타일 conventions를 AI 오케스트레이션에 적용
- .NET 9.0 네이티브 패턴 (async/await, IAsyncEnumerable, DI)
- 얇은 레이어 철학으로 프레임워크 유연성 유지
- Production-ready observability와 testing 기본 제공
