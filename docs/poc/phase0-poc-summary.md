# Phase 0 PoC Summary - Ironbees Agent Mode

**Date**: 2025-11-11
**Phase**: 0 (Foundation Design - Week 1-2)
**Status**: ✅ COMPLETED

---

## Executive Summary

Phase 0 design and PoC validation completed successfully. All critical design documents created and Microsoft.Agents.AI/Microsoft.Extensions.AI verified as viable foundation for Ironbees Agent Mode Phase 1 MVP.

**Key Outcomes**:
- ✅ Architecture Design Document (1,200+ lines)
- ✅ API Specification v1.0 (comprehensive interface definitions)
- ✅ Microsoft.Agents.AI PoC (successful validation)
- ✅ Technology stack validated for Phase 1

---

## 1. Design Documents Created

### 1.1 Architecture Design Document (`docs/architecture/agent-mode-architecture.md`)

**Size**: 1,200+ lines
**Completion**: 100%

**Contents**:
- System overview and core principles
- High-level architecture (Stateful Graph Orchestrator + Microsoft.Agents.AI + MCP)
- Component designs:
  - StatefulGraphOrchestrator interface
  - CodingState record schema (13 fields, immutable)
  - Agent layer (ICodingAgent implementations)
  - MCP protocol layer (IMcpServer)
  - Tool implementations (RoslynTool, MSBuildTool, DotNetTestTool)
- Data flow diagrams
- State transition graphs (`[PLAN] → [WAIT_PLAN_APPROVAL] → [CODE] → [VALIDATE] → [DECIDE]`)
- Technology stack specifications
- Quality attributes (performance, reliability, observability)
- Architecture Decision Records (ADR-001, ADR-002, ADR-003)

**Key Decisions**:
- ADR-001: Stateful Graph Orchestrator over conversational agents (deterministic control)
- ADR-002: Microsoft.Agents.AI adoption (official .NET standard)
- ADR-003: MCP protocol adoption (industry standard)

---

### 1.2 API Specification v1.0 (`docs/api/agent-mode-api-specification.md`)

**Size**: 6,000+ lines
**Completion**: 100%

**Contents**:
- Core interfaces:
  - `IStatefulOrchestrator`: Main entry point for workflows
  - `ICodingAgent`: Agent contract for task execution
  - `IMcpServer`: MCP-compatible tool servers
  - `IToolRegistry`: Tool management
- Data models:
  - `CodingState`: Immutable workflow state (13 properties)
  - `ExecutionPlan`: Structured plan with steps
  - `FileEdit`: File diff representation
  - `BuildResult`: Compilation results
  - `TestResult`: Test execution results
  - `WorkflowContext`: Optional execution context
  - `ApprovalDecision`: HITL approval
- Configuration schema (appsettings.json + agent.yaml)
- Usage examples:
  - Basic DI usage
  - Console application
  - Web API integration
  - Custom agent implementation
  - Custom MCP server
- Error handling hierarchy
- Versioning strategy (Semantic Versioning 2.0.0)
- Migration guide

**API Stability**:
- Stable interfaces: `IStatefulOrchestrator`, `ICodingAgent`, `IMcpServer`, core data models
- Unstable: Internal implementation, prompt templates
- Deprecation policy: 1 major version support

---

## 2. PoC Evaluations

### 2.1 Microsoft.Agents.AI PoC (`poc/AgentsPoc/`)

**Status**: ✅ COMPLETED
**Execution**: Successful
**Package Version**: 1.0.0-preview.251110.2

**Findings**:

#### Package Installation ✅
- ✅ Microsoft.Agents.AI: 1.0.0-preview.251110.2 (PREVIEW)
- ✅ Microsoft.Extensions.AI: 9.10.2 (STABLE)
- ✅ Microsoft.Extensions.AI.OpenAI: 9.10.2-preview.1.25552.1
- ✅ Microsoft.Extensions.Hosting: 9.0.10

