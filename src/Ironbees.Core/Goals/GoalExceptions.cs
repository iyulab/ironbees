// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Goals;

/// <summary>
/// Exception thrown when a goal cannot be loaded.
/// </summary>
public class GoalLoadException : Exception
{
    /// <summary>
    /// The path where the goal was expected.
    /// </summary>
    public string? GoalPath { get; }

    /// <summary>
    /// The goal ID if known.
    /// </summary>
    public string? GoalId { get; }

    public GoalLoadException(string message)
        : base(message)
    {
    }

    public GoalLoadException(string message, string goalPath)
        : base(message)
    {
        GoalPath = goalPath;
    }

    public GoalLoadException(string message, string goalPath, Exception innerException)
        : base(message, innerException)
    {
        GoalPath = goalPath;
    }

    public GoalLoadException(string message, string goalPath, string goalId)
        : base(message)
    {
        GoalPath = goalPath;
        GoalId = goalId;
    }
}

/// <summary>
/// Exception thrown when a goal definition is invalid.
/// </summary>
public class GoalValidationException : Exception
{
    /// <summary>
    /// The path to the invalid goal.
    /// </summary>
    public string? GoalPath { get; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    public GoalValidationException(string message, IEnumerable<string> errors)
        : base(message)
    {
        Errors = errors.ToList();
    }

    public GoalValidationException(string message, string goalPath, IEnumerable<string> errors)
        : base(message)
    {
        GoalPath = goalPath;
        Errors = errors.ToList();
    }
}
