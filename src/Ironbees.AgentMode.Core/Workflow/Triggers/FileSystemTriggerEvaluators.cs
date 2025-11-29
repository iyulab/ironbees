namespace Ironbees.AgentMode.Core.Workflow.Triggers;

/// <summary>
/// Evaluates FileExists trigger - activates when a file exists at specified path.
/// </summary>
public sealed class FileExistsTriggerEvaluator : ITriggerEvaluator
{
    public TriggerType TriggerType => TriggerType.FileExists;

    public Task<bool> EvaluateAsync(
        TriggerDefinition trigger,
        TriggerEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trigger.Path))
        {
            throw new ArgumentException("FileExists trigger requires a path.", nameof(trigger));
        }

        var fullPath = Path.IsPathRooted(trigger.Path)
            ? trigger.Path
            : Path.Combine(context.WorkingDirectory, trigger.Path);

        var exists = File.Exists(fullPath);
        return Task.FromResult(exists);
    }
}

/// <summary>
/// Evaluates DirectoryNotEmpty trigger - activates when directory contains files.
/// </summary>
public sealed class DirectoryNotEmptyTriggerEvaluator : ITriggerEvaluator
{
    public TriggerType TriggerType => TriggerType.DirectoryNotEmpty;

    public Task<bool> EvaluateAsync(
        TriggerDefinition trigger,
        TriggerEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trigger.Path))
        {
            throw new ArgumentException("DirectoryNotEmpty trigger requires a path.", nameof(trigger));
        }

        var fullPath = Path.IsPathRooted(trigger.Path)
            ? trigger.Path
            : Path.Combine(context.WorkingDirectory, trigger.Path);

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        var hasFiles = Directory.EnumerateFileSystemEntries(fullPath).Any();
        return Task.FromResult(hasFiles);
    }
}

/// <summary>
/// Evaluates Immediate trigger - always returns true.
/// </summary>
public sealed class ImmediateTriggerEvaluator : ITriggerEvaluator
{
    public TriggerType TriggerType => TriggerType.Immediate;

    public Task<bool> EvaluateAsync(
        TriggerDefinition trigger,
        TriggerEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

/// <summary>
/// Default implementation of trigger evaluator factory.
/// </summary>
public sealed class TriggerEvaluatorFactory : ITriggerEvaluatorFactory
{
    private readonly Dictionary<TriggerType, ITriggerEvaluator> _evaluators;

    public TriggerEvaluatorFactory()
    {
        _evaluators = new Dictionary<TriggerType, ITriggerEvaluator>
        {
            [TriggerType.FileExists] = new FileExistsTriggerEvaluator(),
            [TriggerType.DirectoryNotEmpty] = new DirectoryNotEmptyTriggerEvaluator(),
            [TriggerType.Immediate] = new ImmediateTriggerEvaluator()
        };
    }

    public TriggerEvaluatorFactory(IEnumerable<ITriggerEvaluator> evaluators)
    {
        _evaluators = evaluators.ToDictionary(e => e.TriggerType);
    }

    public ITriggerEvaluator GetEvaluator(TriggerType triggerType)
    {
        if (_evaluators.TryGetValue(triggerType, out var evaluator))
        {
            return evaluator;
        }

        throw new NotSupportedException($"Trigger type '{triggerType}' is not supported.");
    }
}