#### Core Abstractions ✅
- ✅ **IChatClient**: Primary abstraction for LLM providers
  - Assembly: Microsoft.Extensions.AI.Abstractions 9.10.0.0
  - Methods: GetResponseAsync, GetStreamingResponseAsync, GetService
- ✅ **ChatMessage**: Message structure with roles (System, User, Assistant)
- ✅ **ChatOptions**: Configuration (ModelId, Temperature, MaxOutputTokens, Tools)
- ✅ **AITool**: Tool definition via AIFunctionFactory.Create()

#### Tool Calling Mechanism ✅
- ✅ AIFunctionFactory.Create() creates ReflectionAIFunction
- ✅ Tools can be attached to ChatOptions.Tools
- ✅ Function calling supported in chat completions

#### Dependency Injection ✅
- ✅ Compatible with Host.CreateApplicationBuilder()
- ✅ IChatClient can be registered in DI container
- ✅ Provider configuration supported

#### Preview Status ⚠️
- Microsoft.Agents.AI: PREVIEW (not production-ready yet)
- Microsoft.Extensions.AI: STABLE (production-ready)
- Production GA: Expected Q1 2025

---

### 2.2 Recommendations for Ironbees

Based on PoC findings:

**Phase 1 MVP Strategy** (Weeks 3-6):
1. ✅ Use **Microsoft.Extensions.AI.Abstractions** as foundation (STABLE)
2. ✅ IChatClient as core abstraction (provider-agnostic)
3. ✅ Tool calling mechanism is production-ready
4. ✅ Agent pattern: IChatClient + AITool + ChatOptions
5. ⚠️  Monitor Microsoft.Agents.AI for GA release
6. ✓ Consider Microsoft.SemanticKernel as fallback if needed

**Technology Stack for Phase 1**:
```yaml
Abstraction Layer:
  - Microsoft.Extensions.AI.Abstractions: ^9.10.2

LLM Providers:
  - Primary: Anthropic.SDK (Claude 3.7 Sonnet)
  - Fallback: Microsoft.Extensions.AI.OpenAI (GPT-4o)

Orchestration:
  - Custom: StatefulGraphOrchestrator (lightweight, .NET-native)
  - No dependency on Microsoft.Agents.AI preview

Tools/MCP:
  - Roslyn: Microsoft.CodeAnalysis.Workspaces.MSBuild ^4.14.0
  - MSBuild: Microsoft.Build ^17.7.2
  - Test: dotnet CLI + TRX parsing
  - Git: LibGit2Sharp

Observability:
  - OpenTelemetry: ^1.10.0
  - Serilog: ^4.0.0
```

---

### 2.3 Remaining PoCs (Deferred to Phase 1)

The following PoCs were identified but deferred to early Phase 1 implementation:

**Roslyn API PoC**:
- Package: Microsoft.CodeAnalysis.Workspaces.MSBuild 4.14.0
- Status: Package installed, sample code to be tested during Phase 1 Week 1
- Validation needed:
  - Solution loading (MSBuildWorkspace.OpenSolutionAsync)
  - Symbol resolution (SemanticModel.GetDeclaredSymbol)
  - Reference finding (SymbolFinder.FindReferencesAsync)
  - Type hierarchy (GetBaseType, GetInterfaces)

**MSBuild API PoC**:
- Package: Microsoft.Build 17.7.2
- Status: Deferred to Phase 1 Week 1
- Validation needed:
  - Programmatic build (BuildManager.Build)
  - Error extraction (BuildResult.ResultsByTarget)
  - Diagnostic messages

**dotnet test TRX Parsing PoC**:
- Package: System.Xml.Linq (built-in)
- Status: Deferred to Phase 1 Week 2
- Validation needed:
  - TRX XML structure parsing
  - Test result extraction
  - Failure detail extraction

**Rationale for Deferral**:
- Core architecture validated (Microsoft.Extensions.AI)
- Package availability confirmed
- Detailed API testing better suited for implementation phase
- Focus Phase 0 on design completion (100% achieved)

---

## 3. Architecture Validation

### 3.1 Stateful Graph Orchestrator

**Design**: ✅ COMPLETED
**Implementation**: Pending Phase 1

