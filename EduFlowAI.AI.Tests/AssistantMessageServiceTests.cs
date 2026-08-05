using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Presentation.DTOs;
using EduFlowAI.AI.Presentation.Services;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Shared.Kernel.Common;
using EduFlowAI.Shared.Kernel.Common.Pagination;

namespace EduFlowAI.AI.Tests;

public class AssistantMessageServiceTests
{
    [Fact]
    public async Task KnowledgeIntent_ReturnsRagResult()
    {
        var service = CreateService(
            intent: "knowledge",
            recommendationService:
                new FakeTrackRecommendationService());

        var response = await service.HandleAsync(
            new AssistantMessageRequest(
                "What tracks are available?",
                null,
                "en"),
            Guid.Empty,
            false);

        var result = Assert.Single(response.Results);

        Assert.Equal("knowledge", result.Intent);
        Assert.Equal("Admission Information", result.Title);
        Assert.Equal("Test answer", result.Content);
        Assert.Contains("Test Source", result.Sources);
        Assert.False(response.RequiresClarification);
    }

    [Fact]
    public async Task DocumentStatusIntent_AuthenticatedUser_ReturnsNarratedDocumentStatus()
    {
        var service = CreateService(
            intent: "document_status",
            recommendationService:
                new FakeTrackRecommendationService());

        var response = await service.HandleAsync(
            new AssistantMessageRequest(
                "Did you receive my documents?",
                null,
                "en"),
            Guid.NewGuid(),
            isAuthenticated: true);

        var result = Assert.Single(response.Results);

        Assert.Equal("document_status", result.Intent);
        Assert.Equal("Document Status", result.Title);
        Assert.Equal("Test document status", result.Content);
    }

    [Fact]
    public async Task RecommendWithAvailableData_ReturnsCompletedWithoutAskingQuestions()
    {
        var recommendationService =
            new FakeTrackRecommendationService();

        var service = CreateService(
            intent: "recommendation",
            recommendationService);

        var request = new AssistantMessageRequest(
            Message: "Recommend based on what you know.",
            SessionId: null,
            Language: "en",
            Recommendation:
                new RecommendationQuestionnaireProgressDto
                {
                    Interests = ["Backend Development"]
                },
            RecommendWithAvailableData: true);

        var response = await service.HandleAsync(
            request,
            Guid.Empty,
            false);

        var result = Assert.Single(response.Results);

        Assert.Equal("recommendation", result.Intent);
        Assert.Equal("Recommended Tracks", result.Title);
        Assert.Equal("completed", result.Metadata["state"]);
        Assert.Equal(true, result.Metadata["basedOnAvailableData"]);
        Assert.Equal(true, result.Metadata["advisory"]);

        Assert.NotNull(recommendationService.LastQuestionnaire);

        Assert.Contains(
            "Backend Development",
            recommendationService.LastQuestionnaire.Interests);

        Assert.Empty(
            recommendationService.LastQuestionnaire.CareerGoals);

        Assert.Empty(
            recommendationService.LastQuestionnaire.Skills);
    }

    [Fact]
    public async Task AuthenticatedUser_WithProfileMajor_DoesNotAskForMajor()
    {
        var service = CreateService(
            intent: "recommendation",
            recommendationService:
                new FakeTrackRecommendationService(),
            userContext:
                new RecommendationUserContextDto
                {
                    Major = "Computer Science",
                    Faculty = "Computers and Information",
                    University = "Test University",
                    DegreeLevel = "Bachelor",
                    GraduationYear = 2025,
                    CumulativeGrade = "VeryGood",
                    HasProfileData = true
                });

        var response = await service.HandleAsync(
            new AssistantMessageRequest(
                Message: "Recommend me a track.",
                SessionId: null,
                Language: "en",
                Recommendation: null,
                RecommendWithAvailableData: false),
            Guid.NewGuid(),
            true);

        var result = Assert.Single(response.Results);

        Assert.Equal("recommendation", result.Intent);
        Assert.Equal(
            "collecting_answers",
            result.Metadata["state"]);
        Assert.Equal(
            "careerGoals",
            result.Metadata["questionKey"]);
        Assert.Equal(1, result.Metadata["questionNumber"]);
        Assert.Equal(3, result.Metadata["totalQuestions"]);

        var missingContext =
            Assert.IsAssignableFrom<IReadOnlyList<string>>(
                result.Metadata["missingContext"]);

        Assert.DoesNotContain("major", missingContext);
    }

