using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.AI.Presentation.Controllers;

[ApiController]
[Route("api/ai/chat")]
public class AiChatController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly IIntentClassifier _intentClassifier;
    private readonly AiChatService _chatService;

    public AiChatController(
        ISessionManager sessionManager,
        IIntentClassifier intentClassifier,
        AiChatService chatService)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    }

    [HttpPost("session/create")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CreateSession([FromBody] CreateSessionRequest request)
    {
        var session = _sessionManager.CreateSession(request.UserId);
        return Ok(new
        {
            sessionId = session.SessionId,
            createdAt = session.CreatedAt
        });
    }

    [HttpPost("ask")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AskQuestion(
        [FromBody] ChatQueryRequest request,
        [FromQuery] string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question cannot be empty");

        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest("Session ID is required");

        try
        {
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
                return BadRequest("Invalid session ID");

            var history = _sessionManager.GetHistory(sessionId, lastN: 3);

            var classification = await _intentClassifier.ClassifyAsync(
                request.Question,
                history,
                cancellationToken);

            var language = LanguageDetectionService.DetectLanguage(request.Question);

            string answer = classification.RoutedTo switch
            {
                "knowledge_rag" => await _chatService.AnswerWithContextAsync(request.Question, language, cancellationToken),
                "status_agent" => MockAgentService.GetStatusAgentResponse(request.Question),
                "recommendations_agent" => MockAgentService.GetRecommendationsAgentResponse(request.Question),
                _ => "Unable to process your question at this time."
            };

            var turn = new ConversationTurnDto
            {
                Question = request.Question,
                Intent = classification.PrimaryIntent,
                RoutedTo = classification.RoutedTo,
                Answer = answer,
                Timestamp = DateTimeOffset.UtcNow
            };
            _sessionManager.AddTurn(sessionId, turn);

            return Ok(new
            {
                answer,
                intent = classification.PrimaryIntent,
                confidence = classification.Confidence,
                routedTo = classification.RoutedTo,
                language
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpPost("session/{sessionId}/end")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult EndSession(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            return BadRequest("Invalid session ID");

        _sessionManager.EndSession(sessionId);
        return Ok(new { message = "Session ended successfully" });
    }

    [HttpGet("session/{sessionId}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSessionHistory(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            return BadRequest("Invalid session ID");

        var history = _sessionManager.GetHistory(sessionId, lastN: 100); // effectively "all turns"

        return Ok(new
        {
            sessionId,
            history = history.Select(t => new
            {
                question = t.Question,
                answer = t.Answer,
                intent = t.Intent,
                routedTo = t.RoutedTo,
                timestamp = t.Timestamp
            })
        });
    }
}

public class CreateSessionRequest
{
    public Guid UserId { get; set; }
}

public class ChatQueryRequest
{
    public string Question { get; set; } = string.Empty;
}