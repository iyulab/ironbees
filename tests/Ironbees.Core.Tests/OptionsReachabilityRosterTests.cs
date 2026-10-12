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
    internal static readonly Assembly[] Libraries = [Assembly.Load("Ironbees.Core"), Assembly.Load("Ironbees.Autonomous"), Assembly.Load("Ironbees.AgentMode"), Assembly.Load("Ironbees.AgentFramework"), Assembly.Load("Ironbees.Ironhive")];

    /// <summary>Options accepted as unread today, each with the reason. Shrink this list; never grow it silently.</summary>
    private static readonly Dictionary<string, string[]> KnownUnread = new()
    {
        // Input contracts: the library hands these to code it does not own, which reads them.
        // OracleConfig goes to the IOracleVerifier implementation; AgentConfig.Metadata is the consumer's own bag, and
        // AgentConfig.Version is the definition's declared version - AgentConfigValidator checks it is semver (a check,
        // not a use, since kit 0.7.0 names the validators below) and the consumer reads it; the library acts on neither.
        ["Ironbees.Autonomous.Models.OracleConfig"] = ["MaxTokens", "ReflectionSystemPrompt", "SystemPrompt", "Temperature", "Timeout"],
        ["Ironbees.Core.AgentConfig"] = ["Metadata", "Version"],

        // *Settings types are in the scan too. Classified; each group states why it is unread.
        //
        // Contract members: settings the library loads (SettingsLoader,
        // FileSystemGoalLoader) and hands to the consumer, which applies them - the library makes no LLM calls of its own
        // and the goal schema is executed by the agent framework. Read by samples and documented as such.
        ["Ironbees.Autonomous.Configuration.DebugSettings"] = ["Enabled"],
        ["Ironbees.Autonomous.Configuration.LlmSettings"] = ["FrequencyPenalty", "MaxOutputTokens", "PresencePenalty", "Temperature", "TimeoutSeconds", "TopP"],
        ["Ironbees.Autonomous.Configuration.OrchestratorSettings"] = ["Debug"],
    };

    [Fact]
    public void EveryPublicOption_IsRead() =>
        OptionsReachability.Scan(Libraries, OptionsTypes.WithNestedSettings(Libraries, OptionsTypes.NamedWith("Options", "Config", "Settings")), OptionsValidation.DeclaredIn(typeof(Ironbees.Core.AgentConfigValidator), typeof(Ironbees.Core.Goals.GoalValidator)))
            .ShouldMatchRoster(KnownUnread);
}
