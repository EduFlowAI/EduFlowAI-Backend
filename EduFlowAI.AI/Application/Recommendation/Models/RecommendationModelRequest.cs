namespace EduFlowAI.AI.Application.Recommendation.Models;

public sealed record RecommendationModelRequest
{
    public required RecommendationQuestionnaire Questionnaire { get; init; }

    public required IReadOnlyCollection<OfferedTrackForRecommendation>
        OfferedTracks
    { get; init; }
}