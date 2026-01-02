namespace Ironbees.Autonomous.Abstractions;

/// <summary>
/// Human-in-the-loop interface for autonomous execution oversight.
/// Provides intervention points for human approval, feedback, and control.
/// </summary>
public interface IHumanInTheLoop
{
    /// <summary>
    /// Request human approval before proceeding with an action
    /// </summary>
    /// <param name="request">Approval request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval response with decision and optional feedback</returns>
    Task<HumanApproval> RequestApprovalAsync(
        HumanApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Request human feedback for an execution result
    /// </summary>
    /// <param name="request">Feedback request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Human feedback with corrections or guidance</returns>
    Task<HumanFeedback> RequestFeedbackAsync(
        HumanFeedbackRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify human of an event (non-blocking)
    /// </summary>
    /// <param name="notification">Notification details</param>
    void Notify(HumanNotification notification);

    /// <summary>
    /// Check if human intervention is currently available
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Timeout for waiting on human response
    /// </summary>
    TimeSpan ResponseTimeout { get; }
}

/// <summary>
/// Human approval request details
/// </summary>
public record HumanApprovalRequest
{
    /// <summary>Unique request ID</summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Type of intervention point</summary>
    public required HumanInterventionPoint InterventionPoint { get; init; }

    /// <summary>Summary of what needs approval</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed description for context</summary>
    public string? Details { get; init; }

    /// <summary>Associated task ID</summary>
    public string? TaskId { get; init; }

    /// <summary>Suggested action if auto-approved</summary>
    public string? SuggestedAction { get; init; }

    /// <summary>Risk level assessment</summary>
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;

    /// <summary>When the request was created</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Human approval response
/// </summary>
public record HumanApproval
{
    /// <summary>Request ID this approval responds to</summary>
    public required string RequestId { get; init; }

    /// <summary>Approval decision</summary>
    public required ApprovalDecision Decision { get; init; }

    /// <summary>Optional feedback or modifications</summary>
    public string? Feedback { get; init; }

    /// <summary>Modified action if decision is ModifyAndApprove</summary>
    public string? ModifiedAction { get; init; }

    /// <summary>When the response was provided</summary>
    public DateTimeOffset RespondedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Create an auto-approve response (used when human unavailable)</summary>
    public static HumanApproval AutoApprove(string requestId) => new()
    {
        RequestId = requestId,
        Decision = ApprovalDecision.Approved,
        Feedback = "Auto-approved (human unavailable)"
    };

    /// <summary>Create a timeout response</summary>
    public static HumanApproval Timeout(string requestId) => new()
    {
        RequestId = requestId,
        Decision = ApprovalDecision.Timeout,
        Feedback = "Request timed out waiting for human response"
    };
}

/// <summary>
/// Human feedback request details
/// </summary>
public record HumanFeedbackRequest
{
    /// <summary>Unique request ID</summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Type of feedback requested</summary>
    public required FeedbackType FeedbackType { get; init; }

    /// <summary>Original prompt/goal</summary>
    public required string OriginalPrompt { get; init; }

    /// <summary>Execution output to review</summary>
    public required string ExecutionOutput { get; init; }

    /// <summary>Associated task ID</summary>
    public string? TaskId { get; init; }

    /// <summary>Oracle verdict if available</summary>
    public string? OracleAnalysis { get; init; }

    /// <summary>When the request was created</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Human feedback response
/// </summary>
public record HumanFeedback
{
    /// <summary>Request ID this feedback responds to</summary>
    public required string RequestId { get; init; }

    /// <summary>Whether the output is satisfactory</summary>
    public bool IsSatisfactory { get; init; }

    /// <summary>Rating (0-10, 0=not rated)</summary>
    public int Rating { get; init; }

    /// <summary>Feedback text</summary>
    public string? Comments { get; init; }

    /// <summary>Suggested corrections or improvements</summary>
    public string? SuggestedCorrections { get; init; }

    /// <summary>Whether to retry with modifications</summary>
    public bool ShouldRetry { get; init; }

    /// <summary>Modified prompt for retry</summary>
    public string? ModifiedPrompt { get; init; }

    /// <summary>When the response was provided</summary>
    public DateTimeOffset RespondedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Notification to human (non-blocking)
/// </summary>
public record HumanNotification
{
    /// <summary>Notification level</summary>
    public NotificationLevel Level { get; init; } = NotificationLevel.Info;

    /// <summary>Notification title</summary>
    public required string Title { get; init; }

    /// <summary>Notification message</summary>
    public required string Message { get; init; }

    /// <summary>Associated task ID</summary>
    public string? TaskId { get; init; }

    /// <summary>When the notification was created</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Intervention points where human approval may be required
/// </summary>
public enum HumanInterventionPoint
{
    /// <summary>Before starting a new task</summary>
    BeforeTaskStart = 1,

    /// <summary>After task completion, before continuing</summary>
    AfterTaskComplete = 2,

    /// <summary>When oracle detects uncertainty</summary>
    OracleUncertain = 3,

    /// <summary>When a task has failed</summary>
    TaskFailed = 4,

    /// <summary>Before executing a high-risk action</summary>
    HighRiskAction = 5,

    /// <summary>Before modifying external resources</summary>
    ExternalModification = 6,

    /// <summary>When maximum iterations approached</summary>
    MaxIterationsApproaching = 7,

    /// <summary>Before checkpoint restoration</summary>
    BeforeCheckpointRestore = 8,

    /// <summary>Custom intervention point</summary>
    Custom = 99
}

/// <summary>
/// Approval decision from human
/// </summary>
public enum ApprovalDecision
{
    /// <summary>Approved to proceed</summary>
    Approved = 1,

    /// <summary>Rejected - stop execution</summary>
    Rejected = 2,

    /// <summary>Modified - proceed with changes</summary>
    ModifyAndApprove = 3,

    /// <summary>Deferred - ask again later</summary>
    Deferred = 4,

    /// <summary>Timed out waiting for response</summary>
    Timeout = 5
}

/// <summary>
/// Type of feedback requested
/// </summary>
public enum FeedbackType
{
    /// <summary>Quality assessment of output</summary>
    QualityAssessment = 1,

    /// <summary>Correctness verification</summary>
    CorrectnessCheck = 2,

    /// <summary>Guidance on next steps</summary>
    NextStepsGuidance = 3,

    /// <summary>Error diagnosis help</summary>
    ErrorDiagnosis = 4,

    /// <summary>General feedback</summary>
    General = 5
}

/// <summary>
/// Risk level assessment
/// </summary>
public enum RiskLevel
{
    /// <summary>Low risk - minimal impact</summary>
    Low = 1,

    /// <summary>Medium risk - moderate impact</summary>
    Medium = 2,

    /// <summary>High risk - significant impact</summary>
    High = 3,

    /// <summary>Critical - requires immediate attention</summary>
    Critical = 4
}

/// <summary>
/// Notification level
/// </summary>
public enum NotificationLevel
{
    /// <summary>Debug information</summary>
    Debug = 0,

    /// <summary>Informational</summary>
    Info = 1,

    /// <summary>Warning</summary>
    Warning = 2,

    /// <summary>Error</summary>
    Error = 3,

    /// <summary>Success</summary>
    Success = 4
}
