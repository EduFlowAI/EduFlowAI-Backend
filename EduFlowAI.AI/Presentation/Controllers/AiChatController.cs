using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.AI.Infrastructure.Services;
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
    private readonly IStatusAgent _statusAgent;
    private readonly IRecommendationsAgent _recommendationsAgent;
    private readonly KnowledgeBaseSyncState _syncState;

    public AiChatController(
        ISessionManager sessionManager,
        IIntentClassifier intentClassifier,
        AiChatService chatService,
        IStatusAgent statusAgent,
        IRecommendationsAgent recommendationsAgent,
        KnowledgeBaseSyncState syncState)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _statusAgent = statusAgent ?? throw new ArgumentNullException(nameof(statusAgent));
        _recommendationsAgent = recommendationsAgent ?? throw new ArgumentNullException(nameof(recommendationsAgent));
        _syncState = syncState ?? throw new ArgumentNullException(nameof(syncState));
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

        // Retrieval would return partial results while the knowledge base is being rebuilt.
        if (_syncState.IsSyncing)
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "The assistant is briefly unavailable while the knowledge base is being updated. Please try again shortly.");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            return BadRequest("Session not found or expired. Create a new session.");

        var history = _sessionManager.GetHistory(sessionId, lastN: 3);

        // Detect language from the ORIGINAL text: masking replaces digits with an English token,
        // which could otherwise flip an Arabic question to "en".
        var language = LanguageDetectionService.DetectLanguage(request.Question);

        // Everything sent outbound to Gemini uses the masked text.
        var sanitizedQuestion = InputSanitizerService.MaskSensitiveNumbers(request.Question);

        var classification = await _intentClassifier.ClassifyAsync(
            sanitizedQuestion,
            history,
            cancellationToken);

        // Too ambiguous to route confidently: ask rather than risk answering from the wrong agent.
        if (classification.RequiresClarification)
        {
            var clarification = ClarificationPrompt(language);

            _sessionManager.AddTurn(sessionId, new ConversationTurnDto
            {
                Question = sanitizedQuestion,
                Intent = classification.PrimaryIntent,
                RoutedTo = "clarification",
                Answer = clarification,
                Timestamp = DateTimeOffset.UtcNow
            });

            return Ok(new
            {
                answer = clarification,
                intent = classification.PrimaryIntent,
                confidence = classification.Confidence,
                requiresClarification = true,
                routedTo = "clarification",
                language
            });
        }

        // Unhandled failures (e.g. a Gemini outage) propagate to the global exception handler,
        // which returns a safe 502/500 ProblemDetails instead of leaking the exception message.
        string answer;
        var sources = new List<string>();

        switch (classification.RoutedTo)
        {
            case "knowledge_rag":
                var rag = await _chatService.AnswerWithContextAsync(sanitizedQuestion, language, cancellationToken);
                answer = rag.Answer;
                sources = rag.Sources; // documents the answer drew on
                break;
            case "status_agent":
                answer = await _statusAgent.GetResponseAsync(sanitizedQuestion, language, cancellationToken);
                break;
            case "recommendations_agent":
                answer = await _recommendationsAgent.GetResponseAsync(sanitizedQuestion, language, cancellationToken);
                break;
            default:
                answer = "Unable to process your question at this time.";
                break;
        }

        // Store the masked text: history is replayed to the classifier as context on later turns,
        // so keeping the raw value here would send it to Gemini anyway.
        var turn = new ConversationTurnDto
        {
            Question = sanitizedQuestion,
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
            requiresClarification = classification.RequiresClarification,
            routedTo = classification.RoutedTo,
            language,
            sources
        });
    }

    /// <summary>
    /// Diagnostic: classifies a message and reports where it would be routed, without creating a
    /// session or dispatching to an agent. One model call instead of two or three, which makes
    /// verifying routing accuracy cheap.
    /// </summary>
    [HttpPost("classify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Classify([FromBody] ChatQueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question cannot be empty");

        var language = LanguageDetectionService.DetectLanguage(request.Question);
        var sanitizedQuestion = InputSanitizerService.MaskSensitiveNumbers(request.Question);

        var classification = await _intentClassifier.ClassifyAsync(
            sanitizedQuestion,
            new List<ConversationTurnDto>(),
            cancellationToken);

        return Ok(new
        {
            intent = classification.PrimaryIntent,
            confidence = classification.Confidence,
            routedTo = classification.RequiresClarification ? "clarification" : classification.RoutedTo,
            requiresClarification = classification.RequiresClarification,
            language
        });
    }

    // Shown when the router is not confident enough to pick an intent.
    private static string ClarificationPrompt(string language) => language == "ar"
        ? "لم أتأكد تماماً من سؤالك. هل تسأل عن معلومات عامة عن المعهد والمسارات، أم عن حالة طلبك أنت، أم تريد ترشيح المسار المناسب لك؟"
        : "I'm not sure I understood. Are you asking about general information on ITI and its tracks, about the status of your own application, or for a recommendation on which track suits you?";

    [HttpPost("session/{sessionId}/end")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult EndSession(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            return BadRequest("Session not found or expired. Create a new session.");

        _sessionManager.EndSession(sessionId);
        return Ok(new { message = "Session ended successfully" });
    }

    [HttpGet("session/{sessionId}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSessionHistory(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            return BadRequest("Session not found or expired. Create a new session.");

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