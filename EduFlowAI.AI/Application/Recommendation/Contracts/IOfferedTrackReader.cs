using EduFlowAI.AI.Application.Recommendation.Models;

namespace EduFlowAI.AI.Application.Recommendation.Contracts;

public interface IOfferedTrackReader
{
    Task<IReadOnlyList<OfferedTrackForRecommendation>>
        GetActiveOfferedTracksAsync(
            CancellationToken cancellationToken = default);
}