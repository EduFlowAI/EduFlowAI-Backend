using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Application.Interfaces;

public interface IOfferedTrackReader
{
    Task<IReadOnlyList<OfferedTrackForRecommendationDto>>
        GetActiveOfferedTracksAsync(
            CancellationToken cancellationToken = default);
}