using Ironbees.Autonomous.Executors;
using Xunit;

namespace Ironbees.Autonomous.Tests.Executors;

public class GuessRuleTests
{
    [Fact]
    public void Matches_EmptyConditionsWithDefault_ShouldReturnTrue()
    {
        var rule = new GuessRule { Conditions = [], Default = "fallback" };

        Assert.True(rule.Matches(["anything"]));
    }

    [Fact]
    public void Matches_EmptyConditionsNoDefault_ShouldReturnFalse()
    {
        var rule = new GuessRule { Conditions = [], Default = null };

        Assert.False(rule.Matches(["anything"]));
    }

    [Fact]
    public void Matches_AllConditionsMet_ShouldReturnTrue()
    {
        var rule = new GuessRule
        {
            Conditions = ["color", "size"],
            Guess = "red ball"
        };

        Assert.True(rule.Matches(["COLOR", "SIZE", "shape"]));
    }

    [Fact]
    public void Matches_PartialConditions_ShouldReturnFalse()
    {
        var rule = new GuessRule
        {
            Conditions = ["color", "size"],
            Guess = "red ball"
        };

        Assert.False(rule.Matches(["color"]));
    }

    [Fact]
    public void Matches_CaseInsensitive_ShouldWork()
    {
        var rule = new GuessRule
        {
            Conditions = ["animal"],
            Guess = "dog"
        };

        Assert.True(rule.Matches(["ANIMAL"]));
    }

    [Fact]
    public void Matches_ContainsMatch_ShouldWork()
    {
        var rule = new GuessRule
        {
            Conditions = ["color"],
            Guess = "red"
        };

        // "colorful" contains "color"
        Assert.True(rule.Matches(["colorful"]));
    }

    [Fact]
    public void GetGuess_WithGuess_ShouldReturnGuess()
    {
        var rule = new GuessRule { Guess = "answer", Default = "fallback" };

        Assert.Equal("answer", rule.GetGuess());
    }

    [Fact]
    public void GetGuess_WithoutGuess_ShouldReturnDefault()
    {
        var rule = new GuessRule { Guess = null, Default = "fallback" };

        Assert.Equal("fallback", rule.GetGuess());
    }

    [Fact]
    public void GetGuess_NeitherSet_ShouldReturnUnknown()
    {
        var rule = new GuessRule { Guess = null, Default = null };

        Assert.Equal("unknown", rule.GetGuess());
    }
}

public class FallbackConfigTests
{
    [Fact]
    public void GetAllItems_WithItems_ShouldReturnItems()
    {
        var config = new FallbackConfig
        {
            Items = ["a", "b"],
            Default = ["c", "d"]
        };

        var items = config.GetAllItems();

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0]);
    }

    [Fact]
    public void GetAllItems_EmptyItems_ShouldReturnDefault()
    {
        var config = new FallbackConfig
        {
            Items = [],
            Default = ["c", "d"]
        };

        var items = config.GetAllItems();

        Assert.Equal(2, items.Count);
        Assert.Equal("c", items[0]);
    }

    [Fact]
    public void GetAllItems_BothEmpty_ShouldReturnEmpty()
    {
        var config = new FallbackConfig
        {
            Items = [],
            Default = []
        };

        var items = config.GetAllItems();

        Assert.Empty(items);
    }
}
