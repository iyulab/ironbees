using System.Collections.Concurrent;
using Ironbees.Autonomous.Abstractions;

namespace Ironbees.Autonomous.Context;

/// <summary>
/// Simple in-memory implementation of IContextSaturationMonitor.
/// Tracks token usage and triggers events when thresholds are exceeded.
/// </summary>
public class InMemorySaturationMonitor : IContextSaturationMonitor
{
    private readonly ConcurrentDictionary<string, int> _usageBySource = new();
    private SaturationConfig _config = new();
    private SaturationLevel _previousLevel = SaturationLevel.Normal;
    private readonly object _lock = new();

    /// <inheritdoc />
    public SaturationState CurrentState { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler<SaturationChangedEventArgs>? SaturationChanged;

    /// <inheritdoc />
    public event EventHandler<SaturationActionRequiredEventArgs>? ActionRequired;

    /// <summary>
    /// Creates a new saturation monitor with default configuration.
    /// </summary>
    public InMemorySaturationMonitor() { }

    /// <summary>
    /// Creates a new saturation monitor with specified configuration.
    /// </summary>
    /// <param name="config">Saturation configuration</param>
    public InMemorySaturationMonitor(SaturationConfig config)
    {
        _config = config;
    }

    /// <inheritdoc />
    public void RecordUsage(int tokens, string source = "unknown")
    {
        _usageBySource.AddOrUpdate(source, tokens, (_, existing) => existing + tokens);
        UpdateStateInternal();
    }

    /// <inheritdoc />
    public Task<SaturationState> UpdateStateAsync(CancellationToken cancellationToken = default)
    {
        UpdateStateInternal();
        return Task.FromResult(CurrentState);
    }

    /// <inheritdoc />
    public void ResetIteration()
    {
        _usageBySource.Clear();
        _previousLevel = SaturationLevel.Normal;
        CurrentState = new SaturationState
        {
            Level = SaturationLevel.Normal,
            Percentage = 0,
            CurrentTokens = 0,
            MaxTokens = _config.MaxTokens,
            UsageBySource = new Dictionary<string, int>(),
            RecommendedAction = SaturationAction.None,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc />
    public void Configure(SaturationConfig config)
    {
        _config = config;
        UpdateStateInternal();
    }

    private void UpdateStateInternal()
    {
        lock (_lock)
        {
            var totalTokens = _usageBySource.Values.Sum();
            var percentage = _config.MaxTokens > 0
                ? (float)totalTokens / _config.MaxTokens * 100
                : 0;

            var level = DetermineLevel(percentage);
            var action = DetermineAction(level, percentage);

            CurrentState = new SaturationState
            {
                Level = level,
                Percentage = percentage,
                CurrentTokens = totalTokens,
                MaxTokens = _config.MaxTokens,
                UsageBySource = new Dictionary<string, int>(_usageBySource),
                RecommendedAction = action,
                LastUpdated = DateTimeOffset.UtcNow
            };

            // Fire events if level changed
            if (level != _previousLevel)
            {
                SaturationChanged?.Invoke(this, new SaturationChangedEventArgs
                {
                    PreviousLevel = _previousLevel,
                    NewLevel = level,
                    CurrentState = CurrentState
                });

                _previousLevel = level;
            }

            // Fire action required if needed
            if (_config.AutoTriggerActions && action != SaturationAction.None)
            {
                var tokensToFree = (int)(totalTokens - (_config.MaxTokens * _config.TargetAfterEviction / 100));
                ActionRequired?.Invoke(this, new SaturationActionRequiredEventArgs
                {
                    Action = action,
                    CurrentState = CurrentState,
                    SuggestedTokensToFree = Math.Max(0, tokensToFree),
                    Reason = $"Saturation at {percentage:F1}% ({level})"
                });
            }
        }
    }

    private SaturationLevel DetermineLevel(float percentage)
    {
        if (percentage >= _config.OverflowThreshold)
            return SaturationLevel.Overflow;
        if (percentage >= _config.CriticalThreshold)
            return SaturationLevel.Critical;
        if (percentage >= _config.HighThreshold)
            return SaturationLevel.High;
        if (percentage >= _config.ElevatedThreshold)
            return SaturationLevel.Elevated;
        return SaturationLevel.Normal;
    }

    private static SaturationAction DetermineAction(SaturationLevel level, float percentage)
    {
        return level switch
        {
            SaturationLevel.Overflow => SaturationAction.Emergency,
            SaturationLevel.Critical => SaturationAction.MustEvict,
            SaturationLevel.High => SaturationAction.ShouldPageOut,
            SaturationLevel.Elevated => SaturationAction.ConsiderSummarization,
            _ => SaturationAction.None
        };
    }
}
