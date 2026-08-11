using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Infrastructure.Seeding;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionDataSeederReconciliationTests
{
    [Fact]
    public void Supported_legacy_name_is_renamed_in_place_and_reactivated()
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
        Assert.True(track.IsActive);
        Assert.Equal(now, track.UpdatedAt);
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
    [InlineData(true)]
    [InlineData(false)]
    public void Non_official_9m_tracks_are_inactive_without_being_deleted(
        bool useKnownLegacyTrack)
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var track = new Track
        {
            Id = useKnownLegacyTrack
                ? AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId
                : Guid.NewGuid(),
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = useKnownLegacyTrack
                ? AdmissionLegacyTrackCatalog.IntegratedSoftwareArchitectureName
                : "Unrecognized historical track",
            PrerequisiteTopics = [],
            IsActive = true
        };

        bool changed = AdmissionDataSeeder.DeactivateNonOfficialTrack(
            track,
            AdmissionSeedIds.NineMonthProgramId,
            now);

        Assert.True(changed);
        Assert.False(track.IsActive);
        Assert.Equal(now, track.UpdatedAt);
    }

    [Fact]
    public void Renamed_official_branch_is_reconciled_without_changing_its_id()
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
        Assert.Equal(definition.Name, branch.Name);
        Assert.Equal(definition.Governorate, branch.Governorate);
        Assert.True(branch.IsActive);
    }
}
