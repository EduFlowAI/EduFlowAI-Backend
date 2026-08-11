using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Infrastructure.Seeding;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionDataSeederReconciliationTests
{
    [Fact]
    public void Supported_legacy_name_is_renamed_and_missing_metadata_is_backfilled_without_reactivating()
    {
        var definition = AdmissionTrackSeedCatalog.All.Single(track =>
            track.Id == AdmissionSeedIds.OpenSourceApplicationsDevelopmentTrackId);
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var track = new Track
        {
            Id = definition.Id,
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = "Open Source Applications Development",
            Description = null,
            PrerequisiteTopics = [],
            IsActive = false,
            UpdatedAt = now.AddDays(-1)
        };

        bool changed = AdmissionDataSeeder.ReconcileOfficialTrack(
            track,
            AdmissionSeedIds.NineMonthProgramId,
            definition,
            now);

        Assert.True(changed);
        Assert.Equal(definition.Id, track.Id);
        Assert.Equal(definition.Name, track.Name);
        Assert.Equal(definition.Description, track.Description);
        Assert.Equal(definition.PrerequisiteTopics, track.PrerequisiteTopics);
        Assert.False(track.IsActive);
        Assert.Equal(now, track.UpdatedAt);
    }

    [Fact]
    public void Administrator_edits_on_stable_official_id_are_not_overwritten()
    {
        var definition = AdmissionTrackSeedCatalog.All[0];
        var originalUpdatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var track = new Track
        {
            Id = definition.Id,
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = "Admin renamed track",
            Description = "Admin description",
            PrerequisiteTopics = ["Admin topic"],
            IsActive = false,
            UpdatedAt = originalUpdatedAt
        };

        bool changed = AdmissionDataSeeder.ReconcileOfficialTrack(
            track,
            AdmissionSeedIds.NineMonthProgramId,
            definition,
            originalUpdatedAt.AddDays(1));

        Assert.False(changed);
        Assert.Equal("Admin renamed track", track.Name);
        Assert.Equal("Admin description", track.Description);
        Assert.Equal(new[] { "Admin topic" }, track.PrerequisiteTopics);
        Assert.False(track.IsActive);
        Assert.Equal(originalUpdatedAt, track.UpdatedAt);
    }

    [Fact]
    public void Ambiguous_legacy_track_cannot_be_reinterpreted_as_dotnet()
    {
        var definition = AdmissionTrackSeedCatalog.All.Single(track =>
            track.Id == AdmissionSeedIds.DotNetEnterpriseTrackId);
        var legacy = new Track
        {
            Id = AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = AdmissionLegacyTrackCatalog.ProfessionalDevelopmentCrmName,
            PrerequisiteTopics = [],
            IsActive = true
        };

        Assert.Throws<InvalidOperationException>(() =>
            AdmissionDataSeeder.ReconcileOfficialTrack(
                legacy,
                AdmissionSeedIds.NineMonthProgramId,
                definition,
                DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Only_known_ambiguous_legacy_tracks_are_deactivated(
        bool useKnownLegacyTrack,
        bool expectedChanged)
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var originalUpdatedAt = now.AddDays(-1);
        var track = new Track
        {
            Id = useKnownLegacyTrack
                ? AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId
                : Guid.NewGuid(),
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = useKnownLegacyTrack
                ? AdmissionLegacyTrackCatalog.IntegratedSoftwareArchitectureName
                : "Admin configured 9M track",
            PrerequisiteTopics = [],
            IsActive = true,
            UpdatedAt = originalUpdatedAt
        };

        bool changed = AdmissionDataSeeder.DeactivateNonOfficialTrack(
            track,
            AdmissionSeedIds.NineMonthProgramId,
            now);

        Assert.Equal(expectedChanged, changed);
        Assert.Equal(!expectedChanged, track.IsActive);
        Assert.Equal(expectedChanged ? now : originalUpdatedAt, track.UpdatedAt);
    }

    [Fact]
    public void Official_branch_reconciliation_preserves_admin_name_and_active_state()
    {
        var definition = AdmissionBranchSeedCatalog.All.Single(branch =>
            branch.Name == "Smart Village");
        var branch = new Branch
        {
            Id = definition.Id,
            Name = "Renamed branch",
            Governorate = null,
            IsActive = false
        };

        bool changed = AdmissionDataSeeder.ReconcileOfficialBranch(
            branch,
            definition);

        Assert.True(changed);
        Assert.Equal(definition.Id, branch.Id);
        Assert.Equal("Renamed branch", branch.Name);
        Assert.Equal(definition.Governorate, branch.Governorate);
        Assert.False(branch.IsActive);
    }
}
