using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Presentation.DTOs;
using EduFlowAI.AI.Presentation.Interfaces;
using EduFlowAI.Documents.Application.Interfaces;

namespace EduFlowAI.AI.Presentation.Services;

public sealed class AssistantMessageService : IAssistantMessageService
{
    private const int RecommendationQuestionCount = 6;

    private readonly IIntentRouter _intentRouter;
    private readonly IRagAnswerService _ragAnswerService;
    private readonly ISessionManager _sessionManager;
    private readonly ITrackRecommendationService
        _trackRecommendationService;
    private readonly IApplicationStatusQueryService
        _applicationStatusQueryService;
    private readonly IApplicantDocumentService
        _applicantDocumentService;
    private readonly IDocumentStatusNarrator
        _documentStatusNarrator;

    public AssistantMessageService(
        IIntentRouter intentRouter,
        IRagAnswerService ragAnswerService,
        ISessionManager sessionManager,
        ITrackRecommendationService trackRecommendationService,
        IApplicationStatusQueryService applicationStatusQueryService,
        IApplicantDocumentService applicantDocumentService,
        IDocumentStatusNarrator documentStatusNarrator)
    {
        _intentRouter = intentRouter;
        _ragAnswerService = ragAnswerService;
        _sessionManager = sessionManager;
        _trackRecommendationService = trackRecommendationService;
        _applicationStatusQueryService = applicationStatusQueryService;
        _applicantDocumentService = applicantDocumentService;
        _documentStatusNarrator = documentStatusNarrator;
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
                request.Recommendation,
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
        RecommendationQuestionnaireProgressDto?
            recommendationProgress,
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
                recommendationProgress,
                language,
                cancellationToken),

            // "status" kept as a transitional alias while clients migrate to "application_status".
            "status" or "application_status" => await HandleApplicationStatusAsync(
                userId,
                isAuthenticated,
                language,
                cancellationToken),

