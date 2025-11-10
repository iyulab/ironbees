namespace Ironbees.Core;

/// <summary>
/// Provides stopwords (common words to ignore) for keyword extraction
/// </summary>
public static class StopwordsProvider
{
    /// <summary>
    /// Gets the default set of stopwords including general English and .NET-specific terms
    /// </summary>
    public static HashSet<string> GetDefaultStopwords()
    {
        var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Common English stopwords
        stopwords.UnionWith(GetEnglishStopwords());

        // .NET and programming common words (should NOT be filtered)
        // These are intentionally excluded from stopwords as they're meaningful in agent context
        // Examples: "code", "test", "debug", "api", "database", "web", etc.

        return stopwords;
    }

    /// <summary>
    /// Gets basic English stopwords
    /// </summary>
    public static HashSet<string> GetEnglishStopwords()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Articles
            "a", "an", "the",

            // Conjunctions
            "and", "or", "but", "nor", "yet", "so",

            // Prepositions
            "in", "on", "at", "to", "for", "of", "with", "from", "about",
            "into", "through", "during", "before", "after", "above", "below",
            "between", "under", "over", "behind", "beside",

            // Pronouns
            "i", "you", "he", "she", "it", "we", "they",
            "me", "him", "her", "us", "them",
            "my", "your", "his", "its", "our", "their",
            "mine", "yours", "hers", "ours", "theirs",
            "this", "that", "these", "those",

            // Verbs (common auxiliary/modal)
            "is", "are", "was", "were", "am",
            "be", "been", "being",
            "have", "has", "had", "having",
            "do", "does", "did", "doing",
            "will", "would", "should", "could", "can", "may", "might", "must",
            "shall",

            // Adverbs (common)
            "not", "very", "too", "also", "just", "only",

            // Questions
            "what", "when", "where", "who", "which", "why", "how",

            // Other common words
            "as", "by", "if", "than", "then", "now",
            "some", "any", "all", "each", "every",
            "more", "most", "less", "least",
            "such", "own", "same", "other",
            "here", "there", "whether"
        };
    }

    /// <summary>
    /// Gets .NET and programming-related terms that should be preserved (NOT stopwords)
    /// Use this to validate that meaningful technical terms are not being filtered
    /// </summary>
    public static HashSet<string> GetTechnicalTermsToPreserve()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Programming concepts
            "code", "coding", "program", "programming", "develop", "development",
            "software", "application", "app", "system",

            // .NET specific
            "dotnet", "csharp", "net", "core", "framework", "asp", "aspnet",
            "blazor", "maui", "xamarin", "entity", "linq", "nuget",

            // Languages
            "python", "java", "javascript", "typescript", "ruby", "php",
            "cpp", "rust", "golang", "kotlin", "swift",

            // Activities
            "debug", "debugging", "test", "testing", "build", "compile",
            "deploy", "deployment", "refactor", "refactoring",
            "review", "analyze", "optimize", "fix",

            // Concepts
            "api", "rest", "graphql", "database", "sql", "nosql",
            "web", "mobile", "desktop", "cloud", "azure", "aws",
            "security", "authentication", "authorization",
            "performance", "scalability", "architecture",

            // File types
            "json", "xml", "yaml", "csv", "config", "configuration",

            // Tools/Frameworks
            "git", "docker", "kubernetes", "jenkins", "react", "vue", "angular"
        };
    }
}
