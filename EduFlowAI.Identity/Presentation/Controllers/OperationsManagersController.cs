using Microsoft.AspNetCore.Http;
using EduFlowAI.Identity.Application.DTOs;
using EduFlowAI.Identity.Domain.Constants;
using EduFlowAI.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduFlowAI.Identity.Presentation.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.SuperAdmin)]
[Route("api/admin/operations-managers")]
public sealed class OperationsManagersController(
    UserManager<AppUser> userManager) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OperationsManagerResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationsManagerResponse>> Create(
        [FromBody] CreateOperationsManagerRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PreferredLanguage = request.PreferredLanguage,
            IsActive = true,
            LockoutEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var createResult = await userManager.CreateAsync(
            user,
            request.Password);
        if (!createResult.Succeeded)
        {
            return IdentityValidationProblem(createResult);
        }

        var roleResult = await userManager.AddToRoleAsync(
            user,
            AppRoles.OperationsManager);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return IdentityValidationProblem(roleResult);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ToResponse(user));
    }

    [HttpPatch("{userId:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null ||
            !await userManager.IsInRoleAsync(
                user,
                AppRoles.OperationsManager))
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Operations Manager not found");
        }

        if (!user.IsActive)
        {
            return NoContent();
        }

        var now = DateTimeOffset.UtcNow;
        user.IsActive = false;
        user.DeactivatedAt = now;
        user.UpdatedAt = now;
        user.SecurityStamp = Guid.NewGuid().ToString();

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        return NoContent();
    }

    private static OperationsManagerResponse ToResponse(AppUser user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.IsActive,
            user.CreatedAt,
            user.DeactivatedAt);

    private ActionResult IdentityValidationProblem(
        IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.Description)
                    .ToArray());

        return BadRequest(new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Identity validation failed"
        });
    }
}