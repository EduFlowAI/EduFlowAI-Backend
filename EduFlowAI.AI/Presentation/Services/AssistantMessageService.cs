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
    private readonly ITrackRecommendationService
        _trackRecommendationService;
    private readonly IApplicationStatusQueryService
        _applicationStatusQueryService;
    private readonly IRecommendationUserContextService
        _recommendationUserContextService;

    public AssistantMessageService(
        IIntentRouter intentRouter,
        IRagAnswerService ragAnswerService,
        ISessionManager sessionManager,
        ITrackRecommendationService trackRecommendationService,
        IApplicationStatusQueryService applicationStatusQueryService,
        IRecommendationUserContextService recommendationUserContextService)
    {
        _intentRouter = intentRouter;
        _ragAnswerService = ragAnswerService;
        _sessionManager = sessionManager;
        _trackRecommendationService = trackRecommendationService;
        _applicationStatusQueryService = applicationStatusQueryService;
        _recommendationUserContextService =
            recommendationUserContextService;
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
                request.RecommendWithAvailableData,
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
        bool recommendWithAvailableData,
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
                recommendWithAvailableData,
                userId,
                isAuthenticated,
                language,
                cancellationToken),

            "status" => await HandleApplicationStatusAsync(
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
        bool recommendWithAvailableData,
        Guid userId,
        bool isAuthenticated,
        string language,
        CancellationToken cancellationToken)
    {
        var userContext = isAuthenticated
            ? await _recommendationUserContextService.GetAsync(
                userId,
                cancellationToken)
            : new RecommendationUserContextDto();

        var missingContext = GetMissingImportantContext(
            progress,
            userContext);

        if (!recommendWithAvailableData)
        {
            var missingQuestion = GetNextMissingQuestion(
                progress,
                userContext,
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
                        ["totalQuestions"] =
                            GetRecommendationQuestionCount(
                                progress,
                                userContext),
                        ["canRecommendNow"] = true,
                        ["missingContext"] = missingContext,
                        ["advisory"] = true
                    });
            }
        }

        var questionnaire = CreateQuestionnaire(
            progress,
            userContext);

        var result = await _trackRecommendationService.RecommendAsync(
            questionnaire,
            cancellationToken);

        var basedOnAvailableData =
            recommendWithAvailableData ||
            HasSkippedImportantFields(progress);

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
                    ["recommendations"] = result.Recommendations,
                    ["usedFallback"] = result.UsedFallback,
                    ["basedOnAvailableData"] = basedOnAvailableData,
                    ["missingContext"] = missingContext,
                    ["advisory"] = true,
                    ["advisoryNotice"] = result.AdvisoryNotice
                });
        }

        var content = basedOnAvailableData
            ? language == "ar"
                ? "بناءً على المعلومات المتاحة حالياً، هذه أنسب المسارات المقترحة لك."
                : "Based on the information currently available, these are the most suitable track recommendations."
            : language == "ar"
                ? "بناءً على معلوماتك، هذه أنسب المسارات المقترحة لك."
                : "Based on your information, these are the most suitable track recommendations.";

        return new AssistantResultDto(
            Intent: "recommendation",
            Title: language == "ar"
                ? "المسارات المقترحة"
                : "Recommended Tracks",
            Content: content,
            Sources: [],
            Metadata: new Dictionary<string, object?>
            {
                ["state"] = "completed",
                ["recommendations"] = result.Recommendations,
                ["usedFallback"] = result.UsedFallback,
                ["basedOnAvailableData"] = basedOnAvailableData,
                ["missingContext"] = missingContext,
                ["advisory"] = true,
                ["advisoryNotice"] = result.AdvisoryNotice
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

    private static RecommendationQuestionnaireDto
        CreateQuestionnaire(
            RecommendationQuestionnaireProgressDto? progress,
            RecommendationUserContextDto userContext)
    {
        return new RecommendationQuestionnaireDto
        {
            Major =
                NormalizeText(userContext.Major) ??
                NormalizeText(progress?.Major),

            TechnicalCourses =
                NormalizeValues(progress?.TechnicalCourses),

            Skills =
                NormalizeValues(progress?.Skills),

            Interests =
                NormalizeValues(progress?.Interests),

            PreferredActivities =
                NormalizeValues(progress?.PreferredActivities),

            CareerGoals =
                NormalizeValues(progress?.CareerGoals),

            AdditionalContext = CombineAdditionalContext(
                progress?.AdditionalContext,
                userContext)
        };
    }

    private static string? CombineAdditionalContext(
        string? chatContext,
        RecommendationUserContextDto userContext)
    {
        var values = new List<string>();

        if (!string.IsNullOrWhiteSpace(chatContext))
        {
            values.Add(chatContext.Trim());
        }

        if (userContext.HasProfileData)
        {
            if (!string.IsNullOrWhiteSpace(userContext.Faculty))
            {
                values.Add($"Faculty: {userContext.Faculty}");
            }

            if (!string.IsNullOrWhiteSpace(userContext.University))
            {
                values.Add($"University: {userContext.University}");
            }

            if (!string.IsNullOrWhiteSpace(userContext.DegreeLevel))
            {
                values.Add($"Degree level: {userContext.DegreeLevel}");
            }

            if (userContext.GraduationYear.HasValue)
            {
                values.Add(
                    $"Graduation year: {userContext.GraduationYear.Value}");
            }

            if (!string.IsNullOrWhiteSpace(
                    userContext.CumulativeGrade))
            {
                values.Add(
                    $"Cumulative grade: {userContext.CumulativeGrade}");
            }
        }

        return values.Count == 0
            ? null
            : string.Join("; ", values);
    }

    private static IReadOnlyCollection<string> NormalizeValues(
        IReadOnlyCollection<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static RecommendationQuestion?
        GetNextMissingQuestion(
            RecommendationQuestionnaireProgressDto? progress,
            RecommendationUserContextDto userContext,
            string language)
    {
        var majorIsMissing =
            IsEffectiveMajorMissing(progress, userContext);

        if (majorIsMissing &&
            !IsSkipped(progress, "major"))
        {
            return new RecommendationQuestion(
                "major",
                1,
                language == "ar"
                    ? "ما هو تخصصك الدراسي؟"
                    : "What is your academic major?");
        }

        var questionOffset = majorIsMissing ? 1 : 0;

        if (IsMissing(progress?.CareerGoals) &&
            !IsSkipped(progress, "careerGoals"))
        {
            return new RecommendationQuestion(
                "careerGoals",
                1 + questionOffset,
                language == "ar"
                    ? "ما هي أهدافك المهنية؟"
                    : "What are your career goals?");
        }

        if (IsMissing(progress?.Interests) &&
            !IsSkipped(progress, "interests"))
        {
            return new RecommendationQuestion(
                "interests",
                2 + questionOffset,
                language == "ar"
                    ? "ما هي المجالات التقنية التي تهتم بها أكثر؟"
                    : "Which technical areas interest you the most?");
        }

        if (IsMissing(progress?.Skills) &&
            !IsSkipped(progress, "skills"))
        {
            return new RecommendationQuestion(
                "skills",
                3 + questionOffset,
                language == "ar"
                    ? "ما هي المهارات التقنية التي تمتلكها؟"
                    : "What technical skills do you currently have?");
        }

        return null;
    }

    private static int GetRecommendationQuestionCount(
        RecommendationQuestionnaireProgressDto? progress,
        RecommendationUserContextDto userContext)
    {
        return IsEffectiveMajorMissing(progress, userContext) &&
               !IsSkipped(progress, "major")
            ? 4
            : 3;
    }

    private static bool IsEffectiveMajorMissing(
        RecommendationQuestionnaireProgressDto? progress,
        RecommendationUserContextDto userContext)
    {
        return string.IsNullOrWhiteSpace(userContext.Major) &&
               string.IsNullOrWhiteSpace(progress?.Major);
    }

    private static bool IsMissing(
        IReadOnlyCollection<string>? values)
    {
        return values is null ||
               values.Count == 0 ||
               values.All(string.IsNullOrWhiteSpace);
    }

    private static bool IsSkipped(
        RecommendationQuestionnaireProgressDto? progress,
        string fieldName)
    {
        return progress?.SkippedFields?.Contains(
            fieldName,
            StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool HasSkippedImportantFields(
        RecommendationQuestionnaireProgressDto? progress)
    {
        return IsSkipped(progress, "major") ||
               IsSkipped(progress, "careerGoals") ||
               IsSkipped(progress, "interests") ||
               IsSkipped(progress, "skills");
    }

    private static IReadOnlyList<string>
        GetMissingImportantContext(
            RecommendationQuestionnaireProgressDto? progress,
            RecommendationUserContextDto userContext)
    {
        var missingContext = new List<string>();

        if (IsEffectiveMajorMissing(progress, userContext) &&
            !IsSkipped(progress, "major"))
        {
            missingContext.Add("major");
        }

        if (IsMissing(progress?.CareerGoals) &&
            !IsSkipped(progress, "careerGoals"))
        {
            missingContext.Add("careerGoals");
        }

        if (IsMissing(progress?.Interests) &&
            !IsSkipped(progress, "interests"))
        {
            missingContext.Add("interests");
        }

        if (IsMissing(progress?.Skills) &&
            !IsSkipped(progress, "skills"))
        {
            missingContext.Add("skills");
        }

        return missingContext;
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
                ? "اختر: سؤال عام، حالة الطلب، أو ترشيح مسار."
                : "Choose: general question, application status, or track recommendation.",
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