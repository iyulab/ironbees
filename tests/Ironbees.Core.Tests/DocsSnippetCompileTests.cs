using System.Reflection;
using Iyu.Conventions.Testing.Snippets;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Ironbees.Core.Tests;

/// <summary>
/// Compiles the C# blocks of the guides under <c>docs/</c> against the current assemblies. A reader copies these; a block
/// that no longer matches the shipped API fails here instead of in their build. The READMEs are compiled by
/// <see cref="ReadmeSnippetCompileTests"/>.
/// </summary>
/// <remarks>
/// Each guide compiles as a whole (<see cref="SnippetCompiler.CompileDocument"/>): a type one block declares is visible to
/// the next, each block's statements run in a method of their own, and a block that restates a library type is checked
/// against the real type (<see cref="SnippetCompiler.CheckListings"/>). A guide's blocks show no usings, so every namespace
/// the Ironbees packages export is in scope, with the stand-ins below for values the surrounding text gives the reader. A
/// section that is deliberately not a program is in <see cref="Fragments"/> with the reason. A document with a C# block is
/// in <see cref="Checked"/>, in <see cref="Historical"/> or in <see cref="NotYetChecked"/> with the reason — never silently
/// in none.
/// </remarks>
public class DocsSnippetCompileTests
{
    // Guides whose every C# block compiles (or is a listed fragment).
    private static readonly string[] Checked =
    [
        "docs/ARCHITECTURE.md",
        "docs/DEPLOYMENT.md",
        "docs/FAQ.md",
        "docs/PHILOSOPHY.md",
        "docs/PROVIDERS.md",
        "docs/autonomous-sdk-guide.md",
        "agents/BUILTIN_AGENTS.md",
        "samples/OpenAISample/README.md",
    ];

    // Documents that show an API as it was, on purpose — a migration guide's «before», a decision record. They are not
    // compiled; their «after» code is what the guides above show.
    private static readonly Dictionary<string, string> Historical = new(StringComparer.Ordinal)
    {
        ["docs/adr/001-remove-conversational-agent.md"] = "a decision record: shows the removed ConversationalAgent it retired",
        ["docs/migration/chatclientbuilder-pattern.md"] = "a migration guide: each «before» block is the API as it was",
        ["docs/migration/namespace-migration.md"] = "a migration guide: the old namespaces next to the new ones",
        ["docs/migration/service-layer-pattern.md"] = "a migration guide: each «before» block is the API as it was",
    };

    // Guides with C# blocks that are not compiled yet, with the reason. Shrink this.
    private static readonly Dictionary<string, string> NotYetChecked = new(StringComparer.Ordinal)
    {
        ["samples/Patterns/MultiExecutionPattern/README.md"] =
            "a pattern walkthrough: Azure Functions and Table/Queue storage excerpts (packages the library does not reference) and members shown outside their class",
        ["samples/WebApiSample/README.md"] =
            "a sample walkthrough: controller members shown outside their class, «...» elisions, and extension ideas (JWT, SignalR, Swagger) on packages the sample does not reference",
    };

    // A block that is deliberately not a program (a signature sketch, pseudocode), by the heading it sits under, with the
    // reason. Shrink this, never grow it silently.
    private static readonly SnippetRoster Fragments = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["API Key Management"] = "its Key Vault part uses Azure.Security.KeyVault.Secrets and Azure.Identity, packages the library does not reference",
        ["Resilience Patterns"] = "registers Polly policies (Microsoft.Extensions.Http.Polly), a package the library does not reference",
        ["Serilog Configuration"] = "configures Serilog and its sinks, packages the library does not reference",
        ["Application Insights"] = "registers Application Insights (Microsoft.ApplicationInsights.AspNetCore), a package the library does not reference",
        ["Question 2: Does it belong in YAML or Code?"] = "a method shown on its own, outside the class that would declare it",
        ["DAG Task Scheduling"] = "a class Ironbees deliberately does not ship (DAG scheduling belongs in MAF), shown with an elided body",
        ["Multi-Execution Coordination"] = "a class Ironbees deliberately does not ship (it belongs in the application), shown with an elided body",
        ["2. Flexibility"] = "builder-call fragments contrasting two designs; the second names a «built-in oracle» API Ironbees deliberately does not have",
        ["OpenAI Adapter 구현"] = "the sample's class outline: its constructor, with the interface members elided («ILLMFrameworkAdapter 구현...»)",
    });

    private static readonly string[] AssembliesToLoad =
    [
        "Ironbees.Core", "Ironbees.AgentFramework", "Ironbees.AgentMode", "Ironbees.Autonomous", "Ironbees.Ironhive",
        "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.AI", "Microsoft.Extensions.AI.Abstractions", "TokenMeter",
        "IronHive.Abstractions", "IronHive.Core", "IronHive.Providers.Anthropic", "IronHive.Providers.GoogleAI",
        "IronHive.Providers.OpenAI", "IronHive.Providers.OpenAI.Compatible",
        "Microsoft.AspNetCore", "Microsoft.Agents.AI", "Microsoft.Agents.AI.Abstractions", "Microsoft.Agents.AI.Workflows",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Diagnostics.HealthChecks", "Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions",
        "Microsoft.AspNetCore.Diagnostics.HealthChecks", "Microsoft.AspNetCore.Http.Abstractions", "Microsoft.AspNetCore.Routing",
    ];

    private static readonly string[] Usings =
    [
        "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Text.Json",
        "System.Threading", "System.Threading.Tasks", "System.Runtime.CompilerServices", "System.Collections.Concurrent",
        "Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Diagnostics.HealthChecks", "Microsoft.AspNetCore.Builder",
        "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.AI",
        .. AssembliesToLoad.Where(n => n.StartsWith("Ironbees.", StringComparison.Ordinal) || n.StartsWith("IronHive.Providers.", StringComparison.Ordinal))
            .SelectMany(n => Assembly.Load(n).GetExportedTypes())
            .Select(t => t.Namespace)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    // Values the guides' running text gives the reader, typed as the API takes them.
    private static readonly Dictionary<string, string> StandIns = new(StringComparer.Ordinal)
    {
        ["services"] = "IServiceCollection services = new ServiceCollection();",
        ["serviceProvider"] = "IServiceProvider serviceProvider = null!;",
        ["cancellationToken"] = "CancellationToken cancellationToken = default;",
        // An ASP.NET Core host's builder and app, as the deployment and Web API guides have them.
        ["builder"] = "Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = null!;",
        ["app"] = "Microsoft.AspNetCore.Builder.WebApplication app = null!;",
        ["hive"] = "IronHive.Abstractions.IHiveServiceBuilder hive = null!;",
        ["userInput"] = "string userInput = \"\";",
        // The Autonomous SDK guides' request and result: the README's shape, under the names the guides use.
        ["Request"] = "public record Request(string RequestId, string Prompt) : ITaskRequest;",
        ["Result"] = "public record Result(string RequestId, bool Success, string Output, string? ErrorOutput = null) : ITaskResult;",
        ["executor"] = "ITaskExecutor<Request, Result> executor = null!;",
        ["oracle"] = "IOracleVerifier oracle = null!;",
        ["agentExecutorFactory"] = "Ironbees.AgentMode.Workflow.IAgentExecutorFactory agentExecutorFactory = null!;",
        ["settings"] = "AutonomousSettings settings = new();",
        ["orchestrator"] = "AutonomousOrchestrator<Request, Result> orchestrator = null!;",
        ["guardrail"] = "IContentGuardrail guardrail = null!;",
        ["agentResolver"] = "Func<string, CancellationToken, Task<Microsoft.Agents.AI.AIAgent>> agentResolver = null!;",
        // The reader's own audit logger, which a guide registers by type.
        ["CustomAuditLogger"] = "public sealed class CustomAuditLogger : IAuditLogger { public Task LogInputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task LogOutputValidationAsync(GuardrailAuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask; }",
    };

    private static readonly IReadOnlyList<MetadataReference> References =
        SnippetCompiler.ReferencesFor([.. AssembliesToLoad.Select(Assembly.Load)]);

    private static readonly SnippetCompiler Compiler = new(References) { Usings = Usings, StandIns = StandIns, CheckListings = true };

    private static readonly IReadOnlyList<DocSnippet> Snippets =
        [.. Checked.SelectMany(d => DocSnippets.Read(Path.Combine(RepositoryRoot(), d), d))];

    private static readonly Dictionary<string, Lazy<DocumentCompilation>> Documents = Checked.ToDictionary(
        d => d,
        d => new Lazy<DocumentCompilation>(() => Compiler.CompileDocument(
            [.. Snippets.Where(s => s.File == d && !Fragments.IsFragment(s))])),
        StringComparer.Ordinal);

    public static TheoryData<string> Blocks() => new(Snippets.Select(s => s.Key));

    public static TheoryData<string> CheckedDocuments() => new(Checked);

    [Theory]
    [MemberData(nameof(Blocks))]
    public void DocsBlock_Compiles(string key)
    {
        var snippet = Snippets.Single(s => s.Key == key);
        if (!Fragments.IsFragment(snippet))
            Documents[snippet.File].Value.ShouldCompile(snippet);
    }

    /// <summary>A document's type stand-ins compile against the current API (they belong to no single example).</summary>
    [Theory]
    [MemberData(nameof(CheckedDocuments))]
    public void Document_HasNoErrorsOutsideItsExamples(string document) => Documents[document].Value.ShouldHaveNoSharedErrors();

    [Fact]
    public void EveryCheckedBlock_IsFoundAndFragmentsNameRealHeadings() => Fragments.ShouldMatch([.. Snippets], atLeast: 54);

    /// <summary>
    /// The guides' type listings (an interface shown with its members) are checked against the real types, not compiled as a
    /// second type of the same name. A floor, so a listing the kit stops recognising shows here instead of passing unchecked.
    /// </summary>
    [Fact]
    public void TypeListings_AreCheckedAgainstTheRealTypes()
    {
        var listed = Checked.Sum(d => Documents[d].Value.ListedTypes);
        Assert.True(listed >= 1, $"expected the guides' type listings to be checked, found {listed}");
    }

    /// <summary>
    /// Positive control: the compiler rejects the settings call four guides showed before they were compiled — a static
    /// <c>OrchestratorSettings.LoadFromFileAsync</c> that neither settings type has. A name check passed it, because a
    /// method of that name exists elsewhere (<c>YamlWorkflowLoader.LoadFromFileAsync</c>).
    /// </summary>
    [Fact]
    public void Compile_RejectsTheSettingsCallTheGuidesShowedBeforeTheyWereCompiled()
    {
        Assert.Empty(Compiler.Compile("""var settings = await new SettingsLoader().LoadFromFileAsync("settings.yaml");"""));
        Assert.NotEmpty(Compiler.Compile("""var settings = await AutonomousSettings.LoadFromFileAsync("settings.yaml");"""));
    }

    /// <summary>
    /// A guide with a C# block is checked, historical or listed as not yet checked with the reason, and the lists name only
    /// documents that exist and have C# blocks — so a new guide cannot be skipped silently and a repaired one cannot stay
    /// on the waiting list. The READMEs are <see cref="ReadmeSnippetCompileTests"/>'.
    /// </summary>
    [Fact]
    public void EveryGuideWithCSharp_IsCheckedOrListed()
    {
        var withCSharp = GuidesWithCSharp().ToList();
        var listed = Checked.Concat(Historical.Keys).Concat(NotYetChecked.Keys).ToList();

        Assert.All(withCSharp, d => Assert.True(listed.Contains(d), $"{d} has C# blocks but is in no list"));
        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.All(listed, d => Assert.Contains(d, withCSharp));
    }

    /// <summary>Every Markdown document of the repository with a C# block, except the READMEs compiled elsewhere, changelogs and the tests' own.</summary>
    private static IEnumerable<string> GuidesWithCSharp()
    {
        var root = RepositoryRoot();
        return Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .Where(p => !p.StartsWith("tests/", StringComparison.Ordinal)
                        && !p.Contains("/bin/", StringComparison.Ordinal) && !p.Contains("/obj/", StringComparison.Ordinal)
                        && !p.StartsWith('.')
                        && !IsReadmeTestsDocument(p)
                        && !Path.GetFileName(p).Equals("CHANGELOG.md", StringComparison.OrdinalIgnoreCase))
            .Where(p => DocSnippets.Read(Path.Combine(root, p), p).Count > 0)
            .Order(StringComparer.Ordinal);
    }

    // The repository README and the package READMEs (src/<package>/README.md) are ReadmeSnippetCompileTests'.
    private static bool IsReadmeTestsDocument(string path) =>
        path == "README.md" || (path.StartsWith("src/", StringComparison.Ordinal) && path.EndsWith("/README.md", StringComparison.Ordinal)
                                && path.Count(c => c == '/') == 2);

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironbees.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Ironbees.slnx not found above the test output directory");
    }
}
