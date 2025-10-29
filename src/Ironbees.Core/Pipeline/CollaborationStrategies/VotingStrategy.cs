namespace Ironbees.Core.Pipeline.CollaborationStrategies;

/// <summary>
/// Selects the most common result through voting (majority consensus)
/// </summary>
public class VotingStrategy : ICollaborationStrategy
{
    private readonly Func<string, string, bool>? _similarityComparer;
    private readonly CollaborationOptions _options;
    private readonly double _similarityThreshold;

    public string Name => "Voting";

    /// <summary>
    /// Create a voting strategy with exact string comparison
    /// </summary>
    public VotingStrategy(CollaborationOptions? options = null)
        : this(null, 1.0, options)
    {
    }

    /// <summary>
    /// Create a voting strategy with custom similarity comparison
    /// </summary>
    /// <param name="similarityComparer">Function to compare two outputs for similarity</param>
    /// <param name="similarityThreshold">Threshold for considering outputs similar (0.0 to 1.0)</param>
    /// <param name="options">Collaboration options</param>
    public VotingStrategy(
        Func<string, string, bool>? similarityComparer,
        double similarityThreshold = 0.8,
        CollaborationOptions? options = null)
    {
        _similarityComparer = similarityComparer;
        _similarityThreshold = similarityThreshold;
        _options = options ?? new CollaborationOptions();
    }

    /// <summary>
    /// Create a voting strategy with fuzzy string matching
    /// </summary>
    public static VotingStrategy WithFuzzyMatching(
        double similarityThreshold = 0.8,
        CollaborationOptions? options = null)
    {
        return new VotingStrategy(
            (output1, output2) => CalculateLevenshteinSimilarity(output1, output2) >= similarityThreshold,
            similarityThreshold,
            options);
    }

    /// <summary>
    /// Create a voting strategy that normalizes outputs before comparison
    /// </summary>
    public static VotingStrategy WithNormalization(
        CollaborationOptions? options = null)
    {
        return new VotingStrategy(
            (output1, output2) => NormalizeOutput(output1) == NormalizeOutput(output2),
            1.0,
            options);
    }

    public Task<CollaborationResult> AggregateAsync(
        IReadOnlyList<PipelineStepResult> results,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        // Filter results
        var filteredResults = FilterResults(results);

        if (filteredResults.Count < _options.MinimumResults)
        {
            throw new InvalidOperationException(
                $"Insufficient results for voting. Required: {_options.MinimumResults}, Got: {filteredResults.Count}");
        }

        // Group similar outputs and count votes
        var voteClusters = GroupSimilarOutputs(filteredResults);

        // Find the cluster with most votes
        var winningCluster = voteClusters
            .OrderByDescending(c => c.Count)
            .First();

        var totalVotes = filteredResults.Count;
        var winningVotes = winningCluster.Count;
        var votePercentage = (double)winningVotes / totalVotes;

        var collaborationResult = new CollaborationResult
        {
            Output = winningCluster.RepresentativeOutput,
            Strategy = Name,
            ResultCount = filteredResults.Count,
            ConfidenceScore = votePercentage
        };

        if (_options.IncludeIndividualResults)
        {
            collaborationResult.IndividualResults = filteredResults;
        }

        if (_options.CollectMetadata)
        {
            collaborationResult.Metadata["winningVotes"] = winningVotes;
            collaborationResult.Metadata["totalVotes"] = totalVotes;
            collaborationResult.Metadata["votePercentage"] = votePercentage;
            collaborationResult.Metadata["clusters"] = voteClusters.Count;
            collaborationResult.Metadata["voteDistribution"] = voteClusters
                .OrderByDescending(c => c.Count)
                .Select(c => new { Votes = c.Count, Output = c.RepresentativeOutput.Substring(0, Math.Min(50, c.RepresentativeOutput.Length)) })
                .ToList();
        }

        return Task.FromResult(collaborationResult);
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

    private List<VoteCluster> GroupSimilarOutputs(List<PipelineStepResult> results)
    {
        var clusters = new List<VoteCluster>();

        foreach (var result in results)
        {
            var output = result.Output;
            var matchedCluster = false;

            // Try to find existing cluster with similar output
            foreach (var cluster in clusters)
            {
                if (AreSimilar(output, cluster.RepresentativeOutput))
                {
                    cluster.Results.Add(result);
                    cluster.Count++;
                    matchedCluster = true;
                    break;
                }
            }

            // Create new cluster if no match found
            if (!matchedCluster)
            {
                clusters.Add(new VoteCluster
                {
                    RepresentativeOutput = output,
                    Results = new List<PipelineStepResult> { result },
                    Count = 1
                });
            }
        }

        return clusters;
    }

    private bool AreSimilar(string output1, string output2)
    {
        if (_similarityComparer != null)
        {
            return _similarityComparer(output1, output2);
        }

        // Default: exact string comparison
        return string.Equals(output1, output2, StringComparison.Ordinal);
    }

    private static string NormalizeOutput(string output)
    {
        return output
            .Trim()
            .ToLowerInvariant()
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }

    private static double CalculateLevenshteinSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0.0;

        var maxLength = Math.Max(s1.Length, s2.Length);
        var distance = LevenshteinDistance(s1, s2);
        return 1.0 - ((double)distance / maxLength);
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    private class VoteCluster
    {
        public required string RepresentativeOutput { get; set; }
        public required List<PipelineStepResult> Results { get; set; }
        public int Count { get; set; }
    }
}
