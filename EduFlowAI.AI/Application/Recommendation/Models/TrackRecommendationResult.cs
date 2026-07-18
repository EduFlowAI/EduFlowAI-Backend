namespace EduFlowAI.AI.Application.Recommendation.Models;

public sealed record TrackRecommendationResult
{
    public required IReadOnlyList<RecommendedTrackResult>
        Recommendations
    { get; init; }

    public required bool UsedFallback { get; init; }

    public required string AdvisoryNotice { get; init; }
}