using Ironbees.Autonomous.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.Autonomous.Configuration;

/// <summary>
/// Loads orchestrator settings from YAML configuration files.
/// Follows Ironbees' filesystem convention-based configuration approach.
/// </summary>
public sealed class SettingsLoader
{
    private readonly IDeserializer _deserializer;

    public SettingsLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Load settings from a YAML file
    /// </summary>
    public async Task<OrchestratorSettings> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Settings file not found: {filePath}", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return LoadFromString(content);
    }

    /// <summary>
    /// Load settings from a YAML string
    /// </summary>
    public OrchestratorSettings LoadFromString(string yamlContent)
    {
        try
        {
            var yamlModel = _deserializer.Deserialize<YamlOrchestratorSettings>(yamlContent);
            return MapToSettings(yamlModel);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new SettingsParseException(
                $"Failed to parse settings YAML: {ex.Message}",
                (int)ex.Start.Line,
                (int)ex.Start.Column);
        }
    }

    /// <summary>
    /// Load settings from environment and optional YAML file
    /// </summary>
    public async Task<OrchestratorSettings> LoadWithEnvironmentAsync(
        string? yamlFilePath = null,
        CancellationToken cancellationToken = default)
    {
        OrchestratorSettings settings;

        if (!string.IsNullOrEmpty(yamlFilePath) && File.Exists(yamlFilePath))
        {
            settings = await LoadFromFileAsync(yamlFilePath, cancellationToken);
        }
        else
        {
            settings = new OrchestratorSettings();
        }

        // Apply environment variable overrides
        return ApplyEnvironmentOverrides(settings);
    }

    private static OrchestratorSettings ApplyEnvironmentOverrides(OrchestratorSettings settings)
    {
        var llm = settings.Llm;

        // Override LLM settings from environment if not set
        if (string.IsNullOrEmpty(llm.Endpoint))
        {
            var endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT")
                ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
                ?? Environment.GetEnvironmentVariable("GPUSTACK_ENDPOINT");
            if (!string.IsNullOrEmpty(endpoint))
            {
                llm = llm with { Endpoint = endpoint };
            }
        }

        if (string.IsNullOrEmpty(llm.ApiKey))
        {
            var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? Environment.GetEnvironmentVariable("GPUSTACK_APIKEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                llm = llm with { ApiKey = apiKey };
            }
        }

        if (string.IsNullOrEmpty(llm.Model))
        {
            var model = Environment.GetEnvironmentVariable("LLM_MODEL")
                ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
                ?? Environment.GetEnvironmentVariable("GPUSTACK_MODEL");
            if (!string.IsNullOrEmpty(model))
            {
                llm = llm with { Model = model };
            }
        }

        return settings with { Llm = llm };
    }

    private static OrchestratorSettings MapToSettings(YamlOrchestratorSettings? yaml)
    {
        if (yaml == null)
        {
            return new OrchestratorSettings();
        }

        return new OrchestratorSettings
        {
            Llm = MapLlmSettings(yaml.Llm),
            Orchestration = MapOrchestrationSettings(yaml.Orchestration),
            Debug = MapDebugSettings(yaml.Debug)
        };
    }

    private static LlmSettings MapLlmSettings(YamlLlmSettings? yaml)
    {
        if (yaml == null)
        {
            return new LlmSettings();
        }

        return new LlmSettings
        {
            Endpoint = yaml.Endpoint,
            ApiKey = yaml.ApiKey,
            Model = yaml.Model,
            MaxOutputTokens = yaml.MaxOutputTokens ?? 200,
            Temperature = yaml.Temperature ?? 0.7f,
            TopP = yaml.TopP,
            FrequencyPenalty = yaml.FrequencyPenalty,
            PresencePenalty = yaml.PresencePenalty,
            TimeoutSeconds = yaml.TimeoutSeconds ?? 60,
            EnableDebugOutput = yaml.EnableDebugOutput ?? false
        };
    }

    private static OrchestrationSettings MapOrchestrationSettings(YamlOrchestrationSettings? yaml)
    {
        if (yaml == null)
        {
            return new OrchestrationSettings();
        }

        return new OrchestrationSettings
        {
            MaxIterations = yaml.MaxIterations ?? 10,
            CompletionMode = ParseCompletionMode(yaml.CompletionMode),
            EnableCheckpointing = yaml.EnableCheckpointing ?? true,
            ContinueOnFailure = yaml.ContinueOnFailure ?? false,
            Oracle = MapOracleSettings(yaml.Oracle),
            Confidence = MapConfidenceSettings(yaml.Confidence),
            HumanInTheLoop = MapHitlSettings(yaml.HumanInTheLoop),
            Context = MapContextSettings(yaml.Context),
            AutoContinue = MapAutoContinueSettings(yaml.AutoContinue),
            Retry = MapRetrySettings(yaml.Retry)
        };
    }

    private static OracleSettings MapOracleSettings(YamlOracleSettings? yaml)
    {
        if (yaml == null)
        {
            return new OracleSettings();
        }

        return new OracleSettings
        {
            Enabled = yaml.Enabled ?? true,
            MaxIterations = yaml.MaxIterations ?? 5
        };
    }

    private static ConfidenceThresholdSettings MapConfidenceSettings(YamlConfidenceSettings? yaml)
    {
        if (yaml == null)
        {
            return new ConfidenceThresholdSettings();
        }

        return new ConfidenceThresholdSettings
        {
            MinThreshold = yaml.MinThreshold ?? 0.7,
            HumanReviewThreshold = yaml.HumanReviewThreshold ?? 0.5
        };
    }

    private static HitlSettings MapHitlSettings(YamlHitlSettings? yaml)
    {
        if (yaml == null)
        {
            return new HitlSettings();
        }

        return new HitlSettings
        {
            Enabled = yaml.Enabled ?? false,
            AutoApproveOnTimeout = yaml.AutoApproveOnTimeout ?? true,
            RequestFeedbackOnComplete = yaml.RequestFeedbackOnComplete ?? false,
            RequiredApprovalPoints = yaml.RequiredApprovalPoints?
                .Select(ParseInterventionPoint)
                .ToList() ?? []
        };
    }

    private static ContextSettings MapContextSettings(YamlContextSettings? yaml)
    {
        if (yaml == null)
        {
            return new ContextSettings();
        }

        return new ContextSettings
        {
            EnableTracking = yaml.EnableTracking ?? true,
            EnableReflection = yaml.EnableReflection ?? true,
            MaxLearnings = yaml.MaxLearnings ?? 10,
            MaxOutputs = yaml.MaxOutputs ?? 5
        };
    }

    private static AutoContinueSettings MapAutoContinueSettings(YamlAutoContinueSettings? yaml)
    {
        if (yaml == null)
        {
            return new AutoContinueSettings();
        }

        return new AutoContinueSettings
        {
            Enabled = yaml.Enabled ?? false,
            PromptTemplate = yaml.PromptTemplate ?? "Continue with iteration {iteration}"
        };
    }

    private static RetrySettings MapRetrySettings(YamlRetrySettings? yaml)
    {
        if (yaml == null)
        {
            return new RetrySettings();
        }

        return new RetrySettings
        {
            Count = yaml.Count ?? 0,
            DelayMs = yaml.DelayMs ?? 1000,
            EnableFallback = yaml.EnableFallback ?? false
        };
    }

    private static DebugSettings MapDebugSettings(YamlDebugSettings? yaml)
    {
        if (yaml == null)
        {
            return new DebugSettings();
        }

        return new DebugSettings
        {
            Enabled = yaml.Enabled ?? false,
            ShowLlmResponses = yaml.ShowLlmResponses ?? false,
            ShowTokenUsage = yaml.ShowTokenUsage ?? false,
            ShowReasoning = yaml.ShowReasoning ?? false
        };
    }

    private static CompletionMode ParseCompletionMode(string? mode) =>
        mode?.ToLowerInvariant() switch
        {
            "until_queue_empty" or "untilqueueempty" => CompletionMode.UntilQueueEmpty,
            "single_goal" or "singlegoal" => CompletionMode.SingleGoal,
            "until_goal_achieved" or "untilgoalachieved" => CompletionMode.UntilGoalAchieved,
            null => CompletionMode.UntilQueueEmpty,
            _ => CompletionMode.UntilQueueEmpty
        };

    private static Abstractions.HumanInterventionPoint ParseInterventionPoint(string point) =>
        point.ToLowerInvariant() switch
        {
            "before_task_start" or "beforetaskstart" => Abstractions.HumanInterventionPoint.BeforeTaskStart,
            "after_task_complete" or "aftertaskcomplete" => Abstractions.HumanInterventionPoint.AfterTaskComplete,
            "oracle_uncertain" or "oracleuncertain" => Abstractions.HumanInterventionPoint.OracleUncertain,
            "task_failed" or "taskfailed" => Abstractions.HumanInterventionPoint.TaskFailed,
            "high_risk_action" or "highriskaction" => Abstractions.HumanInterventionPoint.HighRiskAction,
            "external_modification" or "externalmodification" => Abstractions.HumanInterventionPoint.ExternalModification,
            "max_iterations_approaching" or "maxiterationsapproaching" => Abstractions.HumanInterventionPoint.MaxIterationsApproaching,
            "before_checkpoint_restore" or "beforecheckpointrestore" => Abstractions.HumanInterventionPoint.BeforeCheckpointRestore,
            _ => Abstractions.HumanInterventionPoint.BeforeTaskStart
        };

    #region YAML Model Classes

    private sealed class YamlOrchestratorSettings
    {
        public YamlLlmSettings? Llm { get; set; }
        public YamlOrchestrationSettings? Orchestration { get; set; }
        public YamlDebugSettings? Debug { get; set; }
    }

    private sealed class YamlLlmSettings
    {
        public string? Endpoint { get; set; }
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        public int? MaxOutputTokens { get; set; }
        public float? Temperature { get; set; }
        public float? TopP { get; set; }
        public float? FrequencyPenalty { get; set; }
        public float? PresencePenalty { get; set; }
        public int? TimeoutSeconds { get; set; }
        public bool? EnableDebugOutput { get; set; }
    }

    private sealed class YamlOrchestrationSettings
    {
        public int? MaxIterations { get; set; }
        public string? CompletionMode { get; set; }
        public bool? EnableCheckpointing { get; set; }
        public bool? ContinueOnFailure { get; set; }
        public YamlOracleSettings? Oracle { get; set; }
        public YamlConfidenceSettings? Confidence { get; set; }
        public YamlHitlSettings? HumanInTheLoop { get; set; }
        public YamlContextSettings? Context { get; set; }
        public YamlAutoContinueSettings? AutoContinue { get; set; }
        public YamlRetrySettings? Retry { get; set; }
    }

    private sealed class YamlOracleSettings
    {
        public bool? Enabled { get; set; }
        public int? MaxIterations { get; set; }
    }

    private sealed class YamlConfidenceSettings
    {
        public double? MinThreshold { get; set; }
        public double? HumanReviewThreshold { get; set; }
    }

    private sealed class YamlHitlSettings
    {
        public bool? Enabled { get; set; }
        public bool? AutoApproveOnTimeout { get; set; }
        public bool? RequestFeedbackOnComplete { get; set; }
        public List<string>? RequiredApprovalPoints { get; set; }
    }

    private sealed class YamlContextSettings
    {
        public bool? EnableTracking { get; set; }
        public bool? EnableReflection { get; set; }
        public int? MaxLearnings { get; set; }
        public int? MaxOutputs { get; set; }
    }

    private sealed class YamlAutoContinueSettings
    {
        public bool? Enabled { get; set; }
        public string? PromptTemplate { get; set; }
    }

    private sealed class YamlRetrySettings
    {
        public int? Count { get; set; }
        public int? DelayMs { get; set; }
        public bool? EnableFallback { get; set; }
    }

    private sealed class YamlDebugSettings
    {
        public bool? Enabled { get; set; }
        public bool? ShowLlmResponses { get; set; }
        public bool? ShowTokenUsage { get; set; }
        public bool? ShowReasoning { get; set; }
    }

    #endregion
}

/// <summary>
/// Exception thrown when settings parsing fails
/// </summary>
public class SettingsParseException : Exception
{
    public int? Line { get; }
    public int? Column { get; }

    public SettingsParseException(string message, int? line = null, int? column = null)
        : base(message)
    {
        Line = line;
        Column = column;
    }
}
