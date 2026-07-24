using System.Data;
using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Admission.Infrastructure.Seeding;

internal static class AdmissionDataSeeder
{
    private const long AdmissionSeedLockKey = 1_164_618_325_329_019_809;

    internal static async Task SeedAsync(
        IAdmissionDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!string.Equals(
                context.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Admission seed data requires the PostgreSQL EF Core provider.");
        }

        if (!context.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException(
                "Admission seed data requires an EF Core migration for the shared database schema.");
        }

        var pendingMigrations = await context.Database
            .GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            throw new InvalidOperationException(
                "Apply all pending database migrations before running Admission seed data.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0});",
            new object[] { AdmissionSeedLockKey },
            cancellationToken);

        var now = timeProvider.GetUtcNow();
        var institution = await ResolveInstitutionAsync(context, now, cancellationToken);
        var program = await ResolveProgramAsync(
            context,
            institution.Id,
            now,
            cancellationToken);

        await SeedProgramDocumentRequirementsAsync(
            context,
            program.Id,
            now,
            cancellationToken);

        await SeedTracksAsync(context, program.Id, now, cancellationToken);
        await SeedBranchesAsync(context, now, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<Institution> ResolveInstitutionAsync(
        IAdmissionDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seed = CreateInstitution(now);
        var candidates = await context.Institutions
            .Where(institution =>
                institution.Id == seed.Id ||
                EF.Functions.ILike(institution.Code, seed.Code))
            .ToListAsync(cancellationToken);

        var idMatch = candidates.SingleOrDefault(candidate => candidate.Id == seed.Id);
        if (idMatch is not null &&
            !StringComparer.OrdinalIgnoreCase.Equals(idMatch.Code, seed.Code))
        {
            throw SeedCollision(
                nameof(Institution),
                seed.Id,
                $"the existing row has code '{idMatch.Code}' instead of '{seed.Code}'");
        }

        var codeMatches = candidates
            .Where(candidate =>
                StringComparer.OrdinalIgnoreCase.Equals(candidate.Code, seed.Code))
            .ToList();

        EnsureAtMostOneBusinessKeyMatch(
            nameof(Institution),
            $"code '{seed.Code}'",
            codeMatches.Count);

        var existing = codeMatches.SingleOrDefault();
        if (existing is not null)
        {
            if (idMatch is not null && idMatch.Id != existing.Id)
            {
                throw SeedCollision(
                    nameof(Institution),
                    seed.Id,
                    $"the stable ID and code '{seed.Code}' identify different rows");
            }

            return existing;
        }

        context.Institutions.Add(seed);
        return seed;
    }

    private static async Task<Program> ResolveProgramAsync(
        IAdmissionDbContext context,
        Guid institutionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seed = CreateNineMonthProgram(institutionId, now);
        var candidates = await context.Programs
            .Where(program =>
                program.Id == seed.Id ||
                EF.Functions.ILike(program.Code, seed.Code))
            .ToListAsync(cancellationToken);

        var idMatch = candidates.SingleOrDefault(candidate => candidate.Id == seed.Id);
        if (idMatch is not null &&
            (!StringComparer.OrdinalIgnoreCase.Equals(idMatch.Code, seed.Code) ||
             idMatch.InstitutionId != institutionId))
        {
            throw SeedCollision(
                nameof(Program),
                seed.Id,
                "the existing row has a different code or parent institution");
        }

        var codeMatches = candidates
            .Where(candidate =>
                StringComparer.OrdinalIgnoreCase.Equals(candidate.Code, seed.Code))
            .ToList();

        EnsureAtMostOneBusinessKeyMatch(
            nameof(Program),
            $"code '{seed.Code}'",
            codeMatches.Count);

        var existing = codeMatches.SingleOrDefault();
        if (existing is not null)
        {
            if (existing.InstitutionId != institutionId)
            {
                throw new InvalidOperationException(
                    $"Admission seed conflict: program code '{seed.Code}' already belongs " +
                    "to a different institution.");
            }

            if (idMatch is not null && idMatch.Id != existing.Id)
            {
                throw SeedCollision(
                    nameof(Program),
                    seed.Id,
                    $"the stable ID and code '{seed.Code}' identify different rows");
            }

            return existing;
        }

        context.Programs.Add(seed);
        return seed;
    }

    private static async Task SeedProgramDocumentRequirementsAsync(
        IAdmissionDbContext context,
        Guid programId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seeds = CreateProgramDocumentRequirements(programId, now);
        var seedIds = seeds.Select(seed => seed.Id).ToArray();
        var candidates = await context.ProgramDocumentRequirements
            .Where(requirement =>
                requirement.ProgramId == programId ||
                seedIds.Contains(requirement.Id))
            .ToListAsync(cancellationToken);

        foreach (var seed in seeds)
        {
            var idMatch = candidates.SingleOrDefault(candidate => candidate.Id == seed.Id);
            if (idMatch is not null &&
                (idMatch.ProgramId != seed.ProgramId ||
                 idMatch.DocumentType != seed.DocumentType ||
                 idMatch.RequiredForGender != seed.RequiredForGender))
            {
                throw SeedCollision(
                    nameof(ProgramDocumentRequirement),
                    seed.Id,
                    "the existing row has a different program, document type, or gender");
            }

            var naturalKeyMatches = candidates
                .Where(candidate =>
                    candidate.ProgramId == seed.ProgramId &&
                    candidate.DocumentType == seed.DocumentType &&
                    candidate.RequiredForGender == seed.RequiredForGender)
                .ToList();

            EnsureAtMostOneBusinessKeyMatch(
                nameof(ProgramDocumentRequirement),
                $"{seed.ProgramId}/{seed.DocumentType}/{seed.RequiredForGender?.ToString() ?? "All"}",
                naturalKeyMatches.Count);

            var existing = naturalKeyMatches.SingleOrDefault();
            if (idMatch is not null && existing is not null && idMatch.Id != existing.Id)
            {
                throw SeedCollision(
                    nameof(ProgramDocumentRequirement),
                    seed.Id,
                    "the stable ID and natural key identify different rows");
            }

            if (existing is null)
            {
                context.ProgramDocumentRequirements.Add(seed);
            }
        }
    }

    private static async Task SeedTracksAsync(
        IAdmissionDbContext context,
        Guid programId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seeds = CreateTracks(programId, now);
        var seedIds = seeds.Select(seed => seed.Id).ToArray();
        var candidates = await context.Tracks
            .Where(track => track.ProgramId == programId || seedIds.Contains(track.Id))
            .ToListAsync(cancellationToken);

        foreach (var seed in seeds)
        {
            var idMatch = candidates.SingleOrDefault(candidate => candidate.Id == seed.Id);
            if (idMatch is not null &&
                (idMatch.ProgramId != seed.ProgramId ||
                 !StringComparer.OrdinalIgnoreCase.Equals(idMatch.Name, seed.Name)))
            {
                throw SeedCollision(
                    nameof(Track),
                    seed.Id,
                    "the existing row has a different program or name");
            }

            var naturalKeyMatches = candidates
                .Where(candidate =>
                    candidate.ProgramId == seed.ProgramId &&
                    StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, seed.Name))
                .ToList();

            EnsureAtMostOneBusinessKeyMatch(
                nameof(Track),
                $"{seed.ProgramId}/'{seed.Name}'",
                naturalKeyMatches.Count);

            var existing = naturalKeyMatches.SingleOrDefault();
            if (idMatch is not null && existing is not null && idMatch.Id != existing.Id)
            {
                throw SeedCollision(
                    nameof(Track),
                    seed.Id,
                    "the stable ID and natural key identify different rows");
            }

            if (existing is null)
            {
                context.Tracks.Add(seed);
            }
        }
    }

    private static async Task SeedBranchesAsync(
        IAdmissionDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seeds = CreateBranches(now);
        var candidates = await context.Branches
            .ToListAsync(cancellationToken);

        foreach (var seed in seeds)
        {
            var idMatch = candidates.SingleOrDefault(candidate => candidate.Id == seed.Id);
            if (idMatch is not null &&
                !StringComparer.OrdinalIgnoreCase.Equals(idMatch.Name, seed.Name))
            {
                throw SeedCollision(
                    nameof(Branch),
                    seed.Id,
                    $"the existing row has name '{idMatch.Name}' instead of '{seed.Name}'");
            }

            var naturalKeyMatches = candidates
                .Where(candidate =>
                    StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, seed.Name))
                .ToList();

            EnsureAtMostOneBusinessKeyMatch(
                nameof(Branch),
                $"name '{seed.Name}'",
                naturalKeyMatches.Count);

            var existing = naturalKeyMatches.SingleOrDefault();
            if (idMatch is not null && existing is not null && idMatch.Id != existing.Id)
            {
                throw SeedCollision(
                    nameof(Branch),
                    seed.Id,
                    "the stable ID and natural key identify different rows");
            }

            if (existing is null)
            {
                context.Branches.Add(seed);
            }
        }
    }

