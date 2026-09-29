using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ironbees.Core.Tests;

/// <summary>
/// Compiles every <c>```csharp</c> block in README.md against the current assemblies. <see cref="DocsSnippetRosterTests"/>
/// checks that a documented method name exists on some type and that an initializer's properties exist; a compiler also
/// checks the receiver, the arguments, the return types a block goes on to use, and the namespaces it needs.
/// </summary>
/// <remarks>
/// A block is compiled as a top-level program: its <c>using</c> lines are hoisted, the implicit usings of a console
/// project are added, and the stand-ins below are declared when the block uses the name without declaring it — values a
/// reader already has from the surrounding text (an API key, the service provider, the user's query), not part of what
/// the block shows. No library namespace is assumed: every block states the usings it needs, so a block copied on its
/// own compiles.
/// </remarks>
public class ReadmeSnippetCompileTests
{
    // A block that is deliberately not a program (a signature sketch, pseudocode) is listed here by the heading it sits
    // under, with the reason. Shrink this, never grow it silently.
    private static readonly Dictionary<string, string> Fragments = new(StringComparer.Ordinal);

    // The implicit usings of an SDK console project (ImplicitUsings=enable), nothing more.
    private const string CommonUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Net.Http;
        using System.Threading;
        using System.Threading.Tasks;
        """;

    private static readonly (string Name, string Declaration)[] StandIns =
    [
        ("services", "Microsoft.Extensions.DependencyInjection.IServiceCollection services = null!;"),
        ("serviceProvider", "System.IServiceProvider serviceProvider = null!;"),
        ("builder", "Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = null!;"),
        ("apiKey", "string apiKey = \"\";"),
        ("query", "string query = \"\";"),
        ("sessionId", "string sessionId = \"\";"),
        ("input", "string input = \"\";"),
        ("goalId", "string goalId = \"\";"),
        ("executionId", "string executionId = \"\";"),
        ("agentConfigs", "System.Collections.Generic.IReadOnlyList<Ironbees.Core.AgentConfig> agentConfigs = [];"),
        ("innerClient", "Microsoft.Extensions.AI.IChatClient innerClient = null!;"),
        ("mySearchCodeTool", "IronHive.Abstractions.Tools.ITool mySearchCodeTool = null!;"),
        ("myRunTestsTool", "IronHive.Abstractions.Tools.ITool myRunTestsTool = null!;"),
        ("executor", "Ironbees.Autonomous.Abstractions.ITaskExecutor<MyRequest, MyResult> executor = null!;"),
        ("oracle", "Ironbees.Autonomous.Abstractions.IOracleVerifier oracle = null!;"),
    ];

    private static readonly Dictionary<string, (string Name, string Declaration)[]> DocumentStandIns = new(StringComparer.Ordinal);

    private static readonly string[] AssembliesToLoad =
    [
        "Ironbees.Core", "Ironbees.AgentFramework", "Ironbees.AgentMode", "Ironbees.Autonomous", "Ironbees.Ironhive",
        "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.AI", "Microsoft.Extensions.AI.Abstractions", "TokenMeter",
        "IronHive.Abstractions", "IronHive.Core", "IronHive.Providers.OpenAI", "IronHive.Providers.OpenAI.Compatible",
        "Microsoft.AspNetCore",
    ];

    public static TheoryData<string> Blocks()
    {
        var data = new TheoryData<string>();
        foreach (var block in ReadBlocks())
            data.Add(block.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var block = ReadBlocks().Single(b => b.Key == key);
        if (Fragments.ContainsKey(block.Heading))
            return;

        var errors = Compile(block.Code, block.Document);

        Assert.True(errors.IsEmpty,
            $"README block {key} does not compile against the current API:\n" +
            string.Join("\n", errors.Select(e => e.ToString())) + "\n--- source ---\n" + Program(block.Code, block.Document));
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings()
    {
        var blocks = ReadBlocks();
        Assert.True(blocks.Count >= 11, $"expected the README's C# blocks, found {blocks.Count}");
        Assert.All(Fragments.Keys, heading => Assert.Contains(blocks, b => b.Heading == heading));
    }

    /// <summary>Positive control: the compiler rejects a call the library does not have.</summary>
    [Fact]
    public void Compile_RejectsAMethodTheLibraryDoesNotHave()
    {
        var errors = Compile("""
            using Ironbees.Ironhive;
            services.AddIronbeesThatDoesNotExist();
            """);

        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// Positive control for "no library namespace is assumed": the same line compiles with its using and fails without it.
    /// </summary>
    [Fact]
    public void Compile_RejectsALibraryTypeWithoutItsUsing()
    {
        Assert.Empty(Compile("""
            using Ironbees.Core;
            var options = new ProcessOptions { AgentName = "a" };
            """));
        Assert.NotEmpty(Compile("""
            var options = new ProcessOptions { AgentName = "a" };
            """));
    }

    private sealed record Block(string Key, string Document, string Heading, string Code);

    // The repository README and every package README: the package READMEs are what nuget.org shows each package's readers.
    private static IEnumerable<string> Documents()
    {
        var root = RepoRoot();
        yield return Path.Combine(root, "README.md");
        foreach (var readme in Directory.GetDirectories(Path.Combine(root, "src")).Order(StringComparer.Ordinal)
                     .Select(d => Path.Combine(d, "README.md")).Where(File.Exists))
            yield return readme;
    }

    private static List<Block> ReadBlocks()
    {
        var root = RepoRoot();
        return Documents().SelectMany(path => ReadBlocks(path, Path.GetRelativePath(root, path).Replace('\\', '/'))).ToList();
    }

    private static List<Block> ReadBlocks(string path, string document)
    {
        var lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var blocks = new List<Block>();
        var heading = "(top)";
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
                heading = lines[i].TrimStart('#').Trim();
            if (lines[i].Trim() != "```csharp")
                continue;

            var start = i + 1;
            var code = new StringBuilder();
            for (i++; i < lines.Length && lines[i].Trim() != "```"; i++)
                code.AppendLine(lines[i]);
            blocks.Add(new Block($"{document} line {start}: {heading}", document, heading, code.ToString()));
        }

        return blocks;
    }

    private static string Program(string code, string document = "README.md")
    {
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        // "using X;   // what it is for" is a directive too: the README annotates its usings.
        static string Code(string l) => l.Split("//", 2)[0].TrimEnd();
        bool IsUsingDirective(string l) =>
            l.StartsWith("using ", StringComparison.Ordinal) && Code(l).EndsWith(';') && !l.StartsWith("using var ", StringComparison.Ordinal);

        var body = string.Join("\n", lines.Where(l => !IsUsingDirective(l)));
        var standIns = StandIns.Concat(DocumentStandIns.GetValueOrDefault(document, []))
            .Where(s => Regex.IsMatch(body, $@"\b{s.Name}\b")
                        && !Regex.IsMatch(body, $@"\b(var|[A-Z][\w<>?,\s]*)\s+{s.Name}\s*[=;]"))
            .Select(s => s.Declaration);

        return string.Join("\n", lines.Where(IsUsingDirective)) + "\n" + CommonUsings + "\n"
               + string.Join("\n", PackageNamespaces(document).Select(n => $"using {n};")) + "\n"
               + string.Join("\n", standIns) + "\n" + body;
    }

    // A package README is read with that package's root namespace in scope (Ironbees.Core for Ironbees.Core):
    // the namespaces of its public types with the fewest segments. The repository README assumes none.
    private static IEnumerable<string> PackageNamespaces(string document)
    {
        var match = Regex.Match(document, @"^src/(?<package>[^/]+)/README\.md$");
        if (!match.Success)
            return [];

        var namespaces = Assembly.Load(match.Groups["package"].Value).GetExportedTypes()
            .Select(t => t.Namespace).OfType<string>().Distinct().ToList();
        var fewest = namespaces.Min(n => n.Count(c => c == '.'));
        return namespaces.Where(n => n.Count(c => c == '.') == fewest);
    }

    private static ImmutableArray<Diagnostic> Compile(string code, string document = "README.md")
    {
        var tree = CSharpSyntaxTree.ParseText(Program(code, document), new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "ReadmeSnippet", [tree], References(),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
        return compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
    }

    private static List<MetadataReference> References()
    {
        foreach (var name in AssembliesToLoad)
            Assembly.Load(name);

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
            paths.UnionWith(trusted.Split(Path.PathSeparator).Where(p => p.Length > 0));
        paths.UnionWith(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.Location.Length > 0)
            .Select(a => a.Location));
        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironbees.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Ironbees.slnx not found above the test output directory");
    }
}