**State Graph**:
```
[INIT]
   ↓
[PLAN] ← PlannerAgent
   ↓
[WAIT_PLAN_APPROVAL] ← HITL Gate
   ↓
[CODE] ← CoderAgent
   ↓
[VALIDATE] ← ValidatorAgent (MSBuild + Tests)
   ↓
[DECIDE] ← DecisionAgent
   ├─ Success → [END]
   └─ Failure → [CODE] (max 5 iterations)
```

**CodingState Schema**:
```csharp
public record CodingState
{
    public required string StateId { get; init; }
    public required string UserRequest { get; init; }
    public string? Spec { get; init; }
    public ExecutionPlan? Plan { get; init; }
    public ImmutableList<FileEdit> CodeDiffs { get; init; }
    public BuildResult? BuildResult { get; init; }
    public TestResult? TestResult { get; init; }
    public string? ErrorContext { get; init; }
    public int IterationCount { get; init; }
    public int MaxIterations { get; init; } = 5;
    public required string CurrentNode { get; init; }
    public DateTime Timestamp { get; init; }
    public ImmutableDictionary<string, string> Metadata { get; init; }
}
```

**Characteristics**:
- Immutable state transitions (via `with` expressions)
- IAsyncEnumerable for real-time updates
- HITL approval gates (WAIT_PLAN_APPROVAL, WAIT_DIFF_APPROVAL)
- Generate-Validate-Refine loop (max 5 iterations)

---

### 3.2 Agent Layer

**Design**: ✅ COMPLETED
**Pattern**: Stateless agents + IChatClient + Tools

**Agent Types** (Phase 1):
1. **PlannerAgent**:
   - Input: UserRequest + Context
   - Tools: RoslynTool (load_solution, find_references)
   - Output: ExecutionPlan + Spec
   - Next: WAIT_PLAN_APPROVAL

2. **CoderAgent**:
   - Input: ExecutionPlan + CodingState
   - Tools: RoslynTool (symbol analysis)
   - Output: CodeDiffs (List<FileEdit>)
   - Next: VALIDATE

3. **ValidatorAgent**:
   - Input: CodeDiffs + CodingState
   - Tools: MSBuildTool (build_project), DotNetTestTool (run_tests)
   - Output: BuildResult + TestResult
   - Next: DECIDE

4. **DecisionAgent**:
   - Input: BuildResult + TestResult + IterationCount
   - Tools: None (pure decision logic)
   - Output: Success (END) or Failure (CODE with ErrorContext)

**Convention-Based Discovery**:
```
/agents
  /planner
    agent.yaml      # Agent metadata
    prompt.md       # Prompt template
  /coder
    agent.yaml
    prompt.md
  /validator
    agent.yaml
    prompt.md
```

---

### 3.3 MCP Protocol Layer

**Design**: ✅ COMPLETED
**Standard**: MCP (Model Context Protocol) - Anthropic/OpenAI/Google adopted

**Tool Servers** (Phase 1):
1. **RoslynMcpServer** (Critical):
   - Tools:
     - `load_solution`: Load .NET solution for analysis
     - `find_references`: Find all references to a symbol
     - `get_type_hierarchy`: Get type hierarchy for a class
     - `get_diagnostics`: Get compiler diagnostics
   - Package: Microsoft.CodeAnalysis.Workspaces.MSBuild 4.14.0

2. **MSBuildMcpServer** (Critical):
   - Tools:
     - `build_project`: Build a .NET project
     - `get_build_errors`: Extract compilation errors
   - Package: Microsoft.Build 17.7.2

3. **DotNetTestMcpServer** (Critical):
   - Tools:
     - `run_tests`: Execute dotnet test with TRX output
     - `parse_trx`: Parse TRX XML for results
   - Implementation: System.Diagnostics.Process + XDocument

4. **FileMcpServer** (Optional):
   - Tools:
     - `read_file`: Read file contents
     - `write_file`: Write file contents
     - `apply_diff`: Apply unified diff
   - Implementation: System.IO

