using EduFlowAI.Admission.Application.DbContextAbstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EduFlowAI.Admission.Infrastructure.Seeding;

public static class AdmissionSeedingExtensions
{
    private const string ApiApplicationName = "EduFlowAI.Api";

    /// <summary>
    /// Runs Admission seed data after database migrations have completed.
    /// The method rejects non-API hosts and coordinates concurrent API replicas
    /// with a transaction-scoped PostgreSQL advisory lock.
    /// </summary>
    public static async Task SeedAdmissionDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var hostEnvironment = scope.ServiceProvider
            .GetRequiredService<IHostEnvironment>();

        if (!string.Equals(
                hostEnvironment.ApplicationName,
                ApiApplicationName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Admission seed data may run only from '{ApiApplicationName}', " +
                $"not '{hostEnvironment.ApplicationName}'.");
        }

        var admissionDbContext = scope.ServiceProvider
            .GetRequiredService<IAdmissionDbContext>();
        var timeProvider = scope.ServiceProvider
            .GetRequiredService<TimeProvider>();

        await AdmissionDataSeeder.SeedAsync(
            admissionDbContext,
            timeProvider,
            cancellationToken);
    }
}
