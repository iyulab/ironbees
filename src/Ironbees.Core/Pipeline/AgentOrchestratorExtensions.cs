namespace Ironbees.Core.Pipeline;

/// <summary>
/// Extension methods for IAgentOrchestrator to support pipeline creation
/// </summary>
public static class AgentOrchestratorExtensions
{
    /// <summary>
    /// Create a new pipeline builder
    /// </summary>
    /// <param name="orchestrator">Orchestrator instance</param>
    /// <param name="pipelineName">Name of the pipeline</param>
    /// <returns>Pipeline builder</returns>
    public static AgentPipelineBuilder CreatePipeline(
        this IAgentOrchestrator orchestrator,
        string pipelineName = "default")
    {
        return new AgentPipelineBuilder(pipelineName, orchestrator);
    }

    /// <summary>
    /// Create and execute a simple pipeline in one call
    /// </summary>
    /// <param name="orchestrator">Orchestrator instance</param>
    /// <param name="input">Input to the pipeline</param>
    /// <param name="agentNames">Names of agents to execute in sequence</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline result</returns>
    public static async Task<PipelineResult> ExecutePipelineAsync(
        this IAgentOrchestrator orchestrator,
        string input,
        string[] agentNames,
        CancellationToken cancellationToken = default)
    {
        var builder = orchestrator.CreatePipeline("quick-pipeline");

        foreach (var agentName in agentNames)
        {
            builder.AddAgent(agentName);
        }

        var pipeline = builder.Build();
        return await pipeline.ExecuteAsync(input, cancellationToken);
    }

    /// <summary>
    /// Create and execute a pipeline with configuration
    /// </summary>
    /// <param name="orchestrator">Orchestrator instance</param>
    /// <param name="input">Input to the pipeline</param>
    /// <param name="configure">Pipeline configuration action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline result</returns>
    public static async Task<PipelineResult> ExecutePipelineAsync(
        this IAgentOrchestrator orchestrator,
        string input,
        Action<AgentPipelineBuilder> configure,
        CancellationToken cancellationToken = default)
    {
        var builder = orchestrator.CreatePipeline("configured-pipeline");
        configure(builder);

        var pipeline = builder.Build();
        return await pipeline.ExecuteAsync(input, cancellationToken);
    }
}
