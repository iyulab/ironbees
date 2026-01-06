# ADR-001: Remove ConversationalAgent and Adopt Service Layer Pattern

**Date**: 2026-01-06
**Status**: Accepted
**Decision Makers**: Ironbees Core Team
**Informed By**: MLoop Team Migration Experience (v0.1.8 → v0.4.1)

---

## Context and Problem Statement

The `ConversationalAgent` class in v0.1.8 coupled business logic with LLM orchestration, creating several issues:

1. **Testability**: Business logic embedded in agent classes required LLM calls for testing
2. **Maintainability**: Prompts embedded in C# code were hard to version and review
3. **Philosophy Violation**: Ironbees should provide **declaration** mechanisms, not execution logic
4. **Framework Redundancy**: Microsoft Agent Framework (MAF) already provides robust agent orchestration

**Quantified Impact** (MLoop Team):
- Test coverage: 45% → 85% after migration
- Code reduction: 25% (removed LLM-coupled agent classes)
- Migration time: 4 hours (with comprehensive guides)

---

## Decision Drivers

### Technical Drivers
- **Thin Wrapper Philosophy**: Delegate execution to MAF, focus on conventions
- **Testability**: Separate deterministic logic from non-deterministic LLM calls
- **Maintainability**: Version prompts as markdown, not C# strings
- **Framework Alignment**: Use MAF's agent orchestration, not reinvent

### Real-World Evidence
MLoop team's migration demonstrated:
- ✅ **Business logic became testable**: Unit tests without LLM mocking
- ✅ **Prompts became reviewable**: Markdown files in version control
- ✅ **Architecture improved**: Clear separation of concerns
- ✅ **Code simplified**: 25% reduction in codebase

---

## Considered Options

### Option 1: Keep ConversationalAgent (Status Quo)
**Pros**:
- No breaking change for existing users
- Familiar API for simple scenarios

**Cons**:
- ❌ Violates Thin Wrapper philosophy
- ❌ Couples business logic with LLM orchestration
- ❌ Reduces testability (requires LLM mocking)
- ❌ Duplicates MAF functionality
- ❌ Prompts hard to version and review

**Verdict**: Rejected (violates core philosophy)

---

### Option 2: Deprecate with Migration Path (Chosen)
**Pros**:
- ✅ Aligns with Thin Wrapper philosophy
- ✅ Improves testability (separate business logic)
- ✅ Leverages MAF for orchestration
- ✅ Prompts in version-controlled markdown
- ✅ Clear migration path with automation

**Cons**:
- Breaking change (requires migration effort)
- User education needed

**Verdict**: **Accepted** (long-term benefits outweigh short-term migration cost)

---

### Option 3: Mark as Obsolete (Gradual Deprecation)
**Pros**:
- Less disruptive transition
- Users can migrate at their own pace

**Cons**:
- ❌ Maintains code that violates philosophy
- ❌ Confusion about recommended patterns
- ❌ Delays inevitable migration
- ❌ Maintenance burden for deprecated code

**Verdict**: Rejected (delays problem, creates confusion)

---

## Decision Outcome

**Chosen Option**: Remove `ConversationalAgent` and adopt **Service Layer Pattern**.

### Service Layer Pattern Architecture

```
[Service Layer]          [Agent Configuration]       [Orchestration]
Business Logic    →      agent.yaml                 →  IAgentOrchestrator
(Pure C# classes)        system-prompt.md              (MAF execution)
```

**Responsibilities**:

| Layer | What It Does | Example |
|-------|--------------|---------|
| **Service Layer** | Deterministic business logic (testable) | `DataAnalyzer.Analyze(data)` |
| **Agent Config** | LLM prompt and model settings (declarative) | `agents/data-analyzer/agent.yaml` |
| **Orchestration** | Agent execution and memory (MAF runtime) | `orchestrator.ExecuteAsync("data-analyzer")` |

---

## Migration Strategy

### 3-Step Migration (1-2 hours)

**Step 1: Extract Business Logic**
```csharp
// Before (v0.1.8)
var agent = new ConversationalAgent(chatClient);
var response = await agent.SendAsync("analyze this data");

// After (v0.4.1) - Service Layer
public class DataAnalyzer
{
    public DataAnalysisResult Analyze(DataFrame data)
    {
        // Pure C# logic (no LLM)
        var columns = AnalyzeColumns(data);
        var quality = CalculateDataQuality(columns);
        return new DataAnalysisResult { Columns = columns, Quality = quality };
    }
}
```

**Step 2: Create Agent Configuration**
```yaml
# agents/data-analyzer/agent.yaml
id: data-analyzer
name: Data Analyzer Agent
model:
  provider: openai
  name: gpt-4o-mini
```

