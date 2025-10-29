namespace Ironbees.Core.Pipeline;

/// <summary>
/// Options for configuring parallel agent execution behavior
/// </summary>
public class ParallelExecutionOptions
{
    /// <summary>
    /// Maximum number of agents to execute concurrently
    /// Default: unlimited (null)
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// Timeout for the entire parallel execution
    /// Default: no timeout (null)
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Timeout for each individual agent execution
    /// Default: no timeout (null)
    /// </summary>
    public TimeSpan? PerAgentTimeout { get; set; }

    /// <summary>
    /// Policy for handling partial failures
    /// </summary>
    public ParallelFailurePolicy FailurePolicy { get; set; } = ParallelFailurePolicy.BestEffort;

    /// <summary>
    /// Minimum number of successful results required
    /// Only applies when FailurePolicy is RequireMinimum
    /// </summary>
    public int? MinimumSuccessfulResults { get; set; }

    /// <summary>
    /// Whether to continue executing remaining agents after a failure
    /// Default: true (continue to get as many results as possible)
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

    /// <summary>
    /// Whether to retry failed agents
    /// Default: false
    /// </summary>
    public bool RetryFailedAgents { get; set; } = false;

    /// <summary>
    /// Maximum number of retry attempts per agent
    /// Only applies when RetryFailedAgents is true
    /// </summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to collect and include timing metrics for each agent
    /// Default: true
    /// </summary>
    public bool CollectMetrics { get; set; } = true;

    /// <summary>
    /// Create default options for best-effort parallel execution
    /// </summary>
    public static ParallelExecutionOptions Default => new();

    /// <summary>
    /// Create options requiring all agents to succeed
    /// </summary>
    public static ParallelExecutionOptions RequireAll => new()
    {
        FailurePolicy = ParallelFailurePolicy.RequireAll,
        ContinueOnFailure = false
    };

    /// <summary>
    /// Create options requiring majority of agents to succeed
    /// </summary>
    public static ParallelExecutionOptions RequireMajority => new()
    {
        FailurePolicy = ParallelFailurePolicy.RequireMajority
    };
}

/// <summary>
/// Policy for handling failures in parallel execution
/// </summary>
public enum ParallelFailurePolicy
{
    /// <summary>
    /// All agents must succeed, fail immediately on first error
    /// </summary>
    RequireAll,

    /// <summary>
    /// Majority of agents must succeed (>50%)
    /// </summary>
    RequireMajority,

    /// <summary>
    /// At least MinimumSuccessfulResults agents must succeed
    /// </summary>
    RequireMinimum,

    /// <summary>
    /// Use whatever successful results are available (default)
    /// At least one agent must succeed
    /// </summary>
    BestEffort,

    /// <summary>
    /// Return first successful result, ignore others
    /// </summary>
    FirstSuccess
}
