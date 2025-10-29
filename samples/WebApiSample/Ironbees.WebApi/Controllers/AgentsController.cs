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
    private readonly IConversationManager _conversationManager;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        IAgentOrchestrator orchestrator,
        IConversationManager conversationManager,
        ILogger<AgentsController> logger)
    {
        _orchestrator = orchestrator;
        _conversationManager = conversationManager;
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

    // Conversation Management Endpoints

    /// <summary>
    /// Chat with conversation history context
    /// </summary>
    /// <param name="request">Conversation chat request</param>
    /// <returns>Response with session information</returns>
    [HttpPost("conversation/chat")]
    [ProducesResponseType(typeof(ConversationChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationChatResponse>> ConversationChat(
        [FromBody] ConversationChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        var sw = Stopwatch.StartNew();

        try
        {
            // Get or create session
            ConversationSession session;
            bool isNewSession = false;

            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                session = await _conversationManager.CreateSessionAsync();
                isNewSession = true;
                _logger.LogInformation("Created new conversation session: {SessionId}", session.SessionId);
            }
            else
            {
                session = await _conversationManager.GetSessionAsync(request.SessionId);
                if (session == null)
                {
                    return BadRequest(new { error = $"Session '{request.SessionId}' not found" });
                }
            }

            // Add user message to session
            await _conversationManager.AddMessageAsync(session.SessionId, new ConversationMessage
            {
                Role = "user",
                Content = request.Message
            });

            // Get conversation context
            var previousMessages = await _conversationManager.GetMessagesAsync(
                session.SessionId,
                request.MaxContextMessages);

            // Build context string from previous messages (excluding the just-added user message)
            var contextMessages = previousMessages.Take(previousMessages.Count - 1);
            var contextBuilder = new System.Text.StringBuilder();

            if (contextMessages.Any())
            {
                contextBuilder.AppendLine("Previous conversation:");
                foreach (var msg in contextMessages)
                {
                    contextBuilder.AppendLine($"{msg.Role}: {msg.Content}");
                }
                contextBuilder.AppendLine();
            }

            // Combine context with current message
            var fullInput = contextMessages.Any()
                ? $"{contextBuilder}{request.Message}"
                : request.Message;

            // Process with agent
            string response;
            string agentUsed;
            double? confidence = null;

            if (!string.IsNullOrWhiteSpace(request.AgentName))
            {
                response = await _orchestrator.ProcessAsync(fullInput, request.AgentName);
                agentUsed = request.AgentName;
            }
            else
            {
                var selection = await _orchestrator.SelectAgentAsync(request.Message);
                if (selection.SelectedAgent == null)
                {
                    return BadRequest(new { error = "No suitable agent found" });
                }

                response = await _orchestrator.ProcessAsync(fullInput, selection.SelectedAgent.Name);
                agentUsed = selection.SelectedAgent.Name;
                confidence = selection.ConfidenceScore;
            }

            // Add assistant response to session
            await _conversationManager.AddMessageAsync(session.SessionId, new ConversationMessage
            {
                Role = "assistant",
                Content = response,
                AgentName = agentUsed
            });

            sw.Stop();

            return Ok(new ConversationChatResponse
            {
                SessionId = session.SessionId,
                Message = response,
                AgentName = agentUsed,
                ConfidenceScore = confidence,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                MessageCount = session.Messages.Count,
                IsNewSession = isNewSession
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing conversation chat");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get conversation session information
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Session information</returns>
    [HttpGet("conversation/{sessionId}")]
    [ProducesResponseType(typeof(ConversationSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationSession>> GetSession(string sessionId)
    {
        var session = await _conversationManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            return NotFound(new { error = $"Session '{sessionId}' not found" });
        }

        return Ok(session);
    }

    /// <summary>
    /// Get message history for a session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="maxMessages">Maximum number of recent messages</param>
    /// <returns>List of messages</returns>
    [HttpGet("conversation/{sessionId}/messages")]
    [ProducesResponseType(typeof(List<ConversationMessage>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ConversationMessage>>> GetMessages(
        string sessionId,
        [FromQuery] int? maxMessages = null)
    {
        try
        {
            var messages = await _conversationManager.GetMessagesAsync(sessionId, maxMessages);
            return Ok(messages);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all active conversation sessions
    /// </summary>
    /// <returns>List of session IDs</returns>
    [HttpGet("conversation")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> ListSessions()
    {
        var sessions = await _conversationManager.GetActiveSessionsAsync();
        return Ok(sessions);
    }

    /// <summary>
    /// Delete a conversation session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Success status</returns>
    [HttpDelete("conversation/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteSession(string sessionId)
    {
        await _conversationManager.DeleteSessionAsync(sessionId);
        return Ok(new { message = $"Session '{sessionId}' deleted" });
    }

    /// <summary>
    /// Clear message history in a session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Success status</returns>
    [HttpPost("conversation/{sessionId}/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ClearSession(string sessionId)
    {
        try
        {
            await _conversationManager.ClearSessionAsync(sessionId);
            return Ok(new { message = $"Session '{sessionId}' cleared" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
