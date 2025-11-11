using System.Collections.Immutable;
using Ironbees.AgentMode.Models;
using Ironbees.AgentMode.MCP;
using Microsoft.Extensions.AI;

namespace Ironbees.AgentMode.Agents;

/// <summary>
/// Agent responsible for validating generated code changes.
/// Performs compilation checks, test execution, and static analysis.
/// </summary>
public class ValidatorAgent : ICodingAgent
{
    private readonly IChatClient? _chatClient;
    private readonly IToolRegistry? _toolRegistry;

    public string Name => "validator";
    public string Description => "Validates code changes through compilation, testing, and analysis";
    public IReadOnlyList<string> RequiredTools => new[] { "roslyn", "msbuild", "dotnet-test" };

    public ValidatorAgent(IChatClient? chatClient = null, IToolRegistry? toolRegistry = null)
    {
        _chatClient = chatClient;
        _toolRegistry = toolRegistry;
    }

    public async Task<AgentResponse> ExecuteAsync(
        CodingState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate state - check if we have code diffs to validate
            if (state.CodeDiffs.IsEmpty)
            {
                throw new AgentExecutionException(Name, "No code diffs found in state for validation");
            }

            // Validate the generated code edits
            var buildResult = await ValidateCompilation(state, state.CodeDiffs, cancellationToken);
            var testResult = await RunTests(state, state.CodeDiffs, cancellationToken);

            // Determine if validation passed
            var validationPassed = buildResult.Success && testResult.Success;

            // Generate validation summary using LLM (if available)
            var validationSummary = await GenerateValidationSummary(
                state, buildResult, testResult, cancellationToken);

            // Create response with validation results
            return new AgentResponse
            {
                Updates = new Dictionary<string, object?>
                {
                    ["BuildResult"] = buildResult,
                    ["TestResult"] = testResult,
                    ["ValidationComplete"] = true,
                    ["ValidationPassed"] = validationPassed,
                    ["ValidationSummary"] = validationSummary
                },
                NextNode = validationPassed ? "COMPLETE" : "REFINE",
                Metadata = new Dictionary<string, string>
                {
                    ["agent"] = Name,
                    ["buildSuccess"] = buildResult.Success.ToString(),
                    ["testSuccess"] = testResult.Success.ToString(),
                    ["errorsCount"] = buildResult.Errors.Count.ToString(),
                    ["failuresCount"] = testResult.Failures.Count.ToString(),
                    ["timestamp"] = DateTime.UtcNow.ToString("O")
                }
            };
        }
        catch (Exception ex) when (ex is not AgentExecutionException)
        {
            throw new AgentExecutionException(Name, $"Failed to validate code: {ex.Message}", ex);
        }
    }

    private async Task<BuildResult> ValidateCompilation(
        CodingState state,
        ImmutableList<FileEdit> edits,
        CancellationToken cancellationToken)
    {
        // TODO: Full Roslyn/MSBuild integration - For now using MVP implementation
        // Expected: Use roslyn MCP tool to compile and analyze code
        // Expected: Parse MSBuild output to extract errors and warnings

        // MVP: Return success for now (will be replaced with actual compilation)
        return await Task.FromResult(new BuildResult
        {
            Success = true,
            Errors = ImmutableList<CompilationError>.Empty,
            Warnings = ImmutableList<CompilationWarning>.Empty,
            Duration = TimeSpan.FromSeconds(1)
        });
    }

    private async Task<TestResult> RunTests(
        CodingState state,
        ImmutableList<FileEdit> edits,
        CancellationToken cancellationToken)
    {
        // TODO: Full dotnet test integration - For now using MVP implementation
        // Expected: Use dotnet-test MCP tool to execute tests
        // Expected: Parse test results and extract failures

        // MVP: Return success for now (will be replaced with actual test execution)
        return await Task.FromResult(new TestResult
        {
            Success = true,
            Total = 0,
            Passed = 0,
            Failed = 0,
            Skipped = 0,
            Failures = ImmutableList<TestFailure>.Empty,
            Duration = TimeSpan.FromSeconds(0)
        });
    }

    private async Task<string> GenerateValidationSummary(
        CodingState state,
        BuildResult buildResult,
        TestResult testResult,
        CancellationToken cancellationToken)
    {
        // TODO: Full LLM integration for intelligent validation summary
        // Expected: Use LLM to analyze errors and provide improvement suggestions
        // Expected: Generate human-readable summary of validation results

        // MVP: Generate simple text summary
        var summary = $@"# Validation Results

## Build Status
- **Success**: {buildResult.Success}
- **Errors**: {buildResult.Errors.Count}
- **Warnings**: {buildResult.Warnings.Count}
- **Duration**: {buildResult.Duration.TotalSeconds:F2}s

## Test Status
- **Success**: {testResult.Success}
- **Total Tests**: {testResult.Total}
- **Passed**: {testResult.Passed}
- **Failed**: {testResult.Failed}
- **Skipped**: {testResult.Skipped}
- **Duration**: {testResult.Duration.TotalSeconds:F2}s

## Overall Status
{(buildResult.Success && testResult.Success ? "✅ All validation checks passed" : "❌ Validation failed - review errors above")}";

        return await Task.FromResult(summary);
    }

    private string BuildValidationPrompt(
        CodingState state,
        BuildResult buildResult,
        TestResult testResult)
    {
        var repositoryPath = state.Metadata.TryGetValue("RepositoryPath", out var path)
            ? path : "unknown";

        var prompt = $@"You are an expert code reviewer analyzing validation results.

## Context
- Repository: {repositoryPath}
- User Request: {state.UserRequest}
- Specification: {state.Spec}

## Build Results
- Success: {buildResult.Success}
- Errors: {buildResult.Errors.Count}
- Warnings: {buildResult.Warnings.Count}

{(buildResult.Errors.Count > 0 ? $@"### Compilation Errors:
{string.Join("\n", buildResult.Errors.Select(e => $"- [{e.Code}] {e.Message} ({e.FilePath}:{e.Line})"))}
" : "")}

## Test Results
- Success: {testResult.Success}
- Total: {testResult.Total}
- Passed: {testResult.Passed}
- Failed: {testResult.Failed}

{(testResult.Failures.Count > 0 ? $@"### Test Failures:
{string.Join("\n", testResult.Failures.Select(f => $"- {f.TestName}: {f.Message}"))}
" : "")}

## Instructions
Analyze the validation results and provide:

1. **Summary**: Brief overview of validation status
2. **Critical Issues**: Most important problems to address
3. **Recommendations**: Specific steps to fix issues
4. **Root Causes**: Likely causes of failures
5. **Priority**: Which issues to address first

## Output Format
Provide your analysis in clear, structured markdown format.

Generate the validation analysis now:";

        return prompt;
    }
}
