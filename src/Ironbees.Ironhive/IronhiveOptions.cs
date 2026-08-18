using Ironbees.AgentMode.Goals;
using Ironbees.Core.Orchestration;
using IronHive.Abstractions;
using IronHive.Abstractions.Tools;

namespace Ironbees.Ironhive;

/// <summary>
/// Configuration options for Ironbees + IronHive integration
/// </summary>
public class IronhiveOptions
{
    /// <summary>
    /// Action to configure IronHive service via its builder.
    /// Either this or <see cref="HiveService"/> must be provided.
    /// </summary>
    public Action<IHiveServiceBuilder>? ConfigureHive { get; set; }

    /// <summary>
    /// Pre-configured IronHive service instance.
    /// Alternative to <see cref="ConfigureHive"/>.
    /// </summary>
    public IHiveService? HiveService { get; set; }

    /// <summary>
    /// Directory containing agent configurations
    /// </summary>
    public string? AgentsDirectory { get; set; }

    /// <summary>
    /// Minimum confidence threshold for agent selection (0.0 to 1.0, default: 0.3)
    /// </summary>
    public double MinimumConfidenceThreshold { get; set; } = 0.3;

    /// <summary>
    /// Custom checkpoint store for orchestration state persistence.
    /// If not provided, <see cref="Checkpoint.FileSystemIronhiveCheckpointStore"/> is used.
    /// </summary>
    public ICheckpointStore? CheckpointStore { get; set; }

    /// <summary>
    /// Directory for checkpoint storage when using the default file system store.
    /// Default is ".ironbees/checkpoints".
    /// </summary>
    public string CheckpointDirectory { get; set; } = ".ironbees/checkpoints";

    /// <summary>
    /// Handler for approval requests during HITL orchestration.
    /// Receives agent name and step result, returns true to approve, false to reject.
    /// </summary>
    public Func<HitlRequestDetails, Task<bool>>? ApprovalHandler { get; set; }

    /// <summary>
    /// Whether to enable OpenTelemetry tracing for orchestration.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; }

    /// <summary>
    /// Default model deployment used when an agent's <c>model.deployment</c> is omitted in agent.yaml,
    /// enabling runtime model resolution. Forwarded to
    /// <see cref="Ironbees.Core.IronbeesCoreOptions.DefaultModelDeployment"/> so agents load without a
    /// pinned model. When null and an agent omits its deployment with no per-request
    /// <c>ProcessOptions.ModelOverride</c>, that agent fails to load with an actionable error.
    /// </summary>
    public string? DefaultModelDeployment { get; set; }

    /// <summary>
    /// Pool of tools available to agents. An agent draws from this pool by name via its
    /// <c>agent.yaml</c> <see cref="Core.AgentConfig.Tools"/> list; the resolved subset is
    /// assigned to the underlying IronHive <see cref="IronHive.Abstractions.Agent.IAgent.Tools"/>
    /// at agent-creation time (tools are a fixed per-agent property, not a per-invoke option —
    /// see <see cref="IronHive.Abstractions.Agent.AgentInvokeOptions"/>'s own remarks). Null
    /// means no tools are available to any agent; an agent naming a tool not in this pool fails
    /// creation with an actionable error rather than silently running without it.
    /// </summary>
    public IToolCollection? Tools { get; set; }
}
