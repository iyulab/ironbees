using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Strategies;
using Xunit;

namespace Ironbees.Autonomous.Tests.Strategies;

public class ListBasedFallbackStrategyTests
{
    // --- Test doubles ---

    private sealed record TestRequest(string RequestId, string Prompt) : ITaskRequest;

    private sealed class TestResult : ITaskResult
    {
        public string RequestId { get; set; } = "";
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string? ErrorOutput { get; set; }

        public TestResult() { }

        public TestResult(string requestId, bool success, string output, string? errorOutput = null)
        {
            RequestId = requestId;
            Success = success;
            Output = output;
            ErrorOutput = errorOutput;
        }
    }

    private static FallbackContext<TestRequest> CreateContext(
        string requestId = "r1",
        List<string>? previousOutputs = null) => new()
    {
        FailedRequest = new TestRequest(requestId, "test"),
        Iteration = 1,
        RetryAttempts = 3,
        PreviousOutputs = previousOutputs?.AsReadOnly() ?? new List<string>().AsReadOnly()
    };

    private static StringListFallbackStrategy<TestRequest, TestResult> CreateStrategy(
        IReadOnlyList<string> fallbacks) =>
        new(fallbacks, (req, value) => new TestResult(req.RequestId, true, value));

    // --- Constructor ---

    [Fact]
    public void Constructor_NullFallbacks_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StringListFallbackStrategy<TestRequest, TestResult>(
                null!, (_, v) => new TestResult("r1", true, v)));
    }

    [Fact]
    public void Constructor_NullFactory_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StringListFallbackStrategy<TestRequest, TestResult>(
                ["a"], null!));
    }

    // --- CanProvideFallback ---

    [Fact]
    public void CanProvideFallback_WithAvailableItems_ShouldReturnTrue()
    {
        var strategy = CreateStrategy(["apple", "banana"]);

        Assert.True(strategy.CanProvideFallback(CreateContext()));
    }

    [Fact]
    public void CanProvideFallback_EmptyList_ShouldReturnFalse()
    {
        var strategy = CreateStrategy([]);

        Assert.False(strategy.CanProvideFallback(CreateContext()));
    }

    // --- GetFallbackAsync ---

    [Fact]
    public async Task GetFallback_ShouldReturnFirstUnused()
    {
        var strategy = CreateStrategy(["apple", "banana", "cherry"]);

        var result = await strategy.GetFallbackAsync(CreateContext());

        Assert.NotNull(result);
        Assert.Equal("apple", result!.Output);
    }

    [Fact]
    public async Task GetFallback_CalledTwice_ShouldReturnDifferentItems()
    {
        var strategy = CreateStrategy(["apple", "banana", "cherry"]);
        var context = CreateContext();

        var first = await strategy.GetFallbackAsync(context);
        var second = await strategy.GetFallbackAsync(context);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.Output, second!.Output);
    }

    [Fact]
    public async Task GetFallback_AllUsed_ShouldReturnNull()
    {
        var strategy = CreateStrategy(["apple"]);
        var context = CreateContext();

        var first = await strategy.GetFallbackAsync(context);
        var second = await strategy.GetFallbackAsync(context);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    // --- No repetition with previous outputs ---

    [Fact]
    public async Task GetFallback_SimilarToPreviousOutput_ShouldSkip()
    {
        var strategy = CreateStrategy(["apple", "banana"]);
        var context = CreateContext(previousOutputs: ["apple"]);

        var result = await strategy.GetFallbackAsync(context);

        Assert.NotNull(result);
        Assert.Equal("banana", result!.Output);
    }

    [Fact]
    public void CanProvideFallback_AllSimilarToPreviousOutputs_ShouldReturnFalse()
    {
        var strategy = CreateStrategy(["apple", "banana"]);
        var context = CreateContext(previousOutputs: ["apple", "banana"]);

        Assert.False(strategy.CanProvideFallback(context));
    }

    // --- Reset ---

    [Fact]
    public async Task Reset_ShouldAllowReuse()
    {
        var strategy = CreateStrategy(["apple"]);
        var context = CreateContext();

        var first = await strategy.GetFallbackAsync(context);
        Assert.NotNull(first);

        // After use, cannot provide more
        Assert.False(strategy.CanProvideFallback(CreateContext()));

        // Reset allows reuse
        strategy.Reset();
        Assert.True(strategy.CanProvideFallback(CreateContext()));

        var after = await strategy.GetFallbackAsync(CreateContext());
        Assert.NotNull(after);
        Assert.Equal("apple", after!.Output);
    }

    // --- Result factory ---

    [Fact]
    public async Task GetFallback_ShouldUseResultFactory()
    {
        var strategy = new StringListFallbackStrategy<TestRequest, TestResult>(
            ["test_value"],
            (req, value) => new TestResult(req.RequestId, true, $"fallback:{value}"));

        var result = await strategy.GetFallbackAsync(CreateContext("req-42"));

        Assert.NotNull(result);
        Assert.Equal("req-42", result!.RequestId);
        Assert.Equal("fallback:test_value", result.Output);
        Assert.True(result.Success);
    }

    // --- NoOpFallbackStrategy ---

    [Fact]
    public void NoOp_CanProvideFallback_ShouldReturnFalse()
    {
        var noop = NoOpFallbackStrategy<TestRequest, TestResult>.Instance;

        Assert.False(noop.CanProvideFallback(CreateContext()));
    }

    [Fact]
    public async Task NoOp_GetFallback_ShouldReturnNull()
    {
        var noop = NoOpFallbackStrategy<TestRequest, TestResult>.Instance;

        var result = await noop.GetFallbackAsync(CreateContext());

        Assert.Null(result);
    }

    [Fact]
    public void NoOp_Instance_ShouldBeSingleton()
    {
        var a = NoOpFallbackStrategy<TestRequest, TestResult>.Instance;
        var b = NoOpFallbackStrategy<TestRequest, TestResult>.Instance;

        Assert.Same(a, b);
    }
}
