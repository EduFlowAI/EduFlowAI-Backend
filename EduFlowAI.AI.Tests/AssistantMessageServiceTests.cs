using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Presentation.DTOs;
using EduFlowAI.AI.Presentation.Services;

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

    private static AssistantMessageService CreateService(
        string intent,
        FakeTrackRecommendationService recommendationService)
    {
        return new AssistantMessageService(
            new FakeIntentRouter(intent),
            new FakeRagAnswerService(),
            new FakeSessionManager(),
            recommendationService,
            new FakeApplicationStatusQueryService());
    }

    private sealed class FakeIntentRouter(
        string intent)
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
                "status" => "status_service",
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
    }
}