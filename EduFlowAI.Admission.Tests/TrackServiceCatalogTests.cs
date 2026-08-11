using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Features.Branches;
using EduFlowAI.Admission.Application.Features.Configuration.Common;
using EduFlowAI.Admission.Application.Features.Programs;
using EduFlowAI.Admission.Application.Features.Tracks;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Admission.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AdmissionProgram = EduFlowAI.Admission.Domain.Entities.Program;

namespace EduFlowAI.Admission.Tests;

public sealed class TrackServiceCatalogTests
{
    [Fact]
    public async Task Public_catalog_combines_all_official_tracks_with_offered_non_9m_tracks()
    {
        await using var scenario = await CreateScenarioAsync();
        var service = CreateService(scenario.Context);

        var tracks = await service.GetPublicTracksAsync();

        Assert.Equal(AdmissionTrackSeedCatalog.All.Count + 1, tracks.Count);
        Assert.Equal(
            AdmissionTrackSeedCatalog.All.Count,
            tracks.Count(track => track.IsOfficialIntake47));
        Assert.DoesNotContain(
            tracks,
            track => track.Id == AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId ||
                track.Id == AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId ||
                track.Id == scenario.UnknownNineMonthTrackId);

        var industrial = Assert.Single(
            tracks,
            track => track.Name == "Industrial Automation");
        Assert.Null(industrial.TotalHours);
        Assert.Equal(5, industrial.GraduationYearLimitYears);
        var industrialLocation = Assert.Single(industrial.Locations);
        Assert.Equal(AdmissionSeedIds.SmartVillageBranchId, industrialLocation.BranchId);
        Assert.Equal("Smart Village", industrialLocation.BranchName);
        Assert.Equal("Giza", industrialLocation.Governorate);

        Assert.Null(tracks.Single(track =>
            track.Name == "3D Generalist").Description);

        var digital = Assert.Single(
            tracks,
            track => track.Name == "Digital IC Design");
        var validOffering = Assert.Single(digital.Offerings);
        Assert.Equal("Smart Village", validOffering.BranchName);

        var custom = Assert.Single(
            tracks,
            track => track.Id == scenario.CustomTrackId);
        Assert.False(custom.IsOfficialIntake47);
        Assert.Null(custom.Category);
        var customOffering = Assert.Single(custom.Offerings);
        var customLocation = Assert.Single(custom.Locations);
        Assert.Equal(customOffering.BranchId, customLocation.BranchId);
        Assert.Equal(customOffering.BranchName, customLocation.BranchName);
        Assert.Equal(customOffering.Governorate, customLocation.Governorate);
    }

    [Fact]
    public async Task Non_9m_cycle_and_detail_locations_are_derived_from_current_offerings()
    {
        await using var scenario = await CreateScenarioAsync();
        var service = CreateService(scenario.Context);

        var cycleTrack = Assert.Single(await service.GetPublicTracksAsync(
            scenario.CustomCycleId));
        var cycleOffering = Assert.Single(cycleTrack.Offerings);
        var cycleLocation = Assert.Single(cycleTrack.Locations);
        Assert.Equal(cycleOffering.BranchId, cycleLocation.BranchId);
        Assert.Equal(cycleOffering.BranchName, cycleLocation.BranchName);
        Assert.Equal(cycleOffering.Governorate, cycleLocation.Governorate);

        var detailTrack = await service.GetPublicTrackAsync(
            scenario.CustomTrackId);
        Assert.NotNull(detailTrack);
        var detailOffering = Assert.Single(detailTrack.Offerings);
        var detailLocation = Assert.Single(detailTrack.Locations);
        Assert.Equal(detailOffering.BranchId, detailLocation.BranchId);
        Assert.Equal(detailOffering.BranchName, detailLocation.BranchName);
        Assert.Equal(detailOffering.Governorate, detailLocation.Governorate);
    }

