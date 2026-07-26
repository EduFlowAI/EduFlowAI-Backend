using EduFlowAI.Admission;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Application.Services;
using EduFlowAI.Admission.Application.Services.Configuration;
using EduFlowAI.AI;
using EduFlowAI.AI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
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
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IAdmissionConfigurationService));

        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        Assert.Equal(
            typeof(AdmissionConfigurationService),
            registration.ImplementationType);
    }

    [Fact]
    public void AddAdmissionModule_registers_requirement_resolution_and_time()
    {
        var services = new ServiceCollection();

        services.AddAdmissionModule();

        var requirementRegistration = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IProgramRequirementReader));

        Assert.Equal(
            typeof(ProgramRequirementReader),
            requirementRegistration.ImplementationType);

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void Admission_and_AI_modules_keep_the_real_offered_track_reader()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddAdmissionModule();
        services.AddAIModule(configuration);

        var registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOfferedTrackReader));

        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        Assert.Equal(typeof(OfferedTrackReader), registration.ImplementationType);
    }
}
