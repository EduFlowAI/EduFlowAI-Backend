using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.Admission.Infrastructure.Seeding
{
    internal static class AdmissionDataSeeder
    {
        internal static void Seed(DbContext context)
        {
            var institution = context.Set<Institution>()
                .SingleOrDefault(x => x.Code == "ITI");

            if (institution is null)
            {
                institution = CreateInstitution();
                context.Set<Institution>().Add(institution);
            }

            var program = context.Set<Program>()
                .SingleOrDefault(x => x.Code == "9M");

            if (program is null)
            {
                program = CreateNineMonthProgram(institution.Id);
                context.Set<Program>().Add(program);
            }

            SeedProgramDocumentRequirements(context, program.Id);
            SeedTracks(context, program.Id);
            SeedBranches(context);

            context.SaveChanges();
        }

        internal static async Task SeedAsync(
            DbContext context,
            CancellationToken cancellationToken)
        {
            var institution = await context.Set<Institution>()
                .SingleOrDefaultAsync(
                    x => x.Code == "ITI",
                    cancellationToken);

            if (institution is null)
            {
                institution = CreateInstitution();
                context.Set<Institution>().Add(institution);
            }

            var program = await context.Set<Program>()
                .SingleOrDefaultAsync(
                    x => x.Code == "9M",
                    cancellationToken);

            if (program is null)
            {
                program = CreateNineMonthProgram(institution.Id);
                context.Set<Program>().Add(program);
            }

            await SeedProgramDocumentRequirementsAsync(
                context,
                program.Id,
                cancellationToken);

            await SeedTracksAsync(
                context,
                program.Id,
                cancellationToken);

            await SeedBranchesAsync(
                context,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }

        private static void SeedProgramDocumentRequirements(
            DbContext context,
            Guid programId)
        {
            var existingRequirements = context.Set<ProgramDocumentRequirement>()
                .Where(x => x.ProgramId == programId)
                .Select(x => new { x.DocumentType, x.RequiredForGender })
                .ToList();

            var missingRequirements = CreateProgramDocumentRequirements(programId)
                .Where(seed => !existingRequirements.Any(existing =>
                    existing.DocumentType == seed.DocumentType &&
                    existing.RequiredForGender == seed.RequiredForGender))
                .ToList();

            context.Set<ProgramDocumentRequirement>()
                .AddRange(missingRequirements);
        }

        private static async Task SeedProgramDocumentRequirementsAsync(
            DbContext context,
            Guid programId,
            CancellationToken cancellationToken)
        {
            var existingRequirements = await context.Set<ProgramDocumentRequirement>()
                .Where(x => x.ProgramId == programId)
                .Select(x => new { x.DocumentType, x.RequiredForGender })
                .ToListAsync(cancellationToken);

            var missingRequirements = CreateProgramDocumentRequirements(programId)
                .Where(seed => !existingRequirements.Any(existing =>
                    existing.DocumentType == seed.DocumentType &&
                    existing.RequiredForGender == seed.RequiredForGender))
                .ToList();

            context.Set<ProgramDocumentRequirement>()
                .AddRange(missingRequirements);
        }

        private static void SeedTracks(
            DbContext context,
            Guid programId)
        {
            var existingTrackNames = context.Set<Track>()
                .Where(x => x.ProgramId == programId)
                .Select(x => x.Name)
                .ToHashSet();

            var missingTracks = CreateTracks(programId)
                .Where(x => !existingTrackNames.Contains(x.Name))
                .ToList();

            context.Set<Track>().AddRange(missingTracks);
        }

        private static async Task SeedTracksAsync(
            DbContext context,
            Guid programId,
            CancellationToken cancellationToken)
        {
            var existingTrackNames = (await context.Set<Track>()
                .Where(x => x.ProgramId == programId)
                .Select(x => x.Name)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var missingTracks = CreateTracks(programId)
                .Where(x => !existingTrackNames.Contains(x.Name))
                .ToList();

            context.Set<Track>().AddRange(missingTracks);
        }

        private static void SeedBranches(DbContext context)
        {
            var existingBranchNames = context.Set<Branch>()
                .Select(x => x.Name)
                .ToHashSet();

            var missingBranches = CreateBranches()
                .Where(x => !existingBranchNames.Contains(x.Name))
                .ToList();

            context.Set<Branch>().AddRange(missingBranches);
        }

        private static async Task SeedBranchesAsync(
            DbContext context,
            CancellationToken cancellationToken)
        {
            var existingBranchNames = (await context.Set<Branch>()
                .Select(x => x.Name)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var missingBranches = CreateBranches()
                .Where(x => !existingBranchNames.Contains(x.Name))
                .ToList();

            context.Set<Branch>().AddRange(missingBranches);
        }

        private static Institution CreateInstitution()
        {
            return new Institution
            {
                Id = AdmissionSeedIds.ItiInstitutionId,
                Name = "Information Technology Institute",
                Code = "ITI"
            };
        }

        private static Program CreateNineMonthProgram(Guid institutionId)
        {
            return new Program
            {
                Id = AdmissionSeedIds.NineMonthProgramId,
                InstitutionId = institutionId,
                Name = "9-Month Professional Training Program",
                Code = "9M",
                DurationMonths = 9
            };
        }

        private static IReadOnlyList<ProgramDocumentRequirement>
            CreateProgramDocumentRequirements(Guid programId)
        {
            return new List<ProgramDocumentRequirement>
            {
                new()
                {
                    Id = AdmissionSeedIds.NationalIdRequirementId,
                    ProgramId = programId,
                    DocumentType = DocumentType.NationalId,
                    RequiredForGender = null
                },
                new()
                {
                    Id = AdmissionSeedIds.BirthCertificateRequirementId,
                    ProgramId = programId,
                    DocumentType = DocumentType.BirthCertificate,
                    RequiredForGender = null
                },
                new()
                {
                    Id = AdmissionSeedIds.GraduationCertificateRequirementId,
                    ProgramId = programId,
                    DocumentType = DocumentType.GraduationCertificate,
                    RequiredForGender = null
                },
                new()
                {
                    Id = AdmissionSeedIds.MilitaryCertificateRequirementId,
                    ProgramId = programId,
                    DocumentType = DocumentType.MilitaryCertificate,
                    RequiredForGender = Gender.Male
                }
            };
        }

        private static IReadOnlyList<Track> CreateTracks(Guid programId)
        {
            return new List<Track>
            {
                CreateTrack(
                    AdmissionSeedIds.OpenSourceApplicationsDevelopmentTrackId,
                    programId,
                    "Open Source Applications Development"),

                CreateTrack(
                    AdmissionSeedIds.AiAndMachineLearningTrackId,
                    programId,
                    "AI and Machine Learning"),

                CreateTrack(
                    AdmissionSeedIds.SystemsAdministrationTrackId,
                    programId,
                    "Systems Administration"),

                CreateTrack(
                    AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
                    programId,
                    "Professional Development & BI-infused CRM"),

                CreateTrack(
                    AdmissionSeedIds.CloudPlatformDevelopmentTrackId,
                    programId,
                    "Cloud Platform Development"),

                CreateTrack(
                    AdmissionSeedIds.CloudArchitectureTrackId,
                    programId,
                    "Cloud Architecture"),

                CreateTrack(
                    AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId,
                    programId,
                    "Integrated Software Development & Architecture"),

                CreateTrack(
                    AdmissionSeedIds.CyberSecurityTrackId,
                    programId,
                    "Cyber Security"),

                CreateTrack(
                    AdmissionSeedIds.EnterpriseJavaTrackId,
                    programId,
                    "Enterprise & Web Applications Development (Java)"),

                CreateTrack(
                    AdmissionSeedIds.TelecomApplicationDevelopmentTrackId,
                    programId,
                    "Telecom Application Development")
            };
        }

        private static Track CreateTrack(
            Guid id,
            Guid programId,
            string name)
        {
            return new Track
            {
                Id = id,
                ProgramId = programId,
                Name = name,
                Description = null,
                PrerequisiteTopics = new List<string>(),
                IsActive = true
            };
        }

        private static IReadOnlyList<Branch> CreateBranches()
        {
            return new List<Branch>
            {
                new()
                {
                    Id = AdmissionSeedIds.SmartVillageBranchId,
                    Name = "Smart Village",
                    Governorate = "Giza"
                },
                new()
                {
                    Id = AdmissionSeedIds.AlexandriaBranchId,
                    Name = "Alexandria",
                    Governorate = "Alexandria"
                },
                new()
                {
                    Id = AdmissionSeedIds.AssiutBranchId,
                    Name = "Assiut",
                    Governorate = "Assiut"
                },
                new()
                {
                    Id = AdmissionSeedIds.MansouraBranchId,
                    Name = "Mansoura",
                    Governorate = "Dakahlia"
                },
                new()
                {
                    Id = AdmissionSeedIds.ZagazigBranchId,
                    Name = "Zagazig",
                    Governorate = "Sharqia"
                }
            };
        }
    }
}
