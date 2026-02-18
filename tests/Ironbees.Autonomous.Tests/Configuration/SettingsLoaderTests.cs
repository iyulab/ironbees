using Ironbees.Autonomous.Configuration;
using Ironbees.Autonomous.Models;
using Xunit;

namespace Ironbees.Autonomous.Tests.Configuration;

public class SettingsLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsLoader _loader = new();

    public SettingsLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // --- LoadFromString ---

    [Fact]
    public void LoadFromString_EmptyYaml_ShouldReturnDefaults()
    {
        var settings = _loader.LoadFromString("{}");

        Assert.NotNull(settings);
        Assert.NotNull(settings.Llm);
        Assert.NotNull(settings.Orchestration);
        Assert.NotNull(settings.Debug);
    }

    [Fact]
    public void LoadFromString_NullYaml_ShouldReturnDefaults()
    {
        // YAML null/empty document deserializes as null YamlModel, which maps to defaults
        var settings = _loader.LoadFromString("");

        Assert.NotNull(settings);
    }

    [Fact]
    public void LoadFromString_LlmSettings_ShouldMapCorrectly()
    {
        var yaml = """
            llm:
              endpoint: "https://api.example.com"
              api_key: "sk-test"
              model: "gpt-4"
              max_output_tokens: 500
              temperature: 0.3
              top_p: 0.9
              frequency_penalty: 0.1
              presence_penalty: 0.2
              timeout_seconds: 30
              enable_debug_output: true
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.Equal("https://api.example.com", settings.Llm.Endpoint);
        Assert.Equal("sk-test", settings.Llm.ApiKey);
        Assert.Equal("gpt-4", settings.Llm.Model);
        Assert.Equal(500, settings.Llm.MaxOutputTokens);
        Assert.Equal(0.3f, settings.Llm.Temperature);
        Assert.Equal(0.9f, settings.Llm.TopP);
        Assert.Equal(0.1f, settings.Llm.FrequencyPenalty);
        Assert.Equal(0.2f, settings.Llm.PresencePenalty);
        Assert.Equal(30, settings.Llm.TimeoutSeconds);
        Assert.True(settings.Llm.EnableDebugOutput);
    }

    [Fact]
    public void LoadFromString_LlmDefaults_ShouldBeCorrect()
    {
        var settings = _loader.LoadFromString("{}");

        Assert.Null(settings.Llm.Endpoint);
        Assert.Null(settings.Llm.ApiKey);
        Assert.Null(settings.Llm.Model);
        Assert.Equal(200, settings.Llm.MaxOutputTokens);
        Assert.Equal(0.7f, settings.Llm.Temperature);
        Assert.Equal(60, settings.Llm.TimeoutSeconds);
        Assert.False(settings.Llm.EnableDebugOutput);
    }

    [Fact]
    public void LoadFromString_OrchestrationSettings_ShouldMapCorrectly()
    {
        var yaml = """
            orchestration:
              max_iterations: 20
              completion_mode: "single_goal"
              enable_checkpointing: false
              continue_on_failure: true
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.Equal(20, settings.Orchestration.MaxIterations);
        Assert.Equal(CompletionMode.SingleGoal, settings.Orchestration.CompletionMode);
        Assert.False(settings.Orchestration.EnableCheckpointing);
        Assert.True(settings.Orchestration.ContinueOnFailure);
    }

    [Theory]
    [InlineData("until_queue_empty", CompletionMode.UntilQueueEmpty)]
    [InlineData("untilqueueempty", CompletionMode.UntilQueueEmpty)]
    [InlineData("single_goal", CompletionMode.SingleGoal)]
    [InlineData("singlegoal", CompletionMode.SingleGoal)]
    [InlineData("until_goal_achieved", CompletionMode.UntilGoalAchieved)]
    [InlineData("untilgoalachieved", CompletionMode.UntilGoalAchieved)]
    public void LoadFromString_CompletionMode_ShouldParse(string mode, CompletionMode expected)
    {
        var yaml = $"""
            orchestration:
              completion_mode: "{mode}"
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.Equal(expected, settings.Orchestration.CompletionMode);
    }

    [Fact]
    public void LoadFromString_OracleSettings_ShouldMapCorrectly()
    {
        var yaml = """
            orchestration:
              oracle:
                enabled: false
                max_iterations: 3
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.False(settings.Orchestration.Oracle.Enabled);
        Assert.Equal(3, settings.Orchestration.Oracle.MaxIterations);
    }

    [Fact]
    public void LoadFromString_ConfidenceSettings_ShouldMapCorrectly()
    {
        var yaml = """
            orchestration:
              confidence:
                min_threshold: 0.9
                human_review_threshold: 0.3
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.Equal(0.9, settings.Orchestration.Confidence.MinThreshold);
        Assert.Equal(0.3, settings.Orchestration.Confidence.HumanReviewThreshold);
    }

    [Fact]
    public void LoadFromString_HitlSettings_ShouldMapCorrectly()
    {
        var yaml = """
            orchestration:
              human_in_the_loop:
                enabled: true
                auto_approve_on_timeout: false
                request_feedback_on_complete: true
                required_approval_points:
                  - "before_task_start"
                  - "task_failed"
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.True(settings.Orchestration.HumanInTheLoop.Enabled);
        Assert.False(settings.Orchestration.HumanInTheLoop.AutoApproveOnTimeout);
        Assert.True(settings.Orchestration.HumanInTheLoop.RequestFeedbackOnComplete);
        Assert.Equal(2, settings.Orchestration.HumanInTheLoop.RequiredApprovalPoints.Count);
    }

    [Fact]
    public void LoadFromString_ContextSettings_ShouldMapCorrectly()
    {
        var yaml = """
            orchestration:
              context:
                enable_tracking: false
                enable_reflection: false
                max_learnings: 20
                max_outputs: 10
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.False(settings.Orchestration.Context.EnableTracking);
        Assert.False(settings.Orchestration.Context.EnableReflection);
        Assert.Equal(20, settings.Orchestration.Context.MaxLearnings);
        Assert.Equal(10, settings.Orchestration.Context.MaxOutputs);
    }

    [Fact]
    public void LoadFromString_AutoContinueSettings_ShouldMapCorrectly()
    {
        var yaml = """
            orchestration:
              auto_continue:
                enabled: true
                prompt_template: "Keep going {iteration}"
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.True(settings.Orchestration.AutoContinue.Enabled);
        Assert.Equal("Keep going {iteration}", settings.Orchestration.AutoContinue.PromptTemplate);
    }

    [Fact]
    public void LoadFromString_RetrySettings_ShouldMapCorrectly()
    {
        var yaml = """
            orchestration:
              retry:
                count: 3
                delay_ms: 2000
                enable_fallback: true
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.Equal(3, settings.Orchestration.Retry.Count);
        Assert.Equal(2000, settings.Orchestration.Retry.DelayMs);
        Assert.True(settings.Orchestration.Retry.EnableFallback);
    }

    [Fact]
    public void LoadFromString_DebugSettings_ShouldMapCorrectly()
    {
        var yaml = """
            debug:
              enabled: true
              show_llm_responses: true
              show_token_usage: true
              show_reasoning: true
            """;

        var settings = _loader.LoadFromString(yaml);

        Assert.True(settings.Debug.Enabled);
        Assert.True(settings.Debug.ShowLlmResponses);
        Assert.True(settings.Debug.ShowTokenUsage);
        Assert.True(settings.Debug.ShowReasoning);
    }

    [Fact]
    public void LoadFromString_InvalidYaml_ShouldThrowSettingsParseException()
    {
        var yaml = "invalid: [yaml: {broken";

        Assert.Throws<SettingsParseException>(() =>
            _loader.LoadFromString(yaml));
    }

    [Fact]
    public void SettingsParseException_ShouldHaveLineAndColumn()
    {
        var ex = new SettingsParseException("Error at line 5", line: 5, column: 10);

        Assert.Equal("Error at line 5", ex.Message);
        Assert.Equal(5, ex.Line);
        Assert.Equal(10, ex.Column);
    }

    // --- LoadFromFileAsync ---

    [Fact]
    public async Task LoadFromFileAsync_FileNotFound_ShouldThrow()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _loader.LoadFromFileAsync(Path.Combine(_tempDir, "missing.yaml")));
    }

    [Fact]
    public async Task LoadFromFileAsync_ValidFile_ShouldParse()
    {
        var yaml = """
            llm:
              model: "gpt-4"
            """;
        var filePath = Path.Combine(_tempDir, "settings.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var settings = await _loader.LoadFromFileAsync(filePath);

        Assert.Equal("gpt-4", settings.Llm.Model);
    }

    // --- ToAutonomousConfig ---

    [Fact]
    public void ToAutonomousConfig_ShouldMapAllFields()
    {
        var settings = new OrchestratorSettings
        {
            Orchestration = new OrchestrationSettings
            {
                MaxIterations = 15,
                CompletionMode = CompletionMode.SingleGoal,
                EnableCheckpointing = false,
                ContinueOnFailure = true,
                Oracle = new OracleSettings { Enabled = false, MaxIterations = 3 },
                Confidence = new ConfidenceThresholdSettings { MinThreshold = 0.8, HumanReviewThreshold = 0.4 },
                Context = new ContextSettings { EnableTracking = false, MaxLearnings = 20, MaxOutputs = 8 },
                AutoContinue = new AutoContinueSettings { Enabled = true },
                Retry = new RetrySettings { Count = 2, DelayMs = 500, EnableFallback = true }
            }
        };

        var config = settings.ToAutonomousConfig();

        Assert.Equal(15, config.MaxIterations);
        Assert.False(config.EnableOracle);
        Assert.Equal(3, config.MaxOracleIterations);
        Assert.Equal(CompletionMode.SingleGoal, config.CompletionMode);
        Assert.False(config.EnableCheckpointing);
        Assert.True(config.ContinueOnFailure);
        Assert.Equal(0.8, config.MinConfidenceThreshold);
        Assert.Equal(0.4, config.HumanReviewConfidenceThreshold);
        Assert.True(config.AutoContinueOnOracle);
        Assert.Equal(2, config.RetryOnFailureCount);
        Assert.Equal(500, config.RetryDelayMs);
        Assert.True(config.EnableFallbackStrategy);
    }
}
