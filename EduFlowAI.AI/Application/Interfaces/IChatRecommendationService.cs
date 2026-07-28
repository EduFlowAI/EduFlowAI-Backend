using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IChatRecommendationService
{
    Task<ChatRecommendationResultDto> RecommendFromMessageAsync(
        string message,
        CancellationToken cancellationToken = default);
}