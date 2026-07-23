using EduFlowAI.Admission;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Application.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionModuleRegistrationTests
{
    [Fact]
    public void AddAdmissionModule_registers_configuration_service_exactly_once()
    {
        var services = new ServiceCollection();

        services.AddAdmissionModule();

        var registration = Assert.Single(
            services.Where(descriptor =>
                descriptor.ServiceType == typeof(IAdmissionConfigurationService)));
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        Assert.NotNull(registration.ImplementationType);
    }

    [Fact]
    public void AddAdmissionModule_registers_requirement_resolution_and_time()
    {
        var services = new ServiceCollection();

        services.AddAdmissionModule();

        Assert.Single(services.Where(descriptor =>
            descriptor.ServiceType == typeof(IProgramRequirementReader)));
        Assert.Single(services.Where(descriptor =>
            descriptor.ServiceType == typeof(TimeProvider)));
    }
}
