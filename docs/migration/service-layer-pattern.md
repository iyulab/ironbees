# Migration Guide: Service Layer Pattern

> **Breaking Change**: v0.1.8 → v0.4.1
>
> This guide explains the architectural shift from `ConversationalAgent` base class to the **Service Layer Pattern** introduced in v0.4.1.

**Migration Time Estimate**:
- Per agent: 30-60 minutes
- 5 agents: 3-4 hours (including tests)

**Table of Contents**:
- [Philosophy: Declaration vs Execution](#philosophy-declaration-vs-execution)
- [Migration Strategy](#migration-strategy)
- [Decision Framework](#decision-framework)
- [State Management](#state-management)
- [Testing Strategy](#testing-strategy)
- [Real-World Examples](#real-world-examples)

---

## Philosophy: Declaration vs Execution

### The Old Way (v0.1.8): Mixed Concerns

```csharp
public class DataAnalystAgent : ConversationalAgent
{
    public DataAnalystAgent(IChatClient chatClient) : base(chatClient)
    {
        Name = "data-analyst";
        Description = "Expert data analyst";
    }

    public async Task<DataAnalysisResult> AnalyzeAsync(string data)
    {
        // ❌ Mixed concerns:
        // - Business logic (parsing, calculations)
        // - LLM interaction (prompting, response parsing)
        // - Configuration (temperature, model)
        var response = await base.SendMessageAsync($"Analyze: {data}");
        return ParseResponse(response);  // Fragile string parsing
    }
}
```

**Problems**:
- ❌ **Hard to test**: Requires LLM or complex mocking
- ❌ **Not reusable**: Business logic tied to LLM wrapper
- ❌ **Unclear responsibilities**: What's configuration vs logic?
- ❌ **Low coverage**: MLoop had 45% test coverage

### The New Way (v0.4.1): Separation of Concerns

#### 1. Service Layer (Pure Business Logic)

```csharp
// No LLM dependency, pure C# logic
public class DataAnalyzer
{
    public DataAnalysisResult Analyze(DataFrame data)
    {
        // ✅ Deterministic, testable logic
        var columns = AnalyzeColumns(data);
        var quality = CalculateDataQuality(columns);
        return new DataAnalysisResult { Columns = columns, Quality = quality };
    }
}
```

#### 2. Agent Configuration (YAML Declaration)

```yaml
# agents/data-analyst/agent.yaml
name: data-analyst
description: Expert data analyst
model:
  deployment: gpt-4o
  temperature: 0.0
capabilities:
  - data-analysis
  - statistics
```

#### 3. System Prompt (LLM Behavior)

```markdown
# agents/data-analyst/system-prompt.md

You are an expert data analyst. Analyze datasets and provide insights.

## Output Format
Provide analysis in JSON: { "columns": [...], "quality": ... }
```

#### 4. Orchestration (Execution Layer)

```csharp
public async Task<DataAnalysisResult> ExecuteAnalysisAsync(DataFrame data)
{
    // ✅ Use service for deterministic logic
    var analyzer = new DataAnalyzer();
    var result = analyzer.Analyze(data);

    // ✅ Use agent for insights (if needed)
    if (requiresLLMInsights)
    {
        var insights = await _orchestrator.StreamAsync(
            input: JsonSerializer.Serialize(result),
            agentName: "data-analyst");
        result.LLMInsights = insights;
    }

    return result;
}
```

**Benefits**:
- ✅ **Easy to test**: Service layer has no LLM dependency
- ✅ **Reusable**: Business logic works without ironbees
- ✅ **Clear separation**: Declaration (YAML) vs Execution (C#)
- ✅ **High coverage**: MLoop achieved 85% test coverage

---

## Migration Strategy

### Step 1: Identify Agent Responsibilities

For each `ConversationalAgent` class, categorize methods:

| Method | Category | New Location |
|--------|----------|--------------|
| `AnalyzeData(DataFrame)` | Deterministic Logic | Service Layer |
| System prompt text | LLM Behavior | system-prompt.md |
| Model config (temp, tokens) | LLM Configuration | agent.yaml |
| `SendMessageAsync()` calls | Orchestration | AgentCoordinator |

**Example** (DataAnalystAgent):
```
Methods:
- AnalyzeDataset() → DataAnalyzer.Analyze() [Service]
- Name, Description → agent.yaml [Config]
- Prompt template → system-prompt.md [Behavior]
- LLM calls → IronbeesOrchestrator.StreamAsync() [Orchestration]
```

---

### Step 2: Extract Service Layer

**Before (ConversationalAgent)**:
```csharp
public class DataAnalystAgent : ConversationalAgent
{
    public DataAnalystAgent(IChatClient chatClient, ILogger<DataAnalystAgent> logger)
        : base(chatClient)
    {
        Name = "data-analyst";
        Description = "Expert data analyst";
        _logger = logger;
    }

    public async Task<DataAnalysisResult> AnalyzeDatasetAsync(string csvPath)
    {
        _logger.LogInformation("Analyzing: {Path}", csvPath);

        // ❌ Mixed: File I/O + LLM prompting + parsing
        var data = await File.ReadAllTextAsync(csvPath);
        var prompt = $"Analyze dataset:\n{data}";
        var response = await base.SendMessageAsync(prompt);
        return ParseAnalysisResponse(response);
    }

    private DataAnalysisResult ParseAnalysisResponse(string response)
    {
        // ❌ Fragile string parsing
        // ...
    }
}
```

**After (Service Layer)**:
```csharp
// Pure business logic, no LLM
public class DataAnalyzer
{
    private readonly ILogger<DataAnalyzer>? _logger;

    public DataAnalyzer(ILogger<DataAnalyzer>? logger = null)
    {
        _logger = logger;
    }

    public DataAnalysisResult Analyze(DataFrame data)
    {
        _logger?.LogInformation("Analyzing {RowCount} rows", data.RowCount);

        // ✅ Deterministic, testable logic
        var columns = data.Columns.Select(AnalyzeColumn).ToList();
        var qualityScore = CalculateDataQuality(columns);

        return new DataAnalysisResult
        {
            Columns = columns,
            RowCount = data.RowCount,
            DataQualityScore = qualityScore
        };
    }

    private ColumnInfo AnalyzeColumn(DataFrameColumn column)
    {
        return new ColumnInfo
        {
            Name = column.Name,
            Type = InferType(column),
            NullCount = column.NullCount,
            DistinctCount = column.Unique().Count
        };
    }

    private double CalculateDataQuality(List<ColumnInfo> columns)
    {
        var nullRatio = columns.Average(c => (double)c.NullCount / c.RowCount);
        return 1.0 - nullRatio;  // Simple quality metric
    }

    private DataType InferType(DataFrameColumn column)
    {
        // Type inference logic
        if (column is PrimitiveDataFrameColumn<int>) return DataType.Integer;
        if (column is PrimitiveDataFrameColumn<double>) return DataType.Numeric;
        if (column is StringDataFrameColumn) return DataType.Categorical;
        return DataType.Unknown;
    }
}
```

**Key Changes**:
- ✅ No `ConversationalAgent` base class
- ✅ No `IChatClient` dependency
- ✅ All logic is deterministic and testable
- ✅ Returns strongly-typed objects, not strings

---

### Step 3: Create Agent Template

**File**: `agents/data-analyst/agent.yaml`

```yaml
name: data-analyst
description: Expert data analyst specializing in ML dataset analysis

model:
  deployment: gpt-4o
  temperature: 0.0
  maxTokens: 4096

capabilities:
  - data-analysis
  - statistics
  - ml-readiness
  - data-quality

conversation:
  enabled: true
  max_history_turns: 10
```

**File**: `agents/data-analyst/system-prompt.md`

```markdown
# Data Analyst Agent

You are an expert data analyst specializing in machine learning dataset analysis.

## Core Responsibilities

1. **Analyze Data Quality**
   - Identify missing values, outliers, inconsistencies
   - Assess data readiness for ML training
   - Recommend preprocessing strategies

2. **Statistical Analysis**
   - Provide descriptive statistics for numeric columns
   - Identify categorical value distributions
   - Detect correlations and patterns

3. **ML Readiness Assessment**
   - Recommend target column based on problem type
   - Identify feature engineering opportunities
   - Assess dataset size and balance

## Output Format

Provide analysis in structured JSON format:
```json
{
  "columns": [
    { "name": "age", "type": "numeric", "nullCount": 5, "distinctCount": 42 }
  ],
  "dataQuality": {
    "score": 0.95,
    "issues": ["5 missing values in 'age' column"]
  },
  "recommendations": [
    "Consider imputing missing 'age' values with median",
    "Target column: 'income' (binary classification)"
  ]
}
```

## Example

**Input**: DataFrame with columns [age, income, education]
**Output**: Analysis with type inference, quality score, recommendations
```
```

---

### Step 4: Update Orchestration

**Before (Direct Agent Call)**:
```csharp
var agent = new DataAnalystAgent(chatClient, logger);
var result = await agent.AnalyzeDatasetAsync("dataset.csv");
```

**After (Service + Orchestrator)**:
```csharp
public class AgentCoordinator
{
    private readonly IronbeesOrchestrator _orchestrator;
    private readonly ILogger _logger;

    public async Task<DataAnalysisResult> ExecuteDataAnalysisAsync(
        DataFrame data,
        CancellationToken cancellationToken)
    {
        // Step 1: Use service for deterministic analysis
        var analyzer = new DataAnalyzer(_logger);
        var result = analyzer.Analyze(data);

        // Step 2: Use agent for LLM insights (if needed)
        if (RequiresLLMInsights(result))
        {
            var responseBuilder = new StringBuilder();
            await foreach (var chunk in _orchestrator.StreamAsync(
                input: JsonSerializer.Serialize(result),
                agentName: "data-analyst",
                conversationId: null,
                cancellationToken))
            {
                responseBuilder.Append(chunk);
            }

            // Parse LLM recommendations
            var llmResponse = responseBuilder.ToString();
            result.LLMRecommendations = ParseRecommendations(llmResponse);
        }

        return result;
    }

    private bool RequiresLLMInsights(DataAnalysisResult result)
    {
        // Use LLM for complex pattern recognition, not simple stats
        return result.DataQualityScore < 0.9 || result.Columns.Count > 10;
    }
}
```

---

### Step 5: Refactor Tests

**Before (Hard to Test)**:
```csharp
[Fact]
public async Task AnalyzeDataset_ReturnsValidResult()
{
    // ❌ Requires real LLM or complex mocking
    var mockChatClient = new Mock<IChatClient>();
    mockChatClient.Setup(x => x.CompleteAsync(...))
        .ReturnsAsync(new ChatCompletion { Content = "Mock response" });

    var agent = new DataAnalystAgent(mockChatClient.Object, logger);
    var result = await agent.AnalyzeDatasetAsync("test.csv");

    // ❌ Test depends on LLM response parsing
    Assert.NotNull(result);
}
```

**After (Easy to Test)**:

**Unit Tests (No LLM)**:
```csharp
[Fact]
public void Analyze_WithValidData_ReturnsCorrectStatistics()
{
    // ✅ No LLM, pure business logic
    var analyzer = new DataAnalyzer();
    var data = new DataFrame
    {
        Columns = new[] { CreateColumn("Age", DataType.Integer, 100) }
    };

    var result = analyzer.Analyze(data);

    // ✅ Deterministic assertions
    Assert.Equal(1, result.Columns.Count);
    Assert.Equal(100, result.RowCount);
    Assert.InRange(result.DataQualityScore, 0.0, 1.0);
}

[Fact]
public void Analyze_WithMissingValues_CalculatesCorrectQuality()
{
    var analyzer = new DataAnalyzer();
    var data = CreateDataFrameWithNulls(totalRows: 100, nullCount: 10);

    var result = analyzer.Analyze(data);

    Assert.Equal(0.9, result.DataQualityScore, precision: 2);
}
```

**Integration Tests (With LLM)**:
```csharp
[Trait("Category", "Integration")]
[Fact(Skip = "Requires API key")]
public async Task DataAnalystAgent_WithRealLLM_ProvidesInsights()
{
    // ✅ Optional integration test
    var orchestrator = IronbeesOrchestrator.CreateFromEnvironment();
    var coordinator = new AgentCoordinator(orchestrator, logger);

    var data = LoadSampleDataFrame();
    var result = await coordinator.ExecuteDataAnalysisAsync(data, CancellationToken.None);

    Assert.NotEmpty(result.LLMRecommendations);
}
```

**Test Organization**:
```
tests/
├── MLoop.AIAgent.Tests/
│   ├── Services/                    # Unit tests (fast, no LLM)
│   │   ├── DataAnalyzerTests.cs
│   │   ├── ModelRecommenderTests.cs
│   │   └── PreprocessingScriptGeneratorTests.cs
│   └── Integration/                 # LLM tests (slow, require API key)
│       ├── DataAnalystAgentTests.cs
│       └── MLOpsOrchestratorTests.cs
```

**MLoop Results**:
- Before: 45% test coverage (hard to test agents)
- After: 85% test coverage (service layer tests)

---

## Decision Framework

### What Goes Where?

#### Service Layer (C# Code) ✅

**Deterministic Logic**:
- ✅ Statistics, calculations, algorithms
- ✅ Data transformations (cleaning, normalization)
- ✅ Business rules (validation, constraints)
- ✅ File I/O, database queries, API calls
- ✅ Anything testable without LLM

**Example**:
```csharp
public class ModelRecommender
{
    public ModelRecommendation Recommend(DataAnalysisResult analysis)
    {
        // ✅ Rule-based logic
        if (analysis.Columns.Any(c => c.Type == DataType.Categorical))
            return new ModelRecommendation { Type = "Classification" };

        return new ModelRecommendation { Type = "Regression" };
    }
}
```

#### agent.yaml + system-prompt.md ✅

**LLM Configuration & Behavior**:
- ✅ Model selection (gpt-4o, claude-3-5-sonnet)
- ✅ Temperature, max tokens
- ✅ Agent personality, communication style
- ✅ Domain expertise descriptions
- ✅ Output format instructions
- ✅ Few-shot examples

**Example**:
```yaml
# agent.yaml
model:
  deployment: gpt-4o
  temperature: 0.7  # Creative for recommendations
```

```markdown
# system-prompt.md
You are a friendly ML expert. Provide recommendations in conversational tone.
```

#### IronbeesOrchestrator.StreamAsync() ✅

**When to Use Agent**:
- ✅ Natural language interpretation
- ✅ Reasoning, judgment calls
- ✅ Human-readable explanations
- ✅ Ambiguous/unstructured input
- ✅ Pattern recognition in text

**Example**:
```csharp
// Use agent for interpretation
var insights = await _orchestrator.StreamAsync(
    input: "What's the best model for predicting house prices?",
    agentName: "model-recommender");
```

#### Decision Table

| Task | Service Layer | agent.yaml | Orchestrator |
|------|--------------|------------|--------------|
| Calculate mean/median | ✅ | ❌ | ❌ |
| Parse CSV file | ✅ | ❌ | ❌ |
| Validate data format | ✅ | ❌ | ❌ |
| Model configuration | ❌ | ✅ | ❌ |
| System prompt | ❌ | ✅ | ❌ |
| Interpret user intent | ❌ | ❌ | ✅ |
| Generate recommendations | ❌ | ❌ | ✅ |
| Explain analysis results | ❌ | ❌ | ✅ |

---

## State Management

### The Old Way: Stateful Agents ❌

```csharp
public class PreprocessingExpertAgent : ConversationalAgent
{
    private DataAnalysisResult? _currentAnalysis;  // ❌ Mutable state
    private List<PreprocessingScript> _scripts = new();

    public async Task<PreprocessingResult> GenerateScriptsAsync(DataAnalysisResult analysis)
    {
        _currentAnalysis = analysis;  // ❌ Side effect
        var response = await SendMessageAsync(BuildPrompt(analysis));
        _scripts = ParseScripts(response);  // ❌ Stored in agent
        return new PreprocessingResult { Scripts = _scripts };
    }
}
```

**Problems**:
- ❌ Thread-safety concerns
- ❌ Unclear lifecycle
- ❌ Hard to test (requires setup)

### The New Way: Stateless Services ✅

```csharp
public class PreprocessingScriptGenerator
{
    // ✅ Stateless: all inputs via parameters
    public PreprocessingScriptGenerationResult GenerateScripts(DataAnalysisReport analysis)
    {
        var scripts = new List<PreprocessingScript>();

        // ✅ Pure function
        if (analysis.Columns.Any(c => c.NullCount > 0))
            scripts.Add(GenerateMissingValueScript(analysis));

        if (analysis.Columns.Any(c => c.Type == DataType.Categorical))
            scripts.Add(GenerateCategoricalEncodingScript(analysis));

        return new PreprocessingScriptGenerationResult { Scripts = scripts };
    }
}
```

**Benefits**:
- ✅ Thread-safe by default
- ✅ Easy to test (no setup)
- ✅ Clear lifecycle (request-scoped)

### When You Need State

**Use OrchestrationContext**:
```csharp
public class OrchestrationContext
{
    public DataFrame? DataFrame { get; set; }
    public DataAnalysisResult? AnalysisResult { get; set; }
    public List<PreprocessingScript>? Scripts { get; set; }
}

public async Task<OrchestrationContext> ExecutePipelineAsync()
{
    var context = new OrchestrationContext();

    // Step 1: Analyze
    var analyzer = new DataAnalyzer();
    context.AnalysisResult = analyzer.Analyze(context.DataFrame!);

    // Step 2: Generate scripts
    var generator = new PreprocessingScriptGenerator();
    var scriptResult = generator.GenerateScripts(context.AnalysisResult);
    context.Scripts = scriptResult.Scripts;

    // Step 3: Use agent for refinement (if needed)
    // ...

    return context;
}
```

---

## Real-World Examples

### Example 1: DataAnalyzer (MLoop)

**Before (v0.1.8)**:
- 200 lines of code
- Mixed concerns (file I/O + LLM + parsing)
- 0% test coverage (required LLM)

**After (v0.4.1)**:
- Service: 150 lines (pure logic)
- agent.yaml: 20 lines
- system-prompt.md: 30 lines
- Total: -25% code (removed wrapper boilerplate)
- Test coverage: 95% (service layer)

**Files**:
```
src/MLoop.AIAgent/Services/DataAnalyzer.cs      (Service)
src/MLoop.AIAgent/AgentTemplates/data-analyst/  (Agent config)
tests/MLoop.AIAgent.Tests/Services/DataAnalyzerTests.cs  (Unit tests)
```

---

### Example 2: PreprocessingScriptGenerator (MLoop)

**Hybrid Approach**: Service + Agent

```csharp
public class PreprocessingScriptGenerator
{
    // ✅ Deterministic: Generate scripts based on rules
    public PreprocessingScriptGenerationResult GenerateScripts(DataAnalysisReport analysis)
    {
        var scripts = new List<PreprocessingScript>();

        foreach (var column in analysis.Columns)
        {
            if (column.NullCount > 0)
                scripts.Add(GenerateMissingValueScript(column));

            if (column.Type == DataType.Categorical)
                scripts.Add(GenerateCategoricalEncodingScript(column));

            if (column.Outliers.Count > 0)
                scripts.Add(GenerateOutlierHandlingScript(column));
        }

        return new PreprocessingScriptGenerationResult
        {
            Scripts = scripts,
            ExecutionOrder = scripts.Select((s, i) => (i + 1, s.Name)).ToList()
        };
    }

    // ✅ Agent for optimization (optional)
    public async Task<string> OptimizeScriptOrderAsync(
        List<PreprocessingScript> scripts,
        IronbeesOrchestrator orchestrator)
    {
        var prompt = $"Optimize execution order for: {JsonSerializer.Serialize(scripts)}";

        var responseBuilder = new StringBuilder();
        await foreach (var chunk in orchestrator.StreamAsync(
            input: prompt,
            agentName: "preprocessing-expert"))
        {
            responseBuilder.Append(chunk);
        }

        return responseBuilder.ToString();
    }
}
```

**Benefits**:
- ✅ Deterministic script generation (testable)
- ✅ Optional LLM optimization (when needed)
- ✅ Best of both worlds

---

## Migration Checklist

### For Each Agent

- [ ] **Step 1**: List all methods and categorize (Service/Config/Orchestration)
- [ ] **Step 2**: Extract business logic to Service class
- [ ] **Step 3**: Create `agent.yaml` with model config
- [ ] **Step 4**: Write `system-prompt.md` with LLM behavior
- [ ] **Step 5**: Update orchestration code (use `IronbeesOrchestrator.StreamAsync()`)
- [ ] **Step 6**: Write unit tests for Service (no LLM)
- [ ] **Step 7**: Write integration tests for Agent (with LLM, optional)

### Quality Gates

- [ ] Service layer has 0 LLM dependencies
- [ ] Unit tests run without API keys
- [ ] Integration tests marked with `[Trait("Category", "Integration")]`
- [ ] Test coverage ≥ 80% (service layer)
- [ ] Build succeeds
- [ ] All tests pass

---

## Additional Resources

### Related Guides
- [ChatClientBuilder Pattern](chatclientbuilder-pattern.md)
- [Namespace Migration](namespace-migration.md)

### Architecture Docs
- [ironbees Philosophy](../PHILOSOPHY.md)
- [ADR-001: Remove ConversationalAgent](../adr/001-remove-conversational-agent.md)

### Support
- [GitHub Issues](https://github.com/iyulab/ironbees/issues)
- [Discussions](https://github.com/iyulab/ironbees/discussions)

---

**Last Updated**: 2026-01-06
**Validated By**: MLoop Team (45% → 85% test coverage achieved)