**Step 3: Use Agent Orchestrator**
```csharp
// Orchestration via IAgentOrchestrator
var result = await orchestrator.ExecuteAsync("data-analyzer", request);
```

**Full Guide**: [docs/migration/service-layer-pattern.md](../migration/service-layer-pattern.md)

---

## Consequences

### Positive Consequences

**Testability**:
- ✅ Business logic testable without LLM (unit tests)
- ✅ Integration tests only for orchestration layer
- ✅ Deterministic tests for 85% of codebase (MLoop: 45% → 85%)

**Maintainability**:
- ✅ Prompts in markdown (version-controlled, reviewable)
- ✅ Model config in YAML (declarative, easy to change)
- ✅ Business logic in C# (type-safe, refactorable)

**Architecture**:
- ✅ Clear separation of concerns (logic vs config vs execution)
- ✅ Aligns with Thin Wrapper philosophy
- ✅ Leverages MAF's robust orchestration

**Code Quality**:
- ✅ 25% code reduction (MLoop experience)
- ✅ Easier onboarding (clear architecture)
- ✅ Better debugging (isolated layers)

### Negative Consequences

**Migration Cost**:
- ⚠️ Breaking change (requires user migration)
- ⚠️ 1-2 hours migration time per project
- ⚠️ Learning curve for Service Layer pattern

**Mitigation**:
- ✅ Comprehensive migration guide with real examples
- ✅ MLoop case study documenting successful migration
- ✅ Decision framework for "what goes where"
- ✅ Before/after code samples for common patterns

---

## Validation (MLoop Case Study)

### Migration Results

| Metric | Before (v0.1.8) | After (v0.4.1) | Change |
|--------|-----------------|----------------|--------|
| Test Coverage | 45% | 85% | +40% |
| Codebase Size | 100% | 75% | -25% |
| Migration Time | N/A | 4 hours | - |
| Agent Count | 5 | 5 | 0 (same) |

### Key Learnings

**What Worked**:
1. ✅ Service Layer pattern natural fit for business logic
2. ✅ Markdown prompts easier to review and version
3. ✅ Testing became faster (no LLM mocking needed)
4. ✅ Code reviews improved (prompts visible in PRs)

**Challenges**:
1. ⚠️ Learning curve for "what goes where" (solved with decision framework)
2. ⚠️ State management patterns needed documentation (added to guide)
3. ⚠️ Integration test patterns unclear (examples added)

**Overall**: Migration considered successful by MLoop team (85% test coverage achieved).

---

## Alternatives Considered and Rejected

### Alternative 1: Hybrid Approach
Keep `ConversationalAgent` for simple cases, recommend Service Layer for complex ones.

**Rejected Because**:
- Creates two patterns for same problem
- Confusion about when to use which
- Violates philosophy (Thin Wrapper)
- Maintenance burden for both approaches

### Alternative 2: Provide Migration Shim
Create compatibility layer to ease transition.

**Rejected Because**:
- Delays inevitable migration
- Adds complexity to codebase
- Doesn't solve fundamental architecture issues
- Users would still need to migrate eventually

### Alternative 3: Enhance ConversationalAgent
Improve existing class rather than remove.

**Rejected Because**:
- Doesn't address core philosophy violation
- Continues to couple logic and orchestration
- MAF already provides better orchestration
- Perpetuates anti-pattern

---

## Related Decisions

- **ADR-002**: Adopt ChatClientBuilder Pattern (replaces LLMProviderFactoryRegistry)
- **ADR-003**: Namespace Restructuring (removes `.Core` suffix)
- **Philosophy Document**: [PHILOSOPHY.md](../../PHILOSOPHY.md) - Thin Wrapper principle

---

## References

### Documentation
- [Service Layer Pattern Migration Guide](../migration/service-layer-pattern.md)
- [MLoop Case Study](../migration/service-layer-pattern.md#real-world-migration-mloop-case-study)
- [Decision Framework](../migration/service-layer-pattern.md#decision-framework-what-goes-where)

### External Resources
- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/dotnet/ai/microsoft-agents-ai)
- [Separation of Concerns Principle](https://en.wikipedia.org/wiki/Separation_of_concerns)
- [Testable Architecture Patterns](https://martinfowler.com/articles/practical-test-pyramid.html)

---

## Notes

**Future Considerations**:
- Monitor community feedback on Service Layer pattern adoption
- Collect additional case studies from other migrations
- Consider creating .NET template for Service Layer pattern
- Evaluate if additional automation tools would help

**Review Schedule**: 6 months (2026-07-06) - assess migration success and gather feedback.

---

**Signatures**:
- **Proposed By**: Ironbees Core Team
- **Validated By**: MLoop Team (real-world migration)
- **Approved By**: Ironbees Maintainers
- **Date Effective**: v0.4.0 (2025-12-30)