5. **GitMcpServer** (Phase 2):
   - Tools:
     - `create_branch`: Create feature branch
     - `commit`: Create commit
     - `push`: Push to remote
   - Package: LibGit2Sharp

---

## 4. Quality Attributes

### 4.1 Performance Targets

**Phase 1 MVP**:
- Single-file generation: < 30 seconds
- Multi-file (3 files): < 2 minutes
- Generate-Validate-Refine loop: < 10 minutes (max 5 iterations)

**Phase 2 Advanced**:
- Single-file: < 20 seconds
- Multi-file (5 files): < 3 minutes
- Full workflow: < 8 minutes

---

### 4.2 Reliability Targets

**Phase 1 MVP**:
- Code compilation: >90% success rate (within 5 iterations)
- Test pass rate: >80% (within 5 iterations)
- State transition: 100% consistency

**Phase 2 Advanced**:
- Code compilation: >95%
- Test pass rate: >90%
- Rollback success: 100%

---

### 4.3 Observability

**Requirements**:
- OpenTelemetry integration (traces, metrics, logs)
- Distributed tracing across agents
- State transition logging
- Cost tracking (token usage per agent)
- Performance metrics (latency per node)

**Implementation** (Phase 1):
- OpenTelemetry SDK: ^1.10.0
- Serilog: ^4.0.0
- Structured logging (JSON format)
- Trace context propagation

---

## 5. Package Structure

```
Ironbees.AgentMode/
├── src/
│   ├── Ironbees.AgentMode.Core/              # v1.0.0
│   │   ├── Abstractions/                     # Interfaces
│   │   │   ├── IStatefulOrchestrator.cs
│   │   │   ├── ICodingAgent.cs
│   │   │   └── IMcpServer.cs
│   │   ├── Models/                           # Data models
│   │   │   ├── CodingState.cs
│   │   │   ├── ExecutionPlan.cs
│   │   │   ├── FileEdit.cs
│   │   │   ├── BuildResult.cs
│   │   │   └── TestResult.cs
│   │   ├── Configuration/                    # Configuration
│   │   └── Exceptions/                       # Exception types
│   │
│   ├── Ironbees.AgentMode.Agents/            # v1.0.0
│   │   ├── PlannerAgent.cs
│   │   ├── CoderAgent.cs
│   │   ├── ValidatorAgent.cs
│   │   └── DecisionAgent.cs
│   │
│   ├── Ironbees.AgentMode.MCP/               # v1.0.0
│   │   ├── McpClient.cs
│   │   ├── ToolRegistry.cs
│   │   └── JsonRpcClient.cs
│   │
│   └── Ironbees.AgentMode.Tools/             # v1.0.0
│       ├── RoslynMcpServer.cs
│       ├── MSBuildMcpServer.cs
│       ├── DotNetTestMcpServer.cs
│       └── FileMcpServer.cs
│
├── tests/
│   └── Ironbees.AgentMode.Tests/
│       ├── Unit/
│       ├── Integration/
│       └── Performance/
│
├── samples/
│   └── ConsoleChatSample/                    # (existing)
│
└── poc/                                      # Phase 0 PoCs
    ├── AgentsPoc/                            # ✅ Completed
    └── RoslynPoc/                            # ⏳ Deferred to Phase 1
```

---

## 6. Risk Assessment

### 6.1 Technical Risks

| Risk | Severity | Mitigation | Status |
|------|----------|------------|--------|
| Microsoft.Agents.AI preview instability | Medium | Use Microsoft.Extensions.AI (stable) | ✅ Mitigated |
| Roslyn workspace initialization slow | Low | Lazy loading, caching | ⏳ Phase 1 |
| MSBuild integration complexity | Medium | Use BuildManager API | ⏳ Phase 1 |
| LLM cost (Claude Sonnet 4.5) | Medium | Prompt caching (90% reduction) | 📋 Planned |
| Generate-Validate-Refine loop convergence | High | Max 5 iterations + error context feedback | 📋 Planned |

---

### 6.2 Timeline Risks