            "document_status" => await HandleDocumentStatusAsync(
                userId,
                isAuthenticated,
                language,
                cancellationToken),

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
        RecommendationQuestionnaireProgressDto? progress,
        string language,
        CancellationToken cancellationToken)
    {
        var missingQuestion = GetNextMissingQuestion(
            progress,
            language);

        if (missingQuestion is not null)
        {
            return new AssistantResultDto(
                Intent: "recommendation",
                Title: language == "ar"
                    ? "ترشيح المسار"
                    : "Track Recommendation",
                Content: missingQuestion.Question,
                Sources: [],
                Metadata: new Dictionary<string, object?>
                {
                    ["state"] = "collecting_answers",
                    ["questionKey"] = missingQuestion.Key,
                    ["questionNumber"] = missingQuestion.Number,
                    ["totalQuestions"] = RecommendationQuestionCount,
                    ["advisory"] = true
                });
        }

        var questionnaire = CreateQuestionnaire(progress!);

        var result = await _trackRecommendationService.RecommendAsync(
            questionnaire,
            cancellationToken);

        if (result.Recommendations.Count == 0)
        {
            return new AssistantResultDto(
                Intent: "recommendation",
                Title: language == "ar"
                    ? "ترشيح المسار"
                    : "Track Recommendation",
                Content: language == "ar"
                    ? "لا توجد مسارات متاحة حالياً يمكن ترشيحها."
                    : "No active offered tracks are currently available.",
                Sources: [],
                Metadata: new Dictionary<string, object?>
                {
                    ["state"] = "completed",
                    ["recommendations"] =
                        result.Recommendations,
                    ["usedFallback"] =
                        result.UsedFallback,
                    ["advisory"] = true,
                    ["advisoryNotice"] =
                        result.AdvisoryNotice
                });
        }

        return new AssistantResultDto(
            Intent: "recommendation",
            Title: language == "ar"
                ? "المسارات المقترحة"
                : "Recommended Tracks",
            Content: language == "ar"
                ? "بناءً على إجاباتك، هذه أنسب المسارات المقترحة لك."
                : "Based on your answers, these are the most suitable track recommendations.",
            Sources: [],
            Metadata: new Dictionary<string, object?>
            {
                ["state"] = "completed",
                ["recommendations"] =
                    result.Recommendations,
                ["usedFallback"] =
                    result.UsedFallback,
                ["advisory"] = true,
                ["advisoryNotice"] =
                    result.AdvisoryNotice
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
                Intent: "status",
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
                Intent: "status",
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
            !string.IsNullOrWhiteSpace(
                applicationStatus.StatusMessage)
                ? applicationStatus.StatusMessage
                : language == "ar"
                    ? $"حالة طلبك الحالية هي: {applicationStatus.CurrentStatus}."
                    : $"Your current application status is: {applicationStatus.CurrentStatus}.";

        return new AssistantResultDto(
            Intent: "status",
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

    private async Task<AssistantResultDto> HandleDocumentStatusAsync(
        Guid userId,
        bool isAuthenticated,
        string language,
        CancellationToken cancellationToken)
    {
        if (!isAuthenticated)
        {
            return DocumentStatusResult(
                language,
                language == "ar"
                    ? "من فضلك سجل الدخول لعرض حالة مستنداتك."
                    : "Please log in to view your document status.",
                new Dictionary<string, object?> { ["requiresLogin"] = true });
        }

        var documents =
            await _applicantDocumentService.GetDocumentsForApplicantAsync(
                userId.ToString(),
                cancellationToken);

        if (!documents.IsSuccess)
        {
            return DocumentStatusResult(
                language,
                language == "ar"
                    ? "تعذّر تحميل حالة مستنداتك حالياً. برجاء المحاولة مرة أخرى."
                    : "I couldn't load your document status right now. Please try again.",
                new Dictionary<string, object?> { ["documentsFound"] = false });
        }

        // The narrator handles the empty list, warm phrasing, and the model-down fallback internally.
        var content = await _documentStatusNarrator.NarrateAsync(
            documents.Data,
            language,
            cancellationToken);

        return DocumentStatusResult(
            language,
            content,
            new Dictionary<string, object?>
            {
                ["documentsFound"] = true,
                ["documents"] = documents.Data
            });
    }

    private static AssistantResultDto DocumentStatusResult(
        string language,
        string content,
        Dictionary<string, object?> metadata)
    {
        return new AssistantResultDto(
            Intent: "document_status",
            Title: language == "ar"
                ? "حالة المستندات"
                : "Document Status",
            Content: content,
            Sources: [],
            Metadata: metadata);
    }

    private static RecommendationQuestionnaireDto
        CreateQuestionnaire(
            RecommendationQuestionnaireProgressDto progress)
    {
        return new RecommendationQuestionnaireDto
        {
            Major = progress.Major!.Trim(),
            TechnicalCourses =
                NormalizeValues(progress.TechnicalCourses!),
            Skills =
                NormalizeValues(progress.Skills!),
            Interests =
                NormalizeValues(progress.Interests!),
            PreferredActivities =
                NormalizeValues(progress.PreferredActivities!),
            CareerGoals =
                NormalizeValues(progress.CareerGoals!),
            AdditionalContext =
                string.IsNullOrWhiteSpace(
                    progress.AdditionalContext)
                    ? null
                    : progress.AdditionalContext.Trim()
        };
    }

    private static IReadOnlyCollection<string> NormalizeValues(
        IReadOnlyCollection<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RecommendationQuestion?
        GetNextMissingQuestion(
            RecommendationQuestionnaireProgressDto? progress,
            string language)
    {
        if (progress is null ||
            string.IsNullOrWhiteSpace(progress.Major))
        {
            return new RecommendationQuestion(
                "major",
                1,
                language == "ar"
                    ? "ما هو تخصصك الدراسي؟"
                    : "What is your academic major?");
        }

        if (IsMissing(progress.TechnicalCourses))
        {
            return new RecommendationQuestion(
                "technicalCourses",
                2,
                language == "ar"
                    ? "ما هي المقررات أو الموضوعات التقنية التي درستها؟"
                    : "Which technical courses or topics have you studied?");
        }

        if (IsMissing(progress.Skills))
        {
            return new RecommendationQuestion(
                "skills",
                3,
                language == "ar"
                    ? "ما هي المهارات التقنية التي تمتلكها؟"
                    : "What technical skills do you currently have?");
        }

        if (IsMissing(progress.Interests))
        {
            return new RecommendationQuestion(
                "interests",
                4,
                language == "ar"
                    ? "ما هي المجالات التقنية التي تهتم بها أكثر؟"
                    : "Which technical areas interest you the most?");
        }

        if (IsMissing(progress.PreferredActivities))
        {
            return new RecommendationQuestion(
                "preferredActivities",
                5,
                language == "ar"
                    ? "ما نوع الأنشطة أو المهام التي تستمتع بها؟"
                    : "What kinds of activities or tasks do you enjoy?");
        }

        if (IsMissing(progress.CareerGoals))
        {
            return new RecommendationQuestion(
                "careerGoals",
                6,
                language == "ar"
                    ? "ما هي أهدافك المهنية؟"
                    : "What are your career goals?");
        }

        return null;
    }

    private static bool IsMissing(
        IReadOnlyCollection<string>? values)
    {
        return values is null ||
               values.Count == 0 ||
               values.All(string.IsNullOrWhiteSpace);
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

        return _sessionManager.CreateSession(
            userId.ToString());
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

    private sealed record RecommendationQuestion(
        string Key,
        int Number,
        string Question);
}