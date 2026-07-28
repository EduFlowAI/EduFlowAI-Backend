using EduFlowAI.Admission.Application.Interfaces.Services;
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
    private readonly IApplicationStatusQueryService
        _applicationStatusQueryService;

    public AssistantMessageService(
        IIntentRouter intentRouter,
        IRagAnswerService ragAnswerService,
        ISessionManager sessionManager,
        IChatRecommendationService chatRecommendationService,
        IApplicationStatusQueryService applicationStatusQueryService)
    {
        _intentRouter = intentRouter;
        _ragAnswerService = ragAnswerService;
        _sessionManager = sessionManager;
        _chatRecommendationService = chatRecommendationService;
        _applicationStatusQueryService = applicationStatusQueryService;
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
                history,
                language,
                userId,
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
        IReadOnlyList<ConversationTurnDto> history,
        string language,
        Guid userId,
        bool isAuthenticated,
        CancellationToken cancellationToken)
    {
        return intent.ToLowerInvariant() switch
        {
            "knowledge" => await HandleKnowledgeAsync(
                message,
                history,
                language,
                cancellationToken),

            "recommendation" => await HandleRecommendationAsync(
                message,
                language,
                cancellationToken),

            // Temporary backward compatibility until Khorshid
            // replaces "status" with "application_status".
            "status" or "application_status" =>
                await HandleApplicationStatusAsync(
                    userId,
                    isAuthenticated,
                    language,
                    cancellationToken),

            "document_status" =>
                CreateDocumentStatusPendingResult(
                    isAuthenticated,
                    language),

            _ => CreateUnknownResult(language)
        };
    }

    private async Task<AssistantResultDto> HandleKnowledgeAsync(
        string message,
        IReadOnlyList<ConversationTurnDto> history,
        string language,
        CancellationToken cancellationToken)
    {
        var answer = await _ragAnswerService.AnswerWithContextAsync(
            message,
            history,
            language,
            cancellationToken);

        return new AssistantResultDto(
            Intent: "knowledge",
            Title: language == "ar"
                ? "معلومات القبول"
                : "Admission Information",
            Content: answer.Answer,
            Sources: answer.Sources,
            Metadata: new Dictionary<string, object?>());
    }

    private async Task<AssistantResultDto> HandleRecommendationAsync(
        string message,
        string language,
        CancellationToken cancellationToken)
    {
        var recommendation =
            await _chatRecommendationService.RecommendFromMessageAsync(
                message,
                cancellationToken);

        return new AssistantResultDto(
            Intent: "recommendation",
            Title: language == "ar"
                ? "ترشيح المسار"
                : "Track Recommendation",
            Content: recommendation.Message,
            Sources: [],
            Metadata: new Dictionary<string, object?>
            {
                ["shouldUseQuestionnaire"] =
                    recommendation.ShouldUseQuestionnaire,

                ["recommendations"] =
                    recommendation.Recommendations,

                ["advisory"] = true
            });
    }

    private async Task<AssistantResultDto>
        HandleApplicationStatusAsync(
            Guid userId,
            bool isAuthenticated,
            string language,
            CancellationToken cancellationToken)
    {
        if (!isAuthenticated)
        {
            return new AssistantResultDto(
                Intent: "application_status",
                Title: language == "ar"
                    ? "حالة الطلب"
                    : "Application Status",
                Content: language == "ar"
                    ? "من فضلك سجل الدخول لعرض حالة طلبك."
                    : "Please log in to view your application status.",
                Sources: [],
                Metadata: new Dictionary<string, object?>
                {
                    ["requiresLogin"] = true
                });
        }

        var applicationStatus =
            await _applicationStatusQueryService
                .GetCurrentApplicationStatusForApplicantAsync(
                    userId.ToString(),
                    cancellationToken);

        if (applicationStatus is null)
        {
            return new AssistantResultDto(
                Intent: "application_status",
                Title: language == "ar"
                    ? "حالة الطلب"
                    : "Application Status",
                Content: language == "ar"
                    ? "لا يوجد طلب تقديم حالي مرتبط بحسابك."
                    : "No current application was found for your account.",
                Sources: [],
                Metadata: new Dictionary<string, object?>
                {
                    ["applicationFound"] = false
                });
        }

        var content =
            !string.IsNullOrWhiteSpace(applicationStatus.StatusMessage)
                ? applicationStatus.StatusMessage
                : language == "ar"
                    ? $"حالة طلبك الحالية هي: {applicationStatus.CurrentStatus}."
                    : $"Your current application status is: {applicationStatus.CurrentStatus}.";

        return new AssistantResultDto(
            Intent: "application_status",
            Title: language == "ar"
                ? "حالة الطلب"
                : "Application Status",
            Content: content,
            Sources: [],
            Metadata: new Dictionary<string, object?>
            {
                ["applicationFound"] = true,
                ["applicationId"] =
                    applicationStatus.ApplicationId,
                ["currentStatus"] =
                    applicationStatus.CurrentStatus,
                ["lastUpdatedAt"] =
                    applicationStatus.LastUpdatedAt,
                ["statusMessage"] =
                    applicationStatus.StatusMessage
            });
    }

    private static AssistantResultDto
        CreateDocumentStatusPendingResult(
            bool isAuthenticated,
            string language)
    {
        if (!isAuthenticated)
        {
            return new AssistantResultDto(
                Intent: "document_status",
                Title: language == "ar"
                    ? "حالة المستندات"
                    : "Document Status",
                Content: language == "ar"
                    ? "من فضلك سجل الدخول لعرض حالة مستنداتك."
                    : "Please log in to view your document status.",
                Sources: [],
                Metadata: new Dictionary<string, object?>
                {
                    ["requiresLogin"] = true
                });
        }

        return new AssistantResultDto(
            Intent: "document_status",
            Title: language == "ar"
                ? "حالة المستندات"
                : "Document Status",
            Content: language == "ar"
                ? "تكامل حالة المستندات قيد التحديث حالياً."
                : "Document status integration is currently being updated.",
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
            Title: language == "ar"
                ? "محتاج توضيح"
                : "Clarification Needed",
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

        return _sessionManager.CreateSession(userId.ToString());
    }

    private static IReadOnlyList<DetectedIntentDto>
        GetDetectedIntents(
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

    private static string NormalizeLanguage(
        string? language)
    {
        return string.Equals(
            language,
            "ar",
            StringComparison.OrdinalIgnoreCase)
                ? "ar"
                : "en";
    }
}