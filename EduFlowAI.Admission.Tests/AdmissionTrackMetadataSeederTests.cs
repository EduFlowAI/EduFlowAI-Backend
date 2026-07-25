using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Infrastructure.Seeding;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionTrackMetadataSeederTests
{
    [Fact]
    public void Backfill_populates_missing_description_and_topics()
    {
        var definition = AdmissionTrackSeedCatalog.All[0];
        var originalUpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var track = new Track
        {
            Id = definition.Id,
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = definition.Name,
            Description = null,
            PrerequisiteTopics = [],
            UpdatedAt = originalUpdatedAt
        };

        bool changed = AdmissionTrackMetadataSeeder.BackfillMissingMetadata(
            track,
            definition,
            now);

        Assert.True(changed);
        Assert.Equal(definition.Description, track.Description);
        Assert.Equal(definition.PrerequisiteTopics, track.PrerequisiteTopics);
        Assert.Equal(now, track.UpdatedAt);
    }

    [Fact]
    public void Backfill_preserves_admin_customized_metadata()
    {
        var definition = AdmissionTrackSeedCatalog.All[0];
        var originalUpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var track = new Track
        {
            Id = definition.Id,
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = definition.Name,
            Description = "Custom administrator description.",
            PrerequisiteTopics = ["Custom Topic"],
            UpdatedAt = originalUpdatedAt
        };

        bool changed = AdmissionTrackMetadataSeeder.BackfillMissingMetadata(
            track,
            definition,
            originalUpdatedAt.AddDays(1));

        Assert.False(changed);
        Assert.Equal("Custom administrator description.", track.Description);
        Assert.Equal(["Custom Topic"], track.PrerequisiteTopics);
        Assert.Equal(originalUpdatedAt, track.UpdatedAt);
    }

    [Fact]
    public void Backfill_only_fills_the_missing_field()
    {
        var definition = AdmissionTrackSeedCatalog.All[0];
        var now = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var track = new Track
        {
            Id = definition.Id,
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = definition.Name,
            Description = "Keep this description.",
            PrerequisiteTopics = [],
            UpdatedAt = now.AddDays(-1)
        };

        bool changed = AdmissionTrackMetadataSeeder.BackfillMissingMetadata(
            track,
            definition,
            now);

        Assert.True(changed);
        Assert.Equal("Keep this description.", track.Description);
        Assert.Equal(definition.PrerequisiteTopics, track.PrerequisiteTopics);
        Assert.Equal(now, track.UpdatedAt);
    }
}
