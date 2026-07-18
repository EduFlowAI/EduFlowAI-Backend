namespace EduFlowAI.AI.Application.Recommendation.Models;

public sealed record RecommendationModelResponse
{
    public required IReadOnlyCollection<ModelRecommendedTrack>
        Recommendations
    { get; init; }
}