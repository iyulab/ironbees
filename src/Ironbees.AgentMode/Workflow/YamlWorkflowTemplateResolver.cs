// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Ironbees.Core.Goals;

namespace Ironbees.AgentMode.Workflow;

/// <summary>
/// Resolves YAML workflow templates from filesystem with parameter substitution.
/// </summary>
/// <remarks>
/// Expected directory structure:
/// /workflows/templates/
///   goal-loop.yaml
///   sequential.yaml
///   parallel.yaml
///
/// Parameter Syntax:
/// - {{goal.id}} - Goal ID
/// - {{goal.name}} - Goal name
/// - {{goal.constraints.maxIterations}} - Max iterations
/// - {{parameters.executor}} - Custom parameter from goal.parameters
/// </remarks>
public partial class YamlWorkflowTemplateResolver : IWorkflowTemplateResolver
{
    private const string DefaultTemplatesDirectory = "workflows/templates";
    private const string TemplateExtension = ".yaml";

    private readonly string _templatesDirectory;
    private readonly IWorkflowLoader _workflowLoader;
    private readonly YamlWorkflowTemplateResolverOptions _options;

    /// <summary>
    /// Regex pattern for matching template placeholders.
    /// Matches: {{path.to.value}} or {{ path.to.value }}
    /// </summary>
    [GeneratedRegex(@"\{\{\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex ParameterPlaceholderRegex();

    public YamlWorkflowTemplateResolver(IWorkflowLoader workflowLoader)
        : this(workflowLoader, new YamlWorkflowTemplateResolverOptions())
    {
    }

    public YamlWorkflowTemplateResolver(
        IWorkflowLoader workflowLoader,
        YamlWorkflowTemplateResolverOptions options)
    {
        _workflowLoader = workflowLoader;
        _options = options;
        _templatesDirectory = options.TemplatesDirectory ??
            Path.Combine(Directory.GetCurrentDirectory(), DefaultTemplatesDirectory);
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> ResolveAsync(
        string templateName,
        GoalDefinition goal,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(goal);

        // Build parameters from goal
        var parameters = BuildParametersFromGoal(goal);
        return await ResolveAsync(templateName, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> ResolveAsync(
        string templateName,
        IDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(parameters);

        var templatePath = GetTemplatePath(templateName);
        if (!File.Exists(templatePath))
        {
            throw new WorkflowTemplateNotFoundException(templateName, [_templatesDirectory]);
        }

        try
        {
            // Read template content
            var templateContent = await File.ReadAllTextAsync(templatePath, cancellationToken);

            // Substitute parameters
            var resolvedContent = SubstituteParameters(templateContent, parameters, templateName);

            // Parse resolved YAML
            return await _workflowLoader.LoadFromStringAsync(resolvedContent, cancellationToken);
        }
        catch (WorkflowTemplateNotFoundException)
        {
            throw;
        }
        catch (WorkflowTemplateResolutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WorkflowTemplateResolutionException(templateName, ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public bool TemplateExists(string templateName)
    {
        var templatePath = GetTemplatePath(templateName);
        return File.Exists(templatePath);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableTemplates()
    {
        if (!Directory.Exists(_templatesDirectory))
        {
            return [];
        }

        return Directory.GetFiles(_templatesDirectory, $"*{TemplateExtension}")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();
    }

    /// <inheritdoc />
    public WorkflowTemplateValidationResult ValidateTemplate(string templateName)
    {
        var templatePath = GetTemplatePath(templateName);
        if (!File.Exists(templatePath))
        {
            return WorkflowTemplateValidationResult.Failure(
                templateName,
                [$"Template file not found: {templatePath}"]);
        }

        try
        {
            var content = File.ReadAllText(templatePath);
            var errors = new List<string>();
            var warnings = new List<string>();
            var parameters = new HashSet<string>();

            // Find all parameter placeholders
            var matches = ParameterPlaceholderRegex().Matches(content);
            foreach (Match match in matches)
            {
                var paramName = match.Groups[1].Value;
                parameters.Add(paramName);

                // Validate parameter path syntax
                if (!IsValidParameterPath(paramName))
                {
                    errors.Add($"Invalid parameter path syntax: {paramName}");
                }
            }

            // Check for common issues
            if (!content.Contains("name:"))
            {
                errors.Add("Template must define 'name' field");
            }

            if (!content.Contains("states:"))
            {
                errors.Add("Template must define 'states' field");
            }

            if (parameters.Count == 0)
            {
                warnings.Add("Template has no parameter placeholders");
            }

            return new WorkflowTemplateValidationResult
            {
                TemplateName = templateName,
                Errors = errors,
                Warnings = warnings,
                Parameters = parameters.OrderBy(p => p).ToList()
            };
        }
        catch (Exception ex)
        {
            return WorkflowTemplateValidationResult.Failure(
                templateName,
                [$"Failed to read template: {ex.Message}"]);
        }
    }

    private string GetTemplatePath(string templateName)
    {
        var name = templateName.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase)
            ? templateName
            : templateName + TemplateExtension;
        return Path.Combine(_templatesDirectory, name);
    }

    private Dictionary<string, object> BuildParametersFromGoal(GoalDefinition goal)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Goal root properties
        parameters["goal.id"] = goal.Id;
        parameters["goal.name"] = goal.Name;
        parameters["goal.description"] = goal.Description;
        parameters["goal.version"] = goal.Version;
        parameters["goal.workflowTemplate"] = goal.WorkflowTemplate;

        // Constraints
        parameters["goal.constraints.maxIterations"] = goal.Constraints.MaxIterations;
        if (goal.Constraints.MaxTokens.HasValue)
        {
            parameters["goal.constraints.maxTokens"] = goal.Constraints.MaxTokens.Value;
        }
        if (goal.Constraints.MaxDuration.HasValue)
        {
            parameters["goal.constraints.maxDuration"] = goal.Constraints.MaxDuration.Value.TotalMinutes;
        }

        // Checkpoint settings
        parameters["goal.checkpoint.enabled"] = goal.Checkpoint.Enabled.ToString().ToLowerInvariant();
        parameters["goal.checkpoint.afterEachIteration"] = goal.Checkpoint.AfterEachIteration.ToString().ToLowerInvariant();
        parameters["goal.checkpoint.directory"] = goal.Checkpoint.CheckpointDirectory;

        // Tags as comma-separated string
        parameters["goal.tags"] = string.Join(",", goal.Tags);

        // Custom parameters from goal.Parameters
        foreach (var (key, value) in goal.Parameters)
        {
            parameters[$"parameters.{key}"] = value;
        }

        // Success criteria count
        parameters["goal.successCriteria.count"] = goal.SuccessCriteria.Count;

        return parameters;
    }

    private string SubstituteParameters(
        string template,
        IDictionary<string, object> parameters,
        string templateName)
    {
        var unresolvedParameters = new List<string>();

        var result = ParameterPlaceholderRegex().Replace(template, match =>
        {
            var paramPath = match.Groups[1].Value;

            // Try exact match first (case-insensitive)
            if (parameters.TryGetValue(paramPath, out var value))
            {
                return FormatValue(value);
            }

            // Try with normalized path
            var normalizedPath = paramPath.ToLowerInvariant();
            var matchingKey = parameters.Keys.FirstOrDefault(k =>
                k.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (matchingKey != null)
            {
                return FormatValue(parameters[matchingKey]);
            }

            // Parameter not found
            if (_options.StrictMode)
            {
                unresolvedParameters.Add(paramPath);
            }

            // Return original placeholder or default value
            return _options.UseDefaultForMissing
                ? _options.DefaultValue ?? ""
                : match.Value;
        });

        if (unresolvedParameters.Count > 0)
        {
            throw new WorkflowTemplateResolutionException(templateName, unresolvedParameters);
        }

        return result;
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            bool b => b.ToString().ToLowerInvariant(),
            DateTime dt => dt.ToString("O"),
            TimeSpan ts => ts.TotalMinutes.ToString("F2"),
            IEnumerable<string> list => string.Join(",", list),
            _ => value.ToString() ?? ""
        };
    }

    private static bool IsValidParameterPath(string path)
    {
        // Must start with letter or underscore
        // Can contain letters, numbers, underscores, and dots
        // Dots separate path segments
        var segments = path.Split('.');
        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment))
                return false;

            if (!char.IsLetter(segment[0]) && segment[0] != '_')
                return false;

            if (!segment.All(c => char.IsLetterOrDigit(c) || c == '_'))
                return false;
        }
        return true;
    }
}

/// <summary>
/// Options for YamlWorkflowTemplateResolver.
/// </summary>
public class YamlWorkflowTemplateResolverOptions
{
    /// <summary>
    /// Directory containing workflow templates. Defaults to "workflows/templates".
    /// </summary>
    public string? TemplatesDirectory { get; set; }

    /// <summary>
    /// If true, throws exception on unresolved parameters.
    /// If false, leaves placeholders or uses default value.
    /// </summary>
    public bool StrictMode { get; set; } = true;

    /// <summary>
    /// Use default value for missing parameters instead of keeping placeholder.
    /// Only applies when StrictMode is false.
    /// </summary>
    public bool UseDefaultForMissing { get; set; } = false;

    /// <summary>
    /// Default value to use for missing parameters.
    /// Only applies when UseDefaultForMissing is true.
    /// </summary>
    public string? DefaultValue { get; set; }
}
