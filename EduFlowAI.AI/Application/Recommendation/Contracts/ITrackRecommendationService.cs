using EduFlowAI.AI.Application.Recommendation.Models;

namespace EduFlowAI.AI.Application.Recommendation.Contracts;

public interface ITrackRecommendationService
{
    Task<TrackRecommendationResult> RecommendAsync(
        RecommendationQuestionnaire questionnaire,
        CancellationToken cancellationToken = default);
}