    [Fact]
    public async Task Cycle_and_detail_queries_filter_invalid_9m_edges_but_preserve_non_9m()
    {
        await using var scenario = await CreateScenarioAsync();
        var service = CreateService(scenario.Context);

        var nineMonthTracks = await service.GetPublicTracksAsync(
            scenario.NineMonthCycleId);
        var digital = Assert.Single(nineMonthTracks);
        Assert.Equal("Digital IC Design", digital.Name);
        Assert.Equal("Smart Village", Assert.Single(digital.Offerings).BranchName);

        var customTracks = await service.GetPublicTracksAsync(
            scenario.CustomCycleId);
        Assert.Equal(
            scenario.CustomTrackId,
            Assert.Single(customTracks).Id);

        var industrialDefinition = AdmissionTrackSeedCatalog.All.Single(
            definition => definition.Name == "Industrial Automation");
        var industrialDetail = await service.GetPublicTrackAsync(
            industrialDefinition.Id);
        Assert.NotNull(industrialDetail);
        Assert.Empty(industrialDetail.Offerings);

        var customDetail = await service.GetPublicTrackAsync(
            scenario.CustomTrackId);
        Assert.NotNull(customDetail);
        Assert.False(customDetail.IsOfficialIntake47);

        var adminTracks = await service.GetAdminTracksAsync();
        Assert.DoesNotContain(
            adminTracks,
            track => track.Id == AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId ||
                track.Id == AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId ||
                track.Id == scenario.UnknownNineMonthTrackId);
        Assert.Contains(
            adminTracks,
            track => track.Id == scenario.CustomTrackId &&
                track.Name == AdmissionLegacyTrackCatalog.ProfessionalDevelopmentCrmName);
    }

    [Fact]
    public async Task Update_rejects_official_9m_edits_but_preserves_custom_behavior()
    {
        await using var scenario = await CreateScenarioAsync();
        var service = CreateService(scenario.Context);
        var digital = AdmissionTrackSeedCatalog.All.Single(
            definition => definition.Name == "Digital IC Design");

        var officialResult = await service.UpdateTrackAsync(
            digital.Id,
            new UpdateTrackRequest
            {
                Name = "Renamed official track",
                Description = "Attempted edit",
                PrerequisiteTopics = ["Logic"],
                IsActive = true
            });

        Assert.False(officialResult.IsSuccess);
        Assert.Equal(400, officialResult.StatusCode);
        Assert.Equal(
            "Official Intake-47 tracks are fully seed-managed and cannot be edited.",
            officialResult.Message);
        Assert.Equal(
            digital.Name,
            (await scenario.Context.Tracks.FindAsync(digital.Id))!.Name);

        var activationOnlyResult = await service.UpdateTrackAsync(
            digital.Id,
            new UpdateTrackRequest
            {
                Name = digital.Name,
                Description = digital.Description,
                PrerequisiteTopics = [.. digital.PrerequisiteTopics],
                IsActive = false
            });

        Assert.False(activationOnlyResult.IsSuccess);
        Assert.Equal(400, activationOnlyResult.StatusCode);

        var customResult = await service.UpdateTrackAsync(
            scenario.CustomTrackId,
            new UpdateTrackRequest
            {
                Name = "Renamed custom track",
                Description = "Custom description",
                PrerequisiteTopics = ["Custom topic"],
                IsActive = true
            });

        Assert.True(customResult.IsSuccess);
        Assert.Equal("Renamed custom track", customResult.Data.Name);

        var createNineMonthResult = await service.CreateTrackAsync(
            new CreateTrackRequest
            {
                ProgramId = AdmissionSeedIds.NineMonthProgramId,
                Name = "Arbitrary 9M track",
                Description = "Must be rejected.",
                PrerequisiteTopics = [],
                IsActive = true
            });

        Assert.False(createNineMonthResult.IsSuccess);
        Assert.Equal(400, createNineMonthResult.StatusCode);

        var casingCollision = await service.CreateTrackAsync(
            new CreateTrackRequest
            {
                ProgramId = scenario.CustomProgramId,
                Name = "RENAMED CUSTOM TRACK",
                Description = "Duplicate casing.",
                PrerequisiteTopics = [],
                IsActive = true
            });

        Assert.False(casingCollision.IsSuccess);
        Assert.Equal(409, casingCollision.StatusCode);
    }

