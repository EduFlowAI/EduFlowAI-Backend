using EduFlowAI.Admission.Application.EligibilityRuleStrategy;
using EduFlowAI.Admission.Application.Features.Branches;
using EduFlowAI.Admission.Application.Features.Configuration.Common;
using EduFlowAI.Admission.Application.Features.Cycles;
using EduFlowAI.Admission.Application.Features.Dashboard;
using EduFlowAI.Admission.Application.Features.Offerings;
using EduFlowAI.Admission.Application.Features.Programs;
using EduFlowAI.Admission.Application.Features.Requirements;
using EduFlowAI.Admission.Application.Features.Tracks;
using EduFlowAI.Admission.Application.Interfaces;
using EduFlowAI.Admission.Application.Interfaces.EligibilityRuleStrategy;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Application.Services;
using EduFlowAI.Admission.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduFlowAI.Admission
{
    public static class AdmissionModule
    {
        public static IServiceCollection AddAdmissionModule(
            this IServiceCollection services)
        {
            services.AddAutoMapper(cfg =>
                cfg.AddMaps(
                    typeof(Application.MappingProfiles.ApplicationProfile)
                        .Assembly));

            // Shared mechanics for Admission configuration writes.
            services.AddScoped<AdmissionWriteExecutor>();
            services.AddScoped<CycleConfigurationGuard>();

            // Abdallah-owned Admission feature boundaries.
            services.AddScoped<IProgramConfigurationService, ProgramConfigurationService>();
            services.AddScoped<IProgramRequirementService, ProgramRequirementService>();
            services.AddScoped<ITrackService, TrackService>();
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<IAdmissionCycleService, AdmissionCycleService>();
            services.AddScoped<IOfferingService, OfferingService>();
            services.AddScoped<IAdmissionDashboardService, AdmissionDashboardService>();

            // Public contracts consumed by other modules.
            services.AddScoped<IProgramRequirementReader, ProgramRequirementReader>();
            services.AddScoped<IOfferedTrackReader, OfferedTrackReader>();
            services.AddScoped<IAdmissionApplicationReader, AdmissionApplicationReader>();
            services.TryAddSingleton(TimeProvider.System);

            // Application Layer services.
            services.AddScoped<IApplicationService, ApplicationService>();

            // Infrastructure Layer repositories.
            services.AddScoped<IApplicationRepository, ApplicationRepository>();
            services.AddScoped(
                typeof(IGenericRepository<>),
                typeof(GenericRepository<>));

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
            services.AddScoped<ITimelineCalculator, TimelineCalculator>();
            services.AddScoped<ICycleQueryService, CycleQueryService>();

            return services;
        }

        // for worker
        public static IServiceCollection AddAdmissionWorkerModule(
            this IServiceCollection services)
        {
            services.AddScoped<IApplicationRepository, ApplicationRepository>();
            services.AddScoped<IStatusTransitionCoordinator, StatusTransitionCoordinator>();
            services.AddScoped<IApplicationStatusQueryService, ApplicationStatusQueryService>();
            services.AddScoped<IOfferedTrackReader, OfferedTrackReader>();

            return services;
        }
    }
}
