// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Goals;

/// <summary>
/// Interface for loading Goal definitions from the filesystem.
/// </summary>
public interface IGoalLoader
{
    /// <summary>
    /// Loads a single goal configuration from the specified path.
    /// </summary>
    /// <param name="goalPath">Path to the goal directory containing goal.yaml.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded goal definition.</returns>
    /// <exception cref="GoalLoadException">Thrown when the goal cannot be loaded.</exception>
    Task<GoalDefinition> LoadGoalAsync(
        string goalPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all goal configurations from the specified directory.
    /// </summary>
    /// <param name="goalsDirectory">Directory containing goal subdirectories. Defaults to "./goals".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of loaded goal definitions.</returns>
    Task<IReadOnlyList<GoalDefinition>> LoadAllGoalsAsync(
        string? goalsDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a directory contains a valid goal definition.
    /// </summary>
    /// <param name="goalPath">Path to the goal directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the directory contains a valid goal definition.</returns>
    Task<bool> ValidateGoalDirectoryAsync(
        string goalPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a goal by its ID from the specified directory.
    /// </summary>
    /// <param name="goalId">The goal ID to find.</param>
    /// <param name="goalsDirectory">Directory to search. Defaults to "./goals".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The goal definition if found, null otherwise.</returns>
    Task<GoalDefinition?> GetGoalByIdAsync(
        string goalId,
        string? goalsDirectory = null,
        CancellationToken cancellationToken = default);
}
