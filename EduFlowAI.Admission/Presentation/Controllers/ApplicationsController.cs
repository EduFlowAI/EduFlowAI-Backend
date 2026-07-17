using System.Security.Claims;
using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.Admission.Presentation.Controllers
{
    [ApiController]
    [Route("api/modules/admission/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _applicationService;

        public ApplicationsController(IApplicationService applicationService)
        {
            _applicationService = applicationService;
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
    }
}
