using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Infrastructure.Services;

public sealed class DevelopmentOfferedTrackReader : IOfferedTrackReader
{
    public Task<IReadOnlyList<OfferedTrackForRecommendationDto>>
        GetActiveOfferedTracksAsync(
            CancellationToken cancellationToken = default)
    {
        IReadOnlyList<OfferedTrackForRecommendationDto> tracks =
        [
            new OfferedTrackForRecommendationDto
            {
                TrackId = Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),
                Name = "Full Stack Web Development",
                Description =
                    "Build web applications using frontend and backend technologies.",
                PrerequisiteTopics =
                [
                    "Programming fundamentals",
                    "Databases",
                    "Web fundamentals"
                ]
            },
            new OfferedTrackForRecommendationDto
            {
                TrackId = Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"),
                Name = "Data Engineering",
                Description =
                    "Design data pipelines and scalable data platforms.",
                PrerequisiteTopics =
                [
                    "SQL",
                    "Python",
                    "Database fundamentals"
                ]
            },
            new OfferedTrackForRecommendationDto
            {
                TrackId = Guid.Parse(
                    "33333333-3333-3333-3333-333333333333"),
                Name = "Cloud and DevOps",
                Description =
                    "Learn deployment automation, cloud platforms, and CI/CD.",
                PrerequisiteTopics =
                [
                    "Linux",
                    "Networking",
                    "Programming fundamentals"
                ]
            }
        ];

        return Task.FromResult(tracks);
    }
}