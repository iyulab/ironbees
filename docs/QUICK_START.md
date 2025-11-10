# 🚀 Ironbees 5분 빠른 시작

**목표**: 5분 안에 첫 번째 에이전트를 만들고 실행하기

## 📋 사전 요구사항

- .NET 9.0 SDK
- Azure OpenAI 계정 (또는 OpenAI API)
- 코드 에디터 (VS Code, Visual Studio, Rider 등)

## 1단계: 프로젝트 생성 (30초)

```bash
# 콘솔 앱 생성
dotnet new console -n MyFirstAgent
cd MyFirstAgent

# Ironbees 패키지 설치
dotnet add package Ironbees.Core
dotnet add package Ironbees.AgentFramework
```

## 2단계: 에이전트 디렉터리 생성 (1분)

```bash
# 에이전트 디렉터리 구조 생성
mkdir -p agents/helper-agent

# agent.yaml 생성
cat > agents/helper-agent/agent.yaml << 'EOF'
name: helper-agent
description: A helpful assistant that answers questions
version: 1.0.0
model:
  deployment: gpt-4
  temperature: 0.7
  maxTokens: 1000
  topP: 1.0
capabilities:
  - question-answering
  - general-assistance
tags:
  - helper
  - assistant
EOF

# system-prompt.md 생성
cat > agents/helper-agent/system-prompt.md << 'EOF'
You are a helpful assistant that provides clear, concise answers to user questions.

Guidelines:
- Be friendly and professional
- Provide accurate information
- Ask for clarification if needed
- Keep responses focused and relevant
EOF
```

**Windows PowerShell의 경우:**
```powershell
# 디렉터리 생성
New-Item -ItemType Directory -Path "agents\helper-agent" -Force

# agent.yaml
@"
name: helper-agent
description: A helpful assistant that answers questions
version: 1.0.0
model:
  deployment: gpt-4
  temperature: 0.7
  maxTokens: 1000
  topP: 1.0
capabilities:
  - question-answering
  - general-assistance
tags:
  - helper
  - assistant
"@ | Out-File -FilePath "agents\helper-agent\agent.yaml" -Encoding utf8

# system-prompt.md
@"
You are a helpful assistant that provides clear, concise answers to user questions.

Guidelines:
- Be friendly and professional
- Provide accurate information
- Ask for clarification if needed
- Keep responses focused and relevant
"@ | Out-File -FilePath "agents\helper-agent\system-prompt.md" -Encoding utf8
```

## 3단계: 코드 작성 (2분)

**Program.cs:**
```csharp
using Ironbees.Core;
using Ironbees.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

// 1. 서비스 구성
var services = new ServiceCollection();

services.AddIronbees(options =>
{
    // Azure OpenAI 설정
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com";
    options.AzureOpenAIKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.AgentsDirectory = "./agents";

    // 선택사항: Microsoft Agent Framework 사용
    options.UseMicrosoftAgentFramework = false; // true로 설정하면 MAF 사용
});

var serviceProvider = services.BuildServiceProvider();

// 2. Orchestrator 가져오기
var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();

// 3. 에이전트 로드
Console.WriteLine("Loading agents...");
await orchestrator.LoadAgentsAsync();

// 4. 에이전트와 대화
Console.WriteLine("\n🐝 Ironbees Agent Ready!\n");
Console.WriteLine("Type your question (or 'exit' to quit):\n");

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
        break;

    Console.Write("Agent: ");

    // 스트리밍 응답
    await foreach (var chunk in orchestrator.StreamAsync(input, "helper-agent"))
    {
        Console.Write(chunk);
    }

    Console.WriteLine("\n");
}

Console.WriteLine("Goodbye! 👋");
```

## 4단계: 환경 변수 설정 (30초)

```bash
# Linux/Mac
export AZURE_OPENAI_KEY="your-api-key-here"

# Windows PowerShell
$env:AZURE_OPENAI_KEY="your-api-key-here"

# Windows CMD
set AZURE_OPENAI_KEY=your-api-key-here
```

## 5단계: 실행! (30초)

```bash
dotnet run
```

**예상 출력:**
```
Loading agents...
🐝 Ironbees Agent Ready!

Type your question (or 'exit' to quit):

You: What is Ironbees?
Agent: Ironbees is a lightweight wrapper for LLM agent management in .NET...

You: exit
Goodbye! 👋
```

## 🎉 성공!

축하합니다! 첫 번째 Ironbees 에이전트를 실행했습니다.

## 🔄 다음 단계

### 여러 에이전트 추가

```bash
# 코딩 에이전트 추가
mkdir -p agents/coding-agent
```

**agents/coding-agent/agent.yaml:**
```yaml
name: coding-agent
description: Expert software developer for coding tasks
version: 1.0.0
model:
  deployment: gpt-4
  temperature: 0.3  # 낮은 temperature로 일관된 코드 생성
  maxTokens: 2000
capabilities:
  - code-generation
  - code-review
  - debugging
tags:
  - coding
  - development
```

**agents/coding-agent/system-prompt.md:**
```markdown
You are an expert software developer specializing in C# and .NET.

When writing code:
- Follow best practices and design patterns
- Include error handling
- Add helpful comments
- Use modern C# features
- Ensure code is testable
```

### 자동 라우팅 사용

```csharp
// 에이전트 이름 지정 없이 자동 선택
var response = await orchestrator.ProcessAsync("Write a C# method to calculate fibonacci");
// → 자동으로 coding-agent 선택
```

### 옵션 설정

```csharp
services.AddIronbees(options =>
{
    options.AzureOpenAIEndpoint = "...";
    options.AzureOpenAIKey = "...";

    // 에이전트 선택 신뢰도 임계값 (기본: 0.6)
    options.ConfidenceThreshold = 0.7;

    // 폴백 에이전트 (신뢰도 낮을 때)
    options.FallbackAgentName = "helper-agent";

    // Microsoft Agent Framework 사용
    options.UseMicrosoftAgentFramework = true;
});
```

## 📚 더 알아보기

- [상세 가이드](GETTING_STARTED.md) - 전체 기능 탐색
- [아키텍처](ARCHITECTURE.md) - 내부 동작 이해
- [사용 패턴](USAGE.md) - 고급 사용 사례
- [Microsoft Agent Framework](MICROSOFT_AGENT_FRAMEWORK.md) - MAF 통합
- [프로덕션 배포](PRODUCTION_DEPLOYMENT.md) - 운영 환경 가이드
- [커스텀 어댑터](CUSTOM_ADAPTER.md) - 다른 LLM 프레임워크 통합

## ❓ 문제 해결

### "에이전트를 찾을 수 없습니다"
- `agents/` 디렉터리 경로 확인
- `agent.yaml`과 `system-prompt.md` 파일 존재 확인
- YAML 문법 오류 확인

### "Azure OpenAI 인증 실패"
- `AZURE_OPENAI_KEY` 환경 변수 설정 확인
- 엔드포인트 URL 정확성 확인
- API 키 권한 확인

### "에이전트 검증 오류"
- `agent.yaml`의 필수 필드 확인 (name, description, version, model)
- 버전이 semantic versioning 형식인지 확인 (예: 1.0.0)
- 에이전트 이름이 소문자-하이픈 형식인지 확인 (예: helper-agent)

## 💡 팁

1. **Hot Reload 활성화** (개발 중):
   ```csharp
   var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions
   {
       EnableHotReload = true  // 파일 변경 시 자동 리로드
   });
   ```

2. **상세 검증** (프로덕션):
   ```csharp
   var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions
   {
       EnableValidation = true,
       StrictValidation = true  // 경고도 오류로 처리
   });
   ```

3. **성능 최적화**:
   ```csharp
   var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions
   {
       EnableCaching = true  // 파일 캐싱 (기본값)
   });
   ```

## 🎓 학습 자료

- **샘플 프로젝트**: `samples/` 디렉터리 참조
  - OpenAISample: 기본 사용법
  - WebApiSample: REST API 서버
  - ConsoleChatSample: 대화형 CLI

- **내장 에이전트**: `agents/` 디렉터리에서 예제 확인
  - coding-agent: 소프트웨어 개발
  - writing-agent: 콘텐츠 작성
  - analysis-agent: 데이터 분석
  - review-agent: 품질 검토

---

**다음 읽기**: [상세 시작 가이드](GETTING_STARTED.md) →
