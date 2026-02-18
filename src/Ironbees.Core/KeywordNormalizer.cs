namespace Ironbees.Core;

/// <summary>
/// Normalizes keywords using synonyms and basic stemming for improved matching
/// </summary>
public class KeywordNormalizer
{
    private readonly Dictionary<string, string> _synonymMap;
    private readonly Dictionary<string, string> _stemmingMap;

    /// <summary>
    /// Creates a new keyword normalizer with default mappings
    /// </summary>
    public KeywordNormalizer()
    {
        _synonymMap = BuildSynonymMap();
        _stemmingMap = BuildStemmingMap();
    }

    /// <summary>
    /// Normalizes a word by applying synonym mapping and stemming
    /// </summary>
    /// <param name="word">Word to normalize</param>
    /// <returns>Normalized word</returns>
    public string Normalize(string word)
    {
        var normalized = word.ToLowerInvariant();

        // Apply synonym mapping first
        if (_synonymMap.TryGetValue(normalized, out var synonym))
        {
            normalized = synonym;
        }

        // Apply stemming
        if (_stemmingMap.TryGetValue(normalized, out var stem))
        {
            normalized = stem;
        }

        return normalized;
    }

    /// <summary>
    /// Normalizes a collection of words
    /// </summary>
    /// <param name="words">Words to normalize</param>
    /// <returns>Normalized words as a set</returns>
    public HashSet<string> NormalizeWords(IEnumerable<string> words)
    {
        return words.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> BuildSynonymMap()
    {
        var synonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Programming synonyms
        AddSynonymGroup(synonyms, "code", "coding", "programming", "program", "script", "scripting");
        AddSynonymGroup(synonyms, "develop", "development", "developer", "dev");
        AddSynonymGroup(synonyms, "debug", "debugging", "debugger", "troubleshoot", "troubleshooting");
        AddSynonymGroup(synonyms, "test", "testing", "tester", "qa", "quality");
        AddSynonymGroup(synonyms, "build", "building", "compile", "compiling", "compilation");
        AddSynonymGroup(synonyms, "deploy", "deployment", "deploying", "release", "releasing");
        AddSynonymGroup(synonyms, "write", "writing", "create", "creating", "generate", "generating");
        AddSynonymGroup(synonyms, "fix", "fixing", "repair", "repairing", "resolve", "resolving");
        AddSynonymGroup(synonyms, "optimize", "optimizing", "optimization", "improve", "improving");
        AddSynonymGroup(synonyms, "analyze", "analyzing", "analysis", "examine", "examining");
        AddSynonymGroup(synonyms, "review", "reviewing", "check", "checking", "inspect", "inspecting");
        AddSynonymGroup(synonyms, "refactor", "refactoring", "restructure", "restructuring");

        // .NET synonyms
        AddSynonymGroup(synonyms, "csharp", "c#", "cs");
        AddSynonymGroup(synonyms, "dotnet", ".net", "net");
        AddSynonymGroup(synonyms, "aspnet", "asp.net", "asp");

        // Concept synonyms
        AddSynonymGroup(synonyms, "api", "endpoint", "webservice");
        AddSynonymGroup(synonyms, "database", "db", "datastore");
        AddSynonymGroup(synonyms, "authentication", "auth", "login", "signin");
        AddSynonymGroup(synonyms, "authorization", "authz", "permission");
        AddSynonymGroup(synonyms, "documentation", "doc", "docs", "readme");
        AddSynonymGroup(synonyms, "configuration", "config", "setting", "settings");
        AddSynonymGroup(synonyms, "function", "method", "procedure", "routine");
        AddSynonymGroup(synonyms, "class", "type", "object");
        AddSynonymGroup(synonyms, "interface", "contract", "abstraction");
        AddSynonymGroup(synonyms, "security", "secure", "encryption", "encrypt");
        AddSynonymGroup(synonyms, "mobile", "app", "application");
        AddSynonymGroup(synonyms, "data", "analysis", "analytics");

        return synonyms;
    }

    private static Dictionary<string, string> BuildStemmingMap()
    {
        var stemming = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Basic stemming rules for common programming terms
        // Format: inflected form -> base form

        // -ing forms
        stemming["coding"] = "code";
        stemming["programming"] = "program";
        stemming["developing"] = "develop";
        stemming["testing"] = "test";
        stemming["debugging"] = "debug";
        stemming["building"] = "build";
        stemming["deploying"] = "deploy";
        stemming["writing"] = "write";
        stemming["creating"] = "create";
        stemming["generating"] = "generate";
        stemming["fixing"] = "fix";
        stemming["optimizing"] = "optimize";
        stemming["analyzing"] = "analyze";
        stemming["reviewing"] = "review";
        stemming["refactoring"] = "refactor";

        // -er forms
        stemming["developer"] = "develop";
        stemming["tester"] = "test";
        stemming["debugger"] = "debug";
        stemming["builder"] = "build";
        stemming["analyzer"] = "analyze";
        stemming["reviewer"] = "review";

        // -ed forms
        stemming["coded"] = "code";
        stemming["programmed"] = "program";
        stemming["developed"] = "develop";
        stemming["tested"] = "test";
        stemming["debugged"] = "debug";
        stemming["built"] = "build";
        stemming["deployed"] = "deploy";
        stemming["created"] = "create";
        stemming["generated"] = "generate";
        stemming["fixed"] = "fix";
        stemming["optimized"] = "optimize";
        stemming["analyzed"] = "analyze";
        stemming["reviewed"] = "review";
        stemming["refactored"] = "refactor";

        // -s forms (plural/third person)
        stemming["codes"] = "code";
        stemming["programs"] = "program";
        stemming["develops"] = "develop";
        stemming["tests"] = "test";
        stemming["builds"] = "build";
        stemming["deploys"] = "deploy";
        stemming["writes"] = "write";
        stemming["creates"] = "create";
        stemming["generates"] = "generate";
        stemming["fixes"] = "fix";
        stemming["optimizes"] = "optimize";
        stemming["analyzes"] = "analyze";
        stemming["reviews"] = "review";
        stemming["apis"] = "api";
        stemming["databases"] = "database";
        stemming["functions"] = "function";
        stemming["methods"] = "method";
        stemming["classes"] = "class";

        // -ment forms
        stemming["development"] = "develop";
        stemming["deployment"] = "deploy";
        stemming["improvement"] = "improve";

        // -tion forms
        stemming["optimization"] = "optimize";
        stemming["configuration"] = "config";
        stemming["authentication"] = "auth";
        stemming["authorization"] = "authz";
        stemming["documentation"] = "document";
        stemming["implementation"] = "implement";
        stemming["compilation"] = "compile";

        // -ation forms
        stemming["refactoration"] = "refactor";

        return stemming;
    }

    private static void AddSynonymGroup(Dictionary<string, string> map, string canonical, params string[] synonyms)
    {
        // Map all synonyms to the canonical form
        foreach (var synonym in synonyms)
        {
            map[synonym] = canonical;
        }
    }
}
