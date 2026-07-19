using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Application.Services;
using EduFlowAI.Admission.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;


namespace EduFlowAI.Admission
{
    public static class AdmissionModule
    {
        public static IServiceCollection AddAdmissionModule(this IServiceCollection services)
        {
            // Application + Infrastructure services registrations go here later
            // Register AutoMapper profiles from the Application project
            services.AddAutoMapper(cfg =>
                cfg.AddMaps(typeof(Application.MappingProfiles.ApplicationProfile).Assembly));

            // Register Application Layer services
            services.AddScoped<IApplicationService, ApplicationService>();

            // Register Infrastructure Layer repositories
            services.AddScoped<IApplicationRepository, ApplicationRepository>();
            return services;
        }

        // Called only by Api — Worker never calls this
        // public static IEndpointRouteBuilder MapAdmissionPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
