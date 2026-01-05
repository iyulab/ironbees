using System.Collections.Immutable;
using Ironbees.AgentMode.Exceptions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ironbees.AgentMode.Workflow;

/// <summary>
/// Loads workflow definitions from YAML files.
/// Implements the filesystem convention-based configuration approach.
/// </summary>
public sealed class YamlWorkflowLoader : IWorkflowLoader
{
    private readonly IDeserializer _deserializer;

    public YamlWorkflowLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<WorkflowDefinition> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Workflow file not found: {filePath}", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return await LoadFromStringAsync(content, cancellationToken, filePath);
    }

    public Task<WorkflowDefinition> LoadFromStringAsync(
        string yamlContent,
        CancellationToken cancellationToken = default)
    {
        return LoadFromStringAsync(yamlContent, cancellationToken, null);
    }

    private Task<WorkflowDefinition> LoadFromStringAsync(
        string yamlContent,
        CancellationToken cancellationToken,
        string? filePath)
    {
        try
        {
            var yamlModel = _deserializer.Deserialize<YamlWorkflowModel>(yamlContent);
            var definition = MapToDefinition(yamlModel);
            return Task.FromResult(definition);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new WorkflowParseException(
                $"Failed to parse workflow YAML: {ex.Message}",
                filePath,
                (int)ex.Start.Line,
                (int)ex.Start.Column);
        }
        catch (Exception ex) when (ex is not WorkflowParseException)
        {
            throw new WorkflowParseException(
                $"Failed to parse workflow: {ex.Message}",
                filePath,
                ex);
        }
    }

    public async Task<WorkflowDefinition> LoadFromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return await LoadFromStringAsync(content, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> LoadFromDirectoryAsync(
        string directoryPath,
        string searchPattern = "*.yaml",
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Workflow directory not found: {directoryPath}");
        }

        var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);
        var workflows = new List<WorkflowDefinition>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = await LoadFromFileAsync(file, cancellationToken);
            workflows.Add(workflow);
        }

        return workflows;
    }

    public WorkflowValidationResult Validate(WorkflowDefinition workflow)
    {
        var errors = new List<WorkflowValidationError>();
        var warnings = new List<WorkflowValidationWarning>();

        // Validate workflow has a name
        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            errors.Add(new WorkflowValidationError(
                "WF001",
                "Workflow name is required.",
                "name"));
        }

        // Validate at least one state exists
        if (workflow.States.Count == 0)
        {
            errors.Add(new WorkflowValidationError(
                "WF002",
                "Workflow must have at least one state.",
                "states"));
        }

        // Validate state IDs are unique
        var duplicateIds = workflow.States
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var id in duplicateIds)
        {
            errors.Add(new WorkflowValidationError(
                "WF003",
                $"Duplicate state ID: '{id}'",
                $"states[{id}]"));
        }

        // Validate state transitions reference existing states
        var stateIds = workflow.States.Select(s => s.Id).ToHashSet();
        foreach (var state in workflow.States)
        {
            if (!string.IsNullOrWhiteSpace(state.Next) && !stateIds.Contains(state.Next))
            {
                errors.Add(new WorkflowValidationError(
                    "WF004",
                    $"State '{state.Id}' references non-existent state '{state.Next}'",
                    $"states[{state.Id}].next"));
            }

            foreach (var condition in state.Conditions)
            {
                if (!stateIds.Contains(condition.Then))
                {
                    errors.Add(new WorkflowValidationError(
                        "WF004",
                        $"State '{state.Id}' condition references non-existent state '{condition.Then}'",
                        $"states[{state.Id}].conditions"));
                }
            }

            // Validate human gate settings
            if (state.Type == WorkflowStateType.HumanGate && state.HumanGate != null)
            {
                if (!string.IsNullOrWhiteSpace(state.HumanGate.OnApprove) &&
                    !stateIds.Contains(state.HumanGate.OnApprove))
                {
                    errors.Add(new WorkflowValidationError(
                        "WF004",
                        $"HumanGate '{state.Id}' on_approve references non-existent state '{state.HumanGate.OnApprove}'",
                        $"states[{state.Id}].human_gate.on_approve"));
                }

                if (!string.IsNullOrWhiteSpace(state.HumanGate.OnReject) &&
                    !stateIds.Contains(state.HumanGate.OnReject))
                {
                    errors.Add(new WorkflowValidationError(
                        "WF004",
                        $"HumanGate '{state.Id}' on_reject references non-existent state '{state.HumanGate.OnReject}'",
                        $"states[{state.Id}].human_gate.on_reject"));
                }
            }

            // Validate agent executors reference defined agents
            if (state.Type == WorkflowStateType.Agent && !string.IsNullOrWhiteSpace(state.Executor))
            {
                var agentRefs = workflow.Agents
                    .Select(a => a.Alias ?? Path.GetFileName(a.Ref))
                    .ToHashSet();

                if (!agentRefs.Contains(state.Executor))
                {
                    warnings.Add(new WorkflowValidationWarning(
                        "WF101",
                        $"State '{state.Id}' executor '{state.Executor}' not found in agents list. " +
                        "It may be resolved at runtime.",
                        $"states[{state.Id}].executor"));
                }
            }
        }

        // Warn if no terminal state
        var hasTerminal = workflow.States.Any(s => s.Type == WorkflowStateType.Terminal);
        if (!hasTerminal)
        {
            warnings.Add(new WorkflowValidationWarning(
                "WF102",
                "Workflow has no terminal state. Ensure transitions lead to completion.",
                "states"));
        }

        return new WorkflowValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    private static WorkflowDefinition MapToDefinition(YamlWorkflowModel yaml)
    {
        return new WorkflowDefinition
        {
            Name = yaml.Name ?? throw new WorkflowParseException("Workflow name is required."),
            Version = yaml.Version ?? "1.0",
            Description = yaml.Description,
            Agents = yaml.Agents?.Select(MapAgentReference).ToImmutableList() ?? [],
            States = yaml.States?.Select(MapStateDefinition).ToImmutableList() ?? [],
            Settings = MapSettings(yaml.Settings)
        };
    }

    private static AgentReference MapAgentReference(YamlAgentReference yaml) =>
        new()
        {
            Ref = yaml.Ref ?? throw new WorkflowParseException("Agent reference 'ref' is required."),
            Alias = yaml.Alias
        };

    private static WorkflowStateDefinition MapStateDefinition(YamlStateDefinition yaml) =>
        new()
        {
            Id = yaml.Id ?? throw new WorkflowParseException("State 'id' is required."),
            Type = ParseStateType(yaml.Type),
            Executor = yaml.Executor,
            Executors = yaml.Executors?.ToImmutableList() ?? [],
            Trigger = yaml.Trigger != null ? MapTrigger(yaml.Trigger) : null,
            Next = yaml.Next,
            Conditions = yaml.Conditions?.Select(MapCondition).ToImmutableList() ?? [],
            HumanGate = yaml.HumanGate != null ? MapHumanGate(yaml.HumanGate) : null,
            MaxIterations = yaml.MaxIterations,
            Timeout = yaml.Timeout != null ? ParseTimeSpan(yaml.Timeout) : null
        };

    private static WorkflowStateType ParseStateType(string? type) =>
        type?.ToLowerInvariant() switch
        {
            "start" => WorkflowStateType.Start,
            "agent" => WorkflowStateType.Agent,
            "parallel" => WorkflowStateType.Parallel,
            "human_gate" or "humangate" => WorkflowStateType.HumanGate,
            "escalation" => WorkflowStateType.Escalation,
            "terminal" or "end" => WorkflowStateType.Terminal,
            null => WorkflowStateType.Agent, // Default
            _ => throw new WorkflowParseException($"Unknown state type: '{type}'")
        };

    private static TriggerDefinition MapTrigger(YamlTriggerDefinition yaml) =>
        new()
        {
            Type = ParseTriggerType(yaml.Type),
            Path = yaml.Path,
            Expression = yaml.Expression
        };

    private static TriggerType ParseTriggerType(string? type) =>
        type?.ToLowerInvariant() switch
        {
            "file_exists" or "fileexists" => TriggerType.FileExists,
            "directory_not_empty" or "dir_not_empty" or "directorynotempty" => TriggerType.DirectoryNotEmpty,
            "immediate" => TriggerType.Immediate,
            "expression" => TriggerType.Expression,
            null => TriggerType.Immediate, // Default
            _ => throw new WorkflowParseException($"Unknown trigger type: '{type}'")
        };

    private static ConditionalTransition MapCondition(YamlCondition yaml) =>
        new()
        {
            If = yaml.If,
            Then = yaml.Then ?? throw new WorkflowParseException("Condition 'then' is required."),
            IsDefault = yaml.Else ?? false
        };

    private static HumanGateSettings MapHumanGate(YamlHumanGateSettings yaml) =>
        new()
        {
            ApprovalMode = yaml.ApprovalMode ?? "always_require",
            Timeout = yaml.Timeout != null ? ParseTimeSpan(yaml.Timeout) : TimeSpan.FromHours(24),
            OnApprove = yaml.OnApprove,
            OnReject = yaml.OnReject,
            NotifyEmail = yaml.NotifyEmail
        };

    private static WorkflowSettings MapSettings(YamlWorkflowSettings? yaml) =>
        yaml == null
            ? new WorkflowSettings()
            : new WorkflowSettings
            {
                DefaultTimeout = yaml.DefaultTimeout != null
                    ? ParseTimeSpan(yaml.DefaultTimeout)
                    : TimeSpan.FromMinutes(30),
                DefaultMaxIterations = yaml.DefaultMaxIterations ?? 5,
                EnableCheckpointing = yaml.EnableCheckpointing ?? true,
                CheckpointDirectory = yaml.CheckpointDirectory ?? ".ironbees/checkpoints"
            };

    private static TimeSpan ParseTimeSpan(string value)
    {
        // Support formats: "30m", "1h", "24h", "1d", "00:30:00"
        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            return timeSpan;
        }

        var trimmed = value.Trim().ToLowerInvariant();
        if (trimmed.EndsWith("m") && int.TryParse(trimmed[..^1], out var minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }
        if (trimmed.EndsWith("h") && int.TryParse(trimmed[..^1], out var hours))
        {
            return TimeSpan.FromHours(hours);
        }
        if (trimmed.EndsWith("d") && int.TryParse(trimmed[..^1], out var days))
        {
            return TimeSpan.FromDays(days);
        }
        if (trimmed.EndsWith("s") && int.TryParse(trimmed[..^1], out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        throw new WorkflowParseException($"Invalid timespan format: '{value}'");
    }

    #region YAML Model Classes

    private sealed class YamlWorkflowModel
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
        public List<YamlAgentReference>? Agents { get; set; }
        public List<YamlStateDefinition>? States { get; set; }
        public YamlWorkflowSettings? Settings { get; set; }
    }

    private sealed class YamlAgentReference
    {
        public string? Ref { get; set; }
        public string? Alias { get; set; }
    }

    private sealed class YamlStateDefinition
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Executor { get; set; }
        public List<string>? Executors { get; set; }
        public YamlTriggerDefinition? Trigger { get; set; }
        public string? Next { get; set; }
        public List<YamlCondition>? Conditions { get; set; }
        public YamlHumanGateSettings? HumanGate { get; set; }
        public int? MaxIterations { get; set; }
        public string? Timeout { get; set; }
    }

    private sealed class YamlTriggerDefinition
    {
        public string? Type { get; set; }
        public string? Path { get; set; }
        public string? Expression { get; set; }
    }

    private sealed class YamlCondition
    {
        public string? If { get; set; }
        public string? Then { get; set; }
        public bool? Else { get; set; }
    }

    private sealed class YamlHumanGateSettings
    {
        public string? ApprovalMode { get; set; }
        public string? Timeout { get; set; }
        public string? OnApprove { get; set; }
        public string? OnReject { get; set; }
        public string? NotifyEmail { get; set; }
    }

    private sealed class YamlWorkflowSettings
    {
        public string? DefaultTimeout { get; set; }
        public int? DefaultMaxIterations { get; set; }
        public bool? EnableCheckpointing { get; set; }
        public string? CheckpointDirectory { get; set; }
    }

    #endregion
}
