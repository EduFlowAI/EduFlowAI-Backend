using EduFlowAI.Admission.Infrastructure.Seeding;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionTrackSeedCatalogTests
{
    [Fact]
    public void Seeded_tracks_have_complete_distinct_recommendation_metadata()
    {
        var tracks = AdmissionTrackSeedCatalog.All;

        Assert.Equal(10, tracks.Count);
        Assert.Equal(tracks.Count, tracks.Select(track => track.Id).Distinct().Count());
        Assert.Equal(
            tracks.Count,
            tracks.Select(track => track.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (var track in tracks)
        {
            Assert.NotEqual(Guid.Empty, track.Id);
            Assert.False(string.IsNullOrWhiteSpace(track.Name));
            Assert.InRange(track.Name.Length, 1, 200);
            Assert.False(string.IsNullOrWhiteSpace(track.Description));
            Assert.InRange(track.Description.Length, 1, 4000);
            Assert.NotEmpty(track.PrerequisiteTopics);
            Assert.InRange(track.PrerequisiteTopics.Count, 1, 50);
            Assert.DoesNotContain(
                track.PrerequisiteTopics,
                topic => string.IsNullOrWhiteSpace(topic));
            Assert.All(
                track.PrerequisiteTopics,
                topic => Assert.InRange(topic.Length, 1, 100));
            Assert.Equal(
                track.PrerequisiteTopics.Count,
                track.PrerequisiteTopics
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count());
        }
    }

    [Fact]
    public void Seeded_track_catalog_uses_all_stable_track_ids()
    {
        Guid[] expectedIds =
        [
            AdmissionSeedIds.OpenSourceApplicationsDevelopmentTrackId,
            AdmissionSeedIds.AiAndMachineLearningTrackId,
            AdmissionSeedIds.SystemsAdministrationTrackId,
            AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
            AdmissionSeedIds.CloudPlatformDevelopmentTrackId,
            AdmissionSeedIds.CloudArchitectureTrackId,
            AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId,
            AdmissionSeedIds.CyberSecurityTrackId,
            AdmissionSeedIds.EnterpriseJavaTrackId,
            AdmissionSeedIds.TelecomApplicationDevelopmentTrackId
        ];

        Assert.Equal(
            expectedIds.OrderBy(id => id),
            AdmissionTrackSeedCatalog.All.Select(track => track.Id).OrderBy(id => id));
    }
}
