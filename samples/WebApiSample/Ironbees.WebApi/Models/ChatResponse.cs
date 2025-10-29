namespace Ironbees.WebApi.Models;

/// <summary>
/// Response model for chat endpoint
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// Agent's response message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Name of the agent that processed the request
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// Confidence score (0-1) for automatic agent selection
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}
