# ironbees System Agents

**Version**: 1.0.0
**Target Framework**: .NET 9.0
**Last Updated**: 2025-10-29

---

## Executive Summary

ironbees의 핵심 가치 제안은 **"LLM 애플리케이션을 만들 때 반복되는 패턴 간소화"**입니다. System Agents는 이 철학을 구현한 **내장 에이전트 컬렉션**으로, 일반적으로 필요한 기능들을 즉시 사용 가능하게 제공합니다.

**핵심 원칙**:
- **Zero Configuration**: 설치하면 바로 사용 가능
- **Convention-based**: 파일 구조 기반 자동 로딩
- **Overridable**: 사용자 정의로 대체 가능
- **Production-Ready**: 검증된 프롬프트와 패턴

---

## Conceptual Model

### Agent Types

```
┌─────────────────────────────────────────────────┐
│                 User Application                 │
└─────────────────┬───────────────────────────────┘
                  │
        ┌─────────┴─────────┐
        │                   │
┌───────▼────────┐  ┌──────▼──────────┐
│ System Agents  │  │  User Agents    │
│ (내장, 자동)   │  │  (사용자 정의)  │
└────────────────┘  └─────────────────┘
```

### System Agents vs User Agents

| 특성 | System Agents | User Agents |
|------|---------------|-------------|
| **제공** | ironbees 내장 | 사용자 정의 |
| **위치** | `/system-agents/` (embedded) | `/agents/` (user directory) |
| **로딩** | 자동 (프레임워크) | 명시적 (LoadAgentsAsync) |
| **목적** | 범용 기능 | 도메인 특화 |
| **예시** | 요약, 웹서치, 번역 | 코딩 전문가, 의료 분석 |
| **오버라이드** | 가능 (user-agents 우선) | N/A |
| **업데이트** | NuGet 패키지 | 사용자 관리 |

---

## System Agent Catalog

### Phase 1: Core System Agents (v1.0)

#### 1. **summarizer** - 텍스트 요약 에이전트

**목적**: 긴 텍스트를 간결하게 요약

**Capabilities**:
- 문서 요약 (abstract, executive summary)
- 대화 요약 (meeting notes, chat history)
- 다단계 요약 (long document → hierarchical summary)

**agent.yaml**:
```yaml
name: summarizer
description: Summarizes long text into concise, actionable insights
version: 1.0.0
category: system
model:
  provider: azure-openai
  deployment: gpt-4o-mini  # Cost-effective for summarization
  temperature: 0.3
  max_tokens: 1000
capabilities:
  - text-summarization
  - document-condensing
  - key-points-extraction
tags:
  - system
  - summarization
  - text-processing
```

**system-prompt.md**:
```markdown
You are a professional summarization assistant specializing in distilling complex information into clear, actionable insights.

## Core Responsibilities
- Extract key points and main ideas
- Preserve critical information and context
- Maintain factual accuracy
- Organize information hierarchically

## Output Format
**Summary**: [2-3 sentence overview]

**Key Points**:
- [Main point 1]
- [Main point 2]
- [Main point 3]

**Details**: [Supporting information if needed]

## Guidelines
- Use clear, concise language
- Avoid unnecessary elaboration
- Preserve technical terms and proper nouns
- Indicate uncertainty when information is ambiguous
```

---

#### 2. **web-search** - 웹 검색 에이전트

**목적**: 실시간 정보 검색 및 종합

**Capabilities**:
- 웹 검색 (Tavily MCP 통합)
- 검색 결과 요약
- 소스 신뢰도 평가

**agent.yaml**:
```yaml
name: web-search
description: Performs web searches and synthesizes information from multiple sources
version: 1.0.0
category: system
model:
  provider: azure-openai
  deployment: gpt-4o
  temperature: 0.5
  max_tokens: 2000
capabilities:
  - web-search
  - information-retrieval
  - source-evaluation
tags:
  - system
  - search
  - research
```

**mcp-config.json**:
```json
{
  "servers": {
    "tavily": {
      "command": "tavily-mcp-server",
      "env": {
        "TAVILY_API_KEY": "${TAVILY_API_KEY}"
      }
    }
  }
}
```

**system-prompt.md**:
```markdown
You are a research assistant with access to real-time web search capabilities.

## Core Responsibilities
- Perform comprehensive web searches
- Synthesize information from multiple sources
- Evaluate source credibility
- Provide cited, factual responses

## Search Strategy
1. Formulate effective search queries
2. Analyze top results for relevance
3. Cross-reference information across sources
4. Identify and note conflicting information

## Output Format
**Answer**: [Synthesized response]

**Sources**:
- [Source 1]: [URL] - [Brief description]
- [Source 2]: [URL] - [Brief description]

**Confidence**: [High/Medium/Low based on source quality]

## Guidelines
- Always cite sources with URLs
- Note the publication date of information
- Distinguish between facts and opinions
- Indicate when information is outdated or uncertain
```

---

#### 3. **file-explorer** - 파일 시스템 탐색 에이전트

**목적**: 프로젝트 파일 분석 및 탐색

**Capabilities**:
- 파일 구조 분석
- 코드베이스 이해
- 파일 검색 및 필터링

**agent.yaml**:
```yaml
name: file-explorer
description: Explores and analyzes project file structures and codebases
version: 1.0.0
category: system
model:
  provider: azure-openai
  deployment: gpt-4o
  temperature: 0.3
  max_tokens: 3000
capabilities:
  - file-analysis
  - codebase-exploration
  - pattern-recognition
tags:
  - system
  - files
  - code-analysis
```

**tools.md**:
```markdown
# File Explorer Tools

## list_directory
Lists files and directories in a given path.

**Parameters**:
- `path` (string): Directory path to list
- `recursive` (boolean): Whether to list recursively
- `pattern` (string): File pattern filter (e.g., "*.cs")

## read_file
Reads the contents of a file.

**Parameters**:
- `path` (string): File path to read
- `encoding` (string): File encoding (default: utf-8)

## search_files
Searches for files matching a pattern or content.

**Parameters**:
- `query` (string): Search query
- `path` (string): Root path to search
- `type` (string): "name" or "content"
```

**system-prompt.md**:
```markdown
You are a file system analysis assistant with deep understanding of project structures.

## Core Responsibilities
- Analyze project file organization
- Identify code patterns and structures
- Help locate relevant files
- Suggest improvements to file organization

## Analysis Approach
1. Understand project type (web, library, console, etc.)
2. Identify framework conventions (ASP.NET, React, etc.)
3. Map dependencies and relationships
4. Recognize common patterns

## Output Format
**Structure Analysis**:
```
project-root/
├── src/           [Source code]
├── tests/         [Test files]
└── docs/          [Documentation]
```

**Key Findings**:
- [Notable pattern 1]
- [Notable pattern 2]

**Recommendations**:
- [Improvement suggestion 1]
- [Improvement suggestion 2]

## Guidelines
- Respect framework conventions
- Identify anti-patterns
- Consider maintainability
- Note missing critical files (README, LICENSE, etc.)
```

---

#### 4. **translator** - 다국어 번역 에이전트

**목적**: 자연스러운 다국어 번역

**Capabilities**:
- 텍스트 번역 (50+ 언어)
- 컨텍스트 보존
- 기술 용어 처리

**agent.yaml**:
```yaml
name: translator
description: Translates text between languages while preserving context and technical terms
version: 1.0.0
category: system
model:
  provider: azure-openai
  deployment: gpt-4o
  temperature: 0.3
  max_tokens: 2000
capabilities:
  - translation
  - localization
  - terminology-management
tags:
  - system
  - translation
  - localization
```

**system-prompt.md**:
```markdown
You are a professional translator specializing in natural, context-aware translations.

## Core Responsibilities
- Translate text accurately while preserving meaning
- Maintain technical terminology appropriately
- Adapt to target language conventions
- Preserve formatting and structure

## Translation Guidelines
1. Understand source context before translating
2. Preserve technical terms in original language when appropriate
3. Use natural target language expressions
4. Maintain formality level from source

## Output Format
**Translation**: [Translated text]

**Notes**: [Any ambiguities or decisions made]

## Special Handling
- Code: Keep unchanged
- URLs: Keep unchanged
- Product names: Keep original or use official localization
- Technical terms: Preserve or translate based on convention
```

---

#### 5. **code-reviewer** - 코드 리뷰 에이전트

**목적**: 코드 품질 분석 및 개선 제안

**Capabilities**:
- 코드 품질 분석
- 베스트 프랙티스 확인
- 보안 취약점 탐지

**agent.yaml**:
```yaml
name: code-reviewer
description: Reviews code for quality, security, and best practices
version: 1.0.0
category: system
model:
  provider: azure-openai
  deployment: gpt-4o
  temperature: 0.3
  max_tokens: 3000
capabilities:
  - code-review
  - quality-analysis
  - security-scanning
tags:
  - system
  - code
  - quality
```

**system-prompt.md**:
```markdown
You are an experienced code reviewer focused on quality, security, and maintainability.

## Review Criteria
1. **Correctness**: Does the code work as intended?
2. **Security**: Are there vulnerabilities?
3. **Performance**: Are there inefficiencies?
4. **Maintainability**: Is the code readable and maintainable?
5. **Best Practices**: Does it follow language/framework conventions?

## Review Process
1. Understand the code's purpose
2. Identify immediate issues (bugs, security)
3. Evaluate code quality
4. Suggest improvements

## Output Format
**Summary**: [Overall assessment]

**Critical Issues**: 🔴
- [Issue 1 with severity and location]

**Improvements**: 🟡
- [Suggestion 1 with rationale]

**Positive Points**: ✅
- [What's done well]

## Review Standards
- Be constructive, not critical
- Explain the "why" behind suggestions
- Prioritize by severity
- Provide code examples for complex changes
```

---

### Phase 2: Advanced System Agents (v1.1+)

#### 6. **data-analyst** - 데이터 분석 에이전트
- CSV/JSON/Excel 데이터 분석
- 통계 계산 및 인사이트
- 시각화 제안

#### 7. **doc-generator** - 문서 생성 에이전트
- API 문서 자동 생성
- README 작성
- 아키텍처 다이어그램 설명

#### 8. **test-generator** - 테스트 생성 에이전트
- 단위 테스트 생성
- 테스트 케이스 제안
- 테스트 커버리지 분석

#### 9. **debugger** - 디버깅 지원 에이전트
- 에러 메시지 해석
- 디버깅 전략 제안
- 로그 분석

#### 10. **architect** - 아키텍처 설계 에이전트
- 시스템 설계 제안
- 패턴 추천
- 트레이드오프 분석

---

## Architecture Design

### Package Structure

```
Ironbees.Core                       # 핵심 추상화
Ironbees.AgentFramework             # MS Agent Framework 구현
Ironbees.SystemAgents               # System Agents 패키지 (NEW)
  ├── Agents/
  │   ├── summarizer/
  │   ├── web-search/
  │   ├── file-explorer/
  │   ├── translator/
  │   └── code-reviewer/
  └── SystemAgentLoader.cs
```

### Directory Convention

**Embedded System Agents**:
```
Ironbees.SystemAgents (NuGet 패키지)
  └── Agents/ (Embedded Resources)
      ├── summarizer/
      │   ├── agent.yaml
      │   ├── system-prompt.md
      │   └── tools.md (optional)
      ├── web-search/
      │   ├── agent.yaml
      │   ├── system-prompt.md
      │   └── mcp-config.json
      └── file-explorer/
          ├── agent.yaml
          ├── system-prompt.md
          └── tools.md
```

**User Override Pattern**:
```
user-project/
  ├── agents/                    # User agents (우선순위 1)
  │   ├── coding-agent/
  │   └── summarizer/            # System agent override!
  └── ironbees.config.yaml
```

**Loading Priority**:
1. User agents in `/agents/` directory
2. System agents from `Ironbees.SystemAgents` package

---

## Implementation Design

### SystemAgentLoader

```csharp
namespace Ironbees.SystemAgents;

public class SystemAgentLoader : IAgentLoader
{
    private readonly Assembly _assembly;
    private readonly ILogger<SystemAgentLoader> _logger;

    public SystemAgentLoader(ILogger<SystemAgentLoader> logger)
    {
        _assembly = typeof(SystemAgentLoader).Assembly;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgentConfig>> LoadAllConfigsAsync(
        string? agentsDirectory = null)
    {
        var configs = new List<AgentConfig>();

        // Load embedded system agents
        var resourceNames = _assembly.GetManifestResourceNames()
            .Where(r => r.Contains("Agents.") && r.EndsWith("agent.yaml"));

        foreach (var resourceName in resourceNames)
        {
            try
            {
                var config = await LoadEmbeddedAgentAsync(resourceName);
                configs.Add(config);
                _logger.LogInformation("Loaded system agent: {AgentName}", config.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load system agent: {ResourceName}", resourceName);
            }
        }

        return configs;
    }

    private async Task<AgentConfig> LoadEmbeddedAgentAsync(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");

        using var reader = new StreamReader(stream);
        var yaml = await reader.ReadToEndAsync();

        var deserializer = new DeserializerBuilder().Build();
        var config = deserializer.Deserialize<AgentConfig>(yaml);

        // Load system-prompt.md from embedded resource
        var agentName = config.Name;
        var promptResource = $"Ironbees.SystemAgents.Agents.{agentName}.system-prompt.md";
        config.SystemPrompt = await LoadEmbeddedTextResourceAsync(promptResource);

        return config;
    }

    private async Task<string> LoadEmbeddedTextResourceAsync(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return string.Empty;

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
```

### CompositeAgentLoader

```csharp
namespace Ironbees.Core;

/// <summary>
/// Loads agents from multiple sources with priority:
/// 1. User agents (highest priority)
/// 2. System agents (fallback)
/// </summary>
public class CompositeAgentLoader : IAgentLoader
{
    private readonly IAgentLoader _userAgentLoader;
    private readonly IAgentLoader _systemAgentLoader;
    private readonly ILogger<CompositeAgentLoader> _logger;

    public CompositeAgentLoader(
        FileSystemAgentLoader userAgentLoader,
        SystemAgentLoader systemAgentLoader,
        ILogger<CompositeAgentLoader> logger)
    {
        _userAgentLoader = userAgentLoader;
        _systemAgentLoader = systemAgentLoader;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgentConfig>> LoadAllConfigsAsync(
        string? agentsDirectory = null)
    {
        var allConfigs = new Dictionary<string, AgentConfig>();

        // Load system agents first (lower priority)
        var systemConfigs = await _systemAgentLoader.LoadAllConfigsAsync();
        foreach (var config in systemConfigs)
        {
            allConfigs[config.Name] = config;
            _logger.LogDebug("Loaded system agent: {AgentName}", config.Name);
        }

        // Load user agents (higher priority, can override)
        if (!string.IsNullOrEmpty(agentsDirectory))
        {
            var userConfigs = await _userAgentLoader.LoadAllConfigsAsync(agentsDirectory);
            foreach (var config in userConfigs)
            {
                if (allConfigs.ContainsKey(config.Name))
                {
                    _logger.LogInformation(
                        "User agent '{AgentName}' overrides system agent",
                        config.Name);
                }

                allConfigs[config.Name] = config;
            }
        }

        return allConfigs.Values.ToList();
    }

    public async Task<AgentConfig> LoadConfigAsync(string agentPath)
    {
        // User agents only (no system agent override at individual level)
        return await _userAgentLoader.LoadConfigAsync(agentPath);
    }

    public async Task<bool> ValidateAgentDirectoryAsync(string agentPath)
    {
        return await _userAgentLoader.ValidateAgentDirectoryAsync(agentPath);
    }
}
```

### DI Registration

```csharp
public static class SystemAgentsServiceCollectionExtensions
{
    public static IServiceCollection AddSystemAgents(
        this IServiceCollection services)
    {
        // Register system agent loader
        services.AddSingleton<SystemAgentLoader>();

        // Replace IAgentLoader with composite
        services.AddSingleton<FileSystemAgentLoader>();
        services.AddSingleton<IAgentLoader, CompositeAgentLoader>();

        return services;
    }
}
```

### Usage Example

```csharp
// Startup.cs
services.AddIronbees(options =>
{
    options.AgentsDirectory = "./agents";  // User agents
})
.AddSystemAgents();  // Enable system agents

// Usage
var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

// System agents are automatically loaded
await orchestrator.LoadAgentsAsync();

// Use system agent directly
var summary = await orchestrator.ProcessAsync(
    "Summarize this document: [long text]",
    agentName: "summarizer");

// Use system agent via auto-selection
var searchResult = await orchestrator.ProcessAsync(
    "What's the latest news about .NET 9?");
// → Auto-selects "web-search" system agent

// User agent overrides system agent
// If user has /agents/summarizer/, it takes priority
```

---

## Configuration

### ironbees.config.yaml

```yaml
version: 1.0.0

agents:
  directory: ./agents               # User agents
  system_agents:
    enabled: true                    # Enable system agents
    override_allowed: true           # Allow user agents to override

    # Disable specific system agents
    disabled:
      - translator                   # Don't load translator

    # Override system agent models
    model_overrides:
      summarizer:
        deployment: gpt-4o           # Use larger model
        temperature: 0.2
      web-search:
        deployment: gpt-4o-mini      # Use smaller model for cost

routing:
  strategy: hybrid

  # System agents can be selected automatically
  system_agents_eligible: true

  # Prefer user agents over system agents when ambiguous
  prefer_user_agents: true
```

---

## Agent Metadata

### Category Field

```yaml
# agent.yaml
name: summarizer
category: system        # "system" | "user"
description: ...
```

**Benefits**:
- Identify system agents programmatically
- Filter agents by category
- Apply different routing rules

### System Agent Discovery

```csharp
// List all system agents
var systemAgents = orchestrator.ListAgents()
    .Where(name =>
    {
        var agent = orchestrator.GetAgent(name);
        return agent.Configuration.Metadata.TryGetValue("category", out var category)
            && category?.ToString() == "system";
    });

Console.WriteLine("System Agents Available:");
foreach (var agent in systemAgents)
{
    Console.WriteLine($"  - {agent}");
}
```

---

## Testing Strategy

### System Agent Tests

```csharp
public class SystemAgentTests
{
    [Fact]
    public async Task SystemAgents_Should_LoadAutomatically()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIronbees()
                .AddSystemAgents();

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

        // Act
        await orchestrator.LoadAgentsAsync();
        var agents = orchestrator.ListAgents();

        // Assert
        Assert.Contains("summarizer", agents);
        Assert.Contains("web-search", agents);
        Assert.Contains("file-explorer", agents);
    }

    [Fact]
    public async Task UserAgent_Should_OverrideSystemAgent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIronbees(options =>
        {
            options.AgentsDirectory = "./test-agents";  // Has custom summarizer
        })
        .AddSystemAgents();

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IAgentOrchestrator>();

        // Act
        await orchestrator.LoadAgentsAsync();
        var summarizer = orchestrator.GetAgent("summarizer");

        // Assert
        Assert.Equal("Custom Summarizer", summarizer.Description);
    }

    [Fact]
    public async Task Summarizer_Should_ProduceConciseSummary()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIronbees().AddSystemAgents();
        var orchestrator = services.BuildServiceProvider()
            .GetRequiredService<IAgentOrchestrator>();

        await orchestrator.LoadAgentsAsync();

        var longText = File.ReadAllText("test-data/long-document.txt");

        // Act
        var summary = await orchestrator.ProcessAsync(
            $"Summarize: {longText}",
            agentName: "summarizer");

        // Assert
        Assert.NotEmpty(summary);
        Assert.True(summary.Length < longText.Length * 0.3);
        Assert.Contains("Key Points", summary);
    }
}
```

---

## Performance Considerations

### Lazy Loading

System agents are **embedded resources**, but only loaded when needed:

```csharp
// Option 1: Load all system agents at startup (eager)
await orchestrator.LoadAgentsAsync();

// Option 2: Load on first use (lazy)
var summary = await orchestrator.ProcessAsync(
    "Summarize this text",
    agentName: "summarizer");
// → System agent loaded on-demand
```

### Caching

System agent configurations are **cached after first load**:

```csharp
public class SystemAgentLoader
{
    private readonly ConcurrentDictionary<string, AgentConfig> _cache = new();

    public async Task<AgentConfig> LoadConfigAsync(string agentName)
    {
        if (_cache.TryGetValue(agentName, out var cached))
            return cached;

        var config = await LoadEmbeddedAgentAsync(agentName);
        _cache[agentName] = config;
        return config;
    }
}
```

---

## Distribution Strategy

### NuGet Package: Ironbees.SystemAgents

```xml
<PackageId>Ironbees.SystemAgents</PackageId>
<Version>1.0.0</Version>
<Description>Built-in system agents for common LLM application patterns</Description>
<PackageTags>llm;ai;agents;system-agents;built-in</PackageTags>

<ItemGroup>
  <!-- Ironbees Core dependency -->
  <PackageReference Include="Ironbees.Core" Version="1.0.0" />

  <!-- MCP dependencies for web-search -->
  <PackageReference Include="Tavily.MCP" Version="*" />
</ItemGroup>

<ItemGroup>
  <!-- Embed system agent files -->
  <EmbeddedResource Include="Agents\**\*" />
</ItemGroup>
```

### Installation

```bash
# Basic installation (no system agents)
dotnet add package Ironbees.AgentFramework

# With system agents
dotnet add package Ironbees.AgentFramework
dotnet add package Ironbees.SystemAgents
```

```csharp
// Enable system agents
services.AddIronbees()
        .AddSystemAgents();  // Adds 5+ built-in agents
```

---

## Security Considerations

### System Agent Permissions

System agents have **no special privileges** by default:

```yaml
# system-agent-permissions.yaml
agents:
  summarizer:
    permissions: []                # No special permissions

  web-search:
    permissions:
      - network.http               # Can make HTTP requests
    requires_api_key: tavily       # Requires TAVILY_API_KEY

  file-explorer:
    permissions:
      - filesystem.read            # Can read files
    scope: user_workspace          # Limited to workspace only
```

### User Control

Users can **disable or restrict** system agents:

```yaml
# ironbees.config.yaml
agents:
  system_agents:
    enabled: true

    # Disable potentially dangerous agents
    disabled:
      - file-explorer              # Don't allow file access

    # Restrict permissions
    permissions:
      web-search:
        allowed_domains:           # Only allow specific domains
          - "wikipedia.org"
          - "github.com"
```

---

## Migration Path

### v1.0 → v1.1 (Adding System Agents)

**Breaking Changes**: None

**New Features**:
- System agents available via `Ironbees.SystemAgents` package
- Existing user agents continue to work unchanged
- Opt-in feature via `AddSystemAgents()`

**Migration Steps**:

```bash
# Step 1: Install package
dotnet add package Ironbees.SystemAgents

# Step 2: Enable in code
services.AddIronbees()
        .AddSystemAgents();  // NEW

# Step 3: Use immediately
var summary = await orchestrator.ProcessAsync(
    "Summarize this text",
    agentName: "summarizer");  // Works immediately!
```

---

## Future Enhancements

### v1.2: Community System Agents

Allow community-contributed system agents:

```bash
dotnet add package Ironbees.SystemAgents.Community
dotnet add package Ironbees.SystemAgents.DevOps
dotnet add package Ironbees.SystemAgents.DataScience
```

### v2.0: Agent Marketplace

- Browse and install system agents from marketplace
- Rate and review system agents
- Version management and updates
- Security scanning and verification

### v2.0: Dynamic System Agents

Generate system agents on-the-fly based on user needs:

```csharp
// Auto-generate specialized system agent
var customAgent = await orchestrator.GenerateSystemAgentAsync(
    "Create a system agent that analyzes SQL queries for performance issues");
```

---

## Best Practices

### When to Use System Agents

✅ **Use System Agents for**:
- Common, well-defined tasks (summarization, translation)
- Tasks requiring specific MCP integrations (web search)
- Reusable patterns across projects
- Quick prototyping and MVPs

❌ **Use User Agents for**:
- Domain-specific expertise (medical, legal, finance)
- Company-specific knowledge
- Custom workflows and processes
- Integration with proprietary systems

### Naming Conventions

**System Agents**: Use descriptive, action-oriented names
- `summarizer` not `summary-agent`
- `web-search` not `searcher`
- `file-explorer` not `file-system`

**User Agents**: Use domain-specific names
- `medical-diagnosis`
- `legal-contract-reviewer`
- `customer-support`

### Prompt Engineering

System agent prompts should be:
- **Generic**: Work across different domains
- **Robust**: Handle edge cases gracefully
- **Consistent**: Follow same output format
- **Professional**: Production-ready quality

---

## Conclusion

System Agents는 ironbees의 **핵심 차별화 요소**입니다. "반복되는 패턴 간소화"라는 철학을 구현하여, 개발자들이 **즉시 사용 가능한 검증된 에이전트**를 제공합니다.

**Key Benefits**:
- ⚡ **Zero Configuration**: 설치하면 바로 사용
- 🎯 **Production-Ready**: 검증된 프롬프트와 패턴
- 🔧 **Customizable**: 필요시 오버라이드 가능
- 📦 **Modular**: 필요한 것만 설치
- 🚀 **Fast Start**: 프로토타입 → 프로덕션 빠르게

**Next Steps**:
1. Phase 1 완료 후 Ironbees.SystemAgents 패키지 개발
2. Core 5개 system agents 구현 (summarizer, web-search, file-explorer, translator, code-reviewer)
3. 커뮤니티 피드백 수집 및 개선
4. Phase 2 system agents 추가 (data-analyst, doc-generator, etc.)
