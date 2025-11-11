using System.Collections.Immutable;
using Ironbees.AgentMode.Agents;
using Ironbees.AgentMode.Models;
using Ironbees.AgentMode.MCP;
using Xunit;

namespace Ironbees.AgentMode.Tests;

/// <summary>
/// Integration tests for Agent Mode workflow.
/// </summary>
public class AgentIntegrationTests
{
    [Fact]
    public async Task CoderAgent_ShouldGenerateCodeEdits()
    {
        // Arrange
        var agent = new CoderAgent(chatClient: null); // MVP: null client

        var state = new CodingState
        {
            StateId = Guid.NewGuid().ToString(),
            UserRequest = "Create a simple calculator class",
            Spec = "Calculator with Add and Subtract methods",
            Plan = new ExecutionPlan
            {
                Summary = "Create Calculator class with Add and Subtract methods",
                Steps = ImmutableList.Create(
                    new PlanStep
                    {
                        Number = 1,
                        Description = "Create Calculator class",
                        ActionType = "CREATE",
                        Rationale = "Need basic calculator functionality"
                    }
                ),
                Complexity = "LOW"
            },
            CurrentNode = "CODE"
        };

        // Act
        var response = await agent.ExecuteAsync(state);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("VALIDATE", response.NextNode);
        Assert.True(response.Updates.ContainsKey("CodeDiffs"));
        Assert.True(response.Updates.ContainsKey("CodeGenerationComplete"));

        var codeDiffs = response.Updates["CodeDiffs"] as ImmutableList<FileEdit>;
        Assert.NotNull(codeDiffs);
        Assert.NotEmpty(codeDiffs);
    }

    [Fact]
    public async Task ValidatorAgent_ShouldValidateEmptyCodeDiffs()
    {
        // Arrange
        var agent = new ValidatorAgent();

        var state = new CodingState
        {
            StateId = Guid.NewGuid().ToString(),
            UserRequest = "Test validation",
            Spec = "Test spec",
            CurrentNode = "VALIDATE",
            CodeDiffs = ImmutableList<FileEdit>.Empty
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AgentExecutionException>(
            () => agent.ExecuteAsync(state));

        Assert.Contains("No code diffs found", exception.Message);
    }

    [Fact]
    public async Task ValidatorAgent_WithCodeDiffs_ShouldReturnValidationResults()
    {
        // Arrange
        var agent = new ValidatorAgent();

        var state = new CodingState
        {
            StateId = Guid.NewGuid().ToString(),
            UserRequest = "Test validation",
            Spec = "Test spec",
            CurrentNode = "VALIDATE",
            CodeDiffs = ImmutableList.Create(
                new FileEdit
                {
                    FilePath = "Calculator.cs",
                    Type = EditType.Create,
                    NewContent = "public class Calculator { }"
                }
            )
        };

        // Act
        var response = await agent.ExecuteAsync(state);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Updates.ContainsKey("BuildResult"));
        Assert.True(response.Updates.ContainsKey("TestResult"));
        Assert.True(response.Updates.ContainsKey("ValidationComplete"));

        // MVP: Placeholder implementation returns success
        Assert.Equal("COMPLETE", response.NextNode);
    }

    [Fact]
    public async Task RoslynMcpServer_ShouldCompileValidCode()
    {
        // Arrange
        var server = new RoslynMcpServer();
        await server.InitializeAsync(new Dictionary<string, object>());

        var validCode = @"
using System;

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
}";

        var arguments = new Dictionary<string, object>
        {
            ["code"] = validCode
        };

        // Act
        var result = await server.ExecuteToolAsync("compile_code", arguments);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Content);

        var buildResult = result.Content as BuildResult;
        Assert.NotNull(buildResult);
        Assert.True(buildResult.Success);
        Assert.Empty(buildResult.Errors);
    }

    [Fact]
    public async Task RoslynMcpServer_ShouldDetectCompilationErrors()
    {
        // Arrange
        var server = new RoslynMcpServer();
        await server.InitializeAsync(new Dictionary<string, object>());

        var invalidCode = @"
public class Calculator
{
    public int Add(int a, int b) => a + b
    // Missing semicolon ^
}";

        var arguments = new Dictionary<string, object>
        {
            ["code"] = invalidCode
        };

        // Act
        var result = await server.ExecuteToolAsync("compile_code", arguments);

        // Assert
        Assert.True(result.Success); // Tool executed successfully

        var buildResult = result.Content as BuildResult;
        Assert.NotNull(buildResult);
        Assert.False(buildResult.Success); // But compilation failed
        Assert.NotEmpty(buildResult.Errors);
    }

    [Fact]
    public async Task RoslynMcpServer_AnalyzeSyntax_ShouldDetectSyntaxErrors()
    {
        // Arrange
        var server = new RoslynMcpServer();
        await server.InitializeAsync(new Dictionary<string, object>());

        var invalidCode = @"
public class Calculator {
    public int Add(int a int b) => a + b; // Missing comma
}";

        var arguments = new Dictionary<string, object>
        {
            ["code"] = invalidCode
        };

        // Act
        var result = await server.ExecuteToolAsync("analyze_syntax", arguments);

        // Assert
        Assert.False(result.Success); // Syntax analysis failed
    }
}