    [Fact]
    public async Task AuthenticatedUser_WithoutProfileMajor_AsksForMajorFirst()
    {
        var service = CreateService(
            intent: "recommendation",
            recommendationService:
                new FakeTrackRecommendationService(),
            userContext:
                new RecommendationUserContextDto
                {
                    HasProfileData = false
                });

        var response = await service.HandleAsync(
            new AssistantMessageRequest(
                Message: "Recommend me a track.",
                SessionId: null,
                Language: "en",
                Recommendation: null,
                RecommendWithAvailableData: false),
            Guid.NewGuid(),
            true);

        var result = Assert.Single(response.Results);

        Assert.Equal("recommendation", result.Intent);
        Assert.Equal(
            "collecting_answers",
            result.Metadata["state"]);
        Assert.Equal("major", result.Metadata["questionKey"]);
        Assert.Equal(1, result.Metadata["questionNumber"]);
        Assert.Equal(4, result.Metadata["totalQuestions"]);

        var missingContext =
            Assert.IsAssignableFrom<IReadOnlyList<string>>(
                result.Metadata["missingContext"]);

        Assert.Contains("major", missingContext);
    }

    [Fact]
    public async Task AuthenticatedUser_ProfileData_IsIncludedInRecommendationQuestionnaire()
    {
        var recommendationService =
            new FakeTrackRecommendationService();

        var service = CreateService(
            intent: "recommendation",
            recommendationService,
            new RecommendationUserContextDto
            {
                Major = "Computer Science",
                Faculty = "Computers and Information",
                University = "Test University",
                DegreeLevel = "Bachelor",
                GraduationYear = 2025,
                CumulativeGrade = "VeryGood",
                HasProfileData = true
            });

        var response = await service.HandleAsync(
            new AssistantMessageRequest(
                Message: "Recommend based on my available information.",
                SessionId: null,
                Language: "en",
                Recommendation: null,
                RecommendWithAvailableData: true),
            Guid.NewGuid(),
            true);

        var result = Assert.Single(response.Results);

        Assert.Equal("completed", result.Metadata["state"]);

        var questionnaire =
            Assert.IsType<RecommendationQuestionnaireDto>(
                recommendationService.LastQuestionnaire);

        Assert.Equal(
            "Computer Science",
            questionnaire.Major);

        Assert.NotNull(questionnaire.AdditionalContext);

        Assert.Contains(
            "Faculty: Computers and Information",
            questionnaire.AdditionalContext);

        Assert.Contains(
            "University: Test University",
            questionnaire.AdditionalContext);

        Assert.Contains(
            "Degree level: Bachelor",
            questionnaire.AdditionalContext);

        Assert.Contains(
            "Graduation year: 2025",
            questionnaire.AdditionalContext);

        Assert.Contains(
            "Cumulative grade: VeryGood",
            questionnaire.AdditionalContext);
    }

    private static AssistantMessageService CreateService(
        string intent,
        FakeTrackRecommendationService recommendationService,
        RecommendationUserContextDto? userContext = null)
    {
        return new AssistantMessageService(
            new FakeIntentRouter(intent),
            new FakeRagAnswerService(),
            new FakeSessionManager(),
            recommendationService,
            new FakeApplicationStatusQueryService(),
            new FakeApplicantDocumentService(),
            new FakeDocumentStatusNarrator(),
            new FakeRecommendationUserContextService(
                userContext ??
                new RecommendationUserContextDto()));
    }

    private sealed class FakeIntentRouter(
        string intent = "knowledge")
        : IIntentRouter
    {
        public Task<IntentClassificationDto> ClassifyAsync(
            string userQuestion,
            List<ConversationTurnDto> context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new IntentClassificationDto
                {
                    PrimaryIntent = intent,
                    Confidence = 0.95m,
                    RoutedTo = GetRoute(intent),
                    RequiresClarification = false,
                    Intents =
                    [
                        new DetectedIntentDto
                        {
                            Intent = intent,
                            Confidence = 0.95m,
                            RoutedTo = GetRoute(intent)
                        }
                    ]
                });
        }

        public string GetClarificationMessage(string language)
        {
            return "Clarify";
        }

        private static string GetRoute(string intent)
        {
            return intent switch
            {
                "knowledge" => "knowledge_rag",
                "recommendation" => "recommendations_agent",
                "status" or "application_status" => "status_service",
                "document_status" => "document_status_service",
                _ => "unknown"
            };
        }
    }

    private sealed class FakeRagAnswerService : IRagAnswerService
    {
        public Task<RagAnswerDto> AnswerWithContextAsync(
            string userQuestion,
            IReadOnlyList<ConversationTurnDto> context,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RagAnswerDto
                {
                    Answer = "Test answer",
                    Sources = ["Test Source"]
                });
        }
    }

    private sealed class FakeSessionManager : ISessionManager
    {
        private readonly ChatSessionDto _session = new()
        {
            SessionId = "test-session",
            UserId = string.Empty
        };

        public ChatSessionDto CreateSession(string userId)
        {
            return _session;
        }

        public ChatSessionDto? GetSession(string sessionId)
        {
            return _session;
        }

        public bool AddTurn(
            string sessionId,
            ConversationTurnDto turn)
        {
            _session.History.Add(turn);
            return true;
        }

        public List<ConversationTurnDto> GetHistory(
            string sessionId,
            int lastN = 3)
        {
            return _session.History
                .TakeLast(lastN)
                .ToList();
        }

        public void EndSession(string sessionId)
        {
        }
    }

    private sealed class FakeTrackRecommendationService
        : ITrackRecommendationService
    {
        public RecommendationQuestionnaireDto? LastQuestionnaire
        {
            get;
            private set;
        }

        public Task<TrackRecommendationResultDto> RecommendAsync(
            RecommendationQuestionnaireDto questionnaire,
            CancellationToken cancellationToken = default)
        {
            LastQuestionnaire = questionnaire;

            return Task.FromResult(
                new TrackRecommendationResultDto
                {
                    Recommendations =
                    [
                        new RecommendedTrackResultDto
                        {
                            TrackId = Guid.NewGuid(),
                            TrackName = ".NET Development",
                            Rank = 1,
                            Reason =
                                "Matches the available backend-development interest."
                        }
                    ],
                    UsedFallback = false,
                    AdvisoryNotice =
                        "These recommendations are advisory only."
                });
        }
    }

    private sealed class FakeApplicationStatusQueryService
        : IApplicationStatusQueryService
    {
        public Task<ApplicationDetailsDto?> GetApplicationDetailsAsync(Guid applicationId, string applicantUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ApplicationDetailsDto?>(null);
        }

        public Task<ApplicationStatusDto?> GetApplicationStatusAsync(
            Guid applicationId)
        {
            return Task.FromResult<ApplicationStatusDto?>(null);
        }

        public Task<ApplicationStatusDto?>
            GetCurrentApplicationStatusForApplicantAsync(
                string applicantUserId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ApplicationStatusDto?>(null);
        }

        public Task<ApplicationDashboardSummaryDto?> GetDashboardSummaryAsync(Guid applicationId)
        {
            return Task.FromResult<ApplicationDashboardSummaryDto?>(null);
        }

        public Task<EligibilityDetailsDto?> GetEligibilityDetailsAsync(Guid applicationId, string applicantUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<EligibilityDetailsDto?>(null);
        }

        public Task<PaginatedResult<ApplicationListDto>> GetMyApplicationsAsync(string applicantUserId, QueryParameters queryParams, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PaginatedResult<ApplicationListDto>>(new PaginatedResult<ApplicationListDto>());
        }
    }

    private sealed class FakeApplicantDocumentService
        : IApplicantDocumentService
    {
        public Task<Result<Guid>> UploadDocumentAsync(
            UploadDocumentDto dto,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<ApplicantDocumentDto>>>
            GetDocumentsByApplicationIdAsync(
                Guid applicationId,
                CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<FileDownloadDto>> DownloadDocumentAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<ApplicantDocumentDto>>>
            GetDocumentsForApplicantAsync(
                string userId,
                CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Result<IEnumerable<ApplicantDocumentDto>>.Success(
                    []));
        }

        public Task<Result<RequiredDocumentsDto>> GetRequiredDocumentTypesAsync(Guid applicationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeDocumentStatusNarrator
        : IDocumentStatusNarrator
    {
        public Task<string> NarrateAsync(
            IEnumerable<ApplicantDocumentDto> documents,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Test document status");
        }
    }

    private sealed class FakeRecommendationUserContextService(
        RecommendationUserContextDto context)
        : IRecommendationUserContextService
    {
        public Task<RecommendationUserContextDto> GetAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(context);
        }
    }
}
