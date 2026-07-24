using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using EduFlowAI.Identity.Application.DTOs;
using EduFlowAI.Identity.Application.Interfaces;
using EduFlowAI.Identity.Domain.Constants;
using EduFlowAI.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EduFlowAI.Identity.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("Authentication")]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IJwtTokenService jwtTokenService,
    IIdentityEmailSender emailSender,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
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
            AppRoles.Applicant);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return IdentityValidationProblem(roleResult);
        }

        var token = await userManager
            .GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendConfirmationTokenAsync(
            user,
            token,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new RegisterResponse(
                user.Id,
                email,
                RequiresEmailConfirmation: true,
                DevelopmentConfirmationToken:
                    environment.IsDevelopment() ? token : null));
    }

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return UnauthorizedProblem();
        }

        if (!user.IsActive)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account inactive",
                detail: "This account has been deactivated.");
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account locked",
                detail: "Too many failed attempts. Try again later.");
        }

        if (signInResult.IsNotAllowed)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Email confirmation required",
                detail: "Confirm the account email before signing in.");
        }

        if (!signInResult.Succeeded)
        {
            return UnauthorizedProblem();
        }

        var token = await jwtTokenService.CreateAsync(
            user,
            cancellationToken);

        return Ok(new LoginResponse(
            token.AccessToken,
            token.TokenType,
            token.ExpiresAtUtc,
            token.Roles));
    }

    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(
                nameof(request.Token),
                "The email confirmation request is invalid.");
            return ValidationProblem(ModelState);
        }

        if (user.EmailConfirmed)
        {
            return NoContent();
        }

        var result = await userManager.ConfirmEmailAsync(
            user,
            request.Token);
        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType<ForgotPasswordResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        const string message =
            "If an eligible account exists, password reset instructions " +
            "have been issued.";

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        string? developmentToken = null;

        if (user is { IsActive: true, EmailConfirmed: true })
        {
            var token = await userManager
                .GeneratePasswordResetTokenAsync(user);
            await emailSender.SendPasswordResetTokenAsync(
                user,
                token,
                cancellationToken);

            if (environment.IsDevelopment())
            {
                developmentToken = token;
            }
        }

        return Accepted(new ForgotPasswordResponse(
            message,
            developmentToken));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(
                nameof(request.Token),
                "The password reset request is invalid.");
            return ValidationProblem(ModelState);
        }

        var result = await userManager.ResetPasswordAsync(
            user,
            request.Token,
            request.NewPassword);
        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    private ActionResult UnauthorizedProblem() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Invalid credentials",
        detail: "The email or password is incorrect.");

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