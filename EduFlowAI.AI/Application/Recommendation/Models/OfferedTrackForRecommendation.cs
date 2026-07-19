namespace EduFlowAI.AI.Application.Recommendation.Models;

public sealed record OfferedTrackForRecommendation
{
    public required Guid TrackId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required IReadOnlyCollection<string> PrerequisiteTopics { get; init; }
}