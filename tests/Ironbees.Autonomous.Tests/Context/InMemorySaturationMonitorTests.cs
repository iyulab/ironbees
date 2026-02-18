using Ironbees.Autonomous.Abstractions;
using Ironbees.Autonomous.Context;
using Xunit;

namespace Ironbees.Autonomous.Tests.Context;

public class InMemorySaturationMonitorTests
{
    [Fact]
    public void DefaultConstructor_ShouldHaveNormalState()
    {
        var monitor = new InMemorySaturationMonitor();

        Assert.Equal(SaturationLevel.Normal, monitor.CurrentState.Level);
        Assert.Equal(0, monitor.CurrentState.Percentage);
    }

    [Fact]
    public void RecordUsage_ShouldUpdateTokenCount()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(100, "prompt");

        Assert.Equal(100, monitor.CurrentState.CurrentTokens);
        Assert.Equal(10f, monitor.CurrentState.Percentage, 0.1f);
    }

    [Fact]
    public void RecordUsage_MultipleSourcesShouldAggregate()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(200, "prompt");
        monitor.RecordUsage(300, "response");

        Assert.Equal(500, monitor.CurrentState.CurrentTokens);
        Assert.Equal(50f, monitor.CurrentState.Percentage, 0.1f);
    }

    [Fact]
    public void RecordUsage_SameSourceShouldAccumulate()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(100, "prompt");
        monitor.RecordUsage(200, "prompt");

        Assert.Equal(300, monitor.CurrentState.CurrentTokens);
        Assert.Contains("prompt", (IDictionary<string, int>)monitor.CurrentState.UsageBySource);
        Assert.Equal(300, monitor.CurrentState.UsageBySource["prompt"]);
    }

    [Fact]
    public void DetermineLevel_Normal_Below60Percent()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(500, "test"); // 50%

        Assert.Equal(SaturationLevel.Normal, monitor.CurrentState.Level);
        Assert.Equal(SaturationAction.None, monitor.CurrentState.RecommendedAction);
    }

    [Fact]
    public void DetermineLevel_Elevated_At60Percent()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(650, "test"); // 65%

        Assert.Equal(SaturationLevel.Elevated, monitor.CurrentState.Level);
        Assert.Equal(SaturationAction.ConsiderSummarization, monitor.CurrentState.RecommendedAction);
    }

    [Fact]
    public void DetermineLevel_High_At75Percent()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(800, "test"); // 80%

        Assert.Equal(SaturationLevel.High, monitor.CurrentState.Level);
        Assert.Equal(SaturationAction.ShouldPageOut, monitor.CurrentState.RecommendedAction);
    }

    [Fact]
    public void DetermineLevel_Critical_At85Percent()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(900, "test"); // 90%

        Assert.Equal(SaturationLevel.Critical, monitor.CurrentState.Level);
        Assert.Equal(SaturationAction.MustEvict, monitor.CurrentState.RecommendedAction);
    }

    [Fact]
    public void DetermineLevel_Overflow_At95Percent()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(960, "test"); // 96%

        Assert.Equal(SaturationLevel.Overflow, monitor.CurrentState.Level);
        Assert.Equal(SaturationAction.Emergency, monitor.CurrentState.RecommendedAction);
    }

    [Fact]
    public void SaturationChanged_ShouldFireOnLevelChange()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);
        SaturationChangedEventArgs? captured = null;
        monitor.SaturationChanged += (_, args) => captured = args;

        monitor.RecordUsage(700, "test"); // Normal → Elevated

        Assert.NotNull(captured);
        Assert.Equal(SaturationLevel.Normal, captured.PreviousLevel);
        Assert.Equal(SaturationLevel.Elevated, captured.NewLevel);
    }

    [Fact]
    public void SaturationChanged_ShouldNotFireWhenLevelSame()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);
        var fireCount = 0;
        monitor.SaturationChanged += (_, _) => fireCount++;

        monitor.RecordUsage(100, "test"); // Normal
        monitor.RecordUsage(100, "test"); // Still Normal

        Assert.Equal(0, fireCount); // No level change
    }

    [Fact]
    public void ActionRequired_ShouldFireWhenAutoTriggerEnabled()
    {
        var config = new SaturationConfig { MaxTokens = 1000, AutoTriggerActions = true };
        var monitor = new InMemorySaturationMonitor(config);
        SaturationActionRequiredEventArgs? captured = null;
        monitor.ActionRequired += (_, args) => captured = args;

        monitor.RecordUsage(700, "test"); // Elevated → ConsiderSummarization

        Assert.NotNull(captured);
        Assert.Equal(SaturationAction.ConsiderSummarization, captured.Action);
        Assert.True(captured.SuggestedTokensToFree >= 0);
    }

    [Fact]
    public void ActionRequired_ShouldNotFireWhenAutoTriggerDisabled()
    {
        var config = new SaturationConfig { MaxTokens = 1000, AutoTriggerActions = false };
        var monitor = new InMemorySaturationMonitor(config);
        SaturationActionRequiredEventArgs? captured = null;
        monitor.ActionRequired += (_, args) => captured = args;

        monitor.RecordUsage(700, "test");

        Assert.Null(captured);
    }

    [Fact]
    public void ResetIteration_ShouldClearState()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);
        monitor.RecordUsage(900, "test");

        monitor.ResetIteration();

        Assert.Equal(SaturationLevel.Normal, monitor.CurrentState.Level);
        Assert.Equal(0, monitor.CurrentState.CurrentTokens);
        Assert.Equal(0, monitor.CurrentState.Percentage);
    }

    [Fact]
    public void Configure_ShouldUpdateThresholds()
    {
        var monitor = new InMemorySaturationMonitor();
        monitor.RecordUsage(500, "test"); // With default 128k, this is tiny

        var newConfig = new SaturationConfig { MaxTokens = 600 };
        monitor.Configure(newConfig);

        // 500/600 = ~83%, should be High
        Assert.Equal(SaturationLevel.High, monitor.CurrentState.Level);
    }

    [Fact]
    public async Task UpdateStateAsync_ShouldReturnCurrentState()
    {
        var config = new SaturationConfig { MaxTokens = 1000 };
        var monitor = new InMemorySaturationMonitor(config);
        monitor.RecordUsage(100, "test");

        var state = await monitor.UpdateStateAsync();

        Assert.Equal(100, state.CurrentTokens);
    }

    [Fact]
    public void MaxTokensZero_ShouldNotDivideByZero()
    {
        var config = new SaturationConfig { MaxTokens = 0 };
        var monitor = new InMemorySaturationMonitor(config);

        monitor.RecordUsage(100, "test");

        Assert.Equal(0, monitor.CurrentState.Percentage);
    }
}
