namespace EduFlowAI.AI.Application.Recommendation.Models;

public sealed record ModelRecommendedTrack
{
    public required Guid TrackId { get; init; }

    public required int Rank { get; init; }

    public required string Reason { get; init; }
}