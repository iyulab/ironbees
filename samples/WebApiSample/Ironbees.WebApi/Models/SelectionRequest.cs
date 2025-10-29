namespace Ironbees.WebApi.Models;

/// <summary>
/// Request model for agent selection endpoint
/// </summary>
public class SelectionRequest
{
    /// <summary>
    /// Input text to analyze for agent selection
    /// </summary>
    public required string Input { get; set; }
}