    [Fact]
    public async Task Official_branch_is_immutable_but_custom_branch_remains_editable()
    {
        await using var scenario = await CreateScenarioAsync();
        var service = new BranchService(
            scenario.Context,
            new UnlockedApplicationReader(),
            TimeProvider.System,
            new AdmissionWriteExecutor(scenario.Context));

        var officialResult = await service.UpdateBranchAsync(
            AdmissionSeedIds.SmartVillageBranchId,
            new UpdateBranchRequest
            {
                Name = "Renamed official branch",
                Governorate = "Changed",
                IsActive = false
            });

        Assert.False(officialResult.IsSuccess);
        Assert.Equal(400, officialResult.StatusCode);
        Assert.Equal(
            "Official Intake-47 branches are fully seed-managed and cannot be edited.",
            officialResult.Message);

        var customResult = await service.UpdateBranchAsync(
            scenario.CustomBranchId,
            new UpdateBranchRequest
            {
                Name = "Renamed custom branch",
                Governorate = "Custom governorate",
                IsActive = false
            });

        Assert.True(customResult.IsSuccess);
        Assert.Equal("Renamed custom branch", customResult.Data.Name);
        Assert.False(customResult.Data.IsActive);

        var casingCollision = await service.UpdateBranchAsync(
            scenario.CustomBranchId,
            new UpdateBranchRequest
            {
                Name = "smart village",
                Governorate = "Custom governorate",
                IsActive = true
            });

        Assert.False(casingCollision.IsSuccess);
        Assert.Equal(409, casingCollision.StatusCode);

        var branches = await service.GetBranchesAsync();
        Assert.True(branches.Single(branch =>
            branch.Id == AdmissionSeedIds.SmartVillageBranchId)
            .IsOfficialIntake47Location);
        Assert.False(branches.Single(branch =>
            branch.Id == scenario.CustomBranchId)
            .IsOfficialIntake47Location);
    }

    [Fact]
    public async Task Program_counts_and_source_managed_parent_contract_ignore_legacy_9m_rows()
    {
        await using var scenario = await CreateScenarioAsync();
        var service = new ProgramConfigurationService(
            scenario.Context,
            new UnlockedApplicationReader(),
            TimeProvider.System,
            new AdmissionWriteExecutor(scenario.Context));

        var programs = await service.GetProgramsAsync();
        var nineMonth = programs.Single(program =>
            program.Id == AdmissionSeedIds.NineMonthProgramId);
        Assert.Equal(31, nineMonth.TrackCount);
        Assert.True(nineMonth.IsSourceManaged);

        var custom = programs.Single(program =>
            program.Id == scenario.CustomProgramId);
        Assert.Equal(1, custom.TrackCount);
        Assert.False(custom.IsSourceManaged);

        var officialUpdate = await service.UpdateProgramAsync(
            nineMonth.Id,
            new UpdateProgramRequest
            {
                Name = nineMonth.Name,
                Code = nineMonth.Code,
                DurationMonths = nineMonth.DurationMonths
            });
        Assert.False(officialUpdate.IsSuccess);
        Assert.Equal(400, officialUpdate.StatusCode);

        var customUpdate = await service.UpdateProgramAsync(
            custom.Id,
            new UpdateProgramRequest
            {
                Name = "Updated Custom Program",
                Code = "custom",
                DurationMonths = 5
            });
        Assert.True(customUpdate.IsSuccess);
        Assert.Equal(1, customUpdate.Data.TrackCount);
        Assert.False(customUpdate.Data.IsSourceManaged);

        var institutions = await service.GetInstitutionsAsync();
        var iti = Assert.Single(institutions);
        Assert.True(iti.IsSourceManaged);

        var institutionUpdate = await service.UpdateInstitutionAsync(
            iti.Id,
            new UpdateInstitutionRequest
            {
                Name = iti.Name,
                Code = iti.Code
            });
        Assert.False(institutionUpdate.IsSuccess);
        Assert.Equal(400, institutionUpdate.StatusCode);

        var officialDelete = await service.DeleteProgramAsync(nineMonth.Id);
        Assert.False(officialDelete.IsSuccess);
        Assert.Equal(400, officialDelete.StatusCode);
    }

