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
        var service = new AssistantMessageService(
            new FakeIntentRouter(),
            new FakeRagAnswerService(),
            new FakeSessionManager(),
            new FakeTrackRecommendationService(),
            new FakeApplicationStatusQueryService());

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

    private sealed class FakeIntentRouter : IIntentRouter
    {
        public Task<IntentClassificationDto> ClassifyAsync(
            string userQuestion,
            List<ConversationTurnDto> context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new IntentClassificationDto
                {
                    PrimaryIntent = "knowledge",
                    Confidence = 0.95m,
                    RoutedTo = "knowledge_rag",
                    RequiresClarification = false,
                    Intents =
                    [
                        new DetectedIntentDto
                        {
                            Intent = "knowledge",
                            Confidence = 0.95m,
                            RoutedTo = "knowledge_rag"
                        }
                    ]
                });
        }

        public string GetClarificationMessage(string language)
        {
            return "Clarify";
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
        public ChatSessionDto CreateSession(string userId)
        {
            return new ChatSessionDto { SessionId = Guid.NewGuid().ToString(), UserId = userId };
        }

        public ChatSessionDto? GetSession(string sessionId)
        {
            return new ChatSessionDto { SessionId = sessionId, UserId = "user-1" };
        }

        public bool AddTurn(string sessionId, ConversationTurnDto turn)
        {
            return true;
        }

        public List<ConversationTurnDto> GetHistory(string sessionId, int lastN = 3)
        {
            return [];
        }

        public void EndSession(string sessionId)
        {
        }
    }

    private sealed class FakeTrackRecommendationService
        : ITrackRecommendationService
    {
        public Task<TrackRecommendationResultDto> RecommendAsync(
            RecommendationQuestionnaireDto questionnaire,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new TrackRecommendationResultDto
                {
                    Recommendations = [],
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

        public Task<ApplicationStatusDto?> GetCurrentApplicationStatusForApplicantAsync(
            string applicantUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ApplicationStatusDto?>(null);
        }
    }
}
