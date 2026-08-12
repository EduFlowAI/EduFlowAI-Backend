using EduFlowAI.Admission.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionSeedingGuardTests
{
    [Fact]
    public void AddAdmissionDataSeeding_does_not_register_startup_hosted_service()
    {
        var services = new ServiceCollection();

        services.AddAdmissionDataSeeding();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public async Task Explicit_seed_preflight_allows_postgresql_context_without_compiled_migrations()
    {
        var options = new DbContextOptionsBuilder<PreflightDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Database=unused;Username=unused;Password=unused")
            .Options;
        await using var context = new PreflightDbContext(options);

        Assert.Empty(context.Database.GetMigrations());

        await AdmissionDataSeeder.ValidateDatabasePreconditionsAsync(
            context.Database,
            CancellationToken.None);
    }

    [Fact]
    public async Task Worker_host_cannot_run_admission_seeding()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment
        {
            ApplicationName = "EduFlowAI.Worker"
        });
        await using var provider = services.BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.SeedAdmissionDataAsync());

        Assert.Contains("EduFlowAI.Api", exception.Message);
        Assert.Contains("EduFlowAI.Worker", exception.Message);
    }

    private sealed class PreflightDbContext : DbContext
    {
        public PreflightDbContext(DbContextOptions<PreflightDbContext> options)
            : base(options)
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
