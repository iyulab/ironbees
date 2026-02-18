using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Executors;
using Ironbees.Autonomous.Strategies;
using Xunit;

namespace Ironbees.Autonomous.Tests.Strategies;

public class ContextAwareFallbackStrategyTests
{
    // --- Test helpers ---

    private sealed record TestRequest(string RequestId, string Prompt) : ITaskRequest;

    private sealed record TestResult(string RequestId, bool Success, string Output, string? ErrorOutput = null) : ITaskResult;

    private static TestResult Factory(TestRequest req, string content, bool isGuess)
        => new(req.RequestId, true, content);

    private static FallbackContext<TestRequest> CreateContext(
        IReadOnlyList<string>? previousOutputs = null,
        Dictionary<string, object>? metadata = null)
        => new()
        {
            FailedRequest = new TestRequest("req-1", "test"),
            PreviousOutputs = previousOutputs ?? [],
            Metadata = metadata ?? new Dictionary<string, object>()
        };

    // --- Constructor ---

    [Fact]
    public void Constructor_NullConfig_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ContextAwareFallbackStrategy<TestRequest, TestResult>(null!, null, Factory));
    }

    [Fact]
    public void Constructor_NullFactory_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ContextAwareFallbackStrategy<TestRequest, TestResult>(
                new FallbackConfig(), null, null!));
    }

    // --- CanProvideFallback ---

    [Fact]
    public void CanProvide_DisabledConfig_ShouldReturnFalse()
    {
        var config = new FallbackConfig { Enabled = false, Items = ["question1"] };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        Assert.False(strategy.CanProvideFallback(CreateContext()));
    }

    [Fact]
    public void CanProvide_EnabledWithItems_ShouldReturnTrue()
    {
        var config = new FallbackConfig { Enabled = true, Items = ["Is it alive?"] };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        Assert.True(strategy.CanProvideFallback(CreateContext()));
    }

    [Fact]
    public void CanProvide_EnabledNoItems_ShouldReturnFalse()
    {
        var config = new FallbackConfig { Enabled = true };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        Assert.False(strategy.CanProvideFallback(CreateContext()));
    }

    // --- GetFallbackAsync ---

    [Fact]
    public async Task GetFallback_ShouldReturnFirstItem()
    {
        var config = new FallbackConfig
        {
            Enabled = true,
            Items = ["Is it alive?", "Is it man-made?"]
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        var result = await strategy.GetFallbackAsync(CreateContext());

        Assert.NotNull(result);
        Assert.Equal("Is it alive?", result.Output);
    }

    [Fact]
    public async Task GetFallback_ShouldSkipAlreadyUsed()
    {
        var config = new FallbackConfig
        {
            Enabled = true,
            Items = ["Is it alive?", "Is it man-made?"]
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        // First call uses "Is it alive?"
        await strategy.GetFallbackAsync(CreateContext());
        // Second call should skip "Is it alive?"
        var result = await strategy.GetFallbackAsync(CreateContext());

        Assert.NotNull(result);
        Assert.Equal("Is it man-made?", result.Output);
    }

    [Fact]
    public async Task GetFallback_AllUsed_ShouldReturnNull()
    {
        var config = new FallbackConfig
        {
            Enabled = true,
            Items = ["Is it alive?"]
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        await strategy.GetFallbackAsync(CreateContext()); // uses the only item
        var result = await strategy.GetFallbackAsync(CreateContext());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetFallback_SkipsPreviousOutputs()
    {
        var config = new FallbackConfig
        {
            Enabled = true,
            Items = ["alive", "man-made", "large"]
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        // "alive" was already asked (in previousOutputs)
        var result = await strategy.GetFallbackAsync(CreateContext(previousOutputs: ["alive"]));

        Assert.NotNull(result);
        Assert.Equal("man-made", result.Output);
    }

    // --- Reset ---

    [Fact]
    public async Task Reset_ShouldClearUsedFallbacks()
    {
        var config = new FallbackConfig
        {
            Enabled = true,
            Items = ["Is it alive?"]
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        await strategy.GetFallbackAsync(CreateContext()); // uses the item
        strategy.Reset();
        var result = await strategy.GetFallbackAsync(CreateContext());

        Assert.NotNull(result);
        Assert.Equal("Is it alive?", result.Output);
    }

    // --- Default pool ---

    [Fact]
    public async Task GetFallback_ShouldUseDefaultPoolWhenItemsEmpty()
    {
        var config = new FallbackConfig
        {
            Enabled = true,
            Default = ["default question"]
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        var result = await strategy.GetFallbackAsync(CreateContext());

        Assert.NotNull(result);
        Assert.Equal("default question", result.Output);
    }

    // --- Context-aware pools ---

    [Fact]
    public async Task GetFallback_ContextPool_ShouldMatchOnKeywords()
    {
        var config = new FallbackConfig
        {
            Enabled = true,
            Pools =
            [
                new FallbackPool
                {
                    Context = ["living"],
                    Questions = ["Is it a mammal?"]
                }
            ],
            Items = ["generic question"]
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(config, null, Factory);

        // "yes_answers" metadata with "living" keyword triggers the context pool
        var metadata = new Dictionary<string, object>
        {
            ["yes_answers"] = new List<string> { "Is it a living thing: yes" }
        };

        var result = await strategy.GetFallbackAsync(CreateContext(metadata: metadata));

        Assert.NotNull(result);
        Assert.Equal("Is it a mammal?", result.Output);
    }

    // --- Guess deduction ---

    [Fact]
    public async Task GetFallback_MustGuess_ShouldDeduceFromRules()
    {
        var config = new FallbackConfig { Enabled = true, Items = ["question"] };
        var guessRules = new List<GuessRule>
        {
            new()
            {
                Conditions = ["living", "large"],
                Guess = "elephant"
            },
            new()
            {
                Default = "unknown thing"
            }
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(
            config, guessRules, Factory);

        var metadata = new Dictionary<string, object>
        {
            ["must_guess"] = true,
            ["yes_answers"] = new List<string> { "living creature", "large animal" }
        };

        var result = await strategy.GetFallbackAsync(CreateContext(metadata: metadata));

        Assert.NotNull(result);
        Assert.Equal("elephant", result.Output);
    }

    [Fact]
    public async Task GetFallback_MustGuess_NoMatchingRule_ShouldUseDefault()
    {
        var config = new FallbackConfig { Enabled = true, Items = ["question"] };
        var guessRules = new List<GuessRule>
        {
            new()
            {
                Conditions = ["unique-keyword-xyz"],
                Guess = "specific"
            },
            new()
            {
                Default = "fallback guess"
            }
        };
        var strategy = new ContextAwareFallbackStrategy<TestRequest, TestResult>(
            config, guessRules, Factory);

        var metadata = new Dictionary<string, object>
        {
            ["must_guess"] = true,
            ["yes_answers"] = new List<string> { "some answer" }
        };

        var result = await strategy.GetFallbackAsync(CreateContext(metadata: metadata));

        Assert.NotNull(result);
        Assert.Equal("fallback guess", result.Output);
    }
}
