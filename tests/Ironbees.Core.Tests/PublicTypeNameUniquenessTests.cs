using System.Reflection;
using Xunit;

namespace Ironbees.Core.Tests;

/// <summary>
/// No two public types of the Ironbees packages share a simple name. The packages are used together (Ironbees.Ironhive
/// brings Ironbees.Core along, the Autonomous SDK and the AgentMode workflows sit next to it), so two types of one name
/// in two of their namespaces make every consumer that imports both write the namespace out — CS0104 — and make a guide
/// that names one of them ambiguous to its reader.
/// </summary>
public class PublicTypeNameUniquenessTests
{
    private static readonly string[] Packages =
        ["Ironbees.Core", "Ironbees.AgentFramework", "Ironbees.AgentMode", "Ironbees.Autonomous", "Ironbees.Ironhive"];

    [Fact]
    public void EveryPublicTypeName_IsUsedOnceAcrossThePackages()
    {
        var duplicates = Packages
            .SelectMany(p => Assembly.Load(p).GetExportedTypes())
            .Where(t => !IsExtensionHolder(t))
            .GroupBy(t => t.Name, StringComparer.Ordinal) // Name keeps the generic arity (`AutonomousOrchestrator`2`)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.FullName))}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(duplicates.Count == 0, "public type names used more than once:\n" + string.Join("\n", duplicates));
    }

    [Fact]
    public void TheScan_SeesThePackagesTypes()
    {
        var count = Packages.Sum(p => Assembly.Load(p).GetExportedTypes().Length);
        Assert.True(count >= 200, $"expected the packages' public types, found {count}");
    }

    // A static class that only holds extension methods (ServiceCollectionExtensions) is never named by a caller.
    private static bool IsExtensionHolder(Type type) => type is { IsAbstract: true, IsSealed: true };
}
