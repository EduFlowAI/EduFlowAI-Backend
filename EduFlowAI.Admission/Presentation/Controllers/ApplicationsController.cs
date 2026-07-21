using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Entities;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduFlowAI.Admission.Presentation.Controllers
{
    [ApiController]
    [Route("api/modules/admission/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        private readonly IAdmissionEligibilityService _eligibilityService;
        private readonly IApplicationStatusQueryService _statusQueryService;
        private readonly IStatusTransitionCoordinator _statusTransitionCoordinator;

        public ApplicationsController(IApplicationService applicationService,
            IAdmissionEligibilityService eligibilityService,
            IApplicationStatusQueryService statusQueryService,
            IStatusTransitionCoordinator statusTransitionCoordinator)
        {
            _applicationService = applicationService;
            _eligibilityService = eligibilityService;
            _statusQueryService=statusQueryService;
            _statusTransitionCoordinator = statusTransitionCoordinator;
        }

        // POST: api/modules/admission/applications/draft
        [HttpPost("draft")]
        public async Task<ActionResult<Result<ApplicationDto>>> CreateDraft([FromBody] ApplicationRequestDto request)
        {
            // Retrieve user ID from claims. Using a dummy value if not authenticated for now.
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value ?? "dummy-user-id";

            var result = await _applicationService.CreateDraftApplicationAsync(userId, request);
            // Use the built-in StatusCode method to return both the HTTP code and the Result object
            return StatusCode(result.StatusCode, result);
        }

        // POST: api/modules/admission/applications/evaluate
        [HttpPost("evaluate")]
        public async Task<ActionResult<Result<EligibilityResult>>> EvaluateApplicant([FromBody] EvaluateEligibilityRequestDto request)
        {
            // 1. Basic validation to ensure required IDs are provided
            if (request == null || request.ApplicantId == Guid.Empty || request.CycleId == Guid.Empty)
            {
                var failureResult = Result<EligibilityResult>.Failure(400, "Invalid request data. Please provide valid IDs.");
                return StatusCode(failureResult.StatusCode, failureResult);
            }

            try
            {
                // 2. Call the application service to execute the business logic
                var result = await _eligibilityService.EvaluateApplicantAsync(
                    request.ApplicantId,
                    request.CycleId,
                    request.ApplicationId
                );

                // 3. Return the evaluation result
                return Ok(result);
            }
            catch(Exception ex)
            {
                // General exception handling
                var errorResult = Result<EligibilityResult>.Failure(500, $"An error occurred during evaluation: {ex.Message}");
                return StatusCode(errorResult.StatusCode, errorResult);
            }
        }


        [HttpGet("{applicationId}/status")]
        public async Task<ActionResult<ApplicationStatusDto>> GetApplicationStatus(Guid applicationId)
        {
            var statusDto = await _statusQueryService.GetApplicationStatusAsync(applicationId);
            if(statusDto == null)
            {
                var error = Result<ApplicationStatusDto>.Failure(400, "Invalid application ID!");
                return StatusCode(error.StatusCode, error);
            }

            // Return OK 200
            var res = Result<ApplicationStatusDto>.Success(statusDto, message: "Application status is retreived successfully");
            return StatusCode(res.StatusCode, res);
        }

        [HttpPost("{applicationId}/withdraw")]
        public async Task<ActionResult<Result<ApplicationStatusDto>>> WithdrawApplication(Guid applicationId)
        {
            // Extract the user ID from the token claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "dummy-user-id";

            // Execute the business logic via the coordinator service
            var (isSuccess, errorMessage, data) = await _statusTransitionCoordinator.WithdrawApplicationAsync(applicationId, userId);

            if (!isSuccess)
            {
                int statusCode = 400; // Default to bad request
                if(errorMessage.Contains("not found"))
                {
                    statusCode = 404;   // Not Found
                }
                else if(errorMessage.Contains("authorized"))
                {
                    statusCode = 403;   // Forbidden
                }

                var failureResult = Result<ApplicationStatusDto>.Failure(statusCode, errorMessage);
                return StatusCode(statusCode, failureResult);
            }

            var successResult = Result<ApplicationStatusDto>.Success(data!, 200, message: "Application withdrawn successfully.");
            return Ok(successResult);
        }

        [HttpPost("{applicationId}/process-document-review")]
        public async Task<ActionResult<Result<ApplicationStatusDto>>> ProcessDocumentReview(Guid applicationId, [FromBody] DocumentReviewResultDto reviewResult)
        {
            if (!ModelState.IsValid)
            {
                var validationFailure = Result<ApplicationStatusDto>.Failure(400, "Invalid review data provided.");
                return BadRequest(validationFailure);
            }

            // 2. Execute the business logic via the coordinator service
            var (isSuccess, errorMessage, data) = await _statusTransitionCoordinator.ProcessDocumentReviewAsync(applicationId, reviewResult);

            // 3. Handle failure scenarios and map to appropriate HTTP Status Codes
            if (!isSuccess)
            {
                int statusCode = 400;
                if (errorMessage.Contains("not found"))
                {
                    statusCode = 404;   // Not Found

                }
                
                var failureResult = Result<ApplicationStatusDto>.Failure(statusCode, errorMessage);
                return StatusCode(statusCode, failureResult);
            }

            // 4. Construct the Result object for a successful operation and return 200 OK
            var successResult = Result<ApplicationStatusDto>.Success(data!, 200, message: "Document review processed successfully.");
            return Ok(successResult);
        }

    }
}