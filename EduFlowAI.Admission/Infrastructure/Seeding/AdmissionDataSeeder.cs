using EduFlowAI.Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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

            bool programExists = context.Set<Program>()
                .Any(x => x.Code == "9M");

            if (!programExists)
            {
                context.Set<Program>().Add(
                    CreateNineMonthProgram(institution.Id));
            }

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

            bool programExists = await context.Set<Program>()
                .AnyAsync(
                    x => x.Code == "9M",
                    cancellationToken);

            if (!programExists)
            {
                context.Set<Program>().Add(
                    CreateNineMonthProgram(institution.Id));
            }

            await context.SaveChangesAsync(cancellationToken);
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
    }
}