
using EduFlowAI.Communication.Application.Interfaces;
using EduFlowAI.Shared.Kernel.Common.Pagination;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace EduFlowAI.Communication.Presentation.Controllers;

//[Authorize]
[ApiController]
[Route("api/communication/notifications")]
public sealed class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllNotificationsByUserId([FromQuery] QueryParameters queryParameters, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _notificationService.GetAllNotificationsByUserIdAsync(userId!, queryParameters, cancellationToken);

        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, result.Message);
        }

        return StatusCode(result.StatusCode, result.Data);
    }
}