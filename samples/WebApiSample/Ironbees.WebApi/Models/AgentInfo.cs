namespace Ironbees.WebApi.Models;

/// <summary>
/// Agent information model
/// </summary>
public class AgentInfo
{
    /// <summary>
    /// Agent name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Agent description
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Agent capabilities
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Agent tags
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Model configuration
    /// </summary>
    public ModelInfo? Model { get; set; }
}

/// <summary>
/// Model configuration information
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Model deployment name
    /// </summary>
    public string? Deployment { get; set; }

    /// <summary>
    /// Temperature setting
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Maximum tokens
    /// </summary>
    public int? MaxTokens { get; set; }
}
