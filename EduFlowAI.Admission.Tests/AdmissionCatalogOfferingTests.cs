using EduFlowAI.Admission.Application.Features.Offerings;
using EduFlowAI.Admission.Application.Features.Tracks;
using EduFlowAI.Admission.Application.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Admission.Infrastructure.Seeding;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionCatalogOfferingTests
{
    [Fact]
    public void Official_track_accepts_only_its_canonical_locations_by_stable_branch_id()
    {
        var definition = AdmissionTrackSeedCatalog.All.Single(track =>
            track.Name.StartsWith(".NET Enterprise", StringComparison.Ordinal));
        var track = CreateTrack(definition);
        var validBranch = new Branch
        {
            Id = AdmissionSeedIds.TantaBranchId,
            Name = "Renamed Tanta"
        };
        var invalidBranch = new Branch
        {
            Id = AdmissionSeedIds.ZagazigBranchId,
            Name = "Tanta"
        };

        Assert.True(OfferingService.IsAllowedByOfficialCatalog(
            track,
            validBranch,
            AdmissionSeedIds.NineMonthProgramId,
            "9M"));
        Assert.False(OfferingService.IsAllowedByOfficialCatalog(
            track,
            invalidBranch,
            AdmissionSeedIds.NineMonthProgramId,
            "9M"));
    }

    [Fact]
    public void Invalid_existing_official_9m_offering_is_hidden_but_custom_tracks_are_preserved()
    {
        var definition = AdmissionTrackSeedCatalog.All.Single(track =>
            track.Name == "Digital IC Design");
        var track = CreateTrack(definition);
        var invalidBranch = new Branch
        {
            Id = AdmissionSeedIds.AlexandriaBranchId,
            Name = "Alexandria"
        };

        Assert.False(TrackService.IsPublicOfferingAllowed(
            track,
            invalidBranch,
            AdmissionSeedIds.NineMonthProgramId,
            "9M"));
        Assert.True(TrackService.IsPublicOfferingAllowed(
            track,
            invalidBranch,
            Guid.NewGuid(),
            "CUSTOM"));

        var customNineMonthTrack = new Track
        {
            Id = Guid.NewGuid(),
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = "Admin configured 9M track",
            IsActive = true
        };

        Assert.True(OfferingService.IsAllowedByOfficialCatalog(
            customNineMonthTrack,
            invalidBranch,
            AdmissionSeedIds.NineMonthProgramId,
            "9M"));
    }

    [Fact]
    public void Applicant_preference_requires_active_canonical_offering()
    {
        var definition = AdmissionTrackSeedCatalog.All.Single(track =>
            track.Name == "Digital IC Design");
        var track = CreateTrack(definition);
        var program = new EduFlowAI.Admission.Domain.Entities.Program
        {
            Id = track.ProgramId,
            InstitutionId = Guid.NewGuid(),
            Name = "9-Month Professional Training Program",
            Code = "9M",
            DurationMonths = 9
        };
        track.Program = program;
        var cycle = new AdmissionCycle
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Program = program,
            Label = "Intake 47",
            StartDate = new DateOnly(2026, 9, 1),
            DeadlineUtc = DateTimeOffset.UtcNow.AddMonths(1),
            Status = CycleStatus.Active
        };
        var offering = new TrackBranchOffering
        {
            Id = Guid.NewGuid(),
            CycleId = cycle.Id,
            Cycle = cycle,
            TrackId = track.Id,
            Track = track,
            BranchId = AdmissionSeedIds.SmartVillageBranchId,
            Branch = new Branch
            {
                Id = AdmissionSeedIds.SmartVillageBranchId,
                Name = "Smart Village",
                IsActive = true
            },
            Capacity = 25
        };

        Assert.True(ApplicationService.IsSelectablePreferenceOffering(offering));
        var application = new EduFlowAI.Admission.Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            ApplicantUserId = "applicant",
            CycleId = cycle.Id,
            Preferences =
            [
                new ApplicationPreference
                {
                    Id = Guid.NewGuid(),
                    TrackBranchOfferingId = offering.Id,
                    TrackBranchOffering = offering,
                    Rank = 1
                }
            ]
        };
        Assert.True(
            ApplicationService.HasOnlySelectableStoredPreferences(application));

        application.CycleId = Guid.NewGuid();
        Assert.False(
            ApplicationService.HasOnlySelectableStoredPreferences(application));
        application.CycleId = cycle.Id;

        // Admin display-name changes do not invalidate canonical locations;
        // source identity is the stable branch ID.
        offering.Branch.Name = "Renamed Smart Village";
        Assert.True(ApplicationService.IsSelectablePreferenceOffering(offering));

        offering.BranchId = AdmissionSeedIds.AlexandriaBranchId;
        offering.Branch = new Branch
        {
            Id = AdmissionSeedIds.AlexandriaBranchId,
            Name = "Alexandria",
            IsActive = true
        };
        Assert.False(ApplicationService.IsSelectablePreferenceOffering(offering));
        Assert.False(
            ApplicationService.HasOnlySelectableStoredPreferences(application));

        offering.BranchId = AdmissionSeedIds.SmartVillageBranchId;
        offering.Branch = new Branch
        {
            Id = AdmissionSeedIds.SmartVillageBranchId,
            Name = "Smart Village",
            IsActive = false
        };
        Assert.False(ApplicationService.IsSelectablePreferenceOffering(offering));

        offering.Branch.IsActive = true;
        offering.Track.IsActive = false;
        Assert.False(ApplicationService.IsSelectablePreferenceOffering(offering));

        offering.Track.IsActive = true;
        offering.Cycle.Status = CycleStatus.Closed;
        Assert.False(ApplicationService.IsSelectablePreferenceOffering(offering));
    }

    private static Track CreateTrack(AdmissionTrackSeedDefinition definition)
    {
        return new Track
        {
            Id = definition.Id,
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = definition.Name,
            Description = definition.Description,
            PrerequisiteTopics = [.. definition.PrerequisiteTopics],
            IsActive = true
        };
    }
}
