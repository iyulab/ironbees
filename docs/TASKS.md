# ironbees Development Tasks

**Project**: ironbees - Convention-based LLM Agent Orchestration Layer
**Framework**: .NET 9.0
**Start Date**: 2025-10-29
**Status**: Planning Phase

---

## Table of Contents

- [Overview](#overview)
- [Phase 0: Project Setup](#phase-0-project-setup--foundation)
- [Phase 1: Core Infrastructure](#phase-1-core-infrastructure)
- [Phase 2: Agent Framework Integration](#phase-2-agent-framework-integration)
- [Phase 2.5: System Agents Foundation](#phase-25-system-agents-foundation)
- [Phase 3: Routing & Selection](#phase-3-routing--selection)
- [Phase 4: Pipeline & Security](#phase-4-pipeline--security)
- [Phase 5: Observability & Testing](#phase-5-observability--testing)
- [Phase 6: Performance Optimization](#phase-6-performance-optimization)
- [Phase 7: Production Readiness](#phase-7-production-readiness)
- [Milestones](#milestones)

---

## Overview

### Development Strategy

ironbees는 7개의 Phase로 개발됩니다. 각 Phase는 독립적으로 완료 가능하며, 점진적으로 기능을 추가합니다.

**Design Philosophy**:
- **Iterative Development**: MVP → Enhancement → Production
- **Test-Driven**: 테스트 먼저, 구현 나중
- **Documentation-First**: 아키텍처 문서 기반 구현
- **Convention-Driven**: 파일 구조가 설정

**Success Metrics**:
- **Phase Completion**: 모든 acceptance criteria 충족
- **Test Coverage**: >80% line coverage
- **Performance**: Latency targets 달성
- **Documentation**: 모든 public API 문서화

---

## Phase 0: Project Setup & Foundation

**Goal**: 프로젝트 구조 및 개발 환경 구성

**Duration**: 1-2 days

**Dependencies**: None

### Tasks

#### 0.1 Repository Structure
- [ ] **0.1.1** Create solution structure
  ```
  ironbees/
  ├── src/
  │   ├── Ironbees.Core/           # 핵심 추상화 + 프레임워크 독립적 로직
  │   ├── Ironbees.AgentFramework/ # MS Agent Framework 구현 (v1.0 primary)
  │   ├── Ironbees.SemanticKernel/ # Semantic Kernel 구현 (future, v2.0+)
  │   └── Ironbees.Cli/            # CLI 도구 (Phase 7)
  ├── tests/
  │   ├── Ironbees.Core.Tests/
  │   ├── Ironbees.AgentFramework.Tests/
  │   ├── Ironbees.Integration.Tests/
  │   └── Ironbees.E2E.Tests/
  ├── examples/
  │   ├── basic-agent/             # 단일 에이전트 예제
  │   ├── multi-agent/             # 다중 에이전트 예제
  │   └── custom-pipeline/         # 커스텀 파이프라인 예제
  ├── docs/
  │   ├── ARCHITECTURE.md ✅
  │   ├── TASKS.md ✅
  │   ├── API.md
  │   └── CONTRIBUTING.md
  └── agents/                      # 샘플 에이전트
      ├── coding-agent/
      │   ├── agent.yaml
      │   ├── system-prompt.md
      │   └── tools.md
      └── analysis-agent/
          ├── agent.yaml
          └── system-prompt.md
  ```

- [ ] **0.1.2** Create .NET 9.0 solution file
  ```bash
  dotnet new sln -n Ironbees
  ```

- [ ] **0.1.3** Create project files with proper dependencies
  ```bash
  # Core project (abstractions + framework-agnostic logic)
  dotnet new classlib -f net9.0 -n Ironbees.Core -o src/Ironbees.Core

  # Agent Framework implementation (MS Agent Framework)
  dotnet new classlib -f net9.0 -n Ironbees.AgentFramework -o src/Ironbees.AgentFramework

  # Test projects
  dotnet new xunit -f net9.0 -n Ironbees.Core.Tests -o tests/Ironbees.Core.Tests
  dotnet new xunit -f net9.0 -n Ironbees.AgentFramework.Tests -o tests/Ironbees.AgentFramework.Tests

  # Add to solution
  dotnet sln add src/Ironbees.Core/Ironbees.Core.csproj
  dotnet sln add src/Ironbees.AgentFramework/Ironbees.AgentFramework.csproj
  dotnet sln add tests/Ironbees.Core.Tests/Ironbees.Core.Tests.csproj
  dotnet sln add tests/Ironbees.AgentFramework.Tests/Ironbees.AgentFramework.Tests.csproj

  # Add project reference (AgentFramework depends on Core)
  dotnet add src/Ironbees.AgentFramework/Ironbees.AgentFramework.csproj reference src/Ironbees.Core/Ironbees.Core.csproj
  ```

#### 0.2 Development Environment
- [ ] **0.2.1** Configure .editorconfig for code style
- [ ] **0.2.2** Setup GitHub Actions for CI/CD
  - Build validation
  - Test execution
  - Code coverage reporting
- [ ] **0.2.3** Configure Dependabot for dependency updates
- [ ] **0.2.4** Create CONTRIBUTING.md with development guidelines

#### 0.3 Core Dependencies
- [ ] **0.3.1** Add NuGet packages to Ironbees.Core:
  - Microsoft.Extensions.Configuration
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging
  - YamlDotNet (for YAML parsing)
  - System.Diagnostics.DiagnosticSource

- [ ] **0.3.2** Add NuGet packages to Ironbees.AgentFramework:
  - Ironbees.Core (project reference)
  - Microsoft.Agents.AI.OpenAI (latest preview)
  - Azure.AI.OpenAI (2.*)
  - Azure.Identity (1.*)
  - Microsoft.Extensions.DependencyInjection (9.*)
  - Microsoft.Extensions.Logging (9.*)

- [ ] **0.3.3** Add testing dependencies:
  - xUnit
  - Moq
  - FluentAssertions
  - Microsoft.NET.Test.Sdk

#### 0.4 Project Configuration
- [ ] **0.4.1** Create Directory.Build.props for common properties
  ```xml
  <Project>
    <PropertyGroup>
      <LangVersion>latest</LangVersion>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>
  </Project>
  ```

- [ ] **0.4.2** Create .gitignore
- [ ] **0.4.3** Create LICENSE file (MIT or Apache 2.0)
- [ ] **0.4.4** Update README.md with build status and badges

### Acceptance Criteria
- ✅ Solution builds successfully with `dotnet build`
- ✅ All projects target .NET 9.0
- ✅ CI/CD pipeline runs on push
- ✅ Code style enforced via .editorconfig
- ✅ All dependencies resolve correctly

---

## Phase 1: Core Infrastructure

**Goal**: 기본 인터페이스 및 파일시스템 로더 구현

**Duration**: 3-5 days

**Dependencies**: Phase 0 완료

### Tasks

#### 1.1 Core Abstractions
- [ ] **1.1.1** Define `IAgent` interface
  ```csharp
  namespace Ironbees.Core;

  public interface IAgent
  {
      string Name { get; }
      string Description { get; }
      AgentConfig Configuration { get; }

      Task<string> RunAsync(string input, CancellationToken cancellationToken = default);
      IAsyncEnumerable<string> StreamAsync(string input, CancellationToken cancellationToken = default);
  }
  ```

- [ ] **1.1.2** Define `AgentConfig` model
  ```csharp
  public record AgentConfig
  {
      public required string Name { get; init; }
      public required string Description { get; init; }
      public required string Version { get; init; }
      public required string SystemPrompt { get; init; }
      public ModelConfig Model { get; init; } = new();
      public List<string> Capabilities { get; init; } = new();
      public List<string> Tags { get; init; } = new();
      public Dictionary<string, object> Metadata { get; init; } = new();
  }

  public record ModelConfig
  {
      public string Provider { get; init; } = "azure-openai";
      public required string Deployment { get; init; }
      public double Temperature { get; init; } = 0.7;
      public int MaxTokens { get; init; } = 4000;
  }
  ```

- [ ] **1.1.3** Define `IAgentRegistry` interface
  ```csharp
  public interface IAgentRegistry
  {
      void Register(string name, IAgent agent);
      IAgent? Get(string name);
      bool TryGet(string name, out IAgent? agent);
      IReadOnlyCollection<string> ListAgents();
      bool Contains(string name);
      void Unregister(string name);
      void Clear();
  }
  ```

- [ ] **1.1.4** Implement `AgentRegistry` with thread-safety
  ```csharp
  public class AgentRegistry : IAgentRegistry
  {
      private readonly ConcurrentDictionary<string, IAgent> _agents = new();

      // Implementation...
  }
  ```

#### 1.2 Configuration Management
- [ ] **1.2.1** Define `IronbeesConfig` model
  ```csharp
  public record IronbeesConfig
  {
      public string Version { get; init; } = "1.0.0";
      public FrameworkConfig Framework { get; init; } = new();
      public ModelConfig Model { get; init; } = new();
      public AgentsConfig Agents { get; init; } = new();
      public RoutingConfig Routing { get; init; } = new();
      public PipelineConfig Pipeline { get; init; } = new();
      public ObservabilityConfig Observability { get; init; } = new();
      public CachingConfig Caching { get; init; } = new();
  }
  ```

- [ ] **1.2.2** Implement hierarchical config loading
  - Search order: project local → project root → user global → enterprise
  - Merge strategy: deep merge with local overriding global

- [ ] **1.2.3** Create `ConfigurationLoader` class
  ```csharp
  public class ConfigurationLoader
  {
      public IronbeesConfig Load(string? configPath = null);
      private IronbeesConfig LoadFromFile(string path);
      private IronbeesConfig MergeConfigs(IronbeesConfig lower, IronbeesConfig higher);
  }
  ```

#### 1.3 File-System Agent Loader
- [ ] **1.3.1** Define agent directory convention
  ```
  /agents/{agent-name}/
    agent.yaml           # Required
    system-prompt.md     # Required
    tools.md             # Optional
    mcp-config.json      # Optional
    examples/            # Optional
  ```

- [ ] **1.3.2** Implement `IAgentLoader` interface
  ```csharp
  public interface IAgentLoader
  {
      Task<AgentConfig> LoadConfigAsync(string agentPath);
      Task<IReadOnlyList<AgentConfig>> LoadAllConfigsAsync(string agentsDirectory);
      Task<bool> ValidateAgentDirectoryAsync(string agentPath);
  }
  ```

- [ ] **1.3.3** Implement `FileSystemAgentLoader`
  - Parse agent.yaml with YamlDotNet
  - Read system-prompt.md as text
  - Validate required files exist
  - Handle missing optional files gracefully

- [ ] **1.3.4** Add validation logic
  - Check required fields (name, description, version)
  - Validate model configuration
  - Ensure system prompt is not empty
  - Validate YAML schema

#### 1.4 Error Handling
- [ ] **1.4.1** Define custom exceptions
  ```csharp
  public class AgentNotFoundException : Exception;
  public class AgentConfigurationException : Exception;
  public class AgentLoadException : Exception;
  public class InvalidAgentDirectoryException : Exception;
  ```

- [ ] **1.4.2** Implement error handling patterns
  - Try-catch with specific exception types
  - Logging integration
  - User-friendly error messages

#### 1.5 Unit Tests
- [ ] **1.5.1** AgentRegistry tests
  - Register and retrieve agents
  - Thread-safety tests
  - Duplicate registration handling

- [ ] **1.5.2** ConfigurationLoader tests
  - Single config file loading
  - Hierarchical config merging
  - Missing config file handling

- [ ] **1.5.3** FileSystemAgentLoader tests
  - Valid agent directory loading
  - Missing required files
  - Invalid YAML parsing
  - Empty system prompt handling

### Acceptance Criteria
- ✅ All core interfaces defined and documented
- ✅ AgentRegistry thread-safe implementation
- ✅ Config loading supports hierarchical discovery
- ✅ Agent loader parses all convention files correctly
- ✅ Unit test coverage >80%
- ✅ All tests pass

---

## Phase 2: Agent Framework Integration

**Goal**: Microsoft Agent Framework 통합 및 에이전트 실행

**Duration**: 5-7 days

**Dependencies**: Phase 1 완료

### Tasks

#### 2.1 Framework Adapter Interface
- [ ] **2.1.1** Define `ILLMFrameworkAdapter` interface
  ```csharp
  public interface ILLMFrameworkAdapter
  {
      Task<IAgent> CreateAgentAsync(
          AgentConfig config,
          CancellationToken cancellationToken = default);

      Task<string> RunAsync(
          IAgent agent,
          string input,
          CancellationToken cancellationToken = default);

      IAsyncEnumerable<string> StreamAsync(
          IAgent agent,
          string input,
          [EnumeratorCancellation] CancellationToken cancellationToken = default);
  }
  ```

#### 2.2 Agent Framework Adapter Implementation
- [ ] **2.2.1** Create `AgentFrameworkAdapter` class
  ```csharp
  public class AgentFrameworkAdapter : ILLMFrameworkAdapter
  {
      private readonly AzureOpenAIClient _client;
      private readonly ILogger<AgentFrameworkAdapter> _logger;

      public AgentFrameworkAdapter(
          AzureOpenAIClient client,
          ILogger<AgentFrameworkAdapter> logger)
      {
          _client = client;
          _logger = logger;
      }

      public async Task<IAgent> CreateAgentAsync(
          AgentConfig config,
          CancellationToken ct = default)
      {
          // MS Agent Framework 패턴:
          // AzureOpenAIClient → GetChatClient → CreateAIAgent
          var chatClient = _client.GetChatClient(config.Model.Deployment);

          var innerAgent = chatClient.CreateAIAgent(
              name: config.Name,
              instructions: config.SystemPrompt);

          _logger.LogInformation("Created agent: {AgentName}", config.Name);

          return new AgentFrameworkAgentWrapper(innerAgent, config);
      }

      public async Task<string> RunAsync(
          IAgent agent,
          string input,
          CancellationToken ct = default)
      {
          var wrapper = (AgentFrameworkAgentWrapper)agent;
          var response = await wrapper.InnerAgent.RunAsync(input, ct);
          return response.Text; // MS Agent Framework 패턴
      }

      public async IAsyncEnumerable<string> StreamAsync(
          IAgent agent,
          string input,
          [EnumeratorCancellation] CancellationToken ct = default)
      {
          var wrapper = (AgentFrameworkAgentWrapper)agent;
          await foreach (var update in wrapper.InnerAgent.RunStreamingAsync(input, ct))
          {
              yield return update.Text; // MS Agent Framework 패턴
          }
      }
  }
  ```

- [ ] **2.2.2** Implement `CreateAgentAsync` with MS Agent Framework patterns
  - Use `GetChatClient(deployment)` pattern (Context7 확인됨)
  - Use `CreateAIAgent(name, instructions)` (최신 패턴)
  - Wrap in `AgentFrameworkAgentWrapper`
  - Handle authentication (AzureCliCredential / ManagedIdentity)

- [ ] **2.2.3** Implement `AgentFrameworkAgentWrapper`
  ```csharp
  internal class AgentFrameworkAgentWrapper : IAgent
  {
      internal AIAgent InnerAgent { get; }
      private readonly AgentConfig _config;

      public string Name => _config.Name;
      public string Description => _config.Description;
      public AgentConfig Configuration => _config;

      public AgentFrameworkAgentWrapper(AIAgent innerAgent, AgentConfig config)
      {
          InnerAgent = innerAgent;
          _config = config;
      }

      public async Task<string> RunAsync(string input, CancellationToken ct = default)
      {
          // MS Agent Framework RunAsync returns AgentRunResponse with .Text property
          var response = await InnerAgent.RunAsync(input, ct);
          return response.Text;
      }

      public async IAsyncEnumerable<string> StreamAsync(
          string input,
          [EnumeratorCancellation] CancellationToken ct = default)
      {
          // MS Agent Framework RunStreamingAsync pattern
          await foreach (var update in InnerAgent.RunStreamingAsync(input, ct))
          {
              yield return update.Text;
          }
      }
  }
  ```

#### 2.3 Authentication Configuration
- [ ] **2.3.1** Support multiple auth methods
  - AzureCliCredential (development)
  - ManagedIdentity (production)
  - ApiKeyCredential (testing)

- [ ] **2.3.2** Create `AuthenticationProvider` class
  ```csharp
  public class AuthenticationProvider
  {
      public TokenCredential GetCredential(string authMethod)
      {
          return authMethod.ToLower() switch
          {
              "azure-cli" => new AzureCliCredential(),
              "managed-identity" => new ManagedIdentityCredential(),
              "default" => new DefaultAzureCredential(),
              _ => throw new ArgumentException($"Unknown auth method: {authMethod}")
          };
      }
  }
  ```

#### 2.4 Orchestrator Core
- [ ] **2.4.1** Create `IAgentOrchestrator` interface
  ```csharp
  public interface IAgentOrchestrator
  {
      Task LoadAgentsAsync(
          string? agentsDirectory = null,
          CancellationToken cancellationToken = default);

      IReadOnlyCollection<string> ListAgents();

      Task<string> ProcessAsync(
          string input,
          string? agentName = null,
          CancellationToken cancellationToken = default);

      IAsyncEnumerable<string> StreamAsync(
          string input,
          string? agentName = null,
          [EnumeratorCancellation] CancellationToken cancellationToken = default);
  }
  ```

- [ ] **2.4.2** Implement `AgentOrchestrator` class
  ```csharp
  public class AgentOrchestrator : IAgentOrchestrator
  {
      private readonly IAgentLoader _loader;
      private readonly ILLMFrameworkAdapter _adapter;
      private readonly IAgentRegistry _registry;
      private readonly IronbeesConfig _config;
      private readonly ILogger<AgentOrchestrator> _logger;

      // Implementation...
  }
  ```

- [ ] **2.4.3** Implement agent loading workflow
  ```csharp
  public async Task LoadAgentsAsync(string? directory = null, CancellationToken ct = default)
  {
      directory ??= _config.Agents.Directory;

      var configs = await _loader.LoadAllConfigsAsync(directory);

      foreach (var config in configs)
      {
          var agent = await _adapter.CreateAgentAsync(config, ct);
          _registry.Register(config.Name, agent);
          _logger.LogInformation("Loaded agent: {AgentName}", config.Name);
      }
  }
  ```

- [ ] **2.4.4** Implement basic execution (manual agent selection)
  ```csharp
  public async Task<string> ProcessAsync(
      string input,
      string? agentName = null,
      CancellationToken ct = default)
  {
      if (agentName == null)
          throw new ArgumentException("Agent name required in Phase 2");

      if (!_registry.TryGet(agentName, out var agent))
          throw new AgentNotFoundException(agentName);

      return await agent.RunAsync(input, ct);
  }
  ```

#### 2.5 Dependency Injection Setup
- [ ] **2.5.1** Create service registration extension
  ```csharp
  public static class IronbeesServiceCollectionExtensions
  {
      public static IServiceCollection AddIronbees(
          this IServiceCollection services,
          Action<IronbeesOptions>? configure = null)
      {
          var options = new IronbeesOptions();
          configure?.Invoke(options);

          services.AddSingleton<IAgentRegistry, AgentRegistry>();
          services.AddSingleton<IAgentLoader, FileSystemAgentLoader>();
          services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

          // Configure Azure OpenAI client
          services.AddSingleton(sp =>
          {
              var config = sp.GetRequiredService<IronbeesConfig>();
              var credential = new AuthenticationProvider()
                  .GetCredential(config.Model.Azure_OpenAI.Authentication);

              return new AzureOpenAIClient(
                  new Uri(config.Model.Azure_OpenAI.Endpoint),
                  credential);
          });

          services.AddSingleton<ILLMFrameworkAdapter, AgentFrameworkAdapter>();

          return services;
      }
  }
  ```

#### 2.6 Integration Tests
- [ ] **2.6.1** Setup test infrastructure
  - Use TestContainers or mock Azure OpenAI
  - Create test agent directories
  - Configure test credentials

- [ ] **2.6.2** Agent loading tests
  - Load single agent
  - Load multiple agents
  - Handle duplicate agent names

- [ ] **2.6.3** Agent execution tests
  - Run simple query
  - Streaming execution
  - Cancellation token handling

- [ ] **2.6.4** Error handling tests
  - Missing agent
  - Invalid configuration
  - Network failures

#### 2.7 Example Implementation
- [ ] **2.7.1** Create sample agent: `coding-agent`
  ```yaml
  # agents/coding-agent/agent.yaml
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
  tags:
    - coding
    - development
  ```

  ```markdown
  # agents/coding-agent/system-prompt.md
  You are an expert software developer specializing in C# and .NET.

  Your capabilities:
  - Write clean, maintainable, well-documented code
  - Follow SOLID principles and design patterns
  - Provide code reviews with actionable feedback
  - Debug complex issues systematically
  - Explain technical decisions clearly

  Always:
  - Use latest C# features appropriately
  - Include XML documentation comments
  - Consider error handling and edge cases
  - Follow .NET naming conventions
  ```

- [ ] **2.7.2** Create console app example
  ```csharp
  // examples/basic-usage/Program.cs
  var services = new ServiceCollection();
  services.AddLogging(builder => builder.AddConsole());
  services.AddIronbees(options =>
  {
      options.AgentsDirectory = "./agents";
  });

  var provider = services.BuildServiceProvider();
  var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

  await orchestrator.LoadAgentsAsync();

  Console.WriteLine("Available agents:");
  foreach (var agent in orchestrator.ListAgents())
  {
      Console.WriteLine($"  - {agent}");
  }

  var response = await orchestrator.ProcessAsync(
      "Write a C# method to calculate fibonacci numbers",
      agentName: "coding-agent");

  Console.WriteLine(response);
  ```

### Acceptance Criteria
- ✅ Agent Framework adapter fully functional
- ✅ Orchestrator loads agents from filesystem
- ✅ Manual agent selection works
- ✅ Both sync and streaming execution supported
- ✅ DI integration configured
- ✅ Integration tests pass with real Azure OpenAI
- ✅ Example console app runs successfully
- ✅ Documentation updated with usage examples

---

## Phase 2.5: System Agents Foundation

**Goal**: 내장 시스템 에이전트 구현 (자동 구성 에이전트)

**Duration**: 3-5 days

**Dependencies**: Phase 2 완료

### Overview

System Agents는 ironbees에 내장된 범용 에이전트입니다:
- **자동 구성**: 설치 후 별도 설정 없이 바로 사용 가능
- **검증된 프롬프트**: Production-ready 시스템 프롬프트
- **범용 기능**: 요약, 검색, 번역, 파일 탐색, 코드 리뷰 등
- **커스터마이징 가능**: User agents로 오버라이드 가능

### Tasks

#### 2.5.1 System Agents Package Setup
- [ ] **2.5.1.1** Create Ironbees.SystemAgents project
  ```bash
  dotnet new classlib -f net9.0 -n Ironbees.SystemAgents -o src/Ironbees.SystemAgents
  dotnet sln add src/Ironbees.SystemAgents/Ironbees.SystemAgents.csproj
  dotnet add src/Ironbees.SystemAgents reference src/Ironbees.Core
  ```

- [ ] **2.5.1.2** Configure embedded resources
  ```xml
  <!-- Ironbees.SystemAgents/Ironbees.SystemAgents.csproj -->
  <ItemGroup>
    <EmbeddedResource Include="Agents\**\*.yaml" />
    <EmbeddedResource Include="Agents\**\*.md" />
    <EmbeddedResource Include="Agents\**\*.json" />
  </ItemGroup>
  ```

- [ ] **2.5.1.3** Create system agents directory structure
  ```
  src/Ironbees.SystemAgents/
    Agents/
      summarizer/
        agent.yaml
        system-prompt.md
      web-search/
        agent.yaml
        system-prompt.md
        mcp-config.json
      file-explorer/
        agent.yaml
        system-prompt.md
      translator/
        agent.yaml
        system-prompt.md
      code-reviewer/
        agent.yaml
        system-prompt.md
  ```

#### 2.5.2 Core System Agents Implementation

- [ ] **2.5.2.1** Implement `summarizer` agent
  ```yaml
  # Agents/summarizer/agent.yaml
  name: summarizer
  description: Text summarization agent for concise content extraction
  version: 1.0.0
  model:
    provider: azure-openai
    deployment: gpt-4o-mini
    temperature: 0.3
    max_tokens: 2000
  capabilities:
    - text-summarization
    - key-points-extraction
  tags:
    - system
    - summarization
    - productivity
  ```

  ```markdown
  # Agents/summarizer/system-prompt.md
  You are a professional summarization agent specialized in extracting key information from text.

  Your task:
  - Identify main ideas and key points
  - Create concise, accurate summaries
  - Preserve critical information and context
  - Use clear, structured formatting

  Output format:
  ## Summary
  [2-3 sentence overview]

  ## Key Points
  - [Point 1]
  - [Point 2]
  - [Point 3]

  ## Recommendations (if applicable)
  [Action items or next steps]
  ```

- [ ] **2.5.2.2** Implement `web-search` agent
  ```yaml
  # Agents/web-search/agent.yaml
  name: web-search
  description: Web search agent using Tavily MCP for real-time information
  version: 1.0.0
  model:
    provider: azure-openai
    deployment: gpt-4o
    temperature: 0.7
    max_tokens: 4000
  capabilities:
    - web-search
    - fact-checking
    - research
  tags:
    - system
    - search
    - research
  ```

  ```json
  # Agents/web-search/mcp-config.json
  {
    "mcpServers": {
      "tavily": {
        "command": "npx",
        "args": ["-y", "@modelcontextprotocol/server-tavily"],
        "env": {
          "TAVILY_API_KEY": "${TAVILY_API_KEY}"
        }
      }
    }
  }
  ```

- [ ] **2.5.2.3** Implement `file-explorer` agent
  ```yaml
  # Agents/file-explorer/agent.yaml
  name: file-explorer
  description: File system exploration and code navigation agent
  version: 1.0.0
  model:
    provider: azure-openai
    deployment: gpt-4o
    temperature: 0.5
    max_tokens: 4000
  capabilities:
    - file-exploration
    - code-navigation
    - project-structure-analysis
  tags:
    - system
    - filesystem
    - development
  ```

- [ ] **2.5.2.4** Implement `translator` agent
  ```yaml
  # Agents/translator/agent.yaml
  name: translator
  description: Multi-language translation agent supporting 50+ languages
  version: 1.0.0
  model:
    provider: azure-openai
    deployment: gpt-4o
    temperature: 0.3
    max_tokens: 4000
  capabilities:
    - translation
    - language-detection
    - localization
  tags:
    - system
    - translation
    - localization
  metadata:
    supported_languages:
      - en
      - ko
      - ja
      - zh
      - es
      - fr
      - de
  ```

- [ ] **2.5.2.5** Implement `code-reviewer` agent
  ```yaml
  # Agents/code-reviewer/agent.yaml
  name: code-reviewer
  description: Code quality analysis and review agent
  version: 1.0.0
  model:
    provider: azure-openai
    deployment: gpt-4o
    temperature: 0.5
    max_tokens: 4000
  capabilities:
    - code-review
    - security-analysis
    - best-practices
  tags:
    - system
    - code-quality
    - development
  ```

#### 2.5.3 SystemAgentLoader Implementation

- [ ] **2.5.3.1** Create `SystemAgentLoader` class
  ```csharp
  public class SystemAgentLoader : IAgentLoader
  {
      private readonly Assembly _assembly;
      private readonly ILogger<SystemAgentLoader> _logger;

      public SystemAgentLoader(
          ILogger<SystemAgentLoader> logger,
          Assembly? assembly = null)
      {
          _assembly = assembly ?? typeof(SystemAgentLoader).Assembly;
          _logger = logger;
      }

      public async Task<IReadOnlyList<AgentConfig>> LoadAllConfigsAsync(
          string? agentsDirectory = null)
      {
          var configs = new List<AgentConfig>();

          // Load embedded system agents from resources
          var resourceNames = _assembly.GetManifestResourceNames()
              .Where(r => r.Contains("Agents.") && r.EndsWith("agent.yaml"))
              .ToList();

          _logger.LogInformation(
              "Found {Count} system agent resources",
              resourceNames.Count);

          foreach (var resourceName in resourceNames)
          {
              try
              {
                  var config = await LoadEmbeddedAgentAsync(resourceName);
                  configs.Add(config);
                  _logger.LogDebug(
                      "Loaded system agent: {AgentName}",
                      config.Name);
              }
              catch (Exception ex)
              {
                  _logger.LogWarning(
                      ex,
                      "Failed to load system agent from resource: {ResourceName}",
                      resourceName);
              }
          }

          return configs;
      }

      private async Task<AgentConfig> LoadEmbeddedAgentAsync(string resourceName)
      {
          using var stream = _assembly.GetManifestResourceStream(resourceName)
              ?? throw new InvalidOperationException(
                  $"Resource not found: {resourceName}");

          using var reader = new StreamReader(stream);
          var yamlContent = await reader.ReadToEndAsync();

          var deserializer = new DeserializerBuilder()
              .WithNamingConvention(CamelCaseNamingConvention.Instance)
              .Build();

          var config = deserializer.Deserialize<AgentConfig>(yamlContent);

          // Load system prompt from embedded resource
          var promptResourceName = resourceName.Replace("agent.yaml", "system-prompt.md");
          config = config with
          {
              SystemPrompt = await LoadEmbeddedTextAsync(promptResourceName)
          };

          return config;
      }

      private async Task<string> LoadEmbeddedTextAsync(string resourceName)
      {
          using var stream = _assembly.GetManifestResourceStream(resourceName)
              ?? throw new InvalidOperationException(
                  $"Resource not found: {resourceName}");

          using var reader = new StreamReader(stream);
          return await reader.ReadToEndAsync();
      }
  }
  ```

#### 2.5.4 CompositeAgentLoader Implementation

- [ ] **2.5.4.1** Create `CompositeAgentLoader` class
  ```csharp
  public class CompositeAgentLoader : IAgentLoader
  {
      private readonly SystemAgentLoader _systemAgentLoader;
      private readonly FileSystemAgentLoader _userAgentLoader;
      private readonly ILogger<CompositeAgentLoader> _logger;

      public CompositeAgentLoader(
          SystemAgentLoader systemAgentLoader,
          FileSystemAgentLoader userAgentLoader,
          ILogger<CompositeAgentLoader> logger)
      {
          _systemAgentLoader = systemAgentLoader;
          _userAgentLoader = userAgentLoader;
          _logger = logger;
      }

      public async Task<IReadOnlyList<AgentConfig>> LoadAllConfigsAsync(
          string? agentsDirectory = null)
      {
          var allConfigs = new Dictionary<string, AgentConfig>(
              StringComparer.OrdinalIgnoreCase);

          // Phase 1: Load system agents (lower priority)
          _logger.LogInformation("Loading system agents...");
          var systemConfigs = await _systemAgentLoader.LoadAllConfigsAsync();

          foreach (var config in systemConfigs)
          {
              allConfigs[config.Name] = config;
              _logger.LogDebug(
                  "Loaded system agent: {AgentName}",
                  config.Name);
          }

          // Phase 2: Load user agents (higher priority, can override)
          if (!string.IsNullOrEmpty(agentsDirectory) &&
              Directory.Exists(agentsDirectory))
          {
              _logger.LogInformation(
                  "Loading user agents from {Directory}...",
                  agentsDirectory);

              var userConfigs = await _userAgentLoader
                  .LoadAllConfigsAsync(agentsDirectory);

              foreach (var config in userConfigs)
              {
                  if (allConfigs.ContainsKey(config.Name))
                  {
                      _logger.LogInformation(
                          "User agent '{AgentName}' overrides system agent",
                          config.Name);
                  }

                  allConfigs[config.Name] = config; // Override!
              }
          }

          _logger.LogInformation(
              "Total agents loaded: {Count} " +
              "(System: {SystemCount}, User: {UserCount})",
              allConfigs.Count,
              systemConfigs.Count,
              allConfigs.Count - systemConfigs.Count);

          return allConfigs.Values.ToList();
      }
  }
  ```

- [ ] **2.5.4.2** Update DI registration to use CompositeAgentLoader
  ```csharp
  public static IServiceCollection AddIronbees(
      this IServiceCollection services,
      Action<IronbeesOptions>? configure = null)
  {
      // ... existing registrations ...

      // Register both loaders
      services.AddSingleton<SystemAgentLoader>();
      services.AddSingleton<FileSystemAgentLoader>();

      // Register composite as primary IAgentLoader
      services.AddSingleton<IAgentLoader, CompositeAgentLoader>();

      return services;
  }
  ```

#### 2.5.5 Testing & Validation

- [ ] **2.5.5.1** Unit tests for SystemAgentLoader
  - Load all embedded system agents
  - Validate agent configurations
  - Handle missing resources gracefully

- [ ] **2.5.5.2** Unit tests for CompositeAgentLoader
  - System agents load first
  - User agents override system agents
  - Priority order preserved

- [ ] **2.5.5.3** Integration tests
  - Load system agents without user agents directory
  - Load with user override (create user `summarizer`)
  - Verify correct agent is selected

- [ ] **2.5.5.4** Validation tests
  - All 5 system agents load successfully
  - System prompts are valid and non-empty
  - MCP configurations parse correctly

#### 2.5.6 Documentation & Examples

- [ ] **2.5.6.1** Update README.md with system agents
  ```markdown
  ## Built-in System Agents

  ironbees includes 5 production-ready system agents:

  | Agent | Purpose | Model |
  |-------|---------|-------|
  | `summarizer` | Text summarization | gpt-4o-mini |
  | `web-search` | Web search with Tavily | gpt-4o |
  | `file-explorer` | File system navigation | gpt-4o |
  | `translator` | Multi-language translation | gpt-4o |
  | `code-reviewer` | Code quality analysis | gpt-4o |

  ### Usage

  System agents work automatically:

  ```csharp
  var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();
  await orchestrator.LoadAgentsAsync();

  // Use system agent directly
  var summary = await orchestrator.ProcessAsync(
      "Summarize this document...",
      agentName: "summarizer");
  ```

  ### Customization

  Override system agents by creating user agents:

  ```
  /agents/summarizer/   # Your custom summarizer
    agent.yaml          # Overrides built-in
    system-prompt.md
  ```
  ```

- [ ] **2.5.6.2** Create examples/system-agents demo
  ```csharp
  // examples/system-agents/Program.cs
  var services = new ServiceCollection();
  services.AddLogging(builder => builder.AddConsole());
  services.AddIronbees();

  var provider = services.BuildServiceProvider();
  var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

  await orchestrator.LoadAgentsAsync();

  Console.WriteLine("System Agents Demo\n");
  Console.WriteLine("Available agents:");
  foreach (var agent in orchestrator.ListAgents())
  {
      Console.WriteLine($"  • {agent}");
  }

  // Test summarizer
  Console.WriteLine("\n=== Summarizer Agent ===");
  var summary = await orchestrator.ProcessAsync(
      "Summarize: ironbees is a convention-based...",
      agentName: "summarizer");
  Console.WriteLine(summary);

  // Test translator
  Console.WriteLine("\n=== Translator Agent ===");
  var translation = await orchestrator.ProcessAsync(
      "Translate to Korean: Hello, how are you?",
      agentName: "translator");
  Console.WriteLine(translation);
  ```

### Acceptance Criteria
- ✅ Ironbees.SystemAgents package created with embedded resources
- ✅ 5 core system agents implemented with validated prompts
- ✅ SystemAgentLoader loads from embedded resources
- ✅ CompositeAgentLoader supports priority-based loading
- ✅ User agents can override system agents
- ✅ All unit and integration tests pass
- ✅ Documentation includes system agents usage guide
- ✅ Example project demonstrates system agents

### Notes
- System agents are **optional** (separate NuGet package)
- Designed for v1.1+ release (after core framework is stable)
- MCP integration tested with Tavily for web-search agent
- Translation agent supports 50+ languages via GPT-4o

---

## Phase 3: Routing & Selection

**Goal**: 인텔리전트 에이전트 자동 선택 구현

**Duration**: 7-10 days

**Dependencies**: Phase 2 완료

### Tasks

#### 3.1 Agent Selector Interface
- [ ] **3.1.1** Define `IAgentSelector` interface
  ```csharp
  public interface IAgentSelector
  {
      Task<IAgent> SelectAgentAsync(
          string input,
          IReadOnlyCollection<IAgent> availableAgents,
          CancellationToken cancellationToken = default);

      Task<AgentSelectionResult> SelectWithConfidenceAsync(
          string input,
          IReadOnlyCollection<IAgent> availableAgents,
          CancellationToken cancellationToken = default);
  }

  public record AgentSelectionResult(
      IAgent SelectedAgent,
      double Confidence,
      string Reasoning,
      Dictionary<string, double> AllScores);
  ```

#### 3.2 Semantic Router Implementation
- [ ] **3.2.1** Add embedding generation dependencies
  ```xml
  <PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
  ```

- [ ] **3.2.2** Create `IEmbeddingGenerator` interface
  ```csharp
  public interface IEmbeddingGenerator
  {
      Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
          string text,
          CancellationToken cancellationToken = default);

      Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(
          IEnumerable<string> texts,
          CancellationToken cancellationToken = default);
  }
  ```

- [ ] **3.2.3** Implement `AzureOpenAIEmbeddingGenerator`
  ```csharp
  public class AzureOpenAIEmbeddingGenerator : IEmbeddingGenerator
  {
      private readonly EmbeddingClient _client;

      public AzureOpenAIEmbeddingGenerator(
          AzureOpenAIClient openAIClient,
          string embeddingDeployment = "text-embedding-3-small")
      {
          _client = openAIClient.GetEmbeddingClient(embeddingDeployment);
      }

      public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
          string text,
          CancellationToken ct = default)
      {
          var response = await _client.GenerateEmbeddingAsync(text, ct);
          return response.Value.Vector;
      }
  }
  ```

- [ ] **3.2.4** Implement `SemanticAgentSelector`
  ```csharp
  public class SemanticAgentSelector : IAgentSelector
  {
      private readonly IEmbeddingGenerator _embedder;
      private readonly ILogger<SemanticAgentSelector> _logger;
      private readonly double _similarityThreshold;

      // Cache agent embeddings
      private readonly ConcurrentDictionary<string, ReadOnlyMemory<float>> _agentEmbeddings = new();

      public async Task<IAgent> SelectAgentAsync(
          string input,
          IReadOnlyCollection<IAgent> agents,
          CancellationToken ct = default)
      {
          // Generate input embedding
          var inputEmbedding = await _embedder.GenerateEmbeddingAsync(input, ct);

          // Ensure all agents have embeddings
          await EnsureAgentEmbeddingsAsync(agents, ct);

          // Calculate similarities
          var scores = new Dictionary<string, double>();
          foreach (var agent in agents)
          {
              if (_agentEmbeddings.TryGetValue(agent.Name, out var agentEmbedding))
              {
                  var similarity = CosineSimilarity(inputEmbedding, agentEmbedding);
                  scores[agent.Name] = similarity;
              }
          }

          // Select best match
          var best = scores.MaxBy(x => x.Value);
          if (best.Value < _similarityThreshold)
          {
              throw new NoSuitableAgentException(
                  $"No agent found with confidence above {_similarityThreshold}");
          }

          return agents.First(a => a.Name == best.Key);
      }

      private async Task EnsureAgentEmbeddingsAsync(
          IReadOnlyCollection<IAgent> agents,
          CancellationToken ct)
      {
          var agentsNeedingEmbeddings = agents
              .Where(a => !_agentEmbeddings.ContainsKey(a.Name))
              .ToList();

          if (agentsNeedingEmbeddings.Count == 0)
              return;

          // Generate embeddings for agent descriptions + capabilities
          var texts = agentsNeedingEmbeddings
              .Select(a => $"{a.Description} {string.Join(" ", a.Configuration.Capabilities)}")
              .ToList();

          var embeddings = await _embedder.GenerateBatchEmbeddingsAsync(texts, ct);

          for (int i = 0; i < agentsNeedingEmbeddings.Count; i++)
          {
              _agentEmbeddings[agentsNeedingEmbeddings[i].Name] = embeddings[i];
          }
      }

      private static double CosineSimilarity(
          ReadOnlyMemory<float> a,
          ReadOnlyMemory<float> b)
      {
          var aSpan = a.Span;
          var bSpan = b.Span;

          double dotProduct = 0;
          double magnitudeA = 0;
          double magnitudeB = 0;

          for (int i = 0; i < aSpan.Length; i++)
          {
              dotProduct += aSpan[i] * bSpan[i];
              magnitudeA += aSpan[i] * aSpan[i];
              magnitudeB += bSpan[i] * bSpan[i];
          }

          return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
      }
  }
  ```

#### 3.3 LLM-Based Router (Fallback)
- [ ] **3.3.1** Implement `LLMAgentSelector`
  ```csharp
  public class LLMAgentSelector : IAgentSelector
  {
      private readonly AzureOpenAIClient _client;
      private readonly ILogger<LLMAgentSelector> _logger;

      public async Task<IAgent> SelectAgentAsync(
          string input,
          IReadOnlyCollection<IAgent> agents,
          CancellationToken ct = default)
      {
          // Create prompt with agent descriptions
          var agentDescriptions = string.Join("\n", agents.Select((a, i) =>
              $"{i + 1}. {a.Name}: {a.Description} (capabilities: {string.Join(", ", a.Configuration.Capabilities)})"));

          var prompt = $@"
User query: {input}

Available agents:
{agentDescriptions}

Select the most appropriate agent for this query. Respond with ONLY the agent number.
";

          var chatClient = _client.GetChatClient("gpt-4o-mini");
          var response = await chatClient.CompleteChatAsync(prompt, ct);

          // Parse response and return agent
          if (int.TryParse(response.Value.Content[0].Text.Trim(), out int index))
          {
              return agents.ElementAt(index - 1);
          }

          throw new AgentSelectionException("Failed to parse LLM selection response");
      }
  }
  ```

#### 3.4 Hybrid Router (Semantic + LLM Fallback)
- [ ] **3.4.1** Implement `HybridAgentSelector`
  ```csharp
  public class HybridAgentSelector : IAgentSelector
  {
      private readonly SemanticAgentSelector _semanticSelector;
      private readonly LLMAgentSelector _llmSelector;
      private readonly double _confidenceThreshold;

      public async Task<IAgent> SelectAgentAsync(
          string input,
          IReadOnlyCollection<IAgent> agents,
          CancellationToken ct = default)
      {
          try
          {
              // Try semantic routing first (fast, cheap)
              var result = await _semanticSelector.SelectWithConfidenceAsync(input, agents, ct);

              if (result.Confidence >= _confidenceThreshold)
              {
                  return result.SelectedAgent;
              }

              // Fallback to LLM routing (slower, more expensive, better accuracy)
              return await _llmSelector.SelectAgentAsync(input, agents, ct);
          }
          catch (NoSuitableAgentException)
          {
              // Fallback to LLM routing
              return await _llmSelector.SelectAgentAsync(input, agents, ct);
          }
      }
  }
  ```

#### 3.5 Integrate into Orchestrator
- [ ] **3.5.1** Update `AgentOrchestrator.ProcessAsync` to support auto-selection
  ```csharp
  public async Task<string> ProcessAsync(
      string input,
      string? agentName = null,
      CancellationToken ct = default)
  {
      IAgent agent;

      if (agentName != null)
      {
          // Explicit agent selection
          if (!_registry.TryGet(agentName, out agent))
              throw new AgentNotFoundException(agentName);
      }
      else
      {
          // Automatic agent selection
          var availableAgents = _registry.ListAgents()
              .Select(name => _registry.Get(name)!)
              .ToList();

          agent = await _selector.SelectAgentAsync(input, availableAgents, ct);
          _logger.LogInformation("Auto-selected agent: {AgentName}", agent.Name);
      }

      return await agent.RunAsync(input, ct);
  }
  ```

- [ ] **3.5.2** Add selector configuration to DI
  ```csharp
  services.AddSingleton<IAgentSelector>(sp =>
  {
      var config = sp.GetRequiredService<IronbeesConfig>();

      return config.Routing.Strategy.ToLower() switch
      {
          "semantic" => new SemanticAgentSelector(
              sp.GetRequiredService<IEmbeddingGenerator>(),
              sp.GetRequiredService<ILogger<SemanticAgentSelector>>(),
              config.Routing.Semantic.SimilarityThreshold),

          "llm" => new LLMAgentSelector(
              sp.GetRequiredService<AzureOpenAIClient>(),
              sp.GetRequiredService<ILogger<LLMAgentSelector>>()),

          "hybrid" => new HybridAgentSelector(
              sp.GetRequiredService<SemanticAgentSelector>(),
              sp.GetRequiredService<LLMAgentSelector>(),
              config.Routing.Semantic.SimilarityThreshold),

          _ => throw new ArgumentException($"Unknown routing strategy: {config.Routing.Strategy}")
      };
  });
  ```

#### 3.6 Telemetry & Metrics
- [ ] **3.6.1** Add routing metrics
  ```csharp
  public class RoutingMetrics
  {
      private readonly Meter _meter;
      private readonly Counter<long> _selectionCounter;
      private readonly Histogram<double> _selectionDuration;
      private readonly Histogram<double> _confidenceScore;

      public RoutingMetrics()
      {
          _meter = new Meter("ironbees.routing");
          _selectionCounter = _meter.CreateCounter<long>("selections");
          _selectionDuration = _meter.CreateHistogram<double>("selection_duration_ms");
          _confidenceScore = _meter.CreateHistogram<double>("confidence_score");
      }

      public void RecordSelection(string strategy, string agentName, double confidence, double durationMs)
      {
          _selectionCounter.Add(1, new KeyValuePair<string, object?>("strategy", strategy));
          _selectionDuration.Record(durationMs);
          _confidenceScore.Record(confidence);
      }
  }
  ```

#### 3.7 Unit & Integration Tests
- [ ] **3.7.1** Semantic router tests
  - Exact match selection
  - Similar input matching
  - No suitable agent handling
  - Embedding cache behavior

- [ ] **3.7.2** LLM router tests
  - Correct agent selection
  - Ambiguous query handling
  - Parse error handling

- [ ] **3.7.3** Hybrid router tests
  - High confidence semantic selection
  - Low confidence LLM fallback
  - Performance comparison

- [ ] **3.7.4** End-to-end routing tests
  - Auto-selection with multiple agents
  - Manual override still works
  - Telemetry data recorded correctly

### Acceptance Criteria
- ✅ Semantic routing achieves <100ms latency
- ✅ LLM routing falls back correctly
- ✅ Hybrid router balances cost and accuracy
- ✅ Auto-selection works in orchestrator
- ✅ Routing telemetry collected
- ✅ All tests pass with >80% coverage
- ✅ Documentation includes routing strategy guide

---

## Phase 4: Pipeline & Security

**Goal**: 전처리/후처리 파이프라인 및 가드레일 구현

**Duration**: 5-7 days

**Dependencies**: Phase 3 완료

### Tasks

#### 4.1 Pipeline Abstractions
- [ ] **4.1.1** Define `IPreprocessor` interface
  ```csharp
  public interface IPreprocessor
  {
      Task<string> ProcessAsync(
          string input,
          PipelineContext context,
          CancellationToken cancellationToken = default);

      int Priority { get; } // Lower = runs first
      string Name { get; }
  }

  public class PipelineContext
  {
      public Dictionary<string, object> Metadata { get; } = new();
      public string? SelectedAgent { get; set; }
      public DateTime Timestamp { get; init; } = DateTime.UtcNow;
  }
  ```

- [ ] **4.1.2** Define `IPostprocessor` interface
  ```csharp
  public interface IPostprocessor
  {
      Task<string> ProcessAsync(
          string output,
          PipelineContext context,
          CancellationToken cancellationToken = default);

      int Priority { get; }
      string Name { get; }
  }
  ```

#### 4.2 Built-in Preprocessors
- [ ] **4.2.1** `InputValidationPreprocessor`
  ```csharp
  public class InputValidationPreprocessor : IPreprocessor
  {
      public string Name => "input-validation";
      public int Priority => 10;

      public Task<string> ProcessAsync(string input, PipelineContext ctx, CancellationToken ct)
      {
          // Length validation
          if (input.Length > 10_000)
              throw new InputTooLongException("Input exceeds 10K characters");

          if (string.IsNullOrWhiteSpace(input))
              throw new EmptyInputException("Input cannot be empty");

          return Task.FromResult(input);
      }
  }
  ```

- [ ] **4.2.2** `PiiMaskingPreprocessor`
  ```csharp
  public class PiiMaskingPreprocessor : IPreprocessor
  {
      public string Name => "pii-masking";
      public int Priority => 20;

      public Task<string> ProcessAsync(string input, PipelineContext ctx, CancellationToken ct)
      {
          // Email masking
          input = Regex.Replace(input, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[EMAIL]");

          // Phone number masking (US format)
          input = Regex.Replace(input, @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b", "[PHONE]");

          // SSN masking
          input = Regex.Replace(input, @"\b\d{3}-\d{2}-\d{4}\b", "[SSN]");

          // Credit card masking
          input = Regex.Replace(input, @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", "[CARD]");

          return Task.FromResult(input);
      }
  }
  ```

- [ ] **4.2.3** `ContextInjectionPreprocessor`
  ```csharp
  public class ContextInjectionPreprocessor : IPreprocessor
  {
      public string Name => "context-injection";
      public int Priority => 30;

      public Task<string> ProcessAsync(string input, PipelineContext ctx, CancellationToken ct)
      {
          ctx.Metadata["user_id"] = GetCurrentUserId();
          ctx.Metadata["session_id"] = GetSessionId();
          ctx.Metadata["timestamp"] = DateTime.UtcNow;
          ctx.Metadata["request_id"] = Guid.NewGuid();

          return Task.FromResult(input);
      }
  }
  ```

#### 4.3 Built-in Postprocessors
- [ ] **4.3.1** `OutputValidationPostprocessor`
  ```csharp
  public class OutputValidationPostprocessor : IPostprocessor
  {
      public string Name => "output-validation";
      public int Priority => 10;

      public Task<string> ProcessAsync(string output, PipelineContext ctx, CancellationToken ct)
      {
          // Check output is not empty
          if (string.IsNullOrWhiteSpace(output))
              throw new EmptyOutputException("Agent produced empty output");

          // Check for error patterns
          if (output.Contains("I cannot") || output.Contains("I'm unable"))
          {
              ctx.Metadata["refusal_detected"] = true;
          }

          return Task.FromResult(output);
      }
  }
  ```

- [ ] **4.3.2** `FormatConversionPostprocessor`
  ```csharp
  public class FormatConversionPostprocessor : IPostprocessor
  {
      public string Name => "format-conversion";
      public int Priority => 20;

      public Task<string> ProcessAsync(string output, PipelineContext ctx, CancellationToken ct)
      {
          // Ensure proper markdown formatting
          output = output.Trim();

          // Normalize line endings
          output = output.Replace("\r\n", "\n");

          // Remove excessive blank lines
          output = Regex.Replace(output, @"\n{3,}", "\n\n");

          return Task.FromResult(output);
      }
  }
  ```

- [ ] **4.3.3** `ComplianceCheckPostprocessor`
  ```csharp
  public class ComplianceCheckPostprocessor : IPostprocessor
  {
      private readonly List<string> _bannedPhrases;

      public string Name => "compliance-check";
      public int Priority => 30;

      public Task<string> ProcessAsync(string output, PipelineContext ctx, CancellationToken ct)
      {
          foreach (var phrase in _bannedPhrases)
          {
              if (output.Contains(phrase, StringComparison.OrdinalIgnoreCase))
              {
                  throw new ComplianceViolationException($"Output contains banned phrase: {phrase}");
              }
          }

          return Task.FromResult(output);
      }
  }
  ```

#### 4.4 Guardrails Integration
- [ ] **4.4.1** `JailbreakDetectionPreprocessor`
  ```csharp
  public class JailbreakDetectionPreprocessor : IPreprocessor
  {
      private readonly List<string> _jailbreakPatterns = new()
      {
          "ignore previous instructions",
          "disregard all prior",
          "system override",
          "admin mode",
          "sudo",
          "act as if"
      };

      public string Name => "jailbreak-detection";
      public int Priority => 5; // High priority

      public Task<string> ProcessAsync(string input, PipelineContext ctx, CancellationToken ct)
      {
          foreach (var pattern in _jailbreakPatterns)
          {
              if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
              {
                  throw new JailbreakAttemptException($"Detected jailbreak pattern: {pattern}");
              }
          }

          return Task.FromResult(input);
      }
  }
  ```

- [ ] **4.4.2** `ToxicityDetectionPostprocessor`
  ```csharp
  public class ToxicityDetectionPostprocessor : IPostprocessor
  {
      private readonly AzureOpenAIClient _client;

      public string Name => "toxicity-detection";
      public int Priority => 40;

      public async Task<string> ProcessAsync(string output, PipelineContext ctx, CancellationToken ct)
      {
          // Use Azure Content Safety or OpenAI Moderation API
          var moderationResult = await CheckToxicity(output, ct);

          if (moderationResult.IsToxic)
          {
              throw new ToxicOutputException("Output failed toxicity check");
          }

          return output;
      }

      private async Task<ModerationResult> CheckToxicity(string text, CancellationToken ct)
      {
          // Implementation using OpenAI Moderation API
          // Or Azure AI Content Safety
          throw new NotImplementedException();
      }
  }
  ```

#### 4.5 Pipeline Manager
- [ ] **4.5.1** Implement `PipelineManager`
  ```csharp
  public class PipelineManager
  {
      private readonly List<IPreprocessor> _preprocessors = new();
      private readonly List<IPostprocessor> _postprocessors = new();
      private readonly ILogger<PipelineManager> _logger;

      public void AddPreprocessor(IPreprocessor preprocessor)
      {
          _preprocessors.Add(preprocessor);
          _preprocessors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
      }

      public void AddPostprocessor(IPostprocessor postprocessor)
      {
          _postprocessors.Add(postprocessor);
          _postprocessors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
      }

      public async Task<string> RunPreprocessorsAsync(
          string input,
          PipelineContext context,
          CancellationToken ct = default)
      {
          foreach (var processor in _preprocessors)
          {
              try
              {
                  input = await processor.ProcessAsync(input, context, ct);
                  _logger.LogDebug("Preprocessor {Name} completed", processor.Name);
              }
              catch (Exception ex)
              {
                  _logger.LogError(ex, "Preprocessor {Name} failed", processor.Name);
                  throw;
              }
          }

          return input;
      }

      public async Task<string> RunPostprocessorsAsync(
          string output,
          PipelineContext context,
          CancellationToken ct = default)
      {
          foreach (var processor in _postprocessors)
          {
              try
              {
                  output = await processor.ProcessAsync(output, context, ct);
                  _logger.LogDebug("Postprocessor {Name} completed", processor.Name);
              }
              catch (Exception ex)
              {
                  _logger.LogError(ex, "Postprocessor {Name} failed", processor.Name);
                  throw;
              }
          }

          return output;
      }
  }
  ```

#### 4.6 Integrate into Orchestrator
- [ ] **4.6.1** Update `AgentOrchestrator` to use pipeline
  ```csharp
  public async Task<string> ProcessAsync(
      string input,
      string? agentName = null,
      CancellationToken ct = default)
  {
      var context = new PipelineContext();

      // Run preprocessors
      input = await _pipelineManager.RunPreprocessorsAsync(input, context, ct);

      // Select agent
      IAgent agent = /* ... agent selection logic ... */;
      context.SelectedAgent = agent.Name;

      // Execute agent
      var output = await agent.RunAsync(input, ct);

      // Run postprocessors
      output = await _pipelineManager.RunPostprocessorsAsync(output, context, ct);

      return output;
  }
  ```

- [ ] **4.6.2** Add fluent API for pipeline configuration
  ```csharp
  public interface IAgentOrchestrator
  {
      IAgentOrchestrator AddPreprocessor(IPreprocessor preprocessor);
      IAgentOrchestrator AddPostprocessor(IPostprocessor postprocessor);

      // ... other methods
  }
  ```

#### 4.7 Configuration-based Pipeline Setup
- [ ] **4.7.1** Load preprocessors/postprocessors from config
  ```yaml
  # ironbees.config.yaml
  pipeline:
    preprocessors:
      - name: jailbreak-detection
        enabled: true
      - name: pii-masking
        enabled: true
      - name: input-validation
        enabled: true
        options:
          max_length: 10000

    postprocessors:
      - name: output-validation
        enabled: true
      - name: format-conversion
        enabled: true
      - name: compliance-check
        enabled: true
        options:
          banned_phrases:
            - "confidential information"
            - "internal only"
  ```

- [ ] **4.7.2** Implement pipeline factory
  ```csharp
  public class PipelineFactory
  {
      public static PipelineManager CreateFromConfig(PipelineConfig config)
      {
          var manager = new PipelineManager();

          foreach (var preConfig in config.Preprocessors.Where(p => p.Enabled))
          {
              var preprocessor = CreatePreprocessor(preConfig);
              manager.AddPreprocessor(preprocessor);
          }

          foreach (var postConfig in config.Postprocessors.Where(p => p.Enabled))
          {
              var postprocessor = CreatePostprocessor(postConfig);
              manager.AddPostprocessor(postprocessor);
          }

          return manager;
      }
  }
  ```

#### 4.8 Tests
- [ ] **4.8.1** Preprocessor tests
  - Input validation edge cases
  - PII masking patterns
  - Jailbreak detection patterns

- [ ] **4.8.2** Postprocessor tests
  - Output validation
  - Format conversion
  - Compliance checking

- [ ] **4.8.3** Pipeline integration tests
  - Full pipeline execution
  - Error handling and rollback
  - Priority ordering

### Acceptance Criteria
- ✅ All built-in processors implemented and tested
- ✅ Pipeline manager executes in priority order
- ✅ Orchestrator integrates pipeline seamlessly
- ✅ Configuration-based pipeline setup works
- ✅ Guardrails prevent jailbreak attempts
- ✅ PII masking effective on common patterns
- ✅ Test coverage >80%

---

## Phase 5: Observability & Testing

**Goal**: 포괄적인 관찰성 및 테스트 프레임워크

**Duration**: 5-7 days

**Dependencies**: Phase 4 완료

### Tasks

#### 5.1 OpenTelemetry Integration
- [ ] **5.1.1** Add OpenTelemetry packages
  ```xml
  <PackageReference Include="OpenTelemetry" Version="1.*" />
  <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
  <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
  <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
  ```

- [ ] **5.1.2** Create `ActivitySource` for tracing
  ```csharp
  public static class IronbeesTelemetry
  {
      public static readonly ActivitySource ActivitySource = new("ironbees", "1.0.0");
      public static readonly Meter Meter = new("ironbees", "1.0.0");
  }
  ```

- [ ] **5.1.3** Instrument `AgentOrchestrator`
  ```csharp
  public async Task<string> ProcessAsync(string input, string? agentName, CancellationToken ct)
  {
      using var activity = IronbeesTelemetry.ActivitySource.StartActivity("Process Request");
      activity?.SetTag("input.length", input.Length);
      activity?.SetTag("agent.name", agentName ?? "auto");

      try
      {
          // ... processing logic ...

          activity?.SetTag("output.length", output.Length);
          activity?.SetTag("success", true);

          return output;
      }
      catch (Exception ex)
      {
          activity?.SetTag("success", false);
          activity?.SetTag("error.type", ex.GetType().Name);
          activity?.SetTag("error.message", ex.Message);
          throw;
      }
  }
  ```

- [ ] **5.1.4** Instrument `SemanticAgentSelector`
  ```csharp
  public async Task<IAgent> SelectAgentAsync(...)
  {
      using var activity = IronbeesTelemetry.ActivitySource.StartActivity("Agent Selection");
      activity?.SetTag("strategy", "semantic");
      activity?.SetTag("candidates.count", agents.Count);

      var stopwatch = Stopwatch.StartNew();

      // ... selection logic ...

      stopwatch.Stop();
      activity?.SetTag("selected.agent", selectedAgent.Name);
      activity?.SetTag("confidence", confidence);
      activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);

      return selectedAgent;
  }
  ```

#### 5.2 Metrics Collection
- [ ] **5.2.1** Define metrics
  ```csharp
  public class IronbeesMetrics
  {
      private readonly Counter<long> _requestCounter;
      private readonly Histogram<double> _requestDuration;
      private readonly Histogram<double> _agentExecutionDuration;
      private readonly Counter<long> _tokensUsed;
      private readonly Counter<long> _errorsCounter;
      private readonly Histogram<double> _selectionConfidence;

      public IronbeesMetrics()
      {
          var meter = IronbeesTelemetry.Meter;

          _requestCounter = meter.CreateCounter<long>(
              "ironbees.requests",
              description: "Total number of requests processed");

          _requestDuration = meter.CreateHistogram<double>(
              "ironbees.request.duration",
              unit: "ms",
              description: "Request processing duration");

          _agentExecutionDuration = meter.CreateHistogram<double>(
              "ironbees.agent.execution.duration",
              unit: "ms",
              description: "Agent execution duration");

          _tokensUsed = meter.CreateCounter<long>(
              "ironbees.tokens.used",
              description: "Total tokens consumed");

          _errorsCounter = meter.CreateCounter<long>(
              "ironbees.errors",
              description: "Total errors encountered");

          _selectionConfidence = meter.CreateHistogram<double>(
              "ironbees.selection.confidence",
              description: "Agent selection confidence scores");
      }

      public void RecordRequest(string agentName, double durationMs, bool success)
      {
          _requestCounter.Add(1,
              new KeyValuePair<string, object?>("agent", agentName),
              new KeyValuePair<string, object?>("success", success));

          _requestDuration.Record(durationMs);
      }

      public void RecordAgentExecution(string agentName, double durationMs, int tokensUsed)
      {
          _agentExecutionDuration.Record(durationMs,
              new KeyValuePair<string, object?>("agent", agentName));

          _tokensUsed.Add(tokensUsed,
              new KeyValuePair<string, object?>("agent", agentName));
      }

      public void RecordError(string errorType, string agentName)
      {
          _errorsCounter.Add(1,
              new KeyValuePair<string, object?>("error_type", errorType),
              new KeyValuePair<string, object?>("agent", agentName));
      }

      public void RecordSelection(string strategy, double confidence)
      {
          _selectionConfidence.Record(confidence,
              new KeyValuePair<string, object?>("strategy", strategy));
      }
  }
  ```

#### 5.3 Structured Logging
- [ ] **5.3.1** Configure structured logging
  ```csharp
  public static IServiceCollection AddIronbeesLogging(
      this IServiceCollection services)
  {
      services.AddLogging(builder =>
      {
          builder.AddConsole();
          builder.AddDebug();

          // Add structured logging with enrichment
          builder.AddFilter("Ironbees", LogLevel.Information);
      });

      return services;
  }
  ```

- [ ] **5.3.2** Create log enricher
  ```csharp
  public class IronbeesLogEnricher
  {
      public static void EnrichLog(ILogger logger, PipelineContext context)
      {
          using (logger.BeginScope(new Dictionary<string, object>
          {
              ["RequestId"] = context.Metadata.GetValueOrDefault("request_id"),
              ["UserId"] = context.Metadata.GetValueOrDefault("user_id"),
              ["SessionId"] = context.Metadata.GetValueOrDefault("session_id"),
              ["Agent"] = context.SelectedAgent ?? "unknown"
          }))
          {
              // Logs within this scope will include these properties
          }
      }
  }
  ```

#### 5.4 LLM-Specific Observability
- [ ] **5.4.1** Prompt logging
  ```csharp
  public interface IPromptLogger
  {
      Task LogPromptAsync(
          string agentName,
          string input,
          string systemPrompt,
          Dictionary<string, object> metadata);

      Task LogResponseAsync(
          string agentName,
          string output,
          int tokensUsed,
          TimeSpan duration);
  }

  public class FilePromptLogger : IPromptLogger
  {
      private readonly string _logDirectory;

      public async Task LogPromptAsync(...)
      {
          var logEntry = new
          {
              Timestamp = DateTime.UtcNow,
              Agent = agentName,
              Input = input,
              SystemPrompt = systemPrompt,
              Metadata = metadata
          };

          var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions
          {
              WriteIndented = true
          });

          var filename = $"prompts/{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid()}.json";
          await File.WriteAllTextAsync(Path.Combine(_logDirectory, filename), json);
      }
  }
  ```

- [ ] **5.4.2** Cost tracking
  ```csharp
  public class CostTracker
  {
      private readonly Dictionary<string, ModelPricing> _pricing = new()
      {
          ["gpt-4o"] = new(InputPer1M: 2.50m, OutputPer1M: 10.00m),
          ["gpt-4o-mini"] = new(InputPer1M: 0.15m, OutputPer1M: 0.60m),
          ["text-embedding-3-small"] = new(InputPer1M: 0.02m, OutputPer1M: 0m)
      };

      public decimal CalculateCost(string model, int inputTokens, int outputTokens)
      {
          if (!_pricing.TryGetValue(model, out var pricing))
              return 0m;

          var inputCost = (inputTokens / 1_000_000m) * pricing.InputPer1M;
          var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPer1M;

          return inputCost + outputCost;
      }
  }

  public record ModelPricing(decimal InputPer1M, decimal OutputPer1M);
  ```

#### 5.5 Health Checks
- [ ] **5.5.1** Implement health check endpoints
  ```csharp
  public class IronbeesHealthCheck : IHealthCheck
  {
      private readonly IAgentRegistry _registry;
      private readonly AzureOpenAIClient _client;

      public async Task<HealthCheckResult> CheckHealthAsync(
          HealthCheckContext context,
          CancellationToken ct = default)
      {
          var checks = new Dictionary<string, object>();

          // Check agents loaded
          var agentCount = _registry.ListAgents().Count;
          checks["agents_loaded"] = agentCount;

          if (agentCount == 0)
              return HealthCheckResult.Degraded("No agents loaded", data: checks);

          // Check Azure OpenAI connectivity
          try
          {
              var chatClient = _client.GetChatClient("gpt-4o-mini");
              var response = await chatClient.CompleteChatAsync("test", ct);
              checks["azure_openai"] = "connected";
          }
          catch (Exception ex)
          {
              checks["azure_openai"] = $"error: {ex.Message}";
              return HealthCheckResult.Unhealthy("Azure OpenAI unavailable", data: checks);
          }

          return HealthCheckResult.Healthy("All systems operational", data: checks);
      }
  }
  ```

#### 5.6 Evaluation Framework
- [ ] **5.6.1** Define evaluation metrics
  ```csharp
  public interface IEvaluationMetric
  {
      string Name { get; }
      Task<EvaluationResult> EvaluateAsync(
          string input,
          string output,
          string? expectedOutput = null,
          string? context = null);
  }

  public record EvaluationResult(
      double Score,
      bool Passed,
      string Reasoning,
      Dictionary<string, object> Metadata);
  ```

- [ ] **5.6.2** Implement faithfulness metric
  ```csharp
  public class FaithfulnessMetric : IEvaluationMetric
  {
      public string Name => "faithfulness";

      public async Task<EvaluationResult> EvaluateAsync(
          string input,
          string output,
          string? expectedOutput,
          string? context)
      {
          if (context == null)
              throw new ArgumentException("Context required for faithfulness evaluation");

          // Use LLM to check if output claims can be inferred from context
          var prompt = $@"
Context: {context}

Output: {output}

Can all claims in the output be inferred from the context?
Respond with a score from 0.0 to 1.0 and reasoning.
";

          // Call LLM and parse response
          // ...

          return new EvaluationResult(
              Score: 0.9,
              Passed: true,
              Reasoning: "All claims supported by context",
              Metadata: new());
      }
  }
  ```

- [ ] **5.6.3** Implement relevancy metric
  ```csharp
  public class RelevancyMetric : IEvaluationMetric
  {
      public string Name => "relevancy";

      public async Task<EvaluationResult> EvaluateAsync(
          string input,
          string output,
          string? expectedOutput,
          string? context)
      {
          // Check if output is relevant to input
          var prompt = $@"
Question: {input}

Answer: {output}

Is the answer relevant to the question?
Score from 0.0 (completely irrelevant) to 1.0 (perfectly relevant).
";

          // Implementation...

          return new EvaluationResult(/* ... */);
      }
  }
  ```

#### 5.7 Test Automation
- [ ] **5.7.1** Create evaluation test suite
  ```csharp
  public class AgentEvaluationTests
  {
      private readonly IAgentOrchestrator _orchestrator;
      private readonly List<IEvaluationMetric> _metrics;

      [Theory]
      [MemberData(nameof(GetTestCases))]
      public async Task Agent_Should_Pass_Evaluation(TestCase testCase)
      {
          // Execute agent
          var output = await _orchestrator.ProcessAsync(
              testCase.Input,
              agentName: testCase.AgentName);

          // Evaluate against all metrics
          foreach (var metric in _metrics)
          {
              var result = await metric.EvaluateAsync(
                  testCase.Input,
                  output,
                  testCase.ExpectedOutput,
                  testCase.Context);

              Assert.True(result.Passed,
                  $"{metric.Name} failed: {result.Reasoning} (score: {result.Score})");
          }
      }

      public static IEnumerable<object[]> GetTestCases() =>
          LoadTestCasesFromYaml("test-cases.yaml");
  }
  ```

- [ ] **5.7.2** Create test case format
  ```yaml
  # test-cases.yaml
  test_cases:
    - name: coding-agent-fibonacci
      agent: coding-agent
      input: "Write a C# method to calculate fibonacci numbers"
      expected_keywords:
        - "int"
        - "Fibonacci"
        - "return"
      metrics:
        - faithfulness
        - relevancy
      min_score: 0.8

    - name: analysis-agent-summarize
      agent: analysis-agent
      input: "Summarize the following article"
      context: "Long article text..."
      expected_output: "Brief summary..."
      metrics:
        - faithfulness
        - relevancy
        - hallucination
      min_score: 0.9
  ```

#### 5.8 Documentation
- [ ] **5.8.1** Create observability guide (docs/OBSERVABILITY.md)
- [ ] **5.8.2** Create testing guide (docs/TESTING.md)
- [ ] **5.8.3** Add examples for custom metrics

### Acceptance Criteria
- ✅ OpenTelemetry traces all operations
- ✅ Metrics collected and exportable
- ✅ Structured logging with enrichment
- ✅ Prompt and response logging functional
- ✅ Cost tracking accurate
- ✅ Health checks comprehensive
- ✅ Evaluation framework with 3+ metrics
- ✅ Automated evaluation test suite
- ✅ Documentation complete

---

## Phase 6: Performance Optimization

**Goal**: 캐싱, 성능 최적화, 비용 절감

**Duration**: 5-7 days

**Dependencies**: Phase 5 완료

### Tasks

#### 6.1 Caching Infrastructure
- [ ] **6.1.1** Define cache interfaces
  ```csharp
  public interface IResponseCache
  {
      Task<string?> GetAsync(string key, CancellationToken ct = default);
      Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default);
      Task<bool> ExistsAsync(string key, CancellationToken ct = default);
      Task RemoveAsync(string key, CancellationToken ct = default);
  }

  public interface ISemanticCache
  {
      Task<(string? value, double similarity)> FindSimilarAsync(
          string input,
          double threshold = 0.95,
          CancellationToken ct = default);

      Task StoreAsync(
          string input,
          string output,
          ReadOnlyMemory<float> embedding,
          TimeSpan? ttl = null,
          CancellationToken ct = default);
  }
  ```

- [ ] **6.1.2** Implement `MemoryResponseCache`
  ```csharp
  public class MemoryResponseCache : IResponseCache
  {
      private readonly IMemoryCache _cache;

      public async Task<string?> GetAsync(string key, CancellationToken ct)
      {
          return _cache.TryGetValue(key, out string? value) ? value : null;
      }

      public async Task SetAsync(string key, string value, TimeSpan? ttl, CancellationToken ct)
      {
          var options = new MemoryCacheEntryOptions();
          if (ttl.HasValue)
              options.SetAbsoluteExpiration(ttl.Value);

          _cache.Set(key, value, options);
      }
  }
  ```

- [ ] **6.1.3** Implement `MemorySemanticCache`
  ```csharp
  public class MemorySemanticCache : ISemanticCache
  {
      private readonly IEmbeddingGenerator _embedder;
      private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

      private record CacheEntry(
          string Input,
          string Output,
          ReadOnlyMemory<float> Embedding,
          DateTime ExpiresAt);

      public async Task<(string? value, double similarity)> FindSimilarAsync(
          string input,
          double threshold,
          CancellationToken ct)
      {
          var inputEmbedding = await _embedder.GenerateEmbeddingAsync(input, ct);

          var bestMatch = _cache.Values
              .Where(e => e.ExpiresAt > DateTime.UtcNow)
              .Select(e => new
              {
                  Entry = e,
                  Similarity = CosineSimilarity(inputEmbedding, e.Embedding)
              })
              .Where(x => x.Similarity >= threshold)
              .MaxBy(x => x.Similarity);

          return bestMatch != null
              ? (bestMatch.Entry.Output, bestMatch.Similarity)
              : (null, 0.0);
      }

      public async Task StoreAsync(
          string input,
          string output,
          ReadOnlyMemory<float> embedding,
          TimeSpan? ttl,
          CancellationToken ct)
      {
          var expiresAt = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromHours(1));
          var key = ComputeHash(input);

          _cache[key] = new CacheEntry(input, output, embedding, expiresAt);
      }

      private static double CosineSimilarity(
          ReadOnlyMemory<float> a,
          ReadOnlyMemory<float> b)
      {
          // Same implementation as in SemanticAgentSelector
      }

      private static string ComputeHash(string input)
      {
          using var sha256 = SHA256.Create();
          var bytes = Encoding.UTF8.GetBytes(input);
          var hash = sha256.ComputeHash(bytes);
          return Convert.ToBase64String(hash);
      }
  }
  ```

#### 6.2 Two-Layer Caching Strategy
- [ ] **6.2.1** Implement `HybridCache`
  ```csharp
  public class HybridCache
  {
      private readonly IResponseCache _exactCache;
      private readonly ISemanticCache _semanticCache;
      private readonly IEmbeddingGenerator _embedder;
      private readonly ILogger<HybridCache> _logger;

      public async Task<CacheResult> GetAsync(
          string input,
          CancellationToken ct = default)
      {
          // Layer 1: Exact match (fast)
          var exactMatch = await _exactCache.GetAsync(input, ct);
          if (exactMatch != null)
          {
              _logger.LogInformation("Cache hit: exact match");
              return CacheResult.Hit(exactMatch, CacheHitType.Exact);
          }

          // Layer 2: Semantic similarity (slower, higher hit rate)
          var (semanticMatch, similarity) = await _semanticCache.FindSimilarAsync(
              input,
              threshold: 0.95,
              ct);

          if (semanticMatch != null)
          {
              _logger.LogInformation("Cache hit: semantic match (similarity: {Similarity})", similarity);
              return CacheResult.Hit(semanticMatch, CacheHitType.Semantic, similarity);
          }

          _logger.LogInformation("Cache miss");
          return CacheResult.Miss();
      }

      public async Task SetAsync(
          string input,
          string output,
          TimeSpan? ttl = null,
          CancellationToken ct = default)
      {
          // Store in both caches
          await _exactCache.SetAsync(input, output, ttl, ct);

          var embedding = await _embedder.GenerateEmbeddingAsync(input, ct);
          await _semanticCache.StoreAsync(input, output, embedding, ttl, ct);
      }
  }

  public record CacheResult
  {
      public bool IsHit { get; init; }
      public string? Value { get; init; }
      public CacheHitType HitType { get; init; }
      public double Similarity { get; init; }

      public static CacheResult Hit(string value, CacheHitType type, double similarity = 1.0)
          => new() { IsHit = true, Value = value, HitType = type, Similarity = similarity };

      public static CacheResult Miss()
          => new() { IsHit = false };
  }

  public enum CacheHitType { Exact, Semantic }
  ```

#### 6.3 Integrate Caching into Orchestrator
- [ ] **6.3.1** Add cache-aware execution
  ```csharp
  public async Task<string> ProcessAsync(
      string input,
      string? agentName = null,
      CancellationToken ct = default)
  {
      // Check cache first
      if (_config.Caching.Enabled)
      {
          var cacheResult = await _cache.GetAsync(input, ct);
          if (cacheResult.IsHit)
          {
              _metrics.RecordCacheHit(cacheResult.HitType, cacheResult.Similarity);
              return cacheResult.Value!;
          }

          _metrics.RecordCacheMiss();
      }

      // Cache miss - execute agent
      var context = new PipelineContext();

      input = await _pipelineManager.RunPreprocessorsAsync(input, context, ct);

      var agent = await SelectAgentAsync(input, agentName, ct);

      var output = await agent.RunAsync(input, ct);

      output = await _pipelineManager.RunPostprocessorsAsync(output, context, ct);

      // Store in cache
      if (_config.Caching.Enabled)
      {
          var ttl = TimeSpan.FromSeconds(_config.Caching.TtlSeconds);
          await _cache.SetAsync(input, output, ttl, ct);
      }

      return output;
  }
  ```

#### 6.4 Prompt Caching (Azure OpenAI)
- [ ] **6.4.1** Implement prompt cache for system messages
  ```csharp
  public class PromptCacheManager
  {
      // Azure OpenAI supports prompt caching for frequently used prefixes
      // This reduces costs for repeated system prompts

      public async Task<ChatCompletion> CompleteChatWithCacheAsync(
          ChatClient chatClient,
          string systemPrompt,
          string userInput,
          CancellationToken ct = default)
      {
          // Mark system prompt as cacheable
          var messages = new[]
          {
              new ChatMessage(ChatRole.System, systemPrompt)
              {
                  // Azure OpenAI will cache this automatically
                  // if the same system prompt is used frequently
              },
              new ChatMessage(ChatRole.User, userInput)
          };

          return await chatClient.CompleteChatAsync(messages, ct);
      }
  }
  ```

#### 6.5 Batch Processing Optimization
- [ ] **6.5.1** Implement batch embedding generation
  ```csharp
  public class BatchEmbeddingGenerator
  {
      private readonly IEmbeddingGenerator _embedder;
      private readonly SemaphoreSlim _semaphore;

      public BatchEmbeddingGenerator(
          IEmbeddingGenerator embedder,
          int maxConcurrency = 10)
      {
          _embedder = embedder;
          _semaphore = new SemaphoreSlim(maxConcurrency);
      }

      public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateBatchAsync(
          IEnumerable<string> texts,
          CancellationToken ct = default)
      {
          var tasks = texts.Select(async text =>
          {
              await _semaphore.WaitAsync(ct);
              try
              {
                  return await _embedder.GenerateEmbeddingAsync(text, ct);
              }
              finally
              {
                  _semaphore.Release();
              }
          });

          return await Task.WhenAll(tasks);
      }
  }
  ```

#### 6.6 Performance Monitoring
- [ ] **6.6.1** Add cache metrics
  ```csharp
  public class CacheMetrics
  {
      private readonly Counter<long> _hitCounter;
      private readonly Counter<long> _missCounter;
      private readonly Histogram<double> _similarityScore;

      public void RecordCacheHit(CacheHitType type, double similarity)
      {
          _hitCounter.Add(1,
              new KeyValuePair<string, object?>("type", type.ToString()));

          if (type == CacheHitType.Semantic)
              _similarityScore.Record(similarity);
      }

      public void RecordCacheMiss()
      {
          _missCounter.Add(1);
      }
  }
  ```

- [ ] **6.6.2** Add cost tracking metrics
  ```csharp
  public class CostMetrics
  {
      private readonly Counter<decimal> _totalCost;
      private readonly Counter<decimal> _savedCost;

      public void RecordCost(string model, int inputTokens, int outputTokens, bool cached)
      {
          var cost = _costTracker.CalculateCost(model, inputTokens, outputTokens);

          _totalCost.Add(cost,
              new KeyValuePair<string, object?>("model", model),
              new KeyValuePair<string, object?>("cached", cached));

          if (cached)
              _savedCost.Add(cost);
      }
  }
  ```

#### 6.7 Performance Tests
- [ ] **6.7.1** Cache hit rate tests
  - Exact match caching
  - Semantic similarity caching
  - Cache expiration

- [ ] **6.7.2** Performance benchmarks
  - With/without caching
  - Cache hit rate measurement
  - Cost savings calculation

### Acceptance Criteria
- ✅ Two-layer caching implemented (exact + semantic)
- ✅ Cache hit rate >50% in realistic scenarios
- ✅ Semantic similarity threshold configurable
- ✅ Cost savings tracked and reported
- ✅ Performance tests show 50-70% cost reduction
- ✅ Documentation includes caching strategy guide

---

## Phase 7: Production Readiness

**Goal**: CLI 도구, 문서화, 배포 준비

**Duration**: 7-10 days

**Dependencies**: Phase 6 완료

### Tasks

#### 7.1 CLI Tool
- [ ] **7.1.1** Create Ironbees.Cli project
  ```bash
  dotnet new console -f net9.0 -n Ironbees.Cli
  dotnet add package System.CommandLine
  dotnet add package Spectre.Console
  ```

- [ ] **7.1.2** Implement main commands
  ```csharp
  // ironbees --help
  var rootCommand = new RootCommand("ironbees - Convention-based LLM agent orchestration");

  // ironbees chat
  var chatCommand = new Command("chat", "Start interactive chat with agents");
  chatCommand.AddOption(new Option<string>("--agent", "Specific agent to use"));
  chatCommand.AddOption(new Option<string>("--agent-path", "Path to agents directory"));
  chatCommand.SetHandler(HandleChatCommand);

  // ironbees process
  var processCommand = new Command("process", "Process a single query");
  processCommand.AddArgument(new Argument<string>("query", "Query to process"));
  processCommand.AddOption(new Option<string>("--agent", "Specific agent"));
  processCommand.AddOption(new Option<string>("--agent-path", "Agents directory"));
  processCommand.SetHandler(HandleProcessCommand);

  // ironbees agent
  var agentCommand = new Command("agent", "Manage agents");

  var listCommand = new Command("list", "List all agents");
  listCommand.AddOption(new Option<string>("--agent-path", "Agents directory"));
  listCommand.SetHandler(HandleListCommand);

  var validateCommand = new Command("validate", "Validate agent configuration");
  validateCommand.AddArgument(new Argument<string>("agent-name", "Agent to validate"));
  validateCommand.SetHandler(HandleValidateCommand);

  var createCommand = new Command("create", "Create new agent from template");
  createCommand.AddArgument(new Argument<string>("agent-name", "New agent name"));
  createCommand.AddOption(new Option<string>("--template", "Template to use"));
  createCommand.SetHandler(HandleCreateCommand);

  agentCommand.AddCommand(listCommand);
  agentCommand.AddCommand(validateCommand);
  agentCommand.AddCommand(createCommand);

  rootCommand.AddCommand(chatCommand);
  rootCommand.AddCommand(processCommand);
  rootCommand.AddCommand(agentCommand);

  return await rootCommand.InvokeAsync(args);
  ```

- [ ] **7.1.3** Implement interactive chat
  ```csharp
  private static async Task HandleChatCommand(string? agent, string? agentPath)
  {
      var orchestrator = await SetupOrchestratorAsync(agentPath);

      AnsiConsole.MarkupLine("[bold green]ironbees interactive chat[/]");
      AnsiConsole.MarkupLine("Type 'exit' to quit, 'agents' to list agents\n");

      while (true)
      {
          var input = AnsiConsole.Ask<string>("[blue]You:[/]");

          if (input.ToLower() == "exit")
              break;

          if (input.ToLower() == "agents")
          {
              var agents = orchestrator.ListAgents();
              AnsiConsole.MarkupLine($"[yellow]Available agents: {string.Join(", ", agents)}[/]");
              continue;
          }

          await AnsiConsole.Status()
              .StartAsync("Processing...", async ctx =>
              {
                  string response;
                  if (agent != null)
                  {
                      response = await orchestrator.ProcessAsync(input, agentName: agent);
                  }
                  else
                  {
                      response = await orchestrator.ProcessAsync(input);
                  }

                  AnsiConsole.MarkupLine($"[green]Agent:[/] {response}\n");
              });
      }
  }
  ```

- [ ] **7.1.4** Implement agent validation
  ```csharp
  private static async Task HandleValidateCommand(string agentName)
  {
      var table = new Table();
      table.AddColumn("Check");
      table.AddColumn("Status");
      table.AddColumn("Details");

      var agentPath = Path.Combine("./agents", agentName);

      // Check directory exists
      var dirExists = Directory.Exists(agentPath);
      table.AddRow(
          "Directory",
          dirExists ? "[green]✓[/]" : "[red]✗[/]",
          dirExists ? agentPath : "Not found");

      if (!dirExists)
      {
          AnsiConsole.Write(table);
          return;
      }

      // Check required files
      var requiredFiles = new[] { "agent.yaml", "system-prompt.md" };
      foreach (var file in requiredFiles)
      {
          var filePath = Path.Combine(agentPath, file);
          var exists = File.Exists(filePath);
          table.AddRow(
              file,
              exists ? "[green]✓[/]" : "[red]✗[/]",
              exists ? "Present" : "Missing");
      }

      // Validate YAML
      try
      {
          var loader = new FileSystemAgentLoader();
          var config = await loader.LoadConfigAsync(agentPath);

          table.AddRow("YAML Parsing", "[green]✓[/]", "Valid");
          table.AddRow("Agent Name", "[green]✓[/]", config.Name);
          table.AddRow("Model", "[green]✓[/]", config.Model.Deployment);
      }
      catch (Exception ex)
      {
          table.AddRow("Validation", "[red]✗[/]", ex.Message);
      }

      AnsiConsole.Write(table);
  }
  ```

- [ ] **7.1.5** Package as .NET tool
  ```xml
  <PropertyGroup>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>ironbees</ToolCommandName>
    <PackageOutputPath>./nupkg</PackageOutputPath>
  </PropertyGroup>
  ```

#### 7.2 NuGet Package Preparation
- [ ] **7.2.1** Configure package metadata
  ```xml
  <PropertyGroup>
    <PackageId>Ironbees.Core</PackageId>
    <Version>1.0.0</Version>
    <Authors>ironbees contributors</Authors>
    <Description>Convention-based LLM agent orchestration layer for .NET</Description>
    <PackageTags>llm;ai;agents;orchestration;dotnet</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/yourusername/ironbees</PackageProjectUrl>
    <RepositoryUrl>https://github.com/yourusername/ironbees</RepositoryUrl>
    <PackageIcon>icon.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>
  ```

- [ ] **7.2.2** Create NuGet packages
  ```bash
  dotnet pack src/Ironbees.Core/Ironbees.Core.csproj -c Release
  dotnet pack src/Ironbees.AgentFramework/Ironbees.AgentFramework.csproj -c Release
  ```

- [ ] **7.2.3** Test package installation
  ```bash
  dotnet new console -n TestApp
  cd TestApp
  dotnet add package Ironbees.Core --source ../nupkg
  dotnet add package Ironbees.AgentFramework --source ../nupkg
  ```

#### 7.3 Documentation
- [ ] **7.3.1** API documentation (docs/API.md)
  - IAgentOrchestrator
  - IAgent
  - IAgentSelector
  - Pipeline interfaces
  - Configuration model

- [ ] **7.3.2** User guide (docs/USER_GUIDE.md)
  - Installation
  - Quick start
  - Agent creation guide
  - Configuration reference
  - Pipeline customization

- [ ] **7.3.3** Advanced topics (docs/ADVANCED.md)
  - Custom framework adapters
  - Custom agent selectors
  - Performance tuning
  - Production deployment

- [ ] **7.3.4** Contributing guide (CONTRIBUTING.md)
  - Development setup
  - Code style
  - Testing requirements
  - PR process

- [ ] **7.3.5** Migration guide (docs/MIGRATION.md)
  - From direct Agent Framework usage
  - From Semantic Kernel
  - From LangChain

#### 7.4 Example Projects
- [ ] **7.4.1** Basic example (examples/basic-agent)
  - Single agent setup
  - Console app
  - README with instructions

- [ ] **7.4.2** Multi-agent example (examples/multi-agent)
  - Multiple specialized agents
  - Auto-selection demonstration
  - Console app

- [ ] **7.4.3** Custom pipeline example (examples/custom-pipeline)
  - Custom preprocessors/postprocessors
  - Guardrails setup
  - ASP.NET Core integration

- [ ] **7.4.4** MCP integration example (examples/mcp-integration)
  - MCP server configuration
  - Tool usage
  - Advanced features

- [ ] **7.4.5** Production example (examples/production)
  - Full ASP.NET Core app
  - Observability setup
  - Health checks
  - Docker deployment

#### 7.5 CI/CD Pipeline
- [ ] **7.5.1** GitHub Actions workflows
  ```yaml
  # .github/workflows/ci.yml
  name: CI

  on:
    push:
      branches: [ main ]
    pull_request:
      branches: [ main ]

  jobs:
    build:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '9.0.x'

        - name: Restore dependencies
          run: dotnet restore

        - name: Build
          run: dotnet build --no-restore

        - name: Test
          run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

        - name: Upload coverage
          uses: codecov/codecov-action@v3
          with:
            files: coverage.cobertura.xml
  ```

- [ ] **7.5.2** Release workflow
  ```yaml
  # .github/workflows/release.yml
  name: Release

  on:
    push:
      tags:
        - 'v*'

  jobs:
    release:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-dotnet@v4
          with:
            dotnet-version: '9.0.x'

        - name: Pack
          run: dotnet pack -c Release

        - name: Push to NuGet
          run: dotnet nuget push "**/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }}
  ```

#### 7.6 Container Support
- [ ] **7.6.1** Create Dockerfile
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
  WORKDIR /app
  EXPOSE 8080

  FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
  WORKDIR /src
  COPY ["src/Ironbees.Core/Ironbees.Core.csproj", "Ironbees.Core/"]
  COPY ["src/Ironbees.AgentFramework/Ironbees.AgentFramework.csproj", "Ironbees.AgentFramework/"]
  RUN dotnet restore "Ironbees.Core/Ironbees.Core.csproj"
  RUN dotnet restore "Ironbees.AgentFramework/Ironbees.AgentFramework.csproj"

  COPY src/ .
  WORKDIR "/src/YourApp"
  RUN dotnet build -c Release -o /app/build

  FROM build AS publish
  RUN dotnet publish -c Release -o /app/publish

  FROM base AS final
  WORKDIR /app
  COPY --from=publish /app/publish .
  COPY agents/ ./agents/

  ENTRYPOINT ["dotnet", "YourApp.dll"]
  ```

- [ ] **7.6.2** Create docker-compose.yml
  ```yaml
  version: '3.8'

  services:
    ironbees-app:
      build: .
      ports:
        - "8080:8080"
      environment:
        - AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT}
        - ASPNETCORE_ENVIRONMENT=Production
      volumes:
        - ./agents:/app/agents:ro
      healthcheck:
        test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
        interval: 30s
        timeout: 10s
        retries: 3
  ```

#### 7.7 Final Testing
- [ ] **7.7.1** End-to-end integration tests
  - Full workflow: load → select → process → cache
  - Multiple agents
  - Error scenarios

- [ ] **7.7.2** Performance testing
  - Load testing with k6 or similar
  - Measure latency under load
  - Verify cache effectiveness

- [ ] **7.7.3** Documentation verification
  - All examples run successfully
  - API documentation complete
  - No broken links

### Acceptance Criteria
- ✅ CLI tool fully functional and published as .NET tool
- ✅ NuGet packages published (preview)
- ✅ All documentation complete and verified
- ✅ 5+ working examples
- ✅ CI/CD pipeline operational
- ✅ Docker support working
- ✅ E2E tests pass
- ✅ Performance tests meet targets
- ✅ README badges (build status, coverage, version)

---

## Milestones

### M0: Foundation (Phase 0-1)
**Target**: Week 1
- ✅ Project structure established
- ✅ Core abstractions defined
- ✅ File system loader implemented
- ✅ Basic tests passing

### M1: MVP (Phase 2)
**Target**: Week 2-3
- ✅ Agent Framework integration
- ✅ Manual agent selection working
- ✅ Example console app running
- ✅ Basic documentation

### M1.5: System Agents (Phase 2.5) - v1.1
**Target**: Week 3-4
- ✅ SystemAgents package created
- ✅ 5 core system agents implemented
- ✅ CompositeAgentLoader with priority-based loading
- ✅ User agent override functionality
- ✅ System agents documentation and examples

### M2: Intelligent Routing (Phase 3)
**Target**: Week 4-5
- ✅ Semantic routing implemented
- ✅ Auto-selection functional
- ✅ Hybrid routing strategy
- ✅ Performance metrics

### M3: Production Features (Phase 4-5)
**Target**: Week 6-8
- ✅ Pipeline processing
- ✅ Guardrails integrated
- ✅ Observability complete
- ✅ Evaluation framework

### M4: Optimization (Phase 6)
**Target**: Week 9-10
- ✅ Caching implemented
- ✅ Cost optimization verified
- ✅ Performance benchmarks

### M5: Release (Phase 7)
**Target**: Week 11-12
- ✅ CLI tool published
- ✅ NuGet packages available (Core, AgentFramework)
- ✅ Documentation complete
- ✅ Examples working
- ✅ v1.0.0 released (without system agents)

### M6: Enhanced Release - v1.1
**Target**: Week 13-14
- ✅ Ironbees.SystemAgents NuGet package published
- ✅ System agents fully tested and validated
- ✅ Enhanced documentation with system agents guide
- ✅ v1.1.0 released (with system agents)

---

## Success Metrics

### Technical Metrics
- **Test Coverage**: >80% line coverage
- **Latency**: p50 < 100ms (agent selection), p99 < 500ms
- **Cache Hit Rate**: >50% in production scenarios
- **Cost Reduction**: 50-70% through caching and routing

### Quality Metrics
- **Documentation**: All public APIs documented
- **Examples**: 5+ working examples
- **CI/CD**: 100% automated build and test
- **Code Quality**: No critical issues in static analysis

### Adoption Metrics (Post-Release)
- **NuGet Downloads**: Track weekly downloads
- **GitHub Stars**: Community engagement
- **Issues/PRs**: Active community participation
- **Usage Examples**: Real-world adoption cases

---

## Risk Management

### Technical Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Agent Framework API changes (preview) | High | Pin specific version, monitor releases |
| Azure OpenAI rate limits | Medium | Implement exponential backoff, circuit breakers |
| Embedding generation cost | Medium | Batch operations, aggressive caching |
| Performance degradation | Medium | Continuous benchmarking, performance tests |

### Project Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Scope creep | Medium | Strict phase boundaries, MVP first |
| Timeline slippage | Medium | Buffer time in estimates, parallel work |
| Test coverage gaps | High | TDD approach, coverage requirements |
| Documentation lag | Medium | Document as you build, examples first |

---

## Next Steps

1. ✅ **Review this document** with stakeholders
2. ⏳ **Begin Phase 0** - Project setup
3. ⏳ **Setup project tracking** - GitHub Projects or similar
4. ⏳ **Create initial sprint plan** - 2-week sprints
5. ⏳ **Start development** - Phase 0 tasks

---

**Document Status**: ✅ Complete
**Last Updated**: 2025-10-29
**Version**: 1.0
