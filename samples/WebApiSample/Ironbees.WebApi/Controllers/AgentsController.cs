using Ironbees.Core;
using Ironbees.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Ironbees.WebApi.Controllers;

/// <summary>
/// API endpoints for Ironbees agent interactions
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AgentsController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        IAgentOrchestrator orchestrator,
        ILogger<AgentsController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// List all available agents
    /// </summary>
    /// <returns>List of agent information</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<AgentInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<AgentInfo>> GetAgents()
    {
        var agentNames = _orchestrator.ListAgents();
        var agents = agentNames
            .Select(name =>
            {
                var agent = _orchestrator.GetAgent(name);
                return agent != null ? new AgentInfo
                {
                    Name = agent.Name,
                    Description = agent.Description,
                    Capabilities = agent.Config.Capabilities.ToList(),
                    Tags = agent.Config.Tags.ToList(),
                    Model = new ModelInfo
                    {
                        Deployment = agent.Config.Model?.Deployment,
                        Temperature = agent.Config.Model?.Temperature,
                        MaxTokens = agent.Config.Model?.MaxTokens
                    }
                } : null;
            })
            .Where(a => a != null)
            .Cast<AgentInfo>()
            .ToList();

        return Ok(agents);
    }

    /// <summary>
    /// Get information about a specific agent
    /// </summary>
    /// <param name="name">Agent name</param>
    /// <returns>Agent information</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(AgentInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AgentInfo> GetAgent(string name)
    {
        var agent = _orchestrator.GetAgent(name);
        if (agent == null)
        {
            return NotFound(new { error = $"Agent '{name}' not found" });
        }

        var info = new AgentInfo
        {
            Name = agent.Name,
            Description = agent.Description,
            Capabilities = agent.Config.Capabilities.ToList(),
            Tags = agent.Config.Tags.ToList(),
            Model = new ModelInfo
            {
                Deployment = agent.Config.Model?.Deployment,
                Temperature = agent.Config.Model?.Temperature,
                MaxTokens = agent.Config.Model?.MaxTokens
            }
        };

        return Ok(info);
    }

    /// <summary>
    /// Chat with an agent (automatic or explicit selection)
    /// </summary>
    /// <param name="request">Chat request</param>
    /// <returns>Chat response from the agent</returns>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        var sw = Stopwatch.StartNew();

        try
        {
            string response;
            string agentUsed;
            double? confidence = null;

            if (!string.IsNullOrWhiteSpace(request.AgentName))
            {
                // Explicit agent selection
                _logger.LogInformation("Processing with explicit agent: {AgentName}", request.AgentName);
                response = await _orchestrator.ProcessAsync(request.Message, request.AgentName);
                agentUsed = request.AgentName;
            }
            else
            {
                // Automatic agent selection
                _logger.LogInformation("Processing with automatic agent selection");
                var selection = await _orchestrator.SelectAgentAsync(request.Message);

                if (selection.SelectedAgent == null)
                {
                    return BadRequest(new { error = "No suitable agent found", reason = selection.SelectionReason });
                }

                response = await _orchestrator.ProcessAsync(request.Message, selection.SelectedAgent.Name);
                agentUsed = selection.SelectedAgent.Name;
                confidence = selection.ConfidenceScore;

                _logger.LogInformation(
                    "Selected agent: {AgentName} (confidence: {Confidence:P0})",
                    agentUsed,
                    confidence);
            }

            sw.Stop();

            return Ok(new ChatResponse
            {
                Message = response,
                AgentName = agentUsed,
                ConfidenceScore = confidence,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            });
        }
        catch (AgentNotFoundException ex)
        {
            _logger.LogError(ex, "Agent not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Select the best agent for a given input without processing
    /// </summary>
    /// <param name="request">Selection request</param>
    /// <returns>Agent selection result with scores</returns>
    [HttpPost("select")]
    [ProducesResponseType(typeof(SelectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SelectionResponse>> SelectAgent([FromBody] SelectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new { error = "Input is required" });
        }

        try
        {
            var result = await _orchestrator.SelectAgentAsync(request.Input);

            var response = new SelectionResponse
            {
                SelectedAgent = result.SelectedAgent?.Name,
                ConfidenceScore = result.ConfidenceScore,
                SelectionReason = result.SelectionReason,
                AllScores = result.AllScores.Select(s => new AgentScoreInfo
                {
                    AgentName = s.Agent.Name,
                    Score = s.Score,
                    Reasons = s.Reasons.ToList()
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting agent");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>API health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> Health()
    {
        var agentCount = _orchestrator.ListAgents().Count;

        return Ok(new
        {
            status = "healthy",
            agentsLoaded = agentCount,
            timestamp = DateTime.UtcNow
        });
    }
}
