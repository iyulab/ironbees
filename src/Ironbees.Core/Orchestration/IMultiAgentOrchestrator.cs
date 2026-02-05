// Copyright (c) IYULab. All rights reserved.
// Licensed under the MIT License.

namespace Ironbees.Core.Orchestration;

/// <summary>
/// Abstraction for multi-agent orchestration.
/// Implementations bridge to specific LLM frameworks (IronHive, MAF, etc.).
/// </summary>
public interface IMultiAgentOrchestrator
{
    /// <summary>
    /// Runs the orchestration with streaming events.
    /// </summary>
    /// <param name="input">The input message to start orchestration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of orchestration events.</returns>
    IAsyncEnumerable<OrchestrationStreamEvent> RunStreamingAsync(
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the orchestration and returns the final result.
    /// </summary>
    /// <param name="input">The input message to start orchestration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final orchestration result.</returns>
    Task<OrchestrationResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for orchestration stream events.
/// </summary>
public abstract record OrchestrationStreamEvent
{
    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event when orchestration starts.
/// </summary>
public sealed record OrchestrationStartedEvent : OrchestrationStreamEvent
{
    public required OrchestratorType OrchestrationType { get; init; }
    public required int AgentCount { get; init; }
}

/// <summary>
/// Event when an agent starts execution.
/// </summary>
public sealed record AgentStartedEvent : OrchestrationStreamEvent
{
    public required string AgentName { get; init; }
}

/// <summary>
/// Event for streaming message content delta.
/// </summary>
public sealed record MessageDeltaEvent : OrchestrationStreamEvent
{
    public required string AgentName { get; init; }
    public required string Delta { get; init; }
}

/// <summary>
/// Event when an agent completes execution.
/// </summary>
public sealed record AgentCompletedEvent : OrchestrationStreamEvent
{
    public required string AgentName { get; init; }
    public required bool Success { get; init; }
    public string? Result { get; init; }
    public TokenUsageInfo? TokenUsage { get; init; }
}

/// <summary>
/// Event when approval is required (HITL).
/// </summary>
public sealed record ApprovalRequiredEvent : OrchestrationStreamEvent
{
    public required string AgentName { get; init; }
    public string? StepName { get; init; }
    public string? Reason { get; init; }
    public string? ProposedAction { get; init; }
}

/// <summary>
/// Event when an agent hands off to another agent.
/// </summary>
public sealed record HandoffEvent : OrchestrationStreamEvent
{
    public required string FromAgent { get; init; }
    public required string ToAgent { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event when a speaker is selected in group chat.
/// </summary>
public sealed record SpeakerSelectedEvent : OrchestrationStreamEvent
{
    public required string SelectedAgent { get; init; }
    public required int Round { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Event when human input is required.
/// </summary>
public sealed record HumanInputRequiredEvent : OrchestrationStreamEvent
{
    public string? Prompt { get; init; }
    public string? InputType { get; init; }
}

/// <summary>
/// Event when orchestration completes.
/// </summary>
public sealed record OrchestrationCompletedEvent : OrchestrationStreamEvent
{
    public required string? FinalResult { get; init; }
    public required int TotalAgentsExecuted { get; init; }
    public required TimeSpan Duration { get; init; }
    public TokenUsageInfo? TotalTokenUsage { get; init; }
}

/// <summary>
/// Event when orchestration fails.
/// </summary>
public sealed record OrchestrationFailedEvent : OrchestrationStreamEvent
{
    public string? FailedAgent { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ExceptionType { get; init; }
    public bool IsRecoverable { get; init; }
}

/// <summary>
/// Token usage information.
/// </summary>
public sealed record TokenUsageInfo
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// Final result of an orchestration.
/// </summary>
public sealed record OrchestrationResult
{
    public required bool Success { get; init; }
    public string? FinalOutput { get; init; }
    public int TotalAgentsExecuted { get; init; }
    public TimeSpan Duration { get; init; }
    public TokenUsageInfo? TotalTokenUsage { get; init; }
    public string? ErrorMessage { get; init; }
}