    private static void EnsureAtMostOneBusinessKeyMatch(
        string entityName,
        string businessKey,
        int matchCount)
    {
        if (matchCount > 1)
        {
            throw new InvalidOperationException(
                $"Admission seed conflict: {matchCount} {entityName} rows match " +
                $"business key {businessKey}. Remove the duplicate rows before seeding.");
        }
    }

    private static InvalidOperationException SeedCollision(
        string entityName,
        Guid stableId,
        string reason)
    {
        return new InvalidOperationException(
            $"Admission seed conflict for {entityName} ID '{stableId}': {reason}.");
    }

    private static Institution CreateInstitution(DateTimeOffset now)
    {
        return new Institution
        {
            Id = AdmissionSeedIds.ItiInstitutionId,
            Name = "Information Technology Institute",
            Code = "ITI",
            CreatedAt = now
        };
    }

    private static Program CreateNineMonthProgram(
        Guid institutionId,
        DateTimeOffset now)
    {
        return new Program
        {
            Id = AdmissionSeedIds.NineMonthProgramId,
            InstitutionId = institutionId,
            Name = "9-Month Professional Training Program",
            Code = "9M",
            DurationMonths = 9,
            CreatedAt = now
        };
    }

    private static IReadOnlyList<ProgramDocumentRequirement>
        CreateProgramDocumentRequirements(Guid programId, DateTimeOffset now)
    {
        return
        [
            new()
            {
                Id = AdmissionSeedIds.NationalIdRequirementId,
                ProgramId = programId,
                DocumentType = DocumentType.NationalId,
                RequiredForGender = null,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Id = AdmissionSeedIds.BirthCertificateRequirementId,
                ProgramId = programId,
                DocumentType = DocumentType.BirthCertificate,
                RequiredForGender = null,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Id = AdmissionSeedIds.GraduationCertificateRequirementId,
                ProgramId = programId,
                DocumentType = DocumentType.GraduationCertificate,
                RequiredForGender = null,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Id = AdmissionSeedIds.MilitaryCertificateRequirementId,
                ProgramId = programId,
                DocumentType = DocumentType.MilitaryCertificate,
                RequiredForGender = Gender.Male,
                CreatedAt = now,
                UpdatedAt = now
            }
        ];
    }

