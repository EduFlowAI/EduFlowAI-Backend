using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Application.Services.Configuration;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionConfigurationValidationTests
{
    private readonly AdmissionConfigurationService _service = new(
        new UnusedAdmissionDbContext(),
        new UnusedApplicationReader(),
        TimeProvider.System);

    [Fact]
    public async Task Requirements_reject_undefined_enum_values_before_database_access()
    {
        var request = new UpdateProgramDocumentRequirementsRequest
        {
            Requirements =
            [
                new ProgramDocumentRequirementInput
                {
                    DocumentType = (DocumentType)999
                }
            ]
        };

        var result = await _service.ReplaceProgramRequirementsAsync(
            Guid.NewGuid(),
            request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Requirements_reject_duplicate_natural_keys()
    {
        var request = new UpdateProgramDocumentRequirementsRequest
        {
            Requirements =
            [
                new ProgramDocumentRequirementInput
                {
                    DocumentType = DocumentType.NationalId
                },
                new ProgramDocumentRequirementInput
                {
                    DocumentType = DocumentType.NationalId
                }
            ]
        };

        var result = await _service.ReplaceProgramRequirementsAsync(
            Guid.NewGuid(),
            request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Offerings_reject_duplicate_track_branch_pairs()
    {
        var trackId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var request = new ConfigureOfferingsRequest
        {
            Offerings =
            [
                new OfferingInput
                {
                    TrackId = trackId,
                    BranchId = branchId,
                    Capacity = 10
                },
                new OfferingInput
                {
                    TrackId = trackId,
                    BranchId = branchId,
                    Capacity = 20
                }
            ]
        };

        var result = await _service.ConfigureOfferingsAsync(
            Guid.NewGuid(),
            request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Tracks_reject_oversized_prerequisite_topics()
    {
        var request = new CreateTrackRequest
        {
            ProgramId = Guid.NewGuid(),
            Name = "Cloud Architecture",
            PrerequisiteTopics = [new string('x', 101)]
        };

        var result = await _service.CreateTrackAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Prerequisite_topics_are_trimmed_and_deduplicated_case_insensitively()
    {
        var result = AdmissionConfigurationService.ValidateAndNormalizeTopics(
            ["  C#  ", "c#", "Databases", " "]);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "C#", "Databases" }, result.Topics);
    }

    [Fact]
    public async Task Eligibility_rules_reject_undefined_grade_values()
    {
        var request = new UpdateCycleEligibilityRuleRequest
        {
            MinGrade = (CumulativeGrade)999
        };

        var result = await _service.UpsertEligibilityRuleAsync(
            Guid.NewGuid(),
            request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    private sealed class UnusedApplicationReader : IAdmissionApplicationReader
    {
        public Task<bool> IsInstitutionConfigurationLockedAsync(
            Guid institutionId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();

        public Task<bool> IsProgramConfigurationLockedAsync(
            Guid programId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();

        public Task<bool> IsTrackConfigurationLockedAsync(
            Guid trackId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();

        public Task<bool> IsBranchConfigurationLockedAsync(
            Guid branchId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();

        public Task<bool> HasApplicationsForCycleAsync(
            Guid cycleId,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();

        public Task<bool> HasPreferencesForOfferingsAsync(
            IReadOnlyCollection<Guid> offeringIds,
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();

        public Task<int> CountApplicationsAsync(
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();
    }

    private sealed class UnusedAdmissionDbContext : IAdmissionDbContext
    {
        public DbSet<Institution> Institutions => throw UnexpectedDatabaseAccess();
        public DbSet<EduFlowAI.Admission.Domain.Entities.Program> Programs =>
            throw UnexpectedDatabaseAccess();
        public DbSet<AdmissionCycle> AdmissionCycles => throw UnexpectedDatabaseAccess();
        public DbSet<CycleEligibilityRule> CycleEligibilityRules => throw UnexpectedDatabaseAccess();
        public DbSet<Track> Tracks => throw UnexpectedDatabaseAccess();
        public DbSet<Branch> Branches => throw UnexpectedDatabaseAccess();
        public DbSet<TrackBranchOffering> TrackBranchOfferings => throw UnexpectedDatabaseAccess();
        public DbSet<EduFlowAI.Admission.Domain.Entities.Application> Applications =>
            throw UnexpectedDatabaseAccess();
        public DbSet<ApplicationPreference> ApplicationPreferences => throw UnexpectedDatabaseAccess();
        public DbSet<EligibilityResult> EligibilityResults => throw UnexpectedDatabaseAccess();
        public DbSet<SimulatedStageResult> SimulatedStageResults => throw UnexpectedDatabaseAccess();
        public DbSet<ProgramDocumentRequirement> ProgramDocumentRequirements => throw UnexpectedDatabaseAccess();
        public DatabaseFacade Database => throw UnexpectedDatabaseAccess();

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            throw UnexpectedDatabaseAccess();
    }

    private static InvalidOperationException UnexpectedDatabaseAccess()
    {
        return new InvalidOperationException(
            "Validation should complete before any database access.");
    }
}
