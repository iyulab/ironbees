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
}
