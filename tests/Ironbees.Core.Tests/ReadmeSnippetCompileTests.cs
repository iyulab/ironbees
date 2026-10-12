using System.Reflection;
using System.Text.RegularExpressions;
using Iyu.Conventions.Testing.Snippets;
using Xunit;

namespace Ironbees.Core.Tests;

/// <summary>
/// Compiles every C# block in the README(s) against the current assemblies. A compiler checks the receiver, the
/// arguments, the return types a block goes on to use, and the namespaces it needs.
/// </summary>
/// <remarks>
/// Each block is compiled as a top-level program by <see cref="SnippetCompiler"/>: its own usings, the implicit usings, and
/// the stand-ins below when the block uses the name without declaring it — values a reader already has from the
/// surrounding text, not part of what the block shows. No library namespace is assumed in the repository README: every
/// block states the usings it needs, so a block copied on its own compiles. A package README is read with that package's
/// root namespace in scope.
/// </remarks>
public partial class ReadmeSnippetCompileTests
{
    // A block that is deliberately not a program (a signature sketch, pseudocode), by the heading it sits under, with the
    // reason. Shrink this, never grow it silently.
    private static readonly SnippetRoster Fragments = new(new Dictionary<string, string>(StringComparer.Ordinal));

    // The implicit usings of an SDK console project (ImplicitUsings=enable), nothing more.
    private static readonly string[] CommonUsings =
        ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks"];

    private static readonly Dictionary<string, string> StandIns = new(StringComparer.Ordinal)
    {
        ["services"] = "Microsoft.Extensions.DependencyInjection.IServiceCollection services = null!;",
        ["serviceProvider"] = "System.IServiceProvider serviceProvider = null!;",
        ["builder"] = "Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = null!;",
        ["apiKey"] = "string apiKey = \"\";",
        ["query"] = "string query = \"\";",
        ["sessionId"] = "string sessionId = \"\";",
        ["input"] = "string input = \"\";",
        ["goalId"] = "string goalId = \"\";",
        ["executionId"] = "string executionId = \"\";",
        ["agentConfigs"] = "System.Collections.Generic.IReadOnlyList<Ironbees.Core.AgentConfig> agentConfigs = [];",
        ["innerClient"] = "Microsoft.Extensions.AI.IChatClient innerClient = null!;",
        ["mySearchCodeTool"] = "IronHive.Abstractions.Tools.ITool mySearchCodeTool = null!;",
        ["myRunTestsTool"] = "IronHive.Abstractions.Tools.ITool myRunTestsTool = null!;",
        ["executor"] = "Ironbees.Autonomous.Abstractions.ITaskExecutor<MyRequest, MyResult> executor = null!;",
        ["oracle"] = "Ironbees.Autonomous.Abstractions.IOracleVerifier oracle = null!;",
    };

    private static readonly IReadOnlyList<Microsoft.CodeAnalysis.MetadataReference> References = SnippetCompiler.ReferencesFor(
        [.. new[]
        {
            "Ironbees.Core", "Ironbees.AgentFramework", "Ironbees.AgentMode", "Ironbees.Autonomous", "Ironbees.Ironhive",
            "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.AI", "Microsoft.Extensions.AI.Abstractions", "TokenMeter",
            "IronHive.Abstractions", "IronHive.Core", "IronHive.Providers.OpenAI", "IronHive.Providers.OpenAI.Compatible",
            "Microsoft.AspNetCore",
        }.Select(Assembly.Load)]);

    private static readonly IReadOnlyList<DocSnippet> Snippets = [.. Documents().SelectMany(d => DocSnippets.Read(d.Path, d.Name))];

    public static TheoryData<string> Blocks() => new(Snippets.Select(s => s.Key));

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var snippet = Snippets.Single(s => s.Key == key);
        if (!Fragments.IsFragment(snippet))
            CompilerFor(snippet.File).ShouldCompile(snippet);
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings() => Fragments.ShouldMatch([.. Snippets], atLeast: 11);

    /// <summary>Positive control: the compiler rejects a call the library does not have.</summary>
    [Fact]
    public void Compile_RejectsAMethodTheLibraryDoesNotHave()
    {
        var errors = CompilerFor("README.md").Compile("""
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
        var compiler = CompilerFor("README.md");
        Assert.Empty(compiler.Compile("""
            using Ironbees.Core;
            var options = new ProcessOptions { AgentName = "a" };
            """));
        Assert.NotEmpty(compiler.Compile("""
            var options = new ProcessOptions { AgentName = "a" };
            """));
    }

    private static SnippetCompiler CompilerFor(string document) => new(References)
    {
        Usings = [.. CommonUsings, .. PackageNamespaces(document)],
        StandIns = StandIns,
    };

    // The repository README and every package README: the package READMEs are what nuget.org shows each package's readers.
    private static IEnumerable<(string Path, string Name)> Documents()
    {
        var root = RepoRoot();
        yield return (Path.Combine(root, "README.md"), "README.md");
        foreach (var readme in Directory.GetDirectories(Path.Combine(root, "src")).Order(StringComparer.Ordinal)
                     .Select(d => Path.Combine(d, "README.md")).Where(File.Exists))
            yield return (readme, Path.GetRelativePath(root, readme).Replace('\\', '/'));
    }

    // A package README is read with that package's root namespace in scope (Ironbees.Core for Ironbees.Core):
    // the namespaces of its public types with the fewest segments. The repository README assumes none.
    private static IEnumerable<string> PackageNamespaces(string document)
    {
        var match = PackageReadme().Match(document);
        if (!match.Success)
            return [];

        var namespaces = Assembly.Load(match.Groups["package"].Value).GetExportedTypes()
            .Select(t => t.Namespace).OfType<string>().Distinct().ToList();
        var fewest = namespaces.Min(n => n.Count(c => c == '.'));
        return namespaces.Where(n => n.Count(c => c == '.') == fewest);
    }

    [GeneratedRegex(@"^src/(?<package>[^/]+)/README\.md$")]
    private static partial Regex PackageReadme();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironbees.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Ironbees.slnx not found above the test output directory");
    }
}
