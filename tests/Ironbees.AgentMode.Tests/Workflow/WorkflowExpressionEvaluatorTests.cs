using Ironbees.AgentMode.Workflow;
using Xunit;

namespace Ironbees.AgentMode.Tests.Workflow;

public class WorkflowExpressionEvaluatorTests
{
    #region Null/Empty Conditions

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_NullOrWhitespace_ReturnsTrue(string? expression)
    {
        var state = CreateState();
        Assert.True(WorkflowExpressionEvaluator.Evaluate(expression, state));
    }

    #endregion

    #region Backwards-Compatible Status Keywords

    [Fact]
    public void Evaluate_Success_WhenRunning_ReturnsTrue()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Running);
        Assert.True(WorkflowExpressionEvaluator.Evaluate("success", state));
    }

    [Fact]
    public void Evaluate_Success_WhenFailed_ReturnsFalse()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Failed);
        Assert.False(WorkflowExpressionEvaluator.Evaluate("success", state));
    }

    [Fact]
    public void Evaluate_Failure_WhenFailed_ReturnsTrue()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Failed);
        Assert.True(WorkflowExpressionEvaluator.Evaluate("failure", state));
    }

    [Fact]
    public void Evaluate_Failure_WhenRunning_ReturnsFalse()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Running);
        Assert.False(WorkflowExpressionEvaluator.Evaluate("failure", state));
    }

    #endregion

    #region Output Data Access (Legacy Dot-Notation)

    [Fact]
    public void Evaluate_BuildSuccess_WhenTrue_ReturnsTrue()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["build_success"] = true });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("build.success", state));
    }

    [Fact]
    public void Evaluate_BuildSuccess_WhenFalse_ReturnsFalse()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["build_success"] = false });
        Assert.False(WorkflowExpressionEvaluator.Evaluate("build.success", state));
    }

    [Fact]
    public void Evaluate_TestSuccess_WhenTrue_ReturnsTrue()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["test_success"] = true });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("test.success", state));
    }

    [Fact]
    public void Evaluate_BuildSuccess_WhenMissing_ReturnsFalse()
    {
        var state = CreateState();
        Assert.False(WorkflowExpressionEvaluator.Evaluate("build.success", state));
    }

    #endregion

    #region Output Data Access (output.* Prefix)

    [Fact]
    public void Evaluate_OutputKey_EqualsValue_ReturnsTrue()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["result"] = "ok" });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("output.result == 'ok'", state));
    }

    [Fact]
    public void Evaluate_OutputKey_NotEqualsValue_ReturnsFalse()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["result"] = "error" });
        Assert.False(WorkflowExpressionEvaluator.Evaluate("output.result == 'ok'", state));
    }

    [Fact]
    public void Evaluate_OutputKey_NotEquals_ReturnsTrue()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["result"] = "error" });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("output.result != 'ok'", state));
    }

    #endregion

    #region Iteration Count Comparisons

    [Theory]
    [InlineData("iteration_count >= 5", 5, true)]
    [InlineData("iteration_count >= 5", 6, true)]
    [InlineData("iteration_count >= 5", 4, false)]
    [InlineData("iteration_count > 5", 6, true)]
    [InlineData("iteration_count > 5", 5, false)]
    [InlineData("iteration_count <= 3", 3, true)]
    [InlineData("iteration_count <= 3", 4, false)]
    [InlineData("iteration_count < 3", 2, true)]
    [InlineData("iteration_count < 3", 3, false)]
    [InlineData("iteration_count == 5", 5, true)]
    [InlineData("iteration_count == 5", 4, false)]
    [InlineData("iteration_count != 5", 4, true)]
    [InlineData("iteration_count != 5", 5, false)]
    public void Evaluate_IterationCount_Comparison(string expression, int iterationCount, bool expected)
    {
        var state = CreateState(iterationCount: iterationCount);
        Assert.Equal(expected, WorkflowExpressionEvaluator.Evaluate(expression, state));
    }

    #endregion

    #region Boolean Operators

    [Fact]
    public void Evaluate_And_BothTrue_ReturnsTrue()
    {
        var state = CreateState(
            status: WorkflowExecutionStatus.Running,
            outputData: new Dictionary<string, object> { ["build_success"] = true });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("success && build.success", state));
    }

    [Fact]
    public void Evaluate_And_OneFalse_ReturnsFalse()
    {
        var state = CreateState(
            status: WorkflowExecutionStatus.Running,
            outputData: new Dictionary<string, object> { ["build_success"] = false });
        Assert.False(WorkflowExpressionEvaluator.Evaluate("success && build.success", state));
    }

    [Fact]
    public void Evaluate_Or_OneTrue_ReturnsTrue()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Running);
        Assert.True(WorkflowExpressionEvaluator.Evaluate("success || failure", state));
    }

    [Fact]
    public void Evaluate_Or_BothFalse_ReturnsFalse()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Completed);
        Assert.False(WorkflowExpressionEvaluator.Evaluate("success || failure", state));
    }

    [Fact]
    public void Evaluate_Not_NegatesTrue_ReturnsFalse()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Running);
        Assert.False(WorkflowExpressionEvaluator.Evaluate("!success", state));
    }

    [Fact]
    public void Evaluate_Not_NegatesFalse_ReturnsTrue()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Failed);
        Assert.True(WorkflowExpressionEvaluator.Evaluate("!success", state));
    }

    #endregion

    #region Complex Expressions

    [Fact]
    public void Evaluate_ComplexExpression_AndOrCombination()
    {
        var state = CreateState(
            status: WorkflowExecutionStatus.Running,
            iterationCount: 3,
            outputData: new Dictionary<string, object> { ["build_success"] = true });

        // success AND (build.success OR iteration_count >= 5)
        Assert.True(WorkflowExpressionEvaluator.Evaluate(
            "success && (build.success || iteration_count >= 5)", state));
    }

    [Fact]
    public void Evaluate_ComplexExpression_NestedParentheses()
    {
        var state = CreateState(
            status: WorkflowExecutionStatus.Running,
            iterationCount: 10);

        // (success && iteration_count > 5) || failure
        Assert.True(WorkflowExpressionEvaluator.Evaluate(
            "(success && iteration_count > 5) || failure", state));
    }

    [Fact]
    public void Evaluate_ComplexExpression_NotWithComparison()
    {
        var state = CreateState(iterationCount: 3);

        // !(iteration_count >= 5)
        Assert.True(WorkflowExpressionEvaluator.Evaluate(
            "!(iteration_count >= 5)", state));
    }

    [Fact]
    public void Evaluate_ComplexExpression_MultipleAndConditions()
    {
        var state = CreateState(
            status: WorkflowExecutionStatus.Running,
            iterationCount: 3,
            outputData: new Dictionary<string, object>
            {
                ["build_success"] = true,
                ["test_success"] = true
            });

        Assert.True(WorkflowExpressionEvaluator.Evaluate(
            "success && build.success && test.success && iteration_count < 10", state));
    }

    #endregion

    #region Literal Comparisons

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    [InlineData("False", false)]
    public void Evaluate_BooleanLiteral(string expression, bool expected)
    {
        var state = CreateState();
        Assert.Equal(expected, WorkflowExpressionEvaluator.Evaluate(expression, state));
    }

    [Fact]
    public void Evaluate_NumericLiteral_NonZero_ReturnsTrue()
    {
        var state = CreateState();
        Assert.True(WorkflowExpressionEvaluator.Evaluate("42", state));
    }

    [Fact]
    public void Evaluate_NumericLiteral_Zero_ReturnsFalse()
    {
        var state = CreateState();
        Assert.False(WorkflowExpressionEvaluator.Evaluate("0", state));
    }

    #endregion

    #region Status Variable

    [Fact]
    public void Evaluate_StatusEqualsRunning()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Running);
        Assert.True(WorkflowExpressionEvaluator.Evaluate("status == 'running'", state));
    }

    [Fact]
    public void Evaluate_StatusEqualsFailed()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Failed);
        Assert.True(WorkflowExpressionEvaluator.Evaluate("status == 'failed'", state));
    }

    [Fact]
    public void Evaluate_StatusNotEqualsFailed()
    {
        var state = CreateState(status: WorkflowExecutionStatus.Running);
        Assert.True(WorkflowExpressionEvaluator.Evaluate("status != 'failed'", state));
    }

    #endregion

    #region Direct OutputData Access

    [Fact]
    public void Evaluate_DirectOutputDataKey_WhenExists_ReturnsTrue()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["completed"] = true });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("completed", state));
    }

    [Fact]
    public void Evaluate_DirectOutputDataKey_WhenMissing_ReturnsFalse()
    {
        var state = CreateState();
        Assert.False(WorkflowExpressionEvaluator.Evaluate("unknown_var", state));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Evaluate_QuotedStringComparison_DoubleQuotes()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["phase"] = "build" });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("output.phase == \"build\"", state));
    }

    [Fact]
    public void Evaluate_OperatorPrecedence_AndBeforeOr()
    {
        // false && true || true → (false && true) || true → true
        var state = CreateState(status: WorkflowExecutionStatus.Failed);
        Assert.True(WorkflowExpressionEvaluator.Evaluate(
            "success && failure || failure", state));
    }

    [Fact]
    public void Evaluate_NumericOutputComparison()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["score"] = 85 });
        Assert.True(WorkflowExpressionEvaluator.Evaluate("output.score >= 80", state));
    }

    [Fact]
    public void Evaluate_NumericOutputComparison_BelowThreshold()
    {
        var state = CreateState(outputData: new Dictionary<string, object> { ["score"] = 75 });
        Assert.False(WorkflowExpressionEvaluator.Evaluate("output.score >= 80", state));
    }

    #endregion

    #region Helper Methods

    private static WorkflowRuntimeState CreateState(
        WorkflowExecutionStatus status = WorkflowExecutionStatus.Running,
        int iterationCount = 0,
        Dictionary<string, object>? outputData = null) =>
        new()
        {
            ExecutionId = "test-exec-001",
            WorkflowName = "TestWorkflow",
            CurrentStateId = "TEST",
            Status = status,
            Input = "test input",
            StartedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            IterationCount = iterationCount,
            OutputData = outputData ?? new Dictionary<string, object>()
        };

    #endregion
}
