using EduFlowAI.Admission.Application.Features.Dashboard;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.Admission.Presentation.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/admin/dashboard")]
public sealed class AdminAdmissionDashboardController : ControllerBase
{
    private readonly IAdmissionDashboardService _dashboardService;

    public AdminAdmissionDashboardController(
        IAdmissionDashboardService dashboardService)
    {
        ArgumentNullException.ThrowIfNull(dashboardService);
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<Result<AdminAdmissionDashboardDto>>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(
            cancellationToken);

        return Ok(Result<AdminAdmissionDashboardDto>.Success(dashboard));
    }
}
