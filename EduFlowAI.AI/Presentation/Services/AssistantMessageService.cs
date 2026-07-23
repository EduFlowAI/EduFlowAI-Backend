using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Presentation.DTOs;
using EduFlowAI.AI.Presentation.Interfaces;

namespace EduFlowAI.AI.Presentation.Services;

public sealed class AssistantMessageService : IAssistantMessageService
{
    private readonly IIntentRouter _intentRouter;
    private readonly IRagAnswerService _ragAnswerService;
    private readonly ISessionManager _sessionManager;
    private readonly IChatRecommendationService _chatRecommendationService;

    public AssistantMessageService(
        IIntentRouter intentRouter,
        IRagAnswerService ragAnswerService,
        ISessionManager sessionManager,
        IChatRecommendationService chatRecommendationService)
    {
        _intentRouter = intentRouter;
        _ragAnswerService = ragAnswerService;
        _sessionManager = sessionManager;
        _chatRecommendationService = chatRecommendationService;
    }

    public async Task<AssistantResponse> HandleAsync(
        AssistantMessageRequest request,
        Guid userId,
        bool isAuthenticated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var language = NormalizeLanguage(request.Language);

        var session = GetOrCreateSession(
            request.SessionId,
            isAuthenticated ? userId : Guid.Empty);

        var history = _sessionManager.GetHistory(
            session.SessionId,
            lastN: 3);

        var classification = await _intentRouter.ClassifyAsync(
            request.Message,
            history,
            cancellationToken);

        if (classification.RequiresClarification)
        {
            return new AssistantResponse(
                session.SessionId,
                RequiresClarification: true,
                ClarificationMessage:
                    _intentRouter.GetClarificationMessage(language),
                Results: [],
                Timestamp: DateTimeOffset.UtcNow);
        }

        var detectedIntents = GetDetectedIntents(classification);
        var results = new List<AssistantResultDto>();

        foreach (var detectedIntent in detectedIntents)
        {
            var result = await DispatchAsync(
                detectedIntent.Intent,
                request.Message,
                language,
                isAuthenticated,
                cancellationToken);

            results.Add(result);
        }

        var combinedAnswer = string.Join(
            Environment.NewLine,
            results.Select(result => result.Content));

        _sessionManager.AddTurn(
            session.SessionId,
            new ConversationTurnDto
            {
                Question = request.Message,
                Intent = classification.PrimaryIntent,
                RoutedTo = classification.RoutedTo,
                Answer = combinedAnswer,
                Timestamp = DateTimeOffset.UtcNow
            });

        return new AssistantResponse(
            session.SessionId,
            RequiresClarification: false,
            ClarificationMessage: null,
            Results: results,
            Timestamp: DateTimeOffset.UtcNow);
    }

    private async Task<AssistantResultDto> DispatchAsync(
        string intent,
        string message,
        string language,
        bool isAuthenticated,
        CancellationToken cancellationToken)
    {
        return intent.ToLowerInvariant() switch
        {
            "knowledge" => await HandleKnowledgeAsync(
                message,
                language,
                cancellationToken),

            "recommendation" => await HandleRecommendationAsync(
                message,
                cancellationToken),

            "status" => HandleStatus(isAuthenticated),

            _ => CreateUnknownResult(language)
        };
    }

    private async Task<AssistantResultDto> HandleKnowledgeAsync(
        string message,
        string language,
        CancellationToken cancellationToken)
    {
        var answer = await _ragAnswerService.AnswerWithContextAsync(
            message,
            language,
            cancellationToken);

        return new AssistantResultDto(
            Intent: "knowledge",
            Title: language == "ar" ? "معلومات القبول" : "Admission Information",
            Content: answer.Answer,
            Sources: answer.Sources,
            Metadata: new Dictionary<string, object?>());
    }

    private async Task<AssistantResultDto> HandleRecommendationAsync(
        string message,
        CancellationToken cancellationToken)
    {
        var recommendation =
            await _chatRecommendationService.RecommendFromMessageAsync(
                message,
                cancellationToken);

        return new AssistantResultDto(
            Intent: "recommendation",
            Title: "Track Recommendation",
            Content: recommendation.Message,
            Sources: [],
            Metadata: new Dictionary<string, object?>
            {
                ["shouldUseQuestionnaire"] =
                    recommendation.ShouldUseQuestionnaire,

                ["recommendations"] =
                    recommendation.Recommendations
            });
    }

    private static AssistantResultDto HandleStatus(
        bool isAuthenticated)
    {
        if (!isAuthenticated)
        {
            return new AssistantResultDto(
                Intent: "status",
                Title: "Application Status",
                Content: "Please log in to view your application status.",
                Sources: [],
                Metadata: new Dictionary<string, object?>
                {
                    ["requiresLogin"] = true
                });
        }

        return new AssistantResultDto(
            Intent: "status",
            Title: "Application Status",
            Content:
                "Application status integration is not available yet.",
            Sources: [],
            Metadata: new Dictionary<string, object?>
            {
                ["integrationPending"] = true
            });
    }

    private static AssistantResultDto CreateUnknownResult(
        string language)
    {
        return new AssistantResultDto(
            Intent: "unknown",
            Title: language == "ar" ? "محتاج توضيح" : "Clarification Needed",
            Content: language == "ar"
                ? "اختر: سؤال عام، حالة الطلب، حالة المستندات، أو ترشيح مسار."
                : "Choose: general question, application status, document status, or track recommendation.",
            Sources: [],
            Metadata: new Dictionary<string, object?>());
    }

    private ChatSessionDto GetOrCreateSession(
        string? sessionId,
        Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingSession =
                _sessionManager.GetSession(sessionId);

            if (existingSession is not null)
            {
                return existingSession;
            }
        }

        return _sessionManager.CreateSession(userId);
    }

    private static IReadOnlyList<DetectedIntentDto> GetDetectedIntents(
        IntentClassificationDto classification)
    {
        if (classification.Intents.Count > 0)
        {
            return classification.Intents;
        }

        return
        [
            new DetectedIntentDto
            {
                Intent = classification.PrimaryIntent,
                Confidence = classification.Confidence,
                RoutedTo = classification.RoutedTo
            }
        ];
    }

    private static string NormalizeLanguage(string? language)
    {
        return string.Equals(
            language,
            "ar",
            StringComparison.OrdinalIgnoreCase)
                ? "ar"
                : "en";
    }
}