namespace EduFlowAI.AI.Application.Recommendation.Models;

public sealed record RecommendedTrackResult
{
    public required Guid TrackId { get; init; }

    public required string TrackName { get; init; }

    public required int Rank { get; init; }

    public required string Reason { get; init; }
}