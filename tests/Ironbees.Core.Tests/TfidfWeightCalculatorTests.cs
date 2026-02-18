using NSubstitute;

namespace Ironbees.Core.Tests;

public class TfidfWeightCalculatorTests
{
    private static IAgent CreateAgent(string name, string description,
        List<string>? capabilities = null, List<string>? tags = null)
    {
        var config = new AgentConfig
        {
            Name = name,
            Description = description,
            Version = "1.0.0",
            SystemPrompt = "",
            Model = new ModelConfig { Deployment = "test" },
            Capabilities = capabilities ?? [],
            Tags = tags ?? []
        };

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns(description);
        agent.Config.Returns(config);
        return agent;
    }

    // IDF formula: log(N / (df + 1))
    // For IDF > 0: need N > df + 1, so need >= 3 agents with word in only 1

    private static IAgent[] CreateThreeAgents() =>
    [
        CreateAgent("coder", "writes code programs", ["coding"], ["backend"]),
        CreateAgent("reviewer", "reviews documentation quality", ["review"], ["qa"]),
        CreateAgent("designer", "creates visual designs", ["figma"], ["frontend"])
    ];

    // Constructor / IDF

    [Fact]
    public void Constructor_EmptyAgents_ShouldNotThrow()
    {
        var calc = new TfidfWeightCalculator([]);

        Assert.Equal(0.0, calc.GetIdfScore("anything"));
    }

    [Fact]
    public void GetIdfScore_UniqueWord_ShouldReturnPositive()
    {
        // "code" appears in only 1 of 3 agents → IDF = log(3/2) > 0
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var score = calc.GetIdfScore("code");

        Assert.True(score > 0);
    }

    [Fact]
    public void GetIdfScore_CommonWord_ShouldBeLowerThanRare()
    {
        // All 3 agents have a name that's unique + description words
        // "reviews" in 1 agent, vs a word in all 3 would have lower IDF
        var agents = new[]
        {
            CreateAgent("agent-alpha", "agent shared task"),
            CreateAgent("agent-beta", "agent shared work"),
            CreateAgent("agent-gamma", "agent shared unique-gamma-word")
        };
        var calc = new TfidfWeightCalculator(agents);

        // "agent" appears in all 3 names+descriptions: df=3, IDF=log(3/4)<0
        var commonScore = calc.GetIdfScore("agent");
        // "unique-gamma-word" appears in only 1: df=1, IDF=log(3/2)>0
        // But hyphens are separators, so "unique" appears in 1: df=1
        var rareScore = calc.GetIdfScore("unique");

        Assert.True(rareScore > commonScore);
    }

    [Fact]
    public void GetIdfScore_UnknownWord_ShouldReturnZero()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        Assert.Equal(0.0, calc.GetIdfScore("nonexistent"));
    }

    [Fact]
    public void GetIdfScore_CaseInsensitive_ShouldWork()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var lower = calc.GetIdfScore("code");
        var upper = calc.GetIdfScore("CODE");

        Assert.Equal(lower, upper);
    }

    // CalculateTfidfScore

    [Fact]
    public void CalculateTfidfScore_EmptyQueryWords_ShouldReturnZero()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var score = calc.CalculateTfidfScore([], "writes code");

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void CalculateTfidfScore_EmptyDocument_ShouldReturnZero()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var score = calc.CalculateTfidfScore(["code"], "");

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void CalculateTfidfScore_WhitespaceDocument_ShouldReturnZero()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var score = calc.CalculateTfidfScore(["code"], "   ");

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void CalculateTfidfScore_MatchingTerms_ShouldReturnPositive()
    {
        // "code" appears in 1 of 3 agents → IDF > 0
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var score = calc.CalculateTfidfScore(["code"], "writes code and more code");

        Assert.True(score > 0);
    }

    [Fact]
    public void CalculateTfidfScore_NoMatchingTerms_ShouldReturnZero()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var score = calc.CalculateTfidfScore(["banana"], "writes code and tests");

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void CalculateTfidfScore_HigherTF_ShouldScoreHigher()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        // "code" has IDF > 0 with 3 agents
        // More occurrences of "code" → higher TF
        var highTf = calc.CalculateTfidfScore(["code"], "code code code other");
        var lowTf = calc.CalculateTfidfScore(["code"], "code other other other");

        Assert.True(highTf > lowTf);
    }

    [Fact]
    public void CalculateTfidfScore_ResultNormalized_ShouldBeBetweenZeroAndOne()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        var score = calc.CalculateTfidfScore(
            ["code", "writes"],
            "code writes programs code");

        Assert.True(score >= 0.0);
        Assert.True(score <= 1.0);
    }

    [Fact]
    public void CalculateTfidfScore_PerfectSingleWordMatch_ShouldBePositive()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        // Document is exactly the query word → TF = 1.0
        var score = calc.CalculateTfidfScore(["code"], "code");

        Assert.True(score > 0);
    }

    // Tags and capabilities in document text

    [Fact]
    public void GetIdfScore_IncludesCapabilitiesAndTags()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        // "coding" is in agent1's capabilities → should have IDF score > 0
        var score = calc.GetIdfScore("coding");

        Assert.True(score > 0);
    }

    [Fact]
    public void CalculateTfidfScore_TermInOnlyOneAgent_ShouldHaveHigherIdf()
    {
        var calc = new TfidfWeightCalculator(CreateThreeAgents());

        // "figma" only in 1 agent, "writes" also in 1 agent
        var figmaIdf = calc.GetIdfScore("figma");
        var writesIdf = calc.GetIdfScore("writes");

        // Both appear in exactly 1 of 3 agents → same IDF
        Assert.Equal(figmaIdf, writesIdf, 5);
    }
}
