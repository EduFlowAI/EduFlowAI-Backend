using EduFlowAI.Admission.Application.DbContextAbstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlowAI.Admission.Infrastructure.Seeding;

public static class AdmissionSeedingExtensions
{
    /// <summary>
    /// Runs Admission seed data from the API host only.
    /// The Worker must not call this method because simultaneous seed execution
    /// can race between the two processes.
    /// </summary>
    public static async Task SeedAdmissionDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var admissionDbContext = scope.ServiceProvider
            .GetRequiredService<IAdmissionDbContext>();

        if (admissionDbContext is not DbContext efDbContext)
        {
            throw new InvalidOperationException(
                "The registered IAdmissionDbContext must also inherit from Microsoft.EntityFrameworkCore.DbContext.");
        }

        await AdmissionDataSeeder.SeedAsync(
            efDbContext,
            cancellationToken);
    }
}
