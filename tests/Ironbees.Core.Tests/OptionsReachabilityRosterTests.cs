using System.Reflection;
using Iyu.Conventions.Testing;

namespace Ironbees.Core.Tests;

/// <summary>
/// Every public option in this library is read by the library. An option nothing reads is a promise it does not keep:
/// a caller sets it, and nothing changes and nothing is reported. The roster fails both ways — a new unread option,
/// and a listed one that has since been wired — so each change is recorded on purpose.
/// </summary>
public class OptionsReachabilityRosterTests
{
    private static readonly Assembly[] Libraries = [Assembly.Load("Ironbees.Core"), Assembly.Load("Ironbees.Autonomous"), Assembly.Load("Ironbees.AgentMode"), Assembly.Load("Ironbees.AgentFramework"), Assembly.Load("Ironbees.Ironhive")];

    /// <summary>Options accepted as unread today, each with the reason. Shrink this list; never grow it silently.</summary>
    private static readonly Dictionary<string, string[]> KnownUnread = new()
    {
        // Input contracts: the library hands these to code it does not own, which reads them.
        // OracleConfig goes to the IOracleVerifier implementation; AgentConfig.Metadata is the consumer's own bag.
        ["Ironbees.Autonomous.Models.OracleConfig"] = ["MaxTokens", "ReflectionSystemPrompt", "SystemPrompt", "Temperature", "Timeout"],
        ["Ironbees.Core.AgentConfig"] = ["Metadata"],
    };

    [Fact]
    public void EveryPublicOption_IsRead() =>
        OptionsReachability.Scan(Libraries, OptionsTypes.NamedWith("Options", "Config"))
            .ShouldMatchRoster(KnownUnread);
}
