namespace Ironbees.WebApi.Models;

/// <summary>
/// Response model for agent selection endpoint
/// </summary>
public class SelectionResponse
{
    /// <summary>
    /// Selected agent name
    /// </summary>
    public string? SelectedAgent { get; set; }

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Reason for selection
    /// </summary>
    public required string SelectionReason { get; set; }

    /// <summary>
    /// All agent scores
    /// </summary>
    public List<AgentScoreInfo> AllScores { get; set; } = new();
}

/// <summary>
/// Agent score information
/// </summary>
public class AgentScoreInfo
{
    /// <summary>
    /// Agent name
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// Score (0-1)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Reasons for the score
    /// </summary>
    public List<string> Reasons { get; set; } = new();
}
