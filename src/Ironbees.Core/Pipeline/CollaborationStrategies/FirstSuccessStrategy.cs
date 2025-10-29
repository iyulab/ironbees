namespace Ironbees.Core.Pipeline.CollaborationStrategies;

/// <summary>
/// Returns the first successful result, ignoring others (fastest response wins)
/// Useful for fallback patterns where any valid response is acceptable
/// </summary>
public class FirstSuccessStrategy : ICollaborationStrategy
{
    private readonly CollaborationOptions _options;
    private readonly Func<PipelineStepResult, bool>? _validationFunction;

    public string Name => "FirstSuccess";

    /// <summary>
    /// Create a first-success strategy
    /// </summary>
    /// <param name="validationFunction">Optional function to validate if a result is acceptable</param>
    /// <param name="options">Collaboration options</param>
    public FirstSuccessStrategy(
        Func<PipelineStepResult, bool>? validationFunction = null,
        CollaborationOptions? options = null)
    {
        _validationFunction = validationFunction;
        _options = options ?? new CollaborationOptions();
    }

    /// <summary>
    /// Create a first-success strategy with minimum length requirement
    /// </summary>
    public static FirstSuccessStrategy WithMinimumLength(
        int minimumLength,
        CollaborationOptions? options = null)
    {
        return new FirstSuccessStrategy(
            result => (result.Output?.Length ?? 0) >= minimumLength,
            options);
    }

    /// <summary>
    /// Create a first-success strategy with maximum execution time requirement
    /// </summary>
    public static FirstSuccessStrategy WithMaximumTime(
        TimeSpan maximumTime,
        CollaborationOptions? options = null)
    {
        return new FirstSuccessStrategy(
            result => result.ExecutionTime <= maximumTime,
            options);
    }

    /// <summary>
    /// Create a first-success strategy with keyword validation
    /// </summary>
    public static FirstSuccessStrategy WithKeywords(
        string[] requiredKeywords,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase,
        CollaborationOptions? options = null)
    {
        return new FirstSuccessStrategy(
            result => requiredKeywords.All(keyword =>
                result.Output?.Contains(keyword, comparison) ?? false),
            options);
    }

    public Task<CollaborationResult> AggregateAsync(
        IReadOnlyList<PipelineStepResult> results,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        // Filter to successful results only
        var successfulResults = results
            .Where(r => r.Success)
            .OrderBy(r => r.ExecutionTime) // Fastest first
            .ToList();

        if (successfulResults.Count == 0)
        {
            throw new InvalidOperationException(
                "No successful results available for FirstSuccess strategy");
        }

        // Find first valid result
        PipelineStepResult? firstValid = null;

        if (_validationFunction != null)
        {
            firstValid = successfulResults.FirstOrDefault(_validationFunction);

            if (firstValid == null)
            {
                throw new InvalidOperationException(
                    "No results passed validation for FirstSuccess strategy");
            }
        }
        else
        {
            firstValid = successfulResults.First();
        }

        var collaborationResult = new CollaborationResult
        {
            Output = firstValid.Output,
            Strategy = Name,
            ResultCount = results.Count,
            ConfidenceScore = CalculateConfidence(firstValid, results)
        };

        if (_options.IncludeIndividualResults)
        {
            collaborationResult.IndividualResults = results.ToList();
        }

        if (_options.CollectMetadata)
        {
            collaborationResult.Metadata["selectedAgentName"] = firstValid.AgentName;
            collaborationResult.Metadata["executionTime"] = firstValid.ExecutionTime.TotalMilliseconds;
            collaborationResult.Metadata["totalResults"] = results.Count;
            collaborationResult.Metadata["successfulResults"] = successfulResults.Count;
            collaborationResult.Metadata["fastestTime"] = successfulResults.Min(r => r.ExecutionTime.TotalMilliseconds);
            collaborationResult.Metadata["slowestTime"] = successfulResults.Max(r => r.ExecutionTime.TotalMilliseconds);

            // Ranking of all results by speed
            collaborationResult.Metadata["speedRanking"] = successfulResults
                .Select((r, index) => new
                {
                    Rank = index + 1,
                    AgentName = r.AgentName,
                    Time = r.ExecutionTime.TotalMilliseconds
                })
                .ToList();
        }

        return Task.FromResult(collaborationResult);
    }

    private double CalculateConfidence(PipelineStepResult selected, IReadOnlyList<PipelineStepResult> allResults)
    {
        // Confidence based on how much faster the selected result was compared to others
        var successfulResults = allResults.Where(r => r.Success).ToList();

        if (successfulResults.Count == 1)
        {
            return 1.0; // Only one result, full confidence
        }

        var selectedTime = selected.ExecutionTime.TotalMilliseconds;
        var avgTime = successfulResults.Average(r => r.ExecutionTime.TotalMilliseconds);

        if (avgTime == 0)
        {
            return 1.0;
        }

        // Faster than average = higher confidence
        var speedRatio = selectedTime / avgTime;
        return Math.Max(0.5, Math.Min(1.0, 2.0 - speedRatio));
    }
}