    private static IReadOnlyList<Track> CreateTracks(
        Guid programId,
        DateTimeOffset now)
    {
        return
        [
            CreateTrack(
                AdmissionSeedIds.OpenSourceApplicationsDevelopmentTrackId,
                programId,
                "Open Source Applications Development",
                now),
            CreateTrack(
                AdmissionSeedIds.AiAndMachineLearningTrackId,
                programId,
                "AI and Machine Learning",
                now),
            CreateTrack(
                AdmissionSeedIds.SystemsAdministrationTrackId,
                programId,
                "Systems Administration",
                now),
            CreateTrack(
                AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
                programId,
                "Professional Development & BI-infused CRM",
                now),
            CreateTrack(
                AdmissionSeedIds.CloudPlatformDevelopmentTrackId,
                programId,
                "Cloud Platform Development",
                now),
            CreateTrack(
                AdmissionSeedIds.CloudArchitectureTrackId,
                programId,
                "Cloud Architecture",
                now),
            CreateTrack(
                AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId,
                programId,
                "Integrated Software Development & Architecture",
                now),
            CreateTrack(
                AdmissionSeedIds.CyberSecurityTrackId,
                programId,
                "Cyber Security",
                now),
            CreateTrack(
                AdmissionSeedIds.EnterpriseJavaTrackId,
                programId,
                "Enterprise & Web Applications Development (Java)",
                now),
            CreateTrack(
                AdmissionSeedIds.TelecomApplicationDevelopmentTrackId,
                programId,
                "Telecom Application Development",
                now)
        ];
    }

    private static Track CreateTrack(
        Guid id,
        Guid programId,
        string name,
        DateTimeOffset now)
    {
        return new Track
        {
            Id = id,
            ProgramId = programId,
            Name = name,
            Description = null,
            PrerequisiteTopics = [],
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<Branch> CreateBranches(DateTimeOffset now)
    {
        return
        [
            new()
            {
                Id = AdmissionSeedIds.SmartVillageBranchId,
                Name = "Smart Village",
                Governorate = "Giza",
                IsActive = true,
                CreatedAt = now
            },
            new()
            {
                Id = AdmissionSeedIds.AlexandriaBranchId,
                Name = "Alexandria",
                Governorate = "Alexandria",
                IsActive = true,
                CreatedAt = now
            },
            new()
            {
                Id = AdmissionSeedIds.AssiutBranchId,
                Name = "Assiut",
                Governorate = "Assiut",
                IsActive = true,
                CreatedAt = now
            },
            new()
            {
                Id = AdmissionSeedIds.MansouraBranchId,
                Name = "Mansoura",
                Governorate = "Dakahlia",
                IsActive = true,
                CreatedAt = now
            },
            new()
            {
                Id = AdmissionSeedIds.ZagazigBranchId,
                Name = "Zagazig",
                Governorate = "Sharqia",
                IsActive = true,
                CreatedAt = now
            }
        ];
    }
}
