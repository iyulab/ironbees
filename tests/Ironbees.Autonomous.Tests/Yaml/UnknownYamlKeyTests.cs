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
    public async Task The_bundled_sample_settings_load_without_unread_keys()
    {
        var samples = Path.Combine(RepoRoot(), "samples");
        var ct = TestContext.Current.CancellationToken;

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
