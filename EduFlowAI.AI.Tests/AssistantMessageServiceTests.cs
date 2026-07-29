using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Presentation.DTOs;
using EduFlowAI.AI.Presentation.Services;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Shared.Kernel.Common;

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
            new FakeApplicationStatusQueryService(),
            new FakeApplicantDocumentService(),
            new FakeDocumentStatusNarrator());

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
        var service = new AssistantMessageService(
            new FakeIntentRouter("document_status"),
            new FakeRagAnswerService(),
            new FakeSessionManager(),
            new FakeTrackRecommendationService(),
            new FakeApplicationStatusQueryService(),
            new FakeApplicantDocumentService(),
            new FakeDocumentStatusNarrator());

        var response = await service.HandleAsync(
            new AssistantMessageRequest("did you receive my documents?", null, "en"),
            Guid.NewGuid(),
            isAuthenticated: true);

        var result = Assert.Single(response.Results);

        Assert.Equal("document_status", result.Intent);
        Assert.Equal("Document Status", result.Title);
        Assert.Equal("Test document status", result.Content); // produced by the narrator
    }

    private sealed class FakeIntentRouter : IIntentRouter
    {
        private readonly string _intent;

        public FakeIntentRouter(string intent = "knowledge") => _intent = intent;

        public Task<IntentClassificationDto> ClassifyAsync(
            string userQuestion,
            List<ConversationTurnDto> context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new IntentClassificationDto
                {
                    PrimaryIntent = _intent,
                    Confidence = 0.95m,
                    RoutedTo = _intent,
                    RequiresClarification = false,
                    Intents =
                    [
                        new DetectedIntentDto
                        {
                            Intent = _intent,
                            Confidence = 0.95m,
                            RoutedTo = _intent
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

        public Task<ApplicationStatusDto?>
            GetCurrentApplicationStatusForApplicantAsync(
                string applicantUserId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ApplicationStatusDto?>(null);
        }
    }

    private sealed class FakeApplicantDocumentService : IApplicantDocumentService
    {
        public Task<Result<Guid>> UploadDocumentAsync(
            UploadDocumentDto dto, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Result<IEnumerable<ApplicantDocumentDto>>> GetDocumentsByApplicationIdAsync(
            Guid applicationId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Result<FileDownloadDto>> DownloadDocumentAsync(
            Guid documentId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Result<IEnumerable<ApplicantDocumentDto>>> GetDocumentsForApplicantAsync(
            string userId, CancellationToken cancellationToken)
            => Task.FromResult(
                Result<IEnumerable<ApplicantDocumentDto>>.Success(
                    Enumerable.Empty<ApplicantDocumentDto>()));
    }

    private sealed class FakeDocumentStatusNarrator : IDocumentStatusNarrator
    {
        public Task<string> NarrateAsync(
            IEnumerable<ApplicantDocumentDto> documents,
            string language,
            CancellationToken cancellationToken = default)
            => Task.FromResult("Test document status");
    }
}