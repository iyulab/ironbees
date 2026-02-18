namespace Ironbees.Core;

/// <summary>
/// Calculates TF-IDF (Term Frequency-Inverse Document Frequency) weights for agent selection
/// </summary>
public class TfidfWeightCalculator
{
    private static readonly char[] WordSeparators = [' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '-', '_'];

    private readonly Dictionary<string, double> _idfScores;
    private readonly int _totalDocuments;

    /// <summary>
    /// Creates a new TF-IDF calculator from a collection of agents
    /// </summary>
    /// <param name="agents">Collection of agents to build IDF scores from</param>
    public TfidfWeightCalculator(IReadOnlyCollection<IAgent> agents)
    {
        _totalDocuments = agents.Count;
        _idfScores = CalculateIDF(agents);
    }

    /// <summary>
    /// Calculates TF-IDF weighted score for query words against document text
    /// </summary>
    /// <param name="queryWords">Set of words from user query</param>
    /// <param name="documentText">Text to score against (agent metadata)</param>
    /// <returns>TF-IDF weighted score (0.0 to 1.0)</returns>
    public double CalculateTfidfScore(HashSet<string> queryWords, string documentText)
    {
        if (queryWords.Count == 0 || string.IsNullOrWhiteSpace(documentText))
        {
            return 0.0;
        }

        var documentWords = documentText
            .ToLowerInvariant()
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (documentWords.Count == 0)
        {
            return 0.0;
        }

        var totalScore = 0.0;
        var maxPossibleScore = 0.0;

        foreach (var word in queryWords)
        {
            // Calculate TF (Term Frequency) in document
            var termCount = documentWords.Count(w => w.Equals(word, StringComparison.OrdinalIgnoreCase));
            var tf = (double)termCount / documentWords.Count;

            // Get IDF (Inverse Document Frequency) from pre-calculated scores
            var idf = _idfScores.GetValueOrDefault(word, 0.0);

            // TF-IDF score for this term
            var tfidf = tf * idf;
            totalScore += tfidf;

            // Max possible score (assuming perfect match)
            maxPossibleScore += idf;
        }

        // Normalize to 0-1 range
        return maxPossibleScore > 0 ? totalScore / maxPossibleScore : 0.0;
    }

    /// <summary>
    /// Gets the IDF score for a specific word
    /// </summary>
    /// <param name="word">Word to get IDF score for</param>
    /// <returns>IDF score, or 0 if word not found</returns>
    public double GetIdfScore(string word)
    {
        return _idfScores.GetValueOrDefault(word.ToLowerInvariant(), 0.0);
    }

    private Dictionary<string, double> CalculateIDF(IReadOnlyCollection<IAgent> agents)
    {
        if (agents.Count == 0)
        {
            return new Dictionary<string, double>();
        }

        // Count document frequency for each word
        var documentFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var agent in agents)
        {
            var documentText = GetAgentDocumentText(agent);
            var words = documentText
                .ToLowerInvariant()
                .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToHashSet();

            foreach (var word in words)
            {
                documentFrequency[word] = documentFrequency.GetValueOrDefault(word, 0) + 1;
            }
        }

        // Calculate IDF scores
        var idfScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (word, docCount) in documentFrequency)
        {
            // IDF = log(total documents / documents containing term)
            // Add 1 to avoid division by zero
            idfScores[word] = Math.Log((double)_totalDocuments / (docCount + 1));
        }

        return idfScores;
    }

    private static string GetAgentDocumentText(IAgent agent)
    {
        var config = agent.Config;
        var parts = new List<string>
        {
            config.Name,
            config.Description
        };

        parts.AddRange(config.Capabilities);
        parts.AddRange(config.Tags);

        return string.Join(" ", parts);
    }
}
