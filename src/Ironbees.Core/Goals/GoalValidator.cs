// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Goals;

/// <summary>
/// Result of goal validation.
/// </summary>
public record GoalValidationResult
{
    /// <summary>
    /// Whether the goal is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public required List<string> Errors { get; init; }

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public required List<string> Warnings { get; init; }

    /// <summary>
    /// The path to the goal that was validated.
    /// </summary>
    public required string GoalPath { get; init; }

    /// <summary>
    /// The goal ID if successfully parsed.
    /// </summary>
    public string? GoalId { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static GoalValidationResult Success(string goalPath, string goalId) => new()
    {
        IsValid = true,
        Errors = [],
        Warnings = [],
        GoalPath = goalPath,
        GoalId = goalId
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static GoalValidationResult Failure(string goalPath, IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList(),
        Warnings = [],
        GoalPath = goalPath
    };
}

/// <summary>
/// Validates goal definitions.
/// </summary>
public static class GoalValidator
{
    private const string GoalFileName = "goal.yaml";

    /// <summary>
    /// Validates a goal directory structure.
    /// </summary>
    /// <param name="goalPath">Path to the goal directory.</param>
    /// <returns>Validation result.</returns>
    public static GoalValidationResult ValidateDirectory(string goalPath)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check directory exists
        if (!Directory.Exists(goalPath))
        {
            return GoalValidationResult.Failure(goalPath, [$"Goal directory not found: {goalPath}"]);
        }

        // Check goal.yaml exists
        var goalFile = Path.Combine(goalPath, GoalFileName);
        if (!File.Exists(goalFile))
        {
            errors.Add($"Required file '{GoalFileName}' not found in {goalPath}");
        }

        if (errors.Count > 0)
        {
            return GoalValidationResult.Failure(goalPath, errors);
        }

        return new GoalValidationResult
        {
            IsValid = true,
            Errors = [],
            Warnings = warnings,
            GoalPath = goalPath
        };
    }

    /// <summary>
    /// Validates a goal definition.
    /// </summary>
    /// <param name="goal">The goal to validate.</param>
    /// <returns>Validation result.</returns>
    public static GoalValidationResult Validate(GoalDefinition goal)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var goalPath = goal.SourcePath ?? "unknown";

        // Required fields
        if (string.IsNullOrWhiteSpace(goal.Id))
        {
            errors.Add("Goal 'id' is required");
        }

        if (string.IsNullOrWhiteSpace(goal.Name))
        {
            errors.Add("Goal 'name' is required");
        }

        if (string.IsNullOrWhiteSpace(goal.Description))
        {
            errors.Add("Goal 'description' is required");
        }

        if (string.IsNullOrWhiteSpace(goal.WorkflowTemplate))
        {
            errors.Add("Goal 'workflowTemplate' is required");
        }

        // Validate constraints
        if (goal.Constraints.MaxIterations < 1)
        {
            errors.Add("Constraints 'maxIterations' must be at least 1");
        }

        if (goal.Constraints.MaxTokens.HasValue && goal.Constraints.MaxTokens.Value < 1)
        {
            errors.Add("Constraints 'maxTokens' must be at least 1 if specified");
        }

        // Validate success criteria
        foreach (var criterion in goal.SuccessCriteria)
        {
            if (string.IsNullOrWhiteSpace(criterion.Id))
            {
                errors.Add("Success criterion 'id' is required");
            }

            if (string.IsNullOrWhiteSpace(criterion.Description))
            {
                errors.Add($"Success criterion '{criterion.Id}' requires a description");
            }

            if (criterion.Weight < 0.0 || criterion.Weight > 1.0)
            {
                errors.Add($"Success criterion '{criterion.Id}' weight must be between 0.0 and 1.0");
            }

            if (criterion.Type == SuccessCriterionType.Condition && string.IsNullOrWhiteSpace(criterion.Condition))
            {
                errors.Add($"Success criterion '{criterion.Id}' with type 'Condition' requires a condition expression");
            }
        }

        // Warnings
        if (goal.SuccessCriteria.Count == 0)
        {
            warnings.Add("No success criteria defined; goal completion will be determined by workflow completion");
        }

        if (goal.Constraints.MaxIterations > 100)
        {
            warnings.Add($"High maxIterations ({goal.Constraints.MaxIterations}) may lead to long-running goals");
        }

        return new GoalValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            GoalPath = goalPath,
            GoalId = goal.Id
        };
    }
}
