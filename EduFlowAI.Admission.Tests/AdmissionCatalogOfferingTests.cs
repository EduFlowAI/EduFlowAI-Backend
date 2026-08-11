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
    public void Official_track_accepts_only_its_canonical_locations()
    {
        var definition = AdmissionTrackSeedCatalog.All.Single(track =>
            track.Name.StartsWith(".NET Enterprise", StringComparison.Ordinal));
        var track = CreateTrack(definition);
        var validBranch = new Branch { Id = Guid.NewGuid(), Name = "Tanta" };
        var invalidBranch = new Branch { Id = Guid.NewGuid(), Name = "Zagazig" };

        Assert.True(OfferingService.IsAllowedByOfficialCatalog(
            track,
            validBranch,
            "9M"));
        Assert.False(OfferingService.IsAllowedByOfficialCatalog(
            track,
            invalidBranch,
            "9M"));
    }

    [Fact]
    public void Invalid_existing_9m_offering_is_hidden_but_non_9m_is_preserved()
    {
        var definition = AdmissionTrackSeedCatalog.All.Single(track =>
            track.Name == "Digital IC Design");
        var track = CreateTrack(definition);
        var invalidBranch = new Branch { Id = Guid.NewGuid(), Name = "Alexandria" };

        Assert.False(TrackService.IsPublicOfferingAllowed(
            track,
            invalidBranch,
            "9M"));
        Assert.True(TrackService.IsPublicOfferingAllowed(
            track,
            invalidBranch,
            "CUSTOM"));
        Assert.True(OfferingService.IsAllowedByOfficialCatalog(
            track,
            invalidBranch,
            "CUSTOM"));
    }

    [Fact]
    public void Unknown_track_cannot_be_offered_in_9m_but_non_9m_is_preserved()
    {
        var track = new Track
        {
            Id = Guid.NewGuid(),
            ProgramId = AdmissionSeedIds.NineMonthProgramId,
            Name = "Custom track",
            IsActive = true
        };
        var branch = new Branch
        {
            Id = AdmissionSeedIds.SmartVillageBranchId,
            Name = "Smart Village",
            IsActive = true
        };

        Assert.False(OfferingService.IsAllowedByOfficialCatalog(
            track,
            branch,
            "9M"));
        Assert.True(OfferingService.IsAllowedByOfficialCatalog(
            track,
            branch,
            "CUSTOM"));
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

        offering.Branch.Name = "Alexandria";
        Assert.False(ApplicationService.IsSelectablePreferenceOffering(offering));
        Assert.False(
            ApplicationService.HasOnlySelectableStoredPreferences(application));

        offering.Branch.Name = "Smart Village";
        offering.Branch.IsActive = false;
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
