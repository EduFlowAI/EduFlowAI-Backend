using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

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
        Assert.Equal(
            definition.PrerequisiteTopics.ToArray(),
            track.PrerequisiteTopics);
        Assert.Equal(now, track.UpdatedAt);
    }

    [Fact]
    public void Backfill_preserves_existing_administrator_metadata()
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

        var now = originalUpdatedAt.AddDays(1);
        bool changed = AdmissionTrackMetadataSeeder.BackfillMissingMetadata(
            track,
            definition,
            now);

        Assert.False(changed);
        Assert.Equal("Custom administrator description.", track.Description);
        Assert.Equal(new[] { "Custom Topic" }, track.PrerequisiteTopics);
        Assert.Equal(originalUpdatedAt, track.UpdatedAt);
    }

    [Fact]
    public void Backfill_only_fills_missing_fields_and_keeps_existing_description()
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
        Assert.Equal(
            definition.PrerequisiteTopics.ToArray(),
            track.PrerequisiteTopics);
        Assert.Equal(now, track.UpdatedAt);
    }

    [Fact]
    public async Task SeedAsync_backfills_tracks_when_program_and_tracks_use_legacy_ids()
    {
        var options = new DbContextOptionsBuilder<TestAdmissionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new TestAdmissionDbContext(options);

        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Name = "Legacy ITI",
            Code = "ITI",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var legacyProgram = new Program
        {
            Id = Guid.NewGuid(),
            InstitutionId = institution.Id,
            Institution = institution,
            Name = "9-Month Professional Training Program",
            Code = "9M",
            DurationMonths = 9,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Institutions.Add(institution);
        context.Programs.Add(legacyProgram);

        foreach (var definition in AdmissionTrackSeedCatalog.All)
        {
            context.Tracks.Add(new Track
            {
                Id = Guid.NewGuid(),
                ProgramId = legacyProgram.Id,
                Program = legacyProgram,
                Name = definition.SupportedLegacyNames.FirstOrDefault()
                    ?? definition.Name,
                Description = null,
                PrerequisiteTopics = [],
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync();

        var firstRunAt = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        await AdmissionTrackMetadataSeeder.SeedAsync(
            context,
            firstRunAt,
            CancellationToken.None);

        var tracks = await context.Tracks
            .OrderBy(track => track.Name)
            .ToListAsync();

        Assert.Equal(AdmissionTrackSeedCatalog.All.Count, tracks.Count);
        Assert.All(tracks, track => Assert.Equal(legacyProgram.Id, track.ProgramId));
        Assert.Equal(
            AdmissionTrackSeedCatalog.All.Select(track => track.Name).OrderBy(name => name),
            tracks.Select(track => track.Name));
        Assert.All(tracks, track => Assert.False(track.IsActive));
        Assert.Null(tracks.Single(track => track.Name == "3D Generalist").Description);
        Assert.All(
            tracks.Where(track => track.Name != "3D Generalist"),
            track => Assert.False(string.IsNullOrWhiteSpace(track.Description)));
        Assert.Empty(tracks.Single(track =>
            track.Name == "Industrial Automation").PrerequisiteTopics);
        Assert.All(
            tracks.Where(track => track.Name != "Industrial Automation"),
            track => Assert.NotEmpty(track.PrerequisiteTopics));
        Assert.All(tracks, track => Assert.Equal(firstRunAt, track.UpdatedAt));

        await AdmissionTrackMetadataSeeder.SeedAsync(
            context,
            firstRunAt.AddDays(1),
            CancellationToken.None);

        var tracksAfterSecondRun = await context.Tracks
            .OrderBy(track => track.Name)
            .ToListAsync();

        Assert.All(
            tracksAfterSecondRun,
            track => Assert.Equal(firstRunAt, track.UpdatedAt));
    }

    private sealed class TestAdmissionDbContext : DbContext, IAdmissionDbContext
    {
        public TestAdmissionDbContext(
            DbContextOptions<TestAdmissionDbContext> options)
            : base(options)
        {
        }

        public DbSet<Institution> Institutions => Set<Institution>();
        public DbSet<Program> Programs => Set<Program>();
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

        public DbSet<EnrollmentTaskLookup> EnrollmentTaskLookups => Set<EnrollmentTaskLookup>();

        public DbSet<ApplicationEnrollmentTask> ApplicationEnrollmentTasks => Set<ApplicationEnrollmentTask>();
    }
}
