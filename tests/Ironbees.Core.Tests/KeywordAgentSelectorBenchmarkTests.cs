using Ironbees.Core;
using NSubstitute;
using System.Diagnostics;

namespace Ironbees.Core.Tests;

/// <summary>
/// Performance benchmark tests for KeywordAgentSelector.
/// These tests measure execution time and may be skipped if performance is not critical.
///
/// To skip performance tests during development:
/// dotnet test --filter "Category!=Performance"
///
/// Re-investigated 2026-08-27 (claudedocs/ironbees/issues/closed/
/// ISSUE-ironbees-20260827-064725-keywordagentselector-perf-regression-net10.md): the two
/// thresholds relaxed after the .NET 10 upgrade (2025-11-18, ~18x/~50x) no longer describe
/// typical performance — repeated runs measured 59-98ms (1000-iteration) and 3-4ms (TF-IDF)
/// against the ~1800ms/~500ms this class's comments used to claim. Two things were true at
/// once, not just one: (1) the claimed regression itself is gone (upgrade-adjacent JIT/BCL
/// change, most likely — nothing pins down the exact fix), and (2) both benchmarks' warmup was
/// genuinely too shallow for .NET 10's tiered JIT, which let an occasional tier-up/GC pause
/// land inside the timed window as a large outlier (worst observed: 216ms on the TF-IDF
/// benchmark, 1002ms on the 1000-iteration one) — deepening warmup fixed the TF-IDF outlier's
/// severity but not the 1000-iteration one's, which is now understood as host-level scheduling
/// noise rather than anything either benchmark's code controls. See each test's own comment for
/// its specific recalibration.
/// </summary>
public class KeywordAgentSelectorBenchmarkTests
{
    private static IAgent CreateTestAgent(
        string name,
        string description,
        List<string>? capabilities = null,
        List<string>? tags = null)
    {
        var mockAgent = Substitute.For<IAgent>();
        var config = new AgentConfig
        {
            Name = name,
            Description = description,
            Version = "1.0.0",
            SystemPrompt = "Test prompt",
            Model = new ModelConfig
            {
                Deployment = "gpt-4",
                Temperature = 0.7,
                MaxTokens = 1000
            },
            Capabilities = capabilities ?? new List<string>(),
            Tags = tags ?? new List<string>()
        };

        mockAgent.Name.Returns(name);
        mockAgent.Description.Returns(description);
        mockAgent.Config.Returns(config);

        return mockAgent;
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task SelectAgentAsync_1000Iterations_CompletesUnder1000ms()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agents = new List<IAgent>
        {
            CreateTestAgent(
                "coding-agent",
                "Expert software developer specializing in C# and .NET",
                capabilities: new List<string> { "code-generation", "code-review", "debugging" },
                tags: new List<string> { "programming", "development", "csharp", "dotnet" }),

            CreateTestAgent(
                "writing-agent",
                "Professional technical writer for documentation",
                capabilities: new List<string> { "content-writing", "editing", "documentation" },
                tags: new List<string> { "writing", "documentation", "technical-writing" }),

            CreateTestAgent(
                "testing-agent",
                "QA specialist for automated testing",
                capabilities: new List<string> { "test-automation", "test-design", "qa" },
                tags: new List<string> { "testing", "quality-assurance", "automation" }),

            CreateTestAgent(
                "devops-agent",
                "DevOps engineer for CI/CD and deployment",
                capabilities: new List<string> { "deployment", "ci-cd", "infrastructure" },
                tags: new List<string> { "devops", "deployment", "infrastructure" }),

            CreateTestAgent(
                "database-agent",
                "Database specialist for SQL and NoSQL",
                capabilities: new List<string> { "database-design", "query-optimization", "data-modeling" },
                tags: new List<string> { "database", "sql", "data" })
        };

        var testQueries = new[]
        {
            "Write some C# code for me",
            "Help me with documentation",
            "Create automated tests",
            "Deploy to production",
            "Optimize database queries"
        };

        // Warmup: enough passes over the query set for tiered JIT to promote the hot path
        // before timing starts (a single pass per query left an occasional tier-up/GC pause
        // inside the timed window — see the TF-IDF benchmark below, which hit the same class
        // of outlier and was fixed the same way).
        for (int w = 0; w < 20; w++)
        {
            foreach (var query in testQueries)
            {
                await selector.SelectAgentAsync(query, agents, TestContext.Current.CancellationToken);
            }
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var query = testQueries[i % testQueries.Length];
            await selector.SelectAgentAsync(query, agents, TestContext.Current.CancellationToken);
        }
        stopwatch.Stop();

        // Assert
        // Recalibrated 2026-08-27: the original 100ms target and the ~1800ms this comment used
        // to claim as "current performance" both stopped matching reality. ~35 isolated runs
        // (varying warmup depth — see above) clustered at 59-98ms, with two rare outliers
        // (389ms, 1002ms; ~1-in-15 each) that a 20x-deeper warmup did not eliminate — consistent
        // with host-level scheduling noise (GC/OS/AV) rather than an algorithmic slowdown or an
        // insufficient warmup. 1000ms gives real margin over the typical cluster (10x+) while
        // staying 3x tighter than the previous 3000ms; a threshold tight enough to also catch
        // the rarest outlier would defeat the point of a wall-clock assertion (CLAUDE.md already
        // excludes this whole test category from CI for the same reason — it recurs even in a
        // single local process across repeated runs, not just across shared runners).
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"1000 iterations took {stopwatch.ElapsedMilliseconds}ms (expected < 1000ms)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task SelectAgentAsync_SingleCall_CompletesUnder1ms()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agents = new List<IAgent>
        {
            CreateTestAgent(
                "coding-agent",
                "Expert software developer",
                capabilities: new List<string> { "code-generation", "code-review" },
                tags: new List<string> { "programming", "development" }),

            CreateTestAgent(
                "writing-agent",
                "Professional writer",
                capabilities: new List<string> { "content-writing" },
                tags: new List<string> { "writing", "documentation" })
        };

        // Warmup
        await selector.SelectAgentAsync("Write code", agents, TestContext.Current.CancellationToken);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await selector.SelectAgentAsync("Help me write some code", agents, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 1,
            $"Single call took {stopwatch.ElapsedMilliseconds}ms (expected < 1ms)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task SelectAgentAsync_CachingImprovement_SecondCallFaster()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agents = new List<IAgent>
        {
            CreateTestAgent(
                "coding-agent",
                "Expert software developer",
                capabilities: new List<string> { "code-generation" },
                tags: new List<string> { "programming" })
        };

        // Act - First call (no cache)
        var stopwatch1 = Stopwatch.StartNew();
        await selector.SelectAgentAsync("Write some code", agents, TestContext.Current.CancellationToken);
        stopwatch1.Stop();
        var firstCallTime = stopwatch1.ElapsedTicks;

        // Act - Second call (with cache)
        var stopwatch2 = Stopwatch.StartNew();
        await selector.SelectAgentAsync("Write some code", agents, TestContext.Current.CancellationToken);
        stopwatch2.Stop();
        var secondCallTime = stopwatch2.ElapsedTicks;

        // Assert - Second call should be faster or equal (due to caching)
        Assert.True(secondCallTime <= firstCallTime * 1.5,
            $"Second call ({secondCallTime} ticks) should be faster than first ({firstCallTime} ticks)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task ScoreAgentsAsync_MultipleAgents_CompletesQuickly()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agents = new List<IAgent>();
        for (int i = 0; i < 10; i++)
        {
            agents.Add(CreateTestAgent(
                $"agent-{i}",
                $"Agent number {i} for testing",
                capabilities: new List<string> { $"capability-{i}", $"skill-{i}" },
                tags: new List<string> { $"tag-{i}", $"category-{i}" }));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var scores = await selector.ScoreAgentsAsync("Help me with capability-5", agents, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 5,
            $"Scoring 10 agents took {stopwatch.ElapsedMilliseconds}ms (expected < 5ms)");
        Assert.Equal(10, scores.Count);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task SelectAgentAsync_WithTfidfCalculation_MaintainsPerformance()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agents = new List<IAgent>
        {
            CreateTestAgent(
                "coding-agent",
                "Expert C# developer with .NET expertise and code generation capabilities",
                capabilities: new List<string> { "code-generation", "code-review", "debugging", "refactoring" },
                tags: new List<string> { "programming", "development", "csharp", "dotnet", "coding" }),

            CreateTestAgent(
                "python-agent",
                "Python specialist for data science and machine learning",
                capabilities: new List<string> { "python-coding", "data-analysis", "ml" },
                tags: new List<string> { "python", "data-science", "machine-learning" }),

            CreateTestAgent(
                "web-agent",
                "Full-stack web developer for JavaScript and frameworks",
                capabilities: new List<string> { "frontend", "backend", "javascript" },
                tags: new List<string> { "web", "javascript", "react", "nodejs" })
        };

        // Warmup: a single call only primed the TF-IDF calculator's lazy init, not tiered JIT —
        // one in ~20 runs measured a 50x+ outlier (216ms vs. a normal 3-4ms) with a single-call
        // warmup, consistent with a tier-up recompilation landing inside the timed loop rather
        // than a real perf regression. Looping the actual call shape before timing starts gives
        // the JIT room to promote it to optimized code first.
        for (int i = 0; i < 50; i++)
        {
            await selector.SelectAgentAsync("Help me write C# code", agents, TestContext.Current.CancellationToken);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            await selector.SelectAgentAsync("Help me write C# code", agents, TestContext.Current.CancellationToken);
        }
        stopwatch.Stop();

        // Assert
        // Recalibrated 2026-08-27: the ~500ms this comment used to claim as "current
        // performance" does not reproduce — typical runs measure 3-4ms. The original 10ms
        // target, however, still isn't safe even with the strengthened warmup above: 20
        // repeated runs at a 10ms bound failed 2/20 (11ms, 24ms — an occasional JIT tier-up or
        // GC pause landing inside the 100-call window, not an algorithmic slowdown). 50ms keeps
        // real margin over that observed tail while staying 15x tighter than the previous
        // 750ms, so a genuine regression is still caught.
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"100 iterations with TF-IDF took {stopwatch.ElapsedMilliseconds}ms (expected < 50ms)");
    }

    [Fact]
    public async Task ClearCache_ResetsPerformanceState()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agents = new List<IAgent>
        {
            CreateTestAgent(
                "test-agent",
                "Test agent",
                capabilities: new List<string> { "testing" },
                tags: new List<string> { "test" })
        };

        // Build cache
        for (int i = 0; i < 10; i++)
        {
            await selector.SelectAgentAsync($"Query {i}", agents, TestContext.Current.CancellationToken);
        }

        // Act
        selector.ClearCache();

        // Assert - Should not throw and cache should be cleared
        var result = await selector.SelectAgentAsync("New query", agents, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
    }
}
