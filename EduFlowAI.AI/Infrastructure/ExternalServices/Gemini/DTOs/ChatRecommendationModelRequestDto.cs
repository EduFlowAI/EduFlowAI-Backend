using EduFlowAI.AI.Application.DTOs;

namespace EduFlowAI.AI.Infrastructure.ExternalServices.Gemini.DTOs;

public sealed record ChatRecommendationModelRequestDto
{
    public required string Message { get; init; }

    public required IReadOnlyCollection<OfferedTrackForRecommendationDto>
        OfferedTracks
    { get; init; }
}