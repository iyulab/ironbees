namespace Ironbees.Core.Pipeline.CollaborationStrategies;

/// <summary>
/// Combines multiple agent results into a single synthesized output
/// </summary>
public class EnsembleStrategy : ICollaborationStrategy
{
    private readonly Func<IReadOnlyList<PipelineStepResult>, Task<string>> _combinerFunction;
    private readonly CollaborationOptions _options;

    public string Name => "Ensemble";

    /// <summary>
    /// Create an ensemble strategy with custom combiner function
    /// </summary>
    /// <param name="combinerFunction">Async function to combine multiple results into one</param>
    /// <param name="options">Collaboration options</param>
    public EnsembleStrategy(
        Func<IReadOnlyList<PipelineStepResult>, Task<string>> combinerFunction,
        CollaborationOptions? options = null)
    {
        _combinerFunction = combinerFunction ?? throw new ArgumentNullException(nameof(combinerFunction));
        _options = options ?? new CollaborationOptions();
    }

    /// <summary>
    /// Create an ensemble strategy that concatenates all results
    /// </summary>
    public static EnsembleStrategy Concatenate(
        string separator = "\n\n---\n\n",
        CollaborationOptions? options = null)
    {
        return new EnsembleStrategy(
            results => Task.FromResult(string.Join(separator,
                results.Select(r => $"[{r.AgentName}]\n{r.Output}"))),
            options);
    }

    /// <summary>
    /// Create an ensemble strategy that uses an agent to synthesize results
    /// </summary>
    /// <param name="orchestrator">Orchestrator for running synthesis agent</param>
    /// <param name="synthesizerAgentName">Name of agent to use for synthesis</param>
    /// <param name="options">Collaboration options</param>
    public static EnsembleStrategy WithSynthesisAgent(
        IAgentOrchestrator orchestrator,
        string synthesizerAgentName,
        CollaborationOptions? options = null)
    {
        return new EnsembleStrategy(
            async results =>
            {
                var combinedInput = string.Join("\n\n---\n\n",
                    results.Select((r, i) => $"Result {i + 1} from {r.AgentName}:\n{r.Output}"));

                var synthesisPrompt = $@"Please synthesize the following {results.Count} results into a single, coherent response:

{combinedInput}

Provide a unified response that captures the best insights from all results.";

                return await orchestrator.ProcessAsync(
                    synthesisPrompt,
                    synthesizerAgentName);
            },
            options);
    }

    /// <summary>
    /// Create an ensemble strategy that extracts sections and merges them
    /// </summary>
    public static EnsembleStrategy MergeSections(
        Dictionary<string, Func<string, string>> sectionExtractors,
        CollaborationOptions? options = null)
    {
        return new EnsembleStrategy(
            results =>
            {
                var mergedSections = new Dictionary<string, List<string>>();

                // Extract sections from each result
                foreach (var result in results)
                {
                    foreach (var (sectionName, extractor) in sectionExtractors)
                    {
                        var extracted = extractor(result.Output);
                        if (!string.IsNullOrWhiteSpace(extracted))
                        {
                            if (!mergedSections.ContainsKey(sectionName))
                            {
                                mergedSections[sectionName] = new List<string>();
                            }
                            mergedSections[sectionName].Add(extracted);
                        }
                    }
                }

                // Combine sections
                var output = string.Join("\n\n", mergedSections.Select(kvp =>
                {
                    var sectionContent = string.Join("\n", kvp.Value.Distinct());
                    return $"## {kvp.Key}\n{sectionContent}";
                }));

                return Task.FromResult(output);
            },
            options);
    }

    public async Task<CollaborationResult> AggregateAsync(
        IReadOnlyList<PipelineStepResult> results,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        // Filter results
        var filteredResults = FilterResults(results);

        if (filteredResults.Count < _options.MinimumResults)
        {
            throw new InvalidOperationException(
                $"Insufficient results for ensemble. Required: {_options.MinimumResults}, Got: {filteredResults.Count}");
        }

        // Apply maximum results limit
        if (_options.MaximumResults.HasValue && filteredResults.Count > _options.MaximumResults.Value)
        {
            // Use ranker if available, otherwise take first N
            if (_options.ResultRanker != null)
            {
                filteredResults = filteredResults
                    .OrderByDescending(_options.ResultRanker)
                    .Take(_options.MaximumResults.Value)
                    .ToList();
            }
            else
            {
                filteredResults = filteredResults.Take(_options.MaximumResults.Value).ToList();
            }
        }

        // Combine results using the combiner function
        string combinedOutput;
        try
        {
            using var cts = _options.AggregationTimeout.HasValue
                ? new CancellationTokenSource(_options.AggregationTimeout.Value)
                : new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, cts.Token);

            combinedOutput = await _combinerFunction(filteredResults);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Ensemble aggregation timed out after {_options.AggregationTimeout?.TotalSeconds ?? 0}s");
        }

        var collaborationResult = new CollaborationResult
        {
            Output = combinedOutput,
            Strategy = Name,
            ResultCount = filteredResults.Count,
            ConfidenceScore = CalculateEnsembleConfidence(filteredResults)
        };

        if (_options.IncludeIndividualResults)
        {
            collaborationResult.IndividualResults = filteredResults;
        }

        if (_options.CollectMetadata)
        {
            collaborationResult.Metadata["agentsInvolved"] = filteredResults
                .Select(r => r.AgentName)
                .Distinct()
                .ToList();
            collaborationResult.Metadata["totalInputLength"] = filteredResults.Sum(r => r.Output.Length);
            collaborationResult.Metadata["averageInputLength"] = filteredResults.Average(r => r.Output.Length);
        }

        return collaborationResult;
    }

    private List<PipelineStepResult> FilterResults(IReadOnlyList<PipelineStepResult> results)
    {
        var filtered = results.AsEnumerable();

        if (!_options.IncludeFailedResults)
        {
            filtered = filtered.Where(r => r.Success);
        }

        if (_options.ResultFilter != null)
        {
            filtered = filtered.Where(_options.ResultFilter);
        }

        if (_options.MinimumConfidenceThreshold.HasValue)
        {
            filtered = filtered.Where(r =>
            {
                if (r.Metadata.TryGetValue("confidence", out var confidence))
                {
                    return (confidence as double? ?? 0.0) >= _options.MinimumConfidenceThreshold.Value;
                }
                return false;
            });
        }

        return filtered.ToList();
    }

    private double CalculateEnsembleConfidence(List<PipelineStepResult> results)
    {
        // Confidence increases with diversity and number of results
        var uniqueAgents = results.Select(r => r.AgentName).Distinct().Count();
        var diversityScore = (double)uniqueAgents / results.Count;

        // Base confidence from number of results (more is better, diminishing returns)
        var countScore = Math.Min(1.0, results.Count / 5.0);

        return (diversityScore + countScore) / 2.0;
    }
}