    private static TrackService CreateService(IAdmissionDbContext context) =>
        new(
            context,
            new UnlockedApplicationReader(),
            TimeProvider.System,
            new AdmissionWriteExecutor(context));

    private static async Task<TestScenario> CreateScenarioAsync()
    {
        var options = new DbContextOptionsBuilder<TestAdmissionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TestAdmissionDbContext(options);

        var institution = new Institution
        {
            Id = AdmissionSeedIds.ItiInstitutionId,
            Name = "Information Technology Institute",
            Code = "ITI"
        };
        var nineMonthProgram = new AdmissionProgram
        {
            Id = AdmissionSeedIds.NineMonthProgramId,
            InstitutionId = institution.Id,
            Institution = institution,
            Name = "9-Month Professional Training Program",
            Code = "9M",
            DurationMonths = 9
        };
        var customProgram = new AdmissionProgram
        {
            Id = Guid.NewGuid(),
            InstitutionId = institution.Id,
            Institution = institution,
            Name = "Custom Program",
            Code = "CUSTOM",
            DurationMonths = 4
        };

        context.Institutions.Add(institution);
        context.Programs.AddRange(nineMonthProgram, customProgram);

        var officialTracks = AdmissionTrackSeedCatalog.All
            .Select(definition => new Track
            {
                Id = definition.Id,
                ProgramId = nineMonthProgram.Id,
                Program = nineMonthProgram,
                Name = definition.Name,
                Description = definition.Name == "3D Generalist"
                    ? "Stale inferred description"
                    : definition.Description,
                PrerequisiteTopics = [.. definition.PrerequisiteTopics],
                IsActive = true
            })
            .ToList();
        context.Tracks.AddRange(officialTracks);

        Guid unknownNineMonthTrackId = Guid.NewGuid();
        context.Tracks.AddRange(
            new Track
            {
                Id = AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
                ProgramId = nineMonthProgram.Id,
                Program = nineMonthProgram,
                Name = AdmissionLegacyTrackCatalog.ProfessionalDevelopmentCrmName,
                IsActive = true
            },
            new Track
            {
                Id = AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId,
                ProgramId = nineMonthProgram.Id,
                Program = nineMonthProgram,
                Name = AdmissionLegacyTrackCatalog.IntegratedSoftwareArchitectureName,
                IsActive = true
            },
            new Track
            {
                Id = unknownNineMonthTrackId,
                ProgramId = nineMonthProgram.Id,
                Program = nineMonthProgram,
                Name = "Unrecognized historical track",
                IsActive = true
            });

        var customTrack = new Track
        {
            Id = Guid.NewGuid(),
            ProgramId = customProgram.Id,
            Program = customProgram,
            Name = AdmissionLegacyTrackCatalog.ProfessionalDevelopmentCrmName,
            Description = "A non-9M custom track.",
            PrerequisiteTopics = ["Custom topic"],
            IsActive = true
        };
        context.Tracks.Add(customTrack);

        var branches = AdmissionBranchSeedCatalog.All
            .Select(definition => new Branch
            {
                Id = definition.Id,
                Name = definition.Name,
                Governorate = definition.Governorate,
                IsActive = true
            })
            .ToDictionary(branch => branch.Name);
        context.Branches.AddRange(branches.Values);
        var customBranch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Custom Branch",
            Governorate = "Custom",
            IsActive = true
        };
        context.Branches.Add(customBranch);

