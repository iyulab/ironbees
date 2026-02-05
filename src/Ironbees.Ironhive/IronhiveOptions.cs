using Ironbees.AgentMode.Goals;
using Ironbees.Core.Orchestration;
using IronHive.Abstractions;

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
}
