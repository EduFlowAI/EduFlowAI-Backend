using EduFlowAI.Admission.Application.EligibilityRuleStrategy;
using EduFlowAI.Admission.Application.Interfaces;
using EduFlowAI.Admission.Application.Interfaces.EligibilityRuleStrategy;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Application.Services;
using EduFlowAI.Admission.Application.Services.Configuration;
using EduFlowAI.Admission.Infrastructure.Repositories;
using EduFlowAI.AI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduFlowAI.Admission
{
    public static class AdmissionModule
    {
        public static IServiceCollection AddAdmissionModule(this IServiceCollection services)
        {
            // Register AutoMapper profiles from the Application project.
            services.AddAutoMapper(cfg =>
                cfg.AddMaps(typeof(Application.MappingProfiles.ApplicationProfile).Assembly));

            // Admission catalog, configuration, cycles and Super Admin operations.
            services.AddScoped<IAdmissionConfigurationService, AdmissionConfigurationService>();
            services.AddScoped<IProgramRequirementReader, ProgramRequirementReader>();
            services.AddScoped<IOfferedTrackReader, OfferedTrackReader>();
            services.AddScoped<IAdmissionApplicationReader, AdmissionApplicationReader>();
            services.TryAddSingleton(TimeProvider.System);

            // Application Layer services.
            services.AddScoped<IApplicationService, ApplicationService>();

            // Infrastructure Layer repositories.
            services.AddScoped<IApplicationRepository, ApplicationRepository>();

            // Register the Generic Repository as Open Generic.
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            #region Register Eligibility rules and services
            services.AddScoped<IEligibilityRuleStrategy, GraduationRecencyRule>();
            services.AddScoped<IEligibilityRuleStrategy, CumulativeGradeRule>();
            services.AddScoped<IEligibilityRuleStrategy, NationalityRule>();
            services.AddScoped<IEligibilityRuleStrategy, MilitaryStatusRule>();
            services.AddScoped<IEligibilityEngine, EligibilityEngine>();
            services.AddScoped<IAdmissionEligibilityService, AdmissionEligibilityService>();
            #endregion

            services.AddScoped<IStatusTransitionCoordinator, StatusTransitionCoordinator>();
            services.AddScoped<IApplicationStatusQueryService, ApplicationStatusQueryService>();
            services.AddScoped<IApplicationAcessReader, ApplicationAcessReader>();
            services.AddScoped<IAdminOperationsService, AdminOperationsService>();
            services.AddScoped<IAssessmentSimulationService, AssessmentSimulationService>();
            services.AddScoped<IFinalRankingService, FinalRankingService>();

            return services;
        }

        // Called only by Api — Worker never calls this.
        // public static IEndpointRouteBuilder MapAdmissionPresentation(this IEndpointRouteBuilder app) { ... }
    }
}
