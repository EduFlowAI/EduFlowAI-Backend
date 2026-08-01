using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Extensions;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Admission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Admission.Application.Services
{
    public class ApplicationStatusQueryService : IApplicationStatusQueryService
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IGenericRepository<SimulatedStageResult> _stageRepository;
        private readonly ITimelineCalculator _timelineCalculator;

        public ApplicationStatusQueryService(IApplicationRepository applicationRepository,
            IGenericRepository<SimulatedStageResult> stageRepository,
            ITimelineCalculator timelineCalculator)
        {
            _applicationRepository = applicationRepository;
            _stageRepository = stageRepository;
            _timelineCalculator = timelineCalculator;
        }

        public async Task<ApplicationStatusDto?> GetApplicationStatusAsync(Guid applicationId)
        {
            var application = await _applicationRepository.GetFirstOrDefaultAsync(app => app.Id == applicationId);
            if(application == null)
            {
                return null;
            }

            return new ApplicationStatusDto
            {
                ApplicationId = applicationId,
                CurrentStatus = application.Status.ToString(),
                LastUpdatedAt = application.UpdatedAt,
                StatusMessage = "Status retrieved successfully."
            };
        }

        public async Task<ApplicationStatusDto?> GetCurrentApplicationStatusForApplicantAsync(string applicantUserId, CancellationToken cancellationToken = default)
        {
            var application = await _applicationRepository.GetFirstOrDefaultAsync(app => app.ApplicantUserId == applicantUserId);
            if(application == null)
            {
                return null;
            }

            return new ApplicationStatusDto
            {
                ApplicationId = application.Id,
                CurrentStatus = application.Status.ToString(),
                LastUpdatedAt = application.UpdatedAt,
                StatusMessage = "Status retrieved successfully."
            };
        }

        public async Task<ApplicationDashboardSummaryDto?> GetDashboardSummaryAsync(Guid applicationId)
        {
            // Fetch the application with its related Cycle and EligibilityResult using the generic repository's include parameter
            var application = await _applicationRepository.GetFirstOrDefaultAsync(
                predicate: app => app.Id == applicationId,
                include: query => query.Include(a => a.Cycle)
                                 .Include(a => a.EligibilityResult)
            );

            if (application == null)
            {
                return null;
            }

            // Determine eligibility text based on the Passed boolean
            string eligibilityText = application.EligibilityResult != null
                ? (application.EligibilityResult.Passed ? "Eligible" : "Not Eligible")
                : "Pending";

            // Fetch all simulated stages for this application from the database
            var stages = await _stageRepository.GetAllAsync(s => s.ApplicationId == applicationId) ?? new List<SimulatedStageResult>();

            // Map the domain status to the frontend timeline stages dynamically
            var timeline = _timelineCalculator.Calculate(application.Status, stages, application.EligibilityResult);
            
            return new ApplicationDashboardSummaryDto
            {
                ApplicationId = application.Id,
                IntakeName = application.Cycle?.Label ?? "Unknown Intake",
                SubmittedAt = application.SubmittedAt,
                LastUpdatedAt = application.UpdatedAt,
                CurrentStatus = application.Status.ToString(),
                EligibilityResult = eligibilityText,
                StatusMessage = application.Status.GetDisplayMessage(),

                // Bind the newly calculated timeline fields
                TimelineProgressPercentage = timeline.Percentage,
                ApplicationPhaseStatus = timeline.AppStatus,
                EligibilityPhaseStatus = timeline.EligStatus,
                VerificationPhaseStatus = timeline.VerifStatus,
                EnglishIqPhaseStatus = timeline.EngStatus,
                TechnicalPhaseStatus = timeline.TechStatus,
                InterviewPhaseStatus = timeline.IntStatus,
                FinalResultPhaseStatus = timeline.FinalStatus
            };
        }

        public async Task<ApplicationDetailsDto?> GetApplicationDetailsAsync(Guid applicationId, string applicantUserId, CancellationToken cancellationToken = default)
        {
            // Fetch application with its related preferences, ensuring the user owns it
            var application = await _applicationRepository.GetFirstOrDefaultAsync(
                predicate: app => app.Id == applicationId && app.ApplicantUserId == applicantUserId,
                include: query => query.Include(a => a.Cycle)   // Required to fetch Cycle Label and Deadline
                                       .Include(a => a.Preferences)
                                       .ThenInclude(p => p.TrackBranchOffering)     // Required to extract TrackId and BranchId
            );

            if (application == null)
                return null;

            // Map preferences entity to DTO
            var preferencesDto = application.Preferences?
                .OrderBy(p => p.Rank)
                .Select(p => new PreferenceDto(p.TrackBranchOffering.TrackId,
                p.TrackBranchOffering.BranchId,
                p.Rank))
                .ToList() ?? new List<PreferenceDto>();

            return new ApplicationDetailsDto(
                application.Id,
                application.ApplicantUserId,
                application.CycleId,
                application.Cycle?.Label ?? string.Empty, // Map Label to CycleName
                application.Cycle?.DeadlineUtc ?? DateTimeOffset.MinValue, // Map DeadlineUtc safely
                application.Status.ToString(),
                application.CreatedAt,
                application.UpdatedAt,
                preferencesDto
            );
        }
    }
}
