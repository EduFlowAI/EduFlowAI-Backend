using EduFlowAI.AI.Application.Recommendation.Models;

namespace EduFlowAI.AI.Application.Recommendation.Contracts;

public interface IRecommendationModelClient
{
    Task<RecommendationModelResponse> RankAsync(
        RecommendationModelRequest request,
        CancellationToken cancellationToken = default);
}