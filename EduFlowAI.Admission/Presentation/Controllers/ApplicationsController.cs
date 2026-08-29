using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Shared.Kernel.Common;
using EduFlowAI.Shared.Kernel.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduFlowAI.Admission.Presentation.Controllers
{
    [ApiController]
    [Route("api/modules/admission/[controller]")]
    [Authorize]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        private readonly IAdmissionEligibilityService _eligibilityService;
        private readonly IApplicationStatusQueryService _statusQueryService;
        private readonly IStatusTransitionCoordinator _statusTransitionCoordinator;
        private readonly IAssessmentSimulationService _simulationService;

        public ApplicationsController(IApplicationService applicationService,
            IAdmissionEligibilityService eligibilityService,
            IApplicationStatusQueryService statusQueryService,
            IStatusTransitionCoordinator statusTransitionCoordinator,
            IAssessmentSimulationService simulationService)
        {
            _applicationService = applicationService;
            _eligibilityService = eligibilityService;
            _statusQueryService=statusQueryService;
            _statusTransitionCoordinator = statusTransitionCoordinator;
            _simulationService = simulationService;
        }

        // POST: api/modules/admission/applications/draft
        [HttpPost("draft")]
        public async Task<ActionResult<Result<ApplicationDto>>> CreateDraft([FromBody] ApplicationRequestDto request)
        {
            // Retrieve user ID from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var unauthorizedError = Result<ApplicationDto>.Failure(401, "User is not authenticated or token is invalid.");
                return StatusCode(unauthorizedError.StatusCode, unauthorizedError);
            }

            var result = await _applicationService.CreateDraftApplicationAsync(userId, request);

            return StatusCode(result.StatusCode, result);
        }

        // POST api/modules/admission/applications/{applicationId}/submit
        [HttpPost("{applicationId:guid}/submit")]
        public async Task<ActionResult<Result<ApplicationDetailsDto>>> SubmitApplication(Guid applicationId, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(Result<ApplicationDetailsDto>.Failure(401, "User is not authenticated."));
            }

            var result = await _applicationService.SubmitApplicationAsync(applicationId, userId, cancellationToken);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, result);
            }
            return Ok(result);
        }

        [HttpPost("{applicationId}/withdraw")]
        public async Task<ActionResult<Result<ApplicationStatusDto>>> WithdrawApplication(Guid applicationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var unauthorizedError = Result<ApplicationStatusDto>.Failure(401, "User is not authenticated or token is invalid.");
                return StatusCode(unauthorizedError.StatusCode, unauthorizedError);
            }

            // Execute the business logic via the coordinator service
            var (isSuccess, error, data) = await _statusTransitionCoordinator.WithdrawApplicationAsync(applicationId, userId);

            if (!isSuccess)
            {
                var failureResult = Result<ApplicationStatusDto>.Failure(error!.StatusCode, error.Message);
                return StatusCode(error.StatusCode, failureResult);
            }

            var successResult = Result<ApplicationStatusDto>.Success(data!, 200, message: "Application withdrawn successfully.");
            return Ok(successResult);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("{applicationId}/process-document-review")]
        public async Task<ActionResult<Result<ApplicationStatusDto>>> ProcessDocumentReview(Guid applicationId, [FromBody] DocumentReviewResultDto reviewResult)
        {
            if (!ModelState.IsValid)
            {
                var validationFailure = Result<ApplicationStatusDto>.Failure(400, "Invalid review data provided.");
                return BadRequest(validationFailure);
            }

            // 2. Execute the business logic via the coordinator service
            var (isSuccess, error, data) = await _statusTransitionCoordinator.ProcessDocumentReviewAsync(applicationId, reviewResult);

            // 3. Handle failure scenarios and map to appropriate HTTP Status Codes
            if (!isSuccess)
            {
                var failureResult = Result<ApplicationStatusDto>.Failure(error!.StatusCode, error.Message);
                return StatusCode(error.StatusCode, failureResult);
            }

            var successResult = Result<ApplicationStatusDto>.Success(data!, 200, message: "Document review processed successfully.");
            return Ok(successResult);
        }

        [HttpPut("{applicationId}/preferences")]
        public async Task<ActionResult<Result<ApplicationDetailsDto>>> UpdatePreferences(
            Guid applicationId,
            [FromBody] UpdateApplicationPreferencesDto request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var unauthorizedError = Result<ApplicationDetailsDto>.Failure(401, "User is not authenticated or token is invalid.");
                return StatusCode(unauthorizedError.StatusCode, unauthorizedError);
            }

            var result = await _applicationService.UpdateApplicationPreferencesAsync(applicationId, userId, request, cancellationToken);

            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{applicationId}/status")]
        public async Task<ActionResult<ApplicationStatusDto>> GetApplicationStatus(Guid applicationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) 
                return Unauthorized();

            var statusDto = await _statusQueryService.GetApplicationStatusAsync(applicationId, userId);
            if (statusDto == null)
            {
                var error = Result<ApplicationStatusDto>.Failure(400, "Application not found or unauthorized.");
                return StatusCode(error.StatusCode, error);
            }

            // Return OK 200
            var res = Result<ApplicationStatusDto>.Success(statusDto, message: "Application status is retreived successfully");
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{applicationId}/stages")]
        public async Task<ActionResult<Result<List<SimulatedStageDto>>>> GetStagesResults(Guid applicationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) 
                return Unauthorized();

            var stagesResultDto = await _simulationService.GetApplicantStagesAsync(applicationId, userId);

            // Return OK 200
            var res = Result<IEnumerable<SimulatedStageDto>>.Success(stagesResultDto, message: "Applicant stages results are retreived successfully");
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{applicationId}/dashboard-summary")]
        public async Task<ActionResult<Result<ApplicationDashboardSummaryDto>>> GetDashboardSummary(Guid applicationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) 
                return Unauthorized();

            var summaryDto = await _statusQueryService.GetDashboardSummaryAsync(applicationId, userId);
            if (summaryDto == null)
            {
                var error = Result<ApplicationDashboardSummaryDto>.Failure(404, "Application not found!");
                return StatusCode(error.StatusCode, error);
            }

            var successResult = Result<ApplicationDashboardSummaryDto>.Success(
                summaryDto,
                message: "Dashboard summary retrieved successfully"
            );

            return StatusCode(successResult.StatusCode, successResult);
        }

        [HttpGet("{applicationId}")]
        public async Task<ActionResult<Result<ApplicationDetailsDto>>> GetApplicationDetails(Guid applicationId, CancellationToken cancellationToken)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var unauthorizedError = Result<ApplicationDetailsDto>.Failure(401, "User is not authenticated or token is invalid.");
                return StatusCode(unauthorizedError.StatusCode, unauthorizedError);
            }

            var detailsDto = await _statusQueryService.GetApplicationDetailsAsync(applicationId, userId, cancellationToken);
            if(detailsDto == null)
            {
                var error = Result<ApplicationDetailsDto>.Failure(404, "Application not found or unauthorized access!");
                return StatusCode(error.StatusCode, error);
            }

            var successResult = Result<ApplicationDetailsDto>.Success(detailsDto, message: "Application details retrieved successfully");
            return StatusCode(successResult.StatusCode, successResult);
        }

        [HttpGet("my-applications")]
        public async Task<ActionResult<Result<PaginatedResult<ApplicationListDto>>>> GetMyApplications(
            [FromQuery] QueryParameters queryParameters,
            CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var unauthorizedError = Result<PaginatedResult<ApplicationListDto>>.Failure(401, "User is not authenticated or token is invalid.");
                return StatusCode(unauthorizedError.StatusCode, unauthorizedError);
            }

            var paginatedResult = await _statusQueryService.GetMyApplicationsAsync(userId, queryParameters, cancellationToken);

            var successResult = Result<PaginatedResult<ApplicationListDto>>.Success(
                paginatedResult, message: "Applications retrieved successfully."
            );

            return Ok(successResult);
        }

        [HttpGet("{applicationId}/eligibility")]
        public async Task<ActionResult<Result<EligibilityDetailsDto>>> GetEligibilityDetails(Guid applicationId,
            CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var unauthorizedError = Result<EligibilityDetailsDto>.Failure(401, "User is not authenticated or token is invalid.");
                return StatusCode(unauthorizedError.StatusCode, unauthorizedError);
            }

            var detailsDto = await _statusQueryService.GetEligibilityDetailsAsync(applicationId, userId, cancellationToken);
            if(detailsDto == null)
            {
                var error = Result<EligibilityDetailsDto>.Failure(404, "Application not found, unauthorized, or eligibility results not available yet.");
                return StatusCode(error.StatusCode, error);
            }

            var successResult = Result<EligibilityDetailsDto>.Success(
                detailsDto,
                message: "Eligibility details retrieved successfully."
            );

            return Ok(successResult);
        }

        [HttpGet("{applicationId}/enrollment-checklist")]
        public async Task<ActionResult<Result<EnrollmentChecklistDto>>> GetEnrollmentChecklist(
            Guid applicationId,
            CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var unauthorizedError = Result<EnrollmentChecklistDto>.Failure(401, "User is not authenticated or token is invalid.");
                return StatusCode(unauthorizedError.StatusCode, unauthorizedError);
            }

            var checklistDto = await _statusQueryService.GetEnrollmentChecklistAsync(applicationId, userId, cancellationToken);

            // If null is returned, it means the application doesn't exist or belongs to someone else
            if (checklistDto == null)
            {
                var error = Result<EnrollmentChecklistDto>.Failure(404, "Application not found or unauthorized access.");
                return StatusCode(error.StatusCode, error);
            }

            var successResult = Result<EnrollmentChecklistDto>.Success(
                checklistDto,
                message: "Enrollment checklist retrieved successfully."
            );

            return Ok(successResult);
        }
    }
}