# 🐝 ironbees

> Convention-based agent orchestration layer for practical LLM applications

ironbees는 파일시스템 기반 규칙으로 LLM 에이전트를 구성하고 오케스트레이션하는 경량 레이어입니다. 복잡한 LLM 프레임워크 위에서 실용적인 에이전트 관리를 제공합니다.

## Why ironbees?

LLM 애플리케이션을 만들 때 반복되는 패턴이 있습니다:

- 여러 전문화된 에이전트가 필요함
- 사용자 입력에 따라 적절한 에이전트 선택
- 입력 검증, 출력 필터링 같은 전/후처리
- 에이전트 설정을 코드가 아닌 파일로 관리

ironbees는 이런 패턴을 **convention 기반 접근**으로 단순화합니다.

```
user-input → [preprocessing] → [agent selection] → LLM → [postprocessing] → output
```

## Architecture Position

ironbees는 LLM 프레임워크와 애플리케이션 사이의 얇은 오케스트레이션 레이어입니다:

```
┌─────────────────────────────────────┐
│   Your Application                  │  비즈니스 로직, UI, 인증
├─────────────────────────────────────┤
│   ironbees                          │  에이전트 로딩, 선택, 파이프라인
├─────────────────────────────────────┤
│   LLM Framework                     │  Agent Framework, Semantic Kernel
│   (Agent Framework, LangChain, etc) │  LangChain, LlamaIndex 등
├─────────────────────────────────────┤
│   LLM Provider                      │  OpenAI, Anthropic, Azure
└─────────────────────────────────────┘
```

## Quick Start

### Installation

```bash
dotnet add package ironbees
```

### Basic Usage

```csharp
using Ironbees;

// 에이전트 디렉토리 지정
var orchestrator = new AgentOrchestrator("./agents");

// 자동으로 적절한 에이전트 선택 및 실행
var response = await orchestrator.ProcessAsync("코드를 리뷰해줘");
Console.WriteLine(response);
```

### Agent Structure

에이전트는 파일시스템 기반 규칙을 따릅니다:

```
/agents/
  /coding-agent/
    system-prompt.md      # 시스템 프롬프트
    tools.md              # 도구 정의
    mcp-config.json       # MCP 설정
    examples/             # Few-shot 예제
  /analysis-agent/
    system-prompt.md
    tools.md
    ...
```

**Example: `/agents/coding-agent/system-prompt.md`**
```markdown
You are an expert software developer.
Write clean, maintainable code following best practices.
Always explain your design decisions.
```

**Example: `/agents/coding-agent/mcp-config.json`**
```json
{
  "servers": {
    "filesystem": {
      "command": "mcp-server-filesystem",
      "args": ["--root", "./workspace"]
    }
  }
}
```

## Core Features

### 1. Convention-based Agent Loading

파일 구조만 맞추면 자동으로 로드됩니다:

```csharp
var orchestrator = new AgentOrchestrator("./agents");
// /agents/ 아래의 모든 에이전트 자동 로드
```

### 2. Automatic Agent Selection

사용자 입력을 분석하여 적합한 에이전트를 자동 선택:

```csharp
// "코드 작성"이라는 키워드로 coding-agent 자동 선택
await orchestrator.ProcessAsync("Python으로 API 서버 코드 작성해줘");

// 또는 명시적 지정
await orchestrator.ProcessAsync(
    "코드 작성해줘", 
    agentName: "coding-agent"
);
```

### 3. Pipeline Processing

입력 전처리와 출력 후처리를 위한 확장 지점:

```csharp
// 전처리: 보안 검증, 컨텍스트 주입
orchestrator.AddPreprocessor(async (input, context) => 
{
    // 민감정보 필터링
    var filtered = FilterSensitiveData(input);
    
    // 컨텍스트 추가
    context["user_id"] = GetCurrentUserId();
    context["timestamp"] = DateTime.UtcNow;
    
    return filtered;
});

// 후처리: 출력 검증, 포맷팅
orchestrator.AddPostprocessor(async (output, context) => 
{
    // 규정 위반 확인
    if (ContainsViolation(output))
        return "요청을 처리할 수 없습니다.";
    
    // 포맷 정규화
    return FormatMarkdown(output);
});
```

### 4. Framework Agnostic

기본적으로 Microsoft Agent Framework를 사용하지만 다른 프레임워크도 지원:

```csharp
// 기본 (Agent Framework)
var orchestrator = new AgentOrchestrator("./agents");

// 커스텀 프레임워크
var orchestrator = new AgentOrchestrator(
    "./agents",
    framework: new LangChainAdapter()
);
```

## What ironbees Does

ironbees는 다음에 집중합니다:

| 기능 | 설명 |
|------|------|
| **Agent Loading** | 파일시스템에서 에이전트 구성 로드 |
| **Agent Selection** | 입력 분석 후 적절한 에이전트 자동 선택 |
| **Pipeline Management** | 전처리/후처리 훅 제공 |
| **Framework Integration** | 다양한 LLM 프레임워크와 통합 |

## What ironbees Doesn't Do

다음은 **의도적으로 제공하지 않습니다**:

| 기능 | 이유 | 대안 |
|------|------|------|
| LLM API 호출 | 프레임워크의 역할 | Agent Framework 등 사용 |
| 대화 기록 관리 | 에이전트의 역할 | 에이전트 tools로 구현 |
| 토큰 관리/요약 | 에이전트의 역할 | 에이전트 tools로 구현 |
| 복잡한 워크플로우 | 범위 초과 | 상위 애플리케이션에서 구현 |

**설계 철학**: 복잡한 기능은 에이전트 레벨에서 system-prompt와 tools로 구현하도록 위임합니다.

## Advanced Usage

### Custom Pipeline

```csharp
var orchestrator = new AgentOrchestrator("./agents");

// 여러 전처리 단계
orchestrator
    .AddPreprocessor(ValidateInput)
    .AddPreprocessor(InjectUserContext)
    .AddPreprocessor(LogRequest);

// 여러 후처리 단계
orchestrator
    .AddPostprocessor(ValidateCompliance)
    .AddPostprocessor(FormatOutput)
    .AddPostprocessor(LogResponse);

var response = await orchestrator.ProcessAsync(userInput);
```

### Agent Configuration

에이전트가 복잡한 기능(세션 관리, 메모리 등)을 처리하는 예시:

```
/agents/conversational-agent/
  system-prompt.md
    → 세션 유지, 컨텍스트 관리 로직 설명
  
  tools.md
    → conversation_history: 대화 기록 조회
    → save_context: 중요 정보 저장
    → summarize: 긴 대화 요약
  
  mcp-config.json
    → 메모리 서버 설정
```

이 방식으로 ironbees 코어는 얇게 유지하면서, 복잡한 로직은 에이전트가 담당합니다.

### Agent Selection Strategy

```csharp
var orchestrator = new AgentOrchestrator("./agents");

// 커스텀 선택 전략
orchestrator.SetSelectionStrategy(async (input, agents) => 
{
    // 규칙 기반
    if (input.Contains("코드")) return agents["coding-agent"];
    if (input.Contains("분석")) return agents["analysis-agent"];
    
    // LLM 기반 분류
    var category = await ClassifyInput(input);
    return agents[category];
});
```

## CLI Tool

패키지 참조 없이 독립 실행 가능한 CLI:

```bash
# 설치
dotnet tool install -g ironbees-cli

# 대화형 모드
ironbees chat --agent coding-agent --agent-path ./agents

# 단일 실행
ironbees process "코드를 작성해줘" --agent-path ./agents

# 에이전트 관리
ironbees agent list --agent-path ./agents
ironbees agent validate coding-agent --agent-path ./agents
ironbees agent create new-agent --agent-path ./agents
```

## Design Principles

### 1. Convention over Configuration
파일 구조가 설정입니다. 규칙을 따르면 코드 없이 동작합니다.

### 2. Thin Layer
최소한의 추상화로 기존 프레임워크의 유연성을 보존합니다.

### 3. Delegate Complexity
복잡한 로직은 에이전트(system-prompt + tools)에 위임합니다.

### 4. File-based Visibility
모든 설정은 버전 관리 가능한 파일로 관리됩니다.

## When to Use ironbees

### ✅ 적합한 경우

- 여러 전문화된 에이전트가 필요할 때
- 에이전트 구성을 파일로 관리하고 싶을 때
- 팀원이 쉽게 에이전트를 추가/수정해야 할 때
- 입력/출력 전후처리가 필요할 때
- 기존 프레임워크 위에 더 높은 추상화가 필요할 때

### ❌ 부적합한 경우

- 단일 에이전트만 필요한 간단한 앱
- 복잡한 상태 머신이나 워크플로우 엔진이 필요한 경우
- LLM 프레임워크를 직접 제어하고 싶을 때

## Philosophy: ironbees

**iron** (철, AI) + **bees** (벌, agents)

작지만 협력적인 에이전트들이 실용적인 목표를 향해 움직이는 것. 각 에이전트는 전문화되어 있고, orchestrator는 적절한 에이전트를 선택하여 임무를 수행합니다.

## Roadmap

- [x] 파일시스템 기반 에이전트 로딩
- [x] 자동 에이전트 선택
- [x] Pipeline 전/후처리
- [x] MCP 지원
- [ ] C# NuGet 패키지 v1.0
- [ ] CLI 도구
- [ ] Agent 템플릿 갤러리
- [ ] Python 구현
- [ ] 다양한 프레임워크 어댑터 (LangChain, LlamaIndex)

## Examples

더 많은 예제는 [examples](./examples) 디렉토리를 참조하세요:

- [Basic Agent](./examples/basic-agent) - 간단한 에이전트 구성
- [Multi-Agent System](./examples/multi-agent) - 여러 에이전트 협업
- [Custom Pipeline](./examples/custom-pipeline) - 커스텀 전후처리
- [MCP Integration](./examples/mcp-integration) - MCP 서버 통합

## Documentation

- 📖 [Agent Structure Guide](docs/agent-structure.md)
- 🔧 [Pipeline Customization](docs/pipeline.md)
- 🎯 [Agent Selection Strategies](docs/agent-selection.md)
- 🔌 [Framework Integration](docs/framework-integration.md)
- 🛠️ [CLI Usage](docs/cli.md)

## Contributing

ironbees는 규칙 기반 접근을 지향합니다. 

새로운 기능보다는 **더 나은 규칙(convention)**을 제안해주세요. 파일 구조, 네이밍 규칙, 설정 형식 등에 대한 개선 아이디어를 환영합니다.