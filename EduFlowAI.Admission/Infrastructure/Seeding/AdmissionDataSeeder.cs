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
                new()
                {
                    Id = AdmissionSeedIds.OpenSourceApplicationsDevelopmentTrackId,
                    ProgramId = programId,
                    Name = "Open Source Applications Development",
                    Description = "Build modern applications using open-source technologies and frameworks.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Programming Fundamentals",
                        "Object-Oriented Programming",
                        "Databases"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.AiAndMachineLearningTrackId,
                    ProgramId = programId,
                    Name = "AI and Machine Learning",
                    Description = "Develop machine-learning solutions and data-driven intelligent applications.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Python",
                        "Statistics",
                        "Problem Solving"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.SystemsAdministrationTrackId,
                    ProgramId = programId,
                    Name = "Systems Administration",
                    Description = "Administer operating systems, enterprise infrastructure, and networked services.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Operating Systems",
                        "Networking Fundamentals",
                        "Linux"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
                    ProgramId = programId,
                    Name = "Professional Development & BI-infused CRM",
                    Description = "Build business applications using CRM platforms, analytics, and full-stack development.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Programming Fundamentals",
                        "Databases",
                        "Business Applications"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.CloudPlatformDevelopmentTrackId,
                    ProgramId = programId,
                    Name = "Cloud Platform Development",
                    Description = "Design and build applications that run on modern cloud platforms.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Programming Fundamentals",
                        "Web Development",
                        "Databases"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.CloudArchitectureTrackId,
                    ProgramId = programId,
                    Name = "Cloud Architecture",
                    Description = "Design secure, scalable, and reliable cloud infrastructure and solutions.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Networking Fundamentals",
                        "Operating Systems",
                        "Cloud Fundamentals"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId,
                    ProgramId = programId,
                    Name = "Integrated Software Development & Architecture",
                    Description = "Develop enterprise software while applying architecture and integration practices.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Object-Oriented Programming",
                        "Software Engineering",
                        "Databases"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.CyberSecurityTrackId,
                    ProgramId = programId,
                    Name = "Cyber Security",
                    Description = "Protect systems, networks, and applications against security threats.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Networking Fundamentals",
                        "Operating Systems",
                        "Security Fundamentals"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.EnterpriseJavaTrackId,
                    ProgramId = programId,
                    Name = "Enterprise & Web Applications Development (Java)",
                    Description = "Build enterprise and web applications using Java technologies.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Java",
                        "Object-Oriented Programming",
                        "Databases"
                    }
                },
                new()
                {
                    Id = AdmissionSeedIds.TelecomApplicationDevelopmentTrackId,
                    ProgramId = programId,
                    Name = "Telecom Application Development",
                    Description = "Develop software solutions for telecommunications systems and services.",
                    PrerequisiteTopics = new List<string>
                    {
                        "Programming Fundamentals",
                        "Networking Fundamentals",
                        "Data Structures"
                    }
                }
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
