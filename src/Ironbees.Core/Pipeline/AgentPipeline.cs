using System.Diagnostics;

namespace Ironbees.Core.Pipeline;

/// <summary>
/// Default implementation of agent pipeline
/// </summary>
public class AgentPipeline : IAgentPipeline
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly List<PipelineStep> _steps = new();
    private readonly List<ParallelPipelineStep> _parallelSteps = new();
    private readonly List<(int Index, bool IsParallel)> _stepOrder = new();

    public string Name { get; }
    public IReadOnlyList<PipelineStep> Steps => _steps.AsReadOnly();

    public AgentPipeline(string name, IAgentOrchestrator orchestrator)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <summary>
    /// Add a sequential step to the pipeline
    /// </summary>
    internal void AddStep(PipelineStep step)
    {
        var index = _steps.Count;
        _steps.Add(step);
        _stepOrder.Add((index, false)); // false = sequential
    }

    /// <summary>
    /// Add a parallel step to the pipeline
    /// </summary>
    internal void AddParallelStep(ParallelPipelineStep step)
    {
        var index = _parallelSteps.Count;
        _parallelSteps.Add(step);
        _stepOrder.Add((index, true)); // true = parallel
    }

    /// <inheritdoc/>
    public async Task<PipelineResult> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        var context = new PipelineContext(input);
        return await ExecuteAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PipelineResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var result = new PipelineResult
        {
            PipelineName = Name,
            Context = context,
            Success = true
        };

        try
        {
            // Execute steps in order (sequential or parallel)
            foreach (var (index, isParallel) in _stepOrder)
            {
                if (!context.ShouldContinue)
                {
                    break;
                }

                bool stepSuccess;

                if (isParallel)
                {
                    // Execute parallel step
                    var parallelStep = _parallelSteps[index];

                    // Check condition
                    if (parallelStep.Condition != null && !parallelStep.Condition(context))
                    {
                        continue;
                    }

                    stepSuccess = await ExecuteParallelStepAsync(parallelStep, context, cancellationToken);
                }
                else
                {
                    // Execute sequential step
                    var step = _steps[index];

                    // Check condition
                    if (step.Condition != null && !step.Condition(context))
                    {
                        continue;
                    }

                    stepSuccess = await ExecuteStepWithRetryAsync(step, context, cancellationToken);
                }

                result.StepsExecuted++;

                if (!stepSuccess)
                {
                    result.StepsFailed++;

                    if (isParallel)
                    {
                        var parallelStep = _parallelSteps[index];
                        if (parallelStep.ExecutionOptions.FailurePolicy == ParallelFailurePolicy.RequireAll)
                        {
                            result.Success = false;
                            result.Error = context.Error;
                            break;
                        }
                    }
                    else
                    {
                        var step = _steps[index];
                        if (!step.ContinueOnError)
                        {
                            result.Success = false;
                            result.Error = context.Error;
                            break;
                        }
                    }
                }
            }

            // Set final output
            var lastResult = context.GetLastStepResult();
            result.Output = lastResult?.Output ?? context.CurrentInput;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex;
            context.Error = ex;
        }
        finally
        {
            totalStopwatch.Stop();
            result.TotalExecutionTime = totalStopwatch.Elapsed;
        }

        return result;
    }

    private async Task<bool> ExecuteStepWithRetryAsync(
        PipelineStep step,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        var maxAttempts = step.MaxRetries + 1;

        while (attempts < maxAttempts)
        {
            try
            {
                attempts++;

                // Execute the step
                var success = await ExecuteStepAsync(step, context, cancellationToken);

                if (success)
                {
                    return true;
                }

                // If not successful and retries remain, wait before retry
                if (attempts < maxAttempts)
                {
                    await Task.Delay(step.RetryDelay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                context.Error = ex;

                if (attempts >= maxAttempts)
                {
                    // Add failed step result
                    context.AddStepResult(new PipelineStepResult
                    {
                        AgentName = step.AgentName,
                        Input = context.CurrentInput,
                        Output = string.Empty,
                        Success = false,
                        Error = ex,
                        ExecutionTime = TimeSpan.Zero
                    });

                    return false;
                }

                // Wait before retry
                if (attempts < maxAttempts)
                {
                    await Task.Delay(step.RetryDelay, cancellationToken);
                }
            }
        }

        return false;
    }

    private async Task<bool> ExecuteStepAsync(
        PipelineStep step,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        var stepStopwatch = Stopwatch.StartNew();

        try
        {
            // Transform input if transformer is provided
            var input = step.InputTransformer != null
                ? step.InputTransformer(context)
                : context.CurrentInput;

            // Apply timeout if specified
            var executeTask = _orchestrator.ProcessAsync(input, step.AgentName, cancellationToken);

            string output;
            if (step.Timeout.HasValue)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(step.Timeout.Value);

                output = await executeTask.WaitAsync(timeoutCts.Token);
            }
            else
            {
                output = await executeTask;
            }

            // Transform output if transformer is provided
            if (step.OutputTransformer != null)
            {
                output = step.OutputTransformer(context, output);
            }

            stepStopwatch.Stop();

            // Add successful step result
            context.AddStepResult(new PipelineStepResult
            {
                AgentName = step.AgentName,
                Input = input,
                Output = output,
                Success = true,
                ExecutionTime = stepStopwatch.Elapsed,
                Metadata = new Dictionary<string, object>(step.Metadata)
            });

            return true;
        }
        catch (Exception ex)
        {
            stepStopwatch.Stop();

            context.AddStepResult(new PipelineStepResult
            {
                AgentName = step.AgentName,
                Input = context.CurrentInput,
                Output = string.Empty,
                Success = false,
                Error = ex,
                ExecutionTime = stepStopwatch.Elapsed
            });

            throw;
        }
    }

    private async Task<bool> ExecuteParallelStepAsync(
        ParallelPipelineStep step,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        var stepStopwatch = Stopwatch.StartNew();

        try
        {
            // Transform input if transformer is provided
            var input = step.InputTransformer != null
                ? step.InputTransformer(context)
                : context.CurrentInput;

            // Execute agents in parallel with options
            var parallelResult = await ExecuteAgentsInParallelAsync(
                step.AgentNames,
                input,
                step.ExecutionOptions,
                cancellationToken);

            // Check failure policy
            var failurePolicyMet = CheckFailurePolicy(parallelResult, step.ExecutionOptions.FailurePolicy);

            if (!failurePolicyMet)
            {
                context.Error = new InvalidOperationException(
                    $"Parallel execution failed to meet {step.ExecutionOptions.FailurePolicy} policy. " +
                    $"Successful: {parallelResult.SuccessfulAgents}/{parallelResult.IndividualResults.Count}");

                // Add failed parallel step result
                var failedOutput = $"Parallel execution failed: {parallelResult.SuccessfulAgents}/{parallelResult.IndividualResults.Count} agents succeeded";
                context.AddStepResult(new PipelineStepResult
                {
                    AgentName = $"Parallel({string.Join(",", step.AgentNames)})",
                    Input = input,
                    Output = failedOutput,
                    Success = false,
                    Error = context.Error,
                    ExecutionTime = stepStopwatch.Elapsed
                });

                return false;
            }

            // Aggregate results using collaboration strategy
            string finalOutput;
            if (step.CollaborationStrategy != null)
            {
                var successfulResults = parallelResult.IndividualResults
                    .Where(r => r.Success)
                    .ToList();

                parallelResult.AggregatedResult = await step.CollaborationStrategy.AggregateAsync(
                    successfulResults,
                    context,
                    cancellationToken);

                finalOutput = parallelResult.AggregatedResult.Output;
            }
            else
            {
                // Default: concatenate all successful results
                finalOutput = string.Join("\n\n---\n\n",
                    parallelResult.IndividualResults
                        .Where(r => r.Success)
                        .Select(r => $"[{r.AgentName}]\n{r.Output}"));
            }

            // Transform output if transformer is provided
            if (step.OutputTransformer != null)
            {
                finalOutput = step.OutputTransformer(context, finalOutput);
            }

            parallelResult.Output = finalOutput;
            stepStopwatch.Stop();
            parallelResult.TotalExecutionTime = stepStopwatch.Elapsed;

            // Add successful parallel step result
            context.AddStepResult(new PipelineStepResult
            {
                AgentName = $"Parallel({string.Join(",", step.AgentNames)})",
                Input = input,
                Output = finalOutput,
                Success = true,
                ExecutionTime = stepStopwatch.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["parallelResult"] = parallelResult,
                    ["successfulAgents"] = parallelResult.SuccessfulAgents,
                    ["failedAgents"] = parallelResult.FailedAgents,
                    ["strategy"] = step.CollaborationStrategy?.Name ?? "Concatenate"
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            stepStopwatch.Stop();

            context.Error = ex;
            context.AddStepResult(new PipelineStepResult
            {
                AgentName = $"Parallel({string.Join(",", step.AgentNames)})",
                Input = context.CurrentInput,
                Output = string.Empty,
                Success = false,
                Error = ex,
                ExecutionTime = stepStopwatch.Elapsed
            });

            throw;
        }
    }

    private async Task<ParallelStepResult> ExecuteAgentsInParallelAsync(
        List<string> agentNames,
        string input,
        ParallelExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var result = new ParallelStepResult
        {
            AgentNames = agentNames,
            IndividualResults = new List<PipelineStepResult>(),
            Output = string.Empty // Will be set after aggregation
        };

        // Apply MaxDegreeOfParallelism if specified
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken
        };

        if (options.MaxDegreeOfParallelism.HasValue)
        {
            parallelOptions.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism.Value;
        }

        // Apply overall timeout if specified
        using var timeoutCts = options.Timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeoutCts != null)
        {
            timeoutCts.CancelAfter(options.Timeout.Value);
        }

        var effectiveCancellationToken = timeoutCts?.Token ?? cancellationToken;

        // Execute agents in parallel
        var tasks = agentNames.Select(agentName =>
            ExecuteSingleAgentAsync(agentName, input, options, effectiveCancellationToken));

        var individualResults = await Task.WhenAll(tasks);

        result.IndividualResults = individualResults.ToList();
        result.SuccessfulAgents = individualResults.Count(r => r.Success);
        result.FailedAgents = individualResults.Count(r => !r.Success);
        result.Success = result.SuccessfulAgents > 0;

        if (individualResults.Length > 0)
        {
            result.MaxExecutionTime = individualResults.Max(r => r.ExecutionTime);
            result.MinExecutionTime = individualResults
                .Where(r => r.Success)
                .DefaultIfEmpty(individualResults.First())
                .Min(r => r.ExecutionTime);
        }

        return result;
    }

    private async Task<PipelineStepResult> ExecuteSingleAgentAsync(
        string agentName,
        string input,
        ParallelExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        var maxAttempts = options.RetryFailedAgents ? options.MaxRetries + 1 : 1;

        while (attempts < maxAttempts)
        {
            try
            {
                attempts++;

                // Apply per-agent timeout if specified
                Task<string> executeTask = _orchestrator.ProcessAsync(input, agentName, cancellationToken);

                string output;
                if (options.PerAgentTimeout.HasValue)
                {
                    using var agentTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    agentTimeoutCts.CancelAfter(options.PerAgentTimeout.Value);

                    output = await executeTask.WaitAsync(agentTimeoutCts.Token);
                }
                else
                {
                    output = await executeTask;
                }

                stopwatch.Stop();

                return new PipelineStepResult
                {
                    AgentName = agentName,
                    Input = input,
                    Output = output,
                    Success = true,
                    ExecutionTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                if (attempts >= maxAttempts)
                {
                    stopwatch.Stop();

                    // If not continuing on failure, propagate exception
                    if (!options.ContinueOnFailure)
                    {
                        throw;
                    }

                    // Return failed result
                    return new PipelineStepResult
                    {
                        AgentName = agentName,
                        Input = input,
                        Output = string.Empty,
                        Success = false,
                        Error = ex,
                        ExecutionTime = stopwatch.Elapsed
                    };
                }

                // Wait before retry
                await Task.Delay(options.RetryDelay, cancellationToken);
            }
        }

        // Should not reach here
        stopwatch.Stop();
        return new PipelineStepResult
        {
            AgentName = agentName,
            Input = input,
            Output = string.Empty,
            Success = false,
            ExecutionTime = stopwatch.Elapsed
        };
    }

    private bool CheckFailurePolicy(ParallelStepResult result, ParallelFailurePolicy policy)
    {
        var totalAgents = result.IndividualResults.Count;
        var successfulAgents = result.SuccessfulAgents;

        return policy switch
        {
            ParallelFailurePolicy.RequireAll => successfulAgents == totalAgents,
            ParallelFailurePolicy.RequireMajority => successfulAgents > (totalAgents / 2.0),
            ParallelFailurePolicy.BestEffort => successfulAgents >= 1,
            ParallelFailurePolicy.FirstSuccess => successfulAgents >= 1,
            ParallelFailurePolicy.RequireMinimum => successfulAgents >= (result.Metadata.TryGetValue("minimumRequired", out var min) ? (int)min : 1),
            _ => successfulAgents >= 1
        };
    }
}
