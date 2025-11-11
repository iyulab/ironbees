using Ironbees.Core;
using Moq;

namespace Ironbees.Core.Tests;

[Trait("Category", "Integration")]
public class KeywordAgentSelectorEnhancedTests
{
    private IAgent CreateTestAgent(
        string name,
        string description,
        List<string>? capabilities = null,
        List<string>? tags = null)
    {
        var mockAgent = new Mock<IAgent>();
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

        mockAgent.Setup(a => a.Name).Returns(name);
        mockAgent.Setup(a => a.Description).Returns(description);
        mockAgent.Setup(a => a.Config).Returns(config);

        return mockAgent.Object;
    }

    [Fact]
    public async Task SelectAgentAsync_WithSynonyms_MatchesCorrectly()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with programming tasks",
            capabilities: new List<string> { "code-generation", "debugging" },
            tags: new List<string> { "programming", "development" });

        var writingAgent = CreateTestAgent(
            "writing-agent",
            "Helps with writing tasks",
            capabilities: new List<string> { "content-writing" },
            tags: new List<string> { "writing" });

        var agents = new List<IAgent> { codingAgent, writingAgent };

        // Act - Using synonym "script" for "code"
        var result = await selector.SelectAgentAsync("Help me script something", agents);

        // Assert - Should match coding-agent through synonym mapping
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("coding-agent", result.SelectedAgent.Name);
    }

    [Fact]
    public async Task SelectAgentAsync_WithStemming_MatchesDifferentForms()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Expert developer",
            capabilities: new List<string> { "code-generation" },
            tags: new List<string> { "programming", "development" });

        var agents = new List<IAgent> { codingAgent };

        // Act - Using different word forms
        var result1 = await selector.SelectAgentAsync("I need coding help", agents);
        var result2 = await selector.SelectAgentAsync("Help me develop", agents);
        var result3 = await selector.SelectAgentAsync("I want to program", agents);

        // Assert - All should match through stemming
        Assert.Equal("coding-agent", result1.SelectedAgent?.Name);
        Assert.Equal("coding-agent", result2.SelectedAgent?.Name);
        Assert.Equal("coding-agent", result3.SelectedAgent?.Name);
    }

    [Fact]
    public async Task SelectAgentAsync_DotNetTerms_NotFilteredAsStopwords()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var dotnetAgent = CreateTestAgent(
            "dotnet-agent",
            "C# and .NET specialist",
            capabilities: new List<string> { "csharp-development", "dotnet-core" },
            tags: new List<string> { "dotnet", "csharp", "net" });

        var javaAgent = CreateTestAgent(
            "java-agent",
            "Java specialist",
            capabilities: new List<string> { "java-development" },
            tags: new List<string> { "java" });

        var agents = new List<IAgent> { dotnetAgent, javaAgent };

        // Act
        var result = await selector.SelectAgentAsync("Help with .NET code", agents);

        // Assert - Should match dotnet-agent, not be filtered
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("dotnet-agent", result.SelectedAgent.Name);
    }

    [Fact]
    public async Task SelectAgentAsync_TfidfBoost_ImprovesRelevantAgent()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var pythonAgent = CreateTestAgent(
            "python-agent",
            "Python programming expert with extensive Python experience for Python development",
            capabilities: new List<string> { "python-coding", "python-debugging", "python-testing" },
            tags: new List<string> { "python", "python-development", "python-expert" });

        var generalAgent = CreateTestAgent(
            "general-agent",
            "General programming help",
            capabilities: new List<string> { "coding" },
            tags: new List<string> { "programming" });

        var agents = new List<IAgent> { pythonAgent, generalAgent };

        // Act
        var result = await selector.SelectAgentAsync("Python programming", agents);

        // Assert - TF-IDF should boost python-agent due to repeated "python" term
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("python-agent", result.SelectedAgent.Name);
        Assert.True(result.ConfidenceScore > 0.5);
    }

    [Fact]
    public async Task SelectAgentAsync_CsharpSynonyms_AllVariationsMatch()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var csharpAgent = CreateTestAgent(
            "csharp-agent",
            "C# expert developer",
            capabilities: new List<string> { "csharp-development" },
            tags: new List<string> { "csharp", "dotnet" });

        var agents = new List<IAgent> { csharpAgent };

        // Act - Different C# variations
        var result1 = await selector.SelectAgentAsync("Help with C# code", agents);
        var result2 = await selector.SelectAgentAsync("Help with csharp code", agents);
        var result3 = await selector.SelectAgentAsync("Help with cs code", agents);

        // Assert - All variations should match
        Assert.Equal("csharp-agent", result1.SelectedAgent?.Name);
        Assert.Equal("csharp-agent", result2.SelectedAgent?.Name);
        Assert.Equal("csharp-agent", result3.SelectedAgent?.Name);
    }

    [Fact]
    public async Task SelectAgentAsync_DatabaseSynonyms_MatchCorrectly()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var databaseAgent = CreateTestAgent(
            "database-agent",
            "Database specialist",
            capabilities: new List<string> { "database-design", "sql" },
            tags: new List<string> { "database", "data" });

        var agents = new List<IAgent> { databaseAgent };

        // Act - Using synonyms: db, datastore
        var result1 = await selector.SelectAgentAsync("Help with db queries", agents);
        var result2 = await selector.SelectAgentAsync("Design a datastore", agents);

        // Assert
        Assert.Equal("database-agent", result1.SelectedAgent?.Name);
        Assert.Equal("database-agent", result2.SelectedAgent?.Name);
    }

    [Fact]
    public async Task SelectAgentAsync_AuthenticationSynonyms_MatchCorrectly()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var securityAgent = CreateTestAgent(
            "security-agent",
            "Security specialist",
            capabilities: new List<string> { "authentication", "authorization" },
            tags: new List<string> { "security", "auth" });

        var agents = new List<IAgent> { securityAgent };

        // Act - Using synonyms
        var result1 = await selector.SelectAgentAsync("Help with login", agents);
        var result2 = await selector.SelectAgentAsync("Setup signin", agents);
        var result3 = await selector.SelectAgentAsync("Add auth", agents);

        // Assert
        Assert.Equal("security-agent", result1.SelectedAgent?.Name);
        Assert.Equal("security-agent", result2.SelectedAgent?.Name);
        Assert.Equal("security-agent", result3.SelectedAgent?.Name);
    }

    [Fact]
    public async Task SelectAgentAsync_ComplexQuery_UsesAllEnhancements()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var fullstackAgent = CreateTestAgent(
            "fullstack-agent",
            "Full-stack developer specializing in C# backend and React frontend development",
            capabilities: new List<string> { "csharp-development", "react-development", "api-development" },
            tags: new List<string> { "fullstack", "csharp", "react", "web" });

        var backendAgent = CreateTestAgent(
            "backend-agent",
            "Backend specialist",
            capabilities: new List<string> { "api-development" },
            tags: new List<string> { "backend", "api" });

        var agents = new List<IAgent> { fullstackAgent, backendAgent };

        // Act - Complex query with synonyms, stemming, and TF-IDF
        var result = await selector.SelectAgentAsync(
            "I'm developing a C# backend API and React frontend for my web application",
            agents);

        // Assert - Should match fullstack-agent with high confidence
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("fullstack-agent", result.SelectedAgent.Name);
        Assert.True(result.ConfidenceScore > 0.6);
    }

    [Fact]
    public async Task SelectAgentAsync_CachePerformance_SameQueryUsesCache()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agent = CreateTestAgent(
            "test-agent",
            "Test agent",
            capabilities: new List<string> { "testing" },
            tags: new List<string> { "test" });

        var agents = new List<IAgent> { agent };
        var query = "Help me with testing";

        // Act - First call
        var result1 = await selector.SelectAgentAsync(query, agents);

        // Act - Second call with same query (should use cache)
        var result2 = await selector.SelectAgentAsync(query, agents);

        // Assert - Both should return same result
        Assert.Equal(result1.SelectedAgent?.Name, result2.SelectedAgent?.Name);
        Assert.Equal(result1.ConfidenceScore, result2.ConfidenceScore);
    }

    [Fact]
    public void ClearCache_RemovesCachedKeywords()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var agent = CreateTestAgent(
            "test-agent",
            "Test agent",
            capabilities: new List<string> { "testing" },
            tags: new List<string> { "test" });

        var agents = new List<IAgent> { agent };

        // Build cache
        selector.SelectAgentAsync("Query 1", agents).Wait();
        selector.SelectAgentAsync("Query 2", agents).Wait();

        // Act
        selector.ClearCache();

        // Assert - Should work after clearing cache
        var result = selector.SelectAgentAsync("Query 3", agents).Result;
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SelectAgentAsync_StopwordsFiltered_DoesNotAffectMatching()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var codingAgent = CreateTestAgent(
            "coding-agent",
            "Helps with code",
            capabilities: new List<string> { "code-generation" },
            tags: new List<string> { "programming" });

        var agents = new List<IAgent> { codingAgent };

        // Act - Query with many stopwords
        var result = await selector.SelectAgentAsync(
            "I would like you to help me with the code that I am writing",
            agents);

        // Assert - Should still match despite stopwords
        Assert.NotNull(result.SelectedAgent);
        Assert.Equal("coding-agent", result.SelectedAgent.Name);
    }

    [Fact]
    public async Task SelectAgentAsync_ApiSynonyms_MatchCorrectly()
    {
        // Arrange
        var selector = new KeywordAgentSelector();

        var apiAgent = CreateTestAgent(
            "api-agent",
            "API development specialist",
            capabilities: new List<string> { "api-development", "rest-api" },
            tags: new List<string> { "api", "webservice" });

        var agents = new List<IAgent> { apiAgent };

        // Act - Using synonyms: endpoint, service, webservice
        var result1 = await selector.SelectAgentAsync("Create an endpoint", agents);
        var result2 = await selector.SelectAgentAsync("Build a service", agents);
        var result3 = await selector.SelectAgentAsync("Design webservice", agents);

        // Assert
        Assert.Equal("api-agent", result1.SelectedAgent?.Name);
        Assert.Equal("api-agent", result2.SelectedAgent?.Name);
        Assert.Equal("api-agent", result3.SelectedAgent?.Name);
    }
}
