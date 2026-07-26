using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Shared.Kernel.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduFlowAI.Admission.Presentation.Controllers
{
    [ApiController]
    [Route("api/replacement/[controller]")]
    public class ReplacementRequestController : ControllerBase
    {
        private readonly IReplacementRequestService _replacementRequestService;

        public ReplacementRequestController(IReplacementRequestService replacementRequestService)
        {
            _replacementRequestService = replacementRequestService;
        }

        [HttpPost("send-replacement-request")]
        //[Authorize]
        public async Task<IActionResult> SendReplacementRequest([FromBody] ReplacementRequestDto request)
        {
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId = "1"; // for test
            var result = await _replacementRequestService.SendReplacementRequest(request, userId, default);
            if (!result.IsSuccess)
            {
                return BadRequest(result.Message);
            }
            return StatusCode(result.StatusCode, result.Message);
        }

        [HttpGet("applicant/replacement-requests")]
        public async Task<IActionResult> GetReplacementRequests([FromQuery] QueryParameters queryParameters)
        {
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId = "1"; // for test
            var result = await _replacementRequestService.GetAllReplacemntRequestsAsync(userId,queryParameters);

            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode,result.Message);
            }

            return StatusCode(result.StatusCode, result.Data);
        }

        [HttpGet("applicant/replacement-requests/{requestId}")]
        public async Task<IActionResult> GetReplacementRequest(Guid requestId)
        {
            var result = await _replacementRequestService.GetReplacemntRequestAsync(requestId);

            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, result.Message);
            }

            return StatusCode(result.StatusCode, result.Data);
        }
    }
}