using EduFlowAI.Admission.Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionSeedingGuardTests
{
    [Fact]
    public void Admission_module_requires_explicit_seeding_opt_in()
    {
        var services = new ServiceCollection();

        services.AddAdmissionModule();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType ==
                typeof(AdmissionDataSeedHostedService));

        services.AddAdmissionDataSeeding();

        var registration = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType ==
                    typeof(AdmissionDataSeedHostedService));
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public void Worker_module_does_not_register_admission_seed_hosted_service()
    {
        var services = new ServiceCollection();

        services.AddAdmissionWorkerModule();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType ==
                typeof(AdmissionDataSeedHostedService));
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