        var nineMonthCycle = new AdmissionCycle
        {
            Id = Guid.NewGuid(),
            ProgramId = nineMonthProgram.Id,
            Program = nineMonthProgram,
            Label = "Intake 47",
            StartDate = new DateOnly(2026, 9, 1),
            DeadlineUtc = new DateTimeOffset(2026, 8, 31, 21, 59, 59, TimeSpan.Zero),
            Status = CycleStatus.Active
        };
        var customCycle = new AdmissionCycle
        {
            Id = Guid.NewGuid(),
            ProgramId = customProgram.Id,
            Program = customProgram,
            Label = "Custom active cycle",
            StartDate = new DateOnly(2026, 9, 1),
            DeadlineUtc = new DateTimeOffset(2026, 8, 31, 21, 59, 59, TimeSpan.Zero),
            Status = CycleStatus.Active
        };
        context.AdmissionCycles.AddRange(nineMonthCycle, customCycle);

        var digital = officialTracks.Single(track =>
            track.Name == "Digital IC Design");
        context.TrackBranchOfferings.AddRange(
            CreateOffering(nineMonthCycle, digital, branches["Smart Village"]),
            CreateOffering(nineMonthCycle, digital, branches["Alexandria"]),
            CreateOffering(customCycle, customTrack, branches["Alexandria"]));

        await context.SaveChangesAsync();

        return new TestScenario(
            context,
            nineMonthCycle.Id,
            customCycle.Id,
            customProgram.Id,
            customTrack.Id,
            unknownNineMonthTrackId,
            customBranch.Id);
    }

    private static TrackBranchOffering CreateOffering(
        AdmissionCycle cycle,
        Track track,
        Branch branch) =>
        new()
        {
            Id = Guid.NewGuid(),
            CycleId = cycle.Id,
            Cycle = cycle,
            TrackId = track.Id,
            Track = track,
            BranchId = branch.Id,
            Branch = branch,
            Capacity = 25
        };

    private sealed record TestScenario(
        TestAdmissionDbContext Context,
        Guid NineMonthCycleId,
        Guid CustomCycleId,
        Guid CustomProgramId,
        Guid CustomTrackId,
        Guid UnknownNineMonthTrackId,
        Guid CustomBranchId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class UnlockedApplicationReader : IAdmissionApplicationReader
    {
        public Task<bool> IsInstitutionConfigurationLockedAsync(
            Guid institutionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsProgramConfigurationLockedAsync(
            Guid programId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsTrackConfigurationLockedAsync(
            Guid trackId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsBranchConfigurationLockedAsync(
            Guid branchId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasApplicationsForCycleAsync(
            Guid cycleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasPreferencesForOfferingsAsync(
            IReadOnlyCollection<Guid> offeringIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> CountApplicationsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class TestAdmissionDbContext : DbContext, IAdmissionDbContext
    {
        public TestAdmissionDbContext(
            DbContextOptions<TestAdmissionDbContext> options)
            : base(options)
        {
        }

        public DbSet<Institution> Institutions => Set<Institution>();
        public DbSet<AdmissionProgram> Programs => Set<AdmissionProgram>();
        public DbSet<AdmissionCycle> AdmissionCycles => Set<AdmissionCycle>();
        public DbSet<CycleEligibilityRule> CycleEligibilityRules => Set<CycleEligibilityRule>();
        public DbSet<Track> Tracks => Set<Track>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<TrackBranchOffering> TrackBranchOfferings => Set<TrackBranchOffering>();
        public DbSet<EduFlowAI.Admission.Domain.Entities.Application> Applications =>
            Set<EduFlowAI.Admission.Domain.Entities.Application>();
        public DbSet<ApplicationPreference> ApplicationPreferences =>
            Set<ApplicationPreference>();
        public DbSet<EligibilityResult> EligibilityResults => Set<EligibilityResult>();
        public DbSet<SimulatedStageResult> SimulatedStageResults =>
            Set<SimulatedStageResult>();
        public DbSet<ProgramDocumentRequirement> ProgramDocumentRequirements =>
            Set<ProgramDocumentRequirement>();
        public DbSet<EnrollmentTaskLookup> EnrollmentTaskLookups =>
            Set<EnrollmentTaskLookup>();
        public DbSet<ApplicationEnrollmentTask> ApplicationEnrollmentTasks =>
            Set<ApplicationEnrollmentTask>();
    }
}