| Risk | Impact | Mitigation | Status |
|------|--------|------------|--------|
| Phase 0 design delay | Medium | Completed on time | ✅ Mitigated |
| Microsoft.Agents.AI GA delay | Low | Use stable abstractions | ✅ Mitigated |
| Roslyn/MSBuild learning curve | Medium | Defer detailed PoC to Phase 1 | ✅ Mitigated |

---

## 7. Phase 0 Checklist

### Design Phase ✅
- [x] Architecture Design Document (ADD)
- [x] API Specification v1.0
- [x] CodingState schema definition
- [x] Stateful Graph workflow design
- [x] Agent interface contracts
- [x] MCP protocol integration plan

### PoC Phase (Partial) ✅
- [x] Microsoft.Agents.AI evaluation (COMPLETED)
- [~] Roslyn API test (DEFERRED - package installed)
- [~] MSBuild API test (DEFERRED - package installed)
- [~] dotnet test TRX parsing (DEFERRED - built-in)

**Rationale for Partial PoC**:
- Core architecture validated (Microsoft.Extensions.AI stable)
- Package availability confirmed for all tools
- Detailed API testing more appropriate during Phase 1 implementation
- Focus on design completion (100% achieved)

---

## 8. Next Steps

### Immediate (Phase 1 Week 1 - Weeks 3-4)

**Implementation**:
1. Create package structure (Core, Agents, MCP, Tools)
2. Implement CodingState and core models
3. Implement StatefulGraphOrchestrator (basic state machine)
4. Implement PlannerAgent (with Roslyn tools)
5. Complete Roslyn API PoC during implementation
6. Complete MSBuild API PoC during implementation

**Validation**:
- Unit tests for state transitions
- Integration test: INIT → PLAN → END
- Roslyn tool integration test
- MSBuild tool integration test

### Phase 1 Week 2 (Weeks 5-6)

**Implementation**:
1. Implement CoderAgent
2. Implement ValidatorAgent (MSBuild + dotnet test)
3. Complete Generate-Validate-Refine loop
4. Implement HITL approval gates

**Validation**:
- E2E test: Simple function addition
- E2E test: Fix-It scenario
- Generate-Validate-Refine convergence test

---

## 9. Success Metrics

### Phase 0 (Week 1-2) ✅ ACHIEVED
- [x] Complete design documents (ADD + API Spec)
- [x] Validate core technology stack (Microsoft.Extensions.AI)
- [x] Confirm package availability (Roslyn, MSBuild)
- [x] Architecture decision records

### Phase 1 MVP (Week 3-6) 📋 PLANNED
- [ ] Generate-Validate-Refine loop working
- [ ] 80% success rate on Fix-It scenarios
- [ ] Single-file generation < 30s
- [ ] 10+ integration tests passing

### Phase 2 Advanced (Week 7-12) 📋 PLANNED
- [ ] Multi-file coordination (3+ files)
- [ ] HITL workflow (Spec→Plan→Diff approval)
- [ ] 90% success rate on Fix-It scenarios
- [ ] 50+ integration tests passing

---

## 10. References

### Design Documents
- [Architecture Design Document](../architecture/agent-mode-architecture.md)
- [API Specification v1.0](../api/agent-mode-api-specification.md)
- [Agent Mode Roadmap](../../local-docs/agent-mode-roadmap.md)
- [Research Report v1](../../local-docs/research1.md)

### PoC Projects
- [Microsoft.Agents.AI PoC](../../poc/AgentsPoc/)
- [Roslyn PoC](../../poc/RoslynPoc/) (deferred)

### Package Documentation
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI/)
- [Microsoft.CodeAnalysis](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/)
- [Microsoft.Build](https://www.nuget.org/packages/Microsoft.Build/)
- [MCP Protocol](https://modelcontextprotocol.io/)

---

**Phase 0 Status**: ✅ COMPLETED
**Next Phase**: Phase 1 MVP (Week 3-6)
**Approval**: Ready for Phase 1 implementation

---

**Document Version**: 1.0
**Last Updated**: 2025-11-11
**Author**: Ironbees Development Team
