using System.Text.RegularExpressions;

namespace Ironbees.Core;

/// <summary>
/// Validates agent configuration for correctness and completeness
/// </summary>
public static class AgentConfigValidator
{
    private static readonly Regex VersionRegex = new(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9\-\.]+)?$", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new(@"^[a-z0-9\-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Validates an agent configuration and returns validation results
    /// </summary>
    public static ValidationResult Validate(AgentConfig config, string agentPath)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields validation
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            errors.Add("Agent name is required");
        }
        else if (!NameRegex.IsMatch(config.Name))
        {
            errors.Add($"Agent name '{config.Name}' must contain only lowercase letters, numbers, and hyphens");
        }

        if (string.IsNullOrWhiteSpace(config.Description))
        {
            errors.Add("Agent description is required");
        }
        else if (config.Description.Length < 10)
        {
            warnings.Add("Agent description is very short (< 10 characters)");
        }

        if (string.IsNullOrWhiteSpace(config.Version))
        {
            errors.Add("Agent version is required");
        }
        else if (!VersionRegex.IsMatch(config.Version))
        {
            errors.Add($"Agent version '{config.Version}' is not a valid semantic version (e.g., '1.0.0')");
        }

        if (string.IsNullOrWhiteSpace(config.SystemPrompt))
        {
            errors.Add("System prompt is required");
        }
        else if (config.SystemPrompt.Length < 50)
        {
            warnings.Add("System prompt is very short (< 50 characters)");
        }

        // Model configuration validation
        if (config.Model == null)
        {
            errors.Add("Model configuration is required");
        }
        else
        {
            ValidateModelConfig(config.Model, errors, warnings);
        }

        // Capabilities validation
        if (config.Capabilities.Count == 0)
        {
            warnings.Add("Agent has no capabilities defined");
        }
        else
        {
            foreach (var capability in config.Capabilities)
            {
                if (string.IsNullOrWhiteSpace(capability))
                {
                    errors.Add("Capability cannot be empty");
                }
            }
        }

        // Tags validation
        if (config.Tags.Count == 0)
        {
            warnings.Add("Agent has no tags defined");
        }
        else
        {
            foreach (var tag in config.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    errors.Add("Tag cannot be empty");
                }
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            AgentPath = agentPath,
            AgentName = config.Name
        };
    }

    private static void ValidateModelConfig(ModelConfig model, List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(model.Deployment))
        {
            errors.Add("Model deployment name is required");
        }

        if (model.Temperature < 0.0 || model.Temperature > 2.0)
        {
            errors.Add($"Model temperature {model.Temperature} is out of valid range (0.0 to 2.0)");
        }

        if (model.MaxTokens <= 0)
        {
            errors.Add($"Model maxTokens {model.MaxTokens} must be positive");
        }
        else if (model.MaxTokens > 128000)
        {
            warnings.Add($"Model maxTokens {model.MaxTokens} is very high (> 128K)");
        }

        if (model.TopP < 0.0 || model.TopP > 1.0)
        {
            errors.Add($"Model topP {model.TopP} is out of valid range (0.0 to 1.0)");
        }
    }

    /// <summary>
    /// Checks if an agent name is unique within a collection
    /// </summary>
    public static bool IsUniqueAgentName(string agentName, IEnumerable<AgentConfig> existingConfigs)
    {
        return !existingConfigs.Any(c =>
            string.Equals(c.Name, agentName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Result of agent configuration validation
/// </summary>
public record ValidationResult
{
    public required bool IsValid { get; init; }
    public required List<string> Errors { get; init; }
    public required List<string> Warnings { get; init; }
    public required string AgentPath { get; init; }
    public required string AgentName { get; init; }

    /// <summary>
    /// Gets a formatted error message for display
    /// </summary>
    public string GetFormattedErrors()
    {
        if (IsValid)
            return string.Empty;

        var message = $"Validation failed for agent '{AgentName}' at '{AgentPath}':\n";

        if (Errors.Count > 0)
        {
            message += "\nErrors:\n";
            message += string.Join("\n", Errors.Select((e, i) => $"  {i + 1}. {e}"));
        }

        if (Warnings.Count > 0)
        {
            message += "\n\nWarnings:\n";
            message += string.Join("\n", Warnings.Select((w, i) => $"  {i + 1}. {w}"));
        }

        return message;
    }
}
