using System.Text.RegularExpressions;
using Ironbees.Autonomous.Executors;

namespace Ironbees.Autonomous.Utilities;

/// <summary>
/// Builds prompts by substituting {{variables}} with actual values.
/// Supports agent definitions, game configs, and custom variables.
/// </summary>
public partial class PromptTemplateBuilder
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Create a new prompt template builder
    /// </summary>
    public PromptTemplateBuilder() { }

    /// <summary>
    /// Add a variable for substitution
    /// </summary>
    public PromptTemplateBuilder WithVariable(string name, string value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// Add multiple variables from a dictionary
    /// </summary>
    public PromptTemplateBuilder WithVariables(IDictionary<string, string>? variables)
    {
        if (variables != null)
        {
            foreach (var (key, value) in variables)
            {
                _variables[key] = value;
            }
        }
        return this;
    }

    /// <summary>
    /// Add variables from an AgentDefinition
    /// </summary>
    public PromptTemplateBuilder WithAgentVariables(AgentDefinition agent)
    {
        if (agent.Variables != null)
        {
            WithVariables(agent.Variables);
        }
        return this;
    }

    /// <summary>
    /// Add variables from an object's public properties
    /// </summary>
    public PromptTemplateBuilder WithObjectVariables(object obj, string? prefix = null)
    {
        var type = obj.GetType();
        foreach (var prop in type.GetProperties())
        {
            if (prop.CanRead)
            {
                var value = prop.GetValue(obj);
                if (value != null)
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? prop.Name.ToLowerInvariant()
                        : $"{prefix}.{prop.Name.ToLowerInvariant()}";
                    _variables[key] = value.ToString() ?? "";
                }
            }
        }
        return this;
    }

    /// <summary>
    /// Build the prompt by substituting all variables
    /// </summary>
    /// <param name="template">Template with {{variable}} placeholders</param>
    /// <returns>Processed prompt with substituted values</returns>
    public string Build(string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return VariablePattern().Replace(template, match =>
        {
            var varName = match.Groups[1].Value;
            return _variables.TryGetValue(varName, out var value) ? value : match.Value;
        });
    }

    /// <summary>
    /// Build multiple templates
    /// </summary>
    public IEnumerable<string> BuildAll(IEnumerable<string> templates)
    {
        return templates.Select(Build);
    }

    /// <summary>
    /// Check if a template contains unresolved variables
    /// </summary>
    public bool HasUnresolvedVariables(string template)
    {
        var matches = VariablePattern().Matches(template);
        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            if (!_variables.ContainsKey(varName))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get list of unresolved variable names in a template
    /// </summary>
    public IReadOnlyList<string> GetUnresolvedVariables(string template)
    {
        var unresolved = new List<string>();
        var matches = VariablePattern().Matches(template);
        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            if (!_variables.ContainsKey(varName))
                unresolved.Add(varName);
        }
        return unresolved;
    }

    /// <summary>
    /// Clear all variables
    /// </summary>
    public PromptTemplateBuilder Clear()
    {
        _variables.Clear();
        return this;
    }

    /// <summary>
    /// Static helper to quickly substitute variables
    /// </summary>
    public static string Substitute(string template, IDictionary<string, string> variables)
    {
        return new PromptTemplateBuilder()
            .WithVariables(variables)
            .Build(template);
    }

    /// <summary>
    /// Static helper to substitute agent variables in system prompt
    /// </summary>
    public static string BuildAgentPrompt(AgentDefinition agent, IDictionary<string, string>? additionalVariables = null)
    {
        var builder = new PromptTemplateBuilder()
            .WithAgentVariables(agent);

        if (additionalVariables != null)
            builder.WithVariables(additionalVariables);

        return builder.Build(agent.SystemPrompt);
    }

    [GeneratedRegex(@"\{\{(\w+(?:\.\w+)*)\}\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();
}
