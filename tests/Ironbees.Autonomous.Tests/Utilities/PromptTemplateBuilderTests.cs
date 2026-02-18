using Ironbees.Autonomous.Executors;
using Ironbees.Autonomous.Utilities;
using Xunit;

namespace Ironbees.Autonomous.Tests.Utilities;

public class PromptTemplateBuilderTests
{
    [Fact]
    public void Build_NullTemplate_ShouldReturnNull()
    {
        var builder = new PromptTemplateBuilder();

        var result = builder.Build(null!);

        Assert.Null(result);
    }

    [Fact]
    public void Build_EmptyTemplate_ShouldReturnEmpty()
    {
        var builder = new PromptTemplateBuilder();

        var result = builder.Build("");

        Assert.Equal("", result);
    }

    [Fact]
    public void Build_NoVariables_ShouldReturnOriginal()
    {
        var builder = new PromptTemplateBuilder();

        var result = builder.Build("Hello world");

        Assert.Equal("Hello world", result);
    }

    [Fact]
    public void Build_WithVariable_ShouldSubstitute()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("name", "Alice");

        var result = builder.Build("Hello {{name}}!");

        Assert.Equal("Hello Alice!", result);
    }

    [Fact]
    public void Build_MultipleVariables_ShouldSubstituteAll()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("greeting", "Hi")
            .WithVariable("name", "Bob");

        var result = builder.Build("{{greeting}}, {{name}}!");

        Assert.Equal("Hi, Bob!", result);
    }

    [Fact]
    public void Build_CaseInsensitive_ShouldSubstitute()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("Name", "Charlie");

        var result = builder.Build("Hello {{name}}!");

        Assert.Equal("Hello Charlie!", result);
    }

    [Fact]
    public void Build_UnknownVariable_ShouldKeepPlaceholder()
    {
        var builder = new PromptTemplateBuilder();

        var result = builder.Build("Hello {{unknown}}!");

        Assert.Equal("Hello {{unknown}}!", result);
    }

    [Fact]
    public void Build_DottedVariable_ShouldSubstitute()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("agent.name", "MyAgent");

        var result = builder.Build("Agent: {{agent.name}}");

        Assert.Equal("Agent: MyAgent", result);
    }

    [Fact]
    public void WithVariables_NullDictionary_ShouldNotThrow()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariables(null);

        var result = builder.Build("Hello");

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void WithVariables_Dictionary_ShouldAddAll()
    {
        var vars = new Dictionary<string, string>
        {
            ["a"] = "1",
            ["b"] = "2"
        };
        var builder = new PromptTemplateBuilder()
            .WithVariables(vars);

        var result = builder.Build("{{a}}-{{b}}");

        Assert.Equal("1-2", result);
    }

    [Fact]
    public void HasUnresolvedVariables_AllResolved_ShouldReturnFalse()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("x", "1");

        Assert.False(builder.HasUnresolvedVariables("{{x}}"));
    }

    [Fact]
    public void HasUnresolvedVariables_SomeUnresolved_ShouldReturnTrue()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("x", "1");

        Assert.True(builder.HasUnresolvedVariables("{{x}} {{y}}"));
    }

    [Fact]
    public void HasUnresolvedVariables_NoPlaceholders_ShouldReturnFalse()
    {
        var builder = new PromptTemplateBuilder();

        Assert.False(builder.HasUnresolvedVariables("plain text"));
    }

    [Fact]
    public void GetUnresolvedVariables_ShouldReturnUnresolvedNames()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("a", "1");

        var unresolved = builder.GetUnresolvedVariables("{{a}} {{b}} {{c}}");

        Assert.Equal(2, unresolved.Count);
        Assert.Contains("b", unresolved);
        Assert.Contains("c", unresolved);
    }

    [Fact]
    public void GetUnresolvedVariables_AllResolved_ShouldReturnEmpty()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("x", "1");

        var unresolved = builder.GetUnresolvedVariables("{{x}}");

        Assert.Empty(unresolved);
    }

    [Fact]
    public void WithObjectVariables_ShouldExtractProperties()
    {
        var obj = new { Name = "test", Count = 42 };
        var builder = new PromptTemplateBuilder()
            .WithObjectVariables(obj);

        var result = builder.Build("{{name}}-{{count}}");

        Assert.Equal("test-42", result);
    }

    [Fact]
    public void WithObjectVariables_WithPrefix_ShouldAddPrefix()
    {
        var obj = new { Name = "test" };
        var builder = new PromptTemplateBuilder()
            .WithObjectVariables(obj, "agent");

        var result = builder.Build("{{agent.name}}");

        Assert.Equal("test", result);
    }

    [Fact]
    public void BuildAll_ShouldProcessMultipleTemplates()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("x", "1");

        var results = builder.BuildAll(["a {{x}}", "b {{x}}"]).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("a 1", results[0]);
        Assert.Equal("b 1", results[1]);
    }

    [Fact]
    public void Clear_ShouldRemoveAllVariables()
    {
        var builder = new PromptTemplateBuilder()
            .WithVariable("x", "1")
            .Clear();

        var result = builder.Build("{{x}}");

        Assert.Equal("{{x}}", result);
    }

    [Fact]
    public void Substitute_StaticHelper_ShouldWork()
    {
        var vars = new Dictionary<string, string> { ["name"] = "World" };

        var result = PromptTemplateBuilder.Substitute("Hello {{name}}", vars);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void BuildAgentPrompt_ShouldSubstituteAgentVariables()
    {
        var agent = new AgentDefinition
        {
            SystemPrompt = "You are {{role}} agent",
            Variables = new Dictionary<string, string> { ["role"] = "a helpful" }
        };

        var result = PromptTemplateBuilder.BuildAgentPrompt(agent);

        Assert.Equal("You are a helpful agent", result);
    }

    [Fact]
    public void BuildAgentPrompt_WithAdditionalVariables_ShouldMerge()
    {
        var agent = new AgentDefinition
        {
            SystemPrompt = "{{role}} for {{task}}",
            Variables = new Dictionary<string, string> { ["role"] = "helper" }
        };

        var result = PromptTemplateBuilder.BuildAgentPrompt(agent,
            new Dictionary<string, string> { ["task"] = "coding" });

        Assert.Equal("helper for coding", result);
    }

    [Fact]
    public void FluentChaining_ShouldReturnSameBuilder()
    {
        var builder = new PromptTemplateBuilder();

        var result = builder
            .WithVariable("a", "1")
            .WithVariables(new Dictionary<string, string> { ["b"] = "2" })
            .Clear()
            .WithVariable("c", "3");

        Assert.Same(builder, result);
    }
}
