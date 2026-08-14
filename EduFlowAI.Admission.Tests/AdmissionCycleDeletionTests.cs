using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Features.Configuration.Common;
using EduFlowAI.Admission.Application.Features.Cycles;
using EduFlowAI.Admission.Application.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AdmissionApplication = EduFlowAI.Admission.Domain.Entities.Application;
using AdmissionProgram = EduFlowAI.Admission.Domain.Entities.Program;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionCycleDeletionTests
{
    [Fact]
    public async Task Delete_draft_removes_cycle_and_all_cycle_owned_configuration()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedCycleAsync(
            dbContext,
            CycleStatus.Draft,
            offeringCount: 2,
            withApplication: false);

        var result = await CreateService(dbContext).DeleteCycleAsync(seeded.CycleId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        Assert.Empty(await dbContext.AdmissionCycles.ToListAsync());
        Assert.Empty(await dbContext.CycleEligibilityRules.ToListAsync());
        Assert.Empty(await dbContext.TrackBranchOfferings.ToListAsync());

        // Program catalog data is not owned by the cycle and must survive.
        Assert.Single(await dbContext.Programs.ToListAsync());
        Assert.Single(await dbContext.Tracks.ToListAsync());
        Assert.Equal(2, await dbContext.Branches.CountAsync());
    }

    [Fact]
    public async Task Delete_rejects_active_cycle_and_preserves_configuration()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedCycleAsync(
            dbContext,
            CycleStatus.Active,
            offeringCount: 2,
            withApplication: false);

        var result = await CreateService(dbContext).DeleteCycleAsync(seeded.CycleId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("Only Draft", result.Message);
        Assert.Single(await dbContext.AdmissionCycles.ToListAsync());
        Assert.Single(await dbContext.CycleEligibilityRules.ToListAsync());
        Assert.Equal(2, await dbContext.TrackBranchOfferings.CountAsync());
    }

    [Fact]
    public async Task Delete_rejects_closed_cycle_and_preserves_configuration()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedCycleAsync(
            dbContext,
            CycleStatus.Closed,
            offeringCount: 2,
            withApplication: false);

        var result = await CreateService(dbContext).DeleteCycleAsync(seeded.CycleId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("Only Draft", result.Message);
        Assert.Single(await dbContext.AdmissionCycles.ToListAsync());
        Assert.Single(await dbContext.CycleEligibilityRules.ToListAsync());
        Assert.Equal(2, await dbContext.TrackBranchOfferings.CountAsync());
    }

    [Fact]
    public async Task Delete_rejects_draft_with_application_and_leaves_history_untouched()
    {
        await using var dbContext = CreateDbContext();
        var seeded = await SeedCycleAsync(
            dbContext,
            CycleStatus.Draft,
            offeringCount: 2,
            withApplication: true);
        Guid applicationId = await dbContext.Applications
            .Select(application => application.Id)
            .SingleAsync();

        var result = await CreateService(dbContext).DeleteCycleAsync(seeded.CycleId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("admission activity", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await dbContext.Applications.FindAsync(applicationId));
        Assert.Single(await dbContext.AdmissionCycles.ToListAsync());
        Assert.Single(await dbContext.CycleEligibilityRules.ToListAsync());
        Assert.Equal(2, await dbContext.TrackBranchOfferings.CountAsync());
    }

    [Fact]
    public async Task Delete_rejects_missing_cycle()
    {
        await using var dbContext = CreateDbContext();

        var result = await CreateService(dbContext).DeleteCycleAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Admission cycle was not found.", result.Message);
    }

    [Fact]
    public async Task Delete_rejects_historical_reference_to_a_cycle_offering()
    {
        await using var dbContext = CreateDbContext();
        var target = await SeedCycleAsync(
            dbContext,
            CycleStatus.Draft,
            offeringCount: 1,
            withApplication: false);

        Guid otherCycleId = Guid.NewGuid();
        dbContext.AdmissionCycles.Add(new AdmissionCycle
        {
            Id = otherCycleId,
            ProgramId = target.ProgramId,
            Label = "Other historical cycle",
            StartDate = new DateOnly(2026, 1, 1),
            DeadlineUtc = new DateTimeOffset(2026, 1, 31, 21, 0, 0, TimeSpan.Zero),
            Status = CycleStatus.Closed,
            ClosedAt = DateTimeOffset.UtcNow
        });

        Guid applicationId = Guid.NewGuid();
        dbContext.Applications.Add(new AdmissionApplication
        {
            Id = applicationId,
            ApplicantUserId = "historical-applicant",
            CycleId = otherCycleId,
            Status = ApplicationStatus.Admitted,
            AcceptedTrackBranchOfferingId = target.OfferingIds.Single(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext).DeleteCycleAsync(target.CycleId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("track/branch offerings", result.Message);
        Assert.NotNull(await dbContext.AdmissionCycles.FindAsync(target.CycleId));
        Assert.Single(await dbContext.CycleEligibilityRules
            .Where(rule => rule.CycleId == target.CycleId)
            .ToListAsync());
        Assert.Single(await dbContext.TrackBranchOfferings
            .Where(offering => offering.CycleId == target.CycleId)
            .ToListAsync());
        Assert.NotNull(await dbContext.Applications.FindAsync(applicationId));
    }

    private static AdmissionCycleService CreateService(TestAdmissionDbContext dbContext)
    {
        var applicationReader = new AdmissionApplicationReader(dbContext);
        return new AdmissionCycleService(
            dbContext,
            TimeProvider.System,
            new AdmissionWriteExecutor(dbContext),
            new CycleConfigurationGuard(applicationReader));
    }

    private static TestAdmissionDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestAdmissionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestAdmissionDbContext(options);
    }

    private static async Task<SeededCycle> SeedCycleAsync(
        TestAdmissionDbContext dbContext,
        CycleStatus status,
        int offeringCount,
        bool withApplication)
    {
        Guid institutionId = Guid.NewGuid();
        Guid programId = Guid.NewGuid();
        Guid trackId = Guid.NewGuid();
        Guid cycleId = Guid.NewGuid();

        dbContext.Institutions.Add(new Institution
        {
            Id = institutionId,
            Name = "Information Technology Institute",
            Code = "ITI-DELETE-TEST"
        });

        dbContext.Programs.Add(new AdmissionProgram
        {
            Id = programId,
            InstitutionId = institutionId,
            Name = "9-Months",
            Code = "DELETE-9M",
            Description = string.Empty,
            DurationMonths = 9
        });

        dbContext.Tracks.Add(new Track
        {
            Id = trackId,
            ProgramId = programId,
            Name = "Safe Delete Track",
            Description = "Deletion test track",
            PrerequisiteTopics = ["C#"],
            IsActive = true
        });

        var branchIds = Enumerable.Range(0, offeringCount)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        foreach (Guid branchId in branchIds)
        {
            dbContext.Branches.Add(new Branch
            {
                Id = branchId,
                Name = $"Branch {branchId:N}",
                Governorate = "Test",
                IsActive = true
            });
        }

        dbContext.AdmissionCycles.Add(new AdmissionCycle
        {
            Id = cycleId,
            ProgramId = programId,
            Label = "Disposable Draft",
            StartDate = new DateOnly(2026, 8, 1),
            DeadlineUtc = new DateTimeOffset(2026, 8, 31, 21, 0, 0, TimeSpan.Zero),
            Status = status,
            ClosedAt = status == CycleStatus.Closed ? DateTimeOffset.UtcNow : null
        });

        dbContext.CycleEligibilityRules.Add(new CycleEligibilityRule
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            RequiredNationality = "EG",
            RequiredDegreeLevel = "Bachelor",
            MaxYearsSinceGraduation = 5,
            MinGrade = CumulativeGrade.Good
        });

        var offeringIds = new List<Guid>();
        foreach (Guid branchId in branchIds)
        {
            Guid offeringId = Guid.NewGuid();
            offeringIds.Add(offeringId);
            dbContext.TrackBranchOfferings.Add(new TrackBranchOffering
            {
                Id = offeringId,
                CycleId = cycleId,
                TrackId = trackId,
                BranchId = branchId,
                Capacity = 25
            });
        }

        if (withApplication)
        {
            dbContext.Applications.Add(new AdmissionApplication
            {
                Id = Guid.NewGuid(),
                ApplicantUserId = "applicant-1",
                CycleId = cycleId,
                Status = ApplicationStatus.Draft,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        return new SeededCycle(cycleId, programId, offeringIds);
    }

    private sealed record SeededCycle(
        Guid CycleId,
        Guid ProgramId,
        IReadOnlyList<Guid> OfferingIds);

    private sealed class TestAdmissionDbContext : DbContext, IAdmissionDbContext
    {
        public TestAdmissionDbContext(DbContextOptions<TestAdmissionDbContext> options)
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
        public DbSet<AdmissionApplication> Applications => Set<AdmissionApplication>();
        public DbSet<ApplicationPreference> ApplicationPreferences => Set<ApplicationPreference>();
        public DbSet<EligibilityResult> EligibilityResults => Set<EligibilityResult>();
        public DbSet<SimulatedStageResult> SimulatedStageResults => Set<SimulatedStageResult>();
        public DbSet<ProgramDocumentRequirement> ProgramDocumentRequirements => Set<ProgramDocumentRequirement>();
        public DbSet<EnrollmentTaskLookup> EnrollmentTaskLookups => Set<EnrollmentTaskLookup>();
        public DbSet<ApplicationEnrollmentTask> ApplicationEnrollmentTasks => Set<ApplicationEnrollmentTask>();

        DatabaseFacade IAdmissionDbContext.Database => Database;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(EduFlowAI.Admission.Infrastructure.Configurations.ProgramConfiguration)
                    .Assembly);
        }
    }
}
