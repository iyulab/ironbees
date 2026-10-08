using Ironbees.Core.Goals;
using Ironbees.Core.Yaml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.Core.Tests.Yaml;

/// <summary>
/// A key the target type does not read used to vanish (<c>IgnoreUnmatchedProperties()</c>): <c>model.max_tokens</c> in a
/// camelCase file silently became the default. These pin what counts as known and that the loaders report the rest.
/// </summary>
public sealed class YamlUnknownKeysTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ironbees-yamlkeys-{Guid.NewGuid():N}");

    public YamlUnknownKeysTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private const string CamelAgent = """
        name: a
        description: d
        version: 1.0.0
        model:
          deployment: m
          maxTokens: 100
          topP: 0.9
        capabilities: [x]
        metadata:
          any_key: 1
          nested: { whatever: true }
        """;

    [Fact]
    public void A_file_in_the_types_convention_has_no_findings_and_dictionaries_take_any_key()
    {
        Assert.Empty(YamlUnknownKeys.Find(CamelAgent, typeof(AgentConfig), CamelCaseNamingConvention.Instance));
    }

    [Fact]
    public void A_key_in_another_convention_is_reported_with_its_path_and_the_spelling_that_is_read()
    {
        var yaml = CamelAgent.Replace("maxTokens", "max_tokens", StringComparison.Ordinal);

        var finding = Assert.Single(YamlUnknownKeys.Find(yaml, typeof(AgentConfig), CamelCaseNamingConvention.Instance));

        Assert.Equal("model.max_tokens", finding.Path);
        Assert.Equal("maxTokens", finding.Suggestion);
    }

    [Fact]
    public void A_key_nothing_resembles_has_no_suggestion()
    {
        var finding = Assert.Single(YamlUnknownKeys.Find(CamelAgent + "\nflavour: x", typeof(AgentConfig), CamelCaseNamingConvention.Instance));

        Assert.Equal("flavour", finding.Path);
        Assert.Null(finding.Suggestion);
    }

    private sealed class Step
    {
        public string? AgentName { get; set; }
    }

    private sealed class Doc
    {
        [YamlMember(Alias = "kind")]
        public string? Type { get; set; }

        [YamlIgnore]
        public string? Secret { get; set; }

        public List<Step> Steps { get; set; } = [];

        public Dictionary<string, string> Variables { get; set; } = [];
    }

    [Fact]
    public void Aliases_are_read_ignored_properties_are_not_and_list_items_are_checked_with_their_index()
    {
        const string yaml = """
            base: &b { agent_name: x }
            kind: k
            secret: s
            variables: { free_form: v }
            steps:
              - agent_name: ok
              - agentName: wrong
            """;

        var findings = YamlUnknownKeys.Find(yaml, typeof(Doc), UnderscoredNamingConvention.Instance);

        Assert.Equal(["base", "secret", "steps[1].agentName"], findings.Select(f => f.Path));
        Assert.Equal("agent_name", findings.Single(f => f.Path == "steps[1].agentName").Suggestion);
    }

    private string WriteAgent(string yaml, string prompt = "You are a test agent.")
    {
        var path = Path.Combine(_dir, $"agent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "agent.yaml"), yaml);
        File.WriteAllText(Path.Combine(path, "system-prompt.md"), prompt);
        return path;
    }

    [Fact]
    public async Task Strict_loading_fails_on_a_key_it_would_drop_and_names_it()
    {
        var path = WriteAgent(CamelAgent.Replace("maxTokens", "max_tokens", StringComparison.Ordinal));
        var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions { StrictValidation = true, EnableCaching = false });

        var ex = await Assert.ThrowsAsync<AgentConfigurationException>(() => loader.LoadConfigAsync(path, TestContext.Current.CancellationToken));

        Assert.Contains("model.max_tokens", ex.Message, StringComparison.Ordinal);
        Assert.Contains("maxTokens", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Default_loading_reports_a_dropped_key_even_when_there_is_no_warning()
    {
        var path = WriteAgent(CamelAgent.Replace("maxTokens", "max_tokens", StringComparison.Ordinal));
        var logger = new ListLogger();
        var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions { EnableCaching = false }, logger);

        var config = await loader.LoadConfigAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(4000, config.Model.MaxTokens); // the default - which is why it must be reported
        Assert.Contains(logger.Messages, m => m.Contains("model.max_tokens", StringComparison.Ordinal));
    }

    private sealed class ListLogger : ILogger<FileSystemAgentLoader>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task A_system_prompt_in_the_yaml_is_a_warning_because_the_prompt_file_wins()
    {
        var path = WriteAgent(CamelAgent + "\nsystemPrompt: ignored", prompt: "from the file");
        var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions { StrictValidation = true, EnableCaching = false }, NullLogger<FileSystemAgentLoader>.Instance);

        var config = await loader.LoadConfigAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal("from the file", config.SystemPrompt);
        Assert.Equal(100, config.Model.MaxTokens);
    }

    [Fact]
    public async Task Strict_goal_loading_fails_on_a_key_it_would_drop()
    {
        var goalDir = Path.Combine(_dir, "goals", "g1");
        Directory.CreateDirectory(goalDir);
        File.WriteAllText(Path.Combine(goalDir, "goal.yaml"), """
            id: g1
            name: G
            description: d
            max_iterations: 3
            """);
        var loader = new FileSystemGoalLoader(new FileSystemGoalLoaderOptions { StrictValidation = true, EnableCaching = false });

        var ex = await Assert.ThrowsAsync<GoalValidationException>(() => loader.LoadGoalAsync(goalDir, TestContext.Current.CancellationToken));

        Assert.Contains("max_iterations", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_bundled_example_agent_loads_strictly_without_findings()
    {
        var agents = FindRepoAgentsDirectory();
        var loader = new FileSystemAgentLoader(new FileSystemAgentLoaderOptions { StrictValidation = true, EnableCaching = false });

        var failures = new List<string>();
        foreach (var dir in Directory.GetDirectories(agents))
        {
            try { await loader.LoadConfigAsync(dir, TestContext.Current.CancellationToken); }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(dir)}: {ex.Message}"); }
        }

        Assert.NotEmpty(Directory.GetDirectories(agents));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static string FindRepoAgentsDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ironbees.slnx")))
                return Path.Combine(dir.FullName, "agents");
        }

        throw new DirectoryNotFoundException("Ironbees.slnx not found above the test output directory");
    }
}
