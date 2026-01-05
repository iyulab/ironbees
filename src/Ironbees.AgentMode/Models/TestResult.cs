using System.Collections.Immutable;

namespace Ironbees.AgentMode.Models;

/// <summary>
/// Result of dotnet test execution.
/// </summary>
public record TestResult
{
    /// <summary>
    /// Whether all tests passed.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Total number of tests executed.
    /// </summary>
    public required int Total { get; init; }

    /// <summary>
    /// Number of tests passed.
    /// </summary>
    public required int Passed { get; init; }

    /// <summary>
    /// Number of tests failed.
    /// </summary>
    public required int Failed { get; init; }

    /// <summary>
    /// Number of tests skipped.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// List of failed test details.
    /// </summary>
    public ImmutableList<TestFailure> Failures { get; init; }
        = ImmutableList<TestFailure>.Empty;

    /// <summary>
    /// Test execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
}
