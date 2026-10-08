using Ironbees.Autonomous.Configuration;
using Ironbees.Autonomous.Executors;
using Xunit;

namespace Ironbees.Autonomous.Tests.Yaml;

/// <summary>
/// The autonomous SDK's YAML files are snake_case. A key no field reads used to vanish (the 0.18.0 removal of unread
/// settings left such keys in fixtures for months); now the loaders name it, and the bundled sample files load cleanly.
/// </summary>
public sealed class UnknownYamlKeyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ironbees-auto-yaml-{Guid.NewGuid():N}");

    public UnknownYamlKeyTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Settings_with_a_camelCase_key_fail_and_name_the_snake_case_spelling()
    {
        var ex = Assert.Throws<SettingsParseException>(() => new SettingsLoader().LoadFromString("""
            llm:
              maxOutputTokens: 100
            """));

        Assert.Contains("llm.maxOutputTokens", ex.Message, StringComparison.Ordinal);
        Assert.Contains("max_output_tokens", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_agent_definition_with_a_key_nothing_reads_fails_and_names_the_file()
    {
        var agent = Path.Combine(_dir, "a");
        Directory.CreateDirectory(agent);
        await File.WriteAllTextAsync(Path.Combine(agent, "agent.yaml"), "name: a\nsystemPrompt: hi\n", TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => new AgentDefinitionLoader().LoadAgentAsync(agent, TestContext.Current.CancellationToken));

        Assert.Contains("system_prompt", ex.Message, StringComparison.Ordinal);
        Assert.Contains("agent.yaml", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_game_definition_with_a_key_nothing_reads_fails()
    {
        Assert.Throws<InvalidDataException>(() => new GameConfigLoader().LoadFromString("no_such_key: 1\n"));
    }

    [Fact]
    public async Task The_bundled_sample_files_load_without_unread_keys()
    {
        var samples = Path.Combine(RepoRoot(), "samples");
        var ct = TestContext.Current.CancellationToken;

        var agents = await new AgentDefinitionLoader().LoadAgentsAsync(Path.Combine(samples, "TwentyQuestionsSample", "agents"), ct);
        Assert.NotEmpty(agents);
        // These sections were never loaded before 0.25.0: AgentDefinition declared them, the YAML model did not.
        var questioner = agents.Values.Single(a => a.GuessRules is not null);
        Assert.NotNull(questioner.Resilience);
        Assert.Equal(3, questioner.Resilience!.MaxRetries);
        Assert.NotEmpty(questioner.Fallback!.Default);
        Assert.NotEmpty(questioner.Fallback.Pools!);
        new GameConfigLoader().LoadFromString(await File.ReadAllTextAsync(Path.Combine(samples, "TwentyQuestionsSample", "config", "game.yaml"), ct));
        new SettingsLoader().LoadFromString(await File.ReadAllTextAsync(Path.Combine(samples, "TwentyQuestionsSample", "game-settings.yaml"), ct));
        new SettingsLoader().LoadFromString(await File.ReadAllTextAsync(Path.Combine(samples, "MinimalAutonomousSample", "settings.yaml"), ct));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ironbees.slnx")))
                return dir.FullName;
        }

        throw new DirectoryNotFoundException("Ironbees.slnx not found above the test output directory");
    }
}
