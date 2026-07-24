using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using EduFlowAI.Identity.Application.DbContextAbstraction;
using EduFlowAI.Identity.Application.DTOs;
using EduFlowAI.Identity.Domain.Constants;
using EduFlowAI.Identity.Domain.Entities;
using EduFlowAI.Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Identity.Presentation.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Applicant)]
[Route("api/profile")]
public sealed class ProfileController( IIdentityDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await dbContext.AppUsers
            .AsNoTracking()
            .Include(item => item.ApplicantProfile)
            .SingleOrDefaultAsync(
                item => item.Id == userId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Ok(ToResponse(user));
    }

    [HttpPut]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status409Conflict)]
   
    public async Task<ActionResult<ProfileResponse>> Put([FromBody] UpdateProfileRequest request,CancellationToken cancellationToken)
    {
        ValidateBusinessRules(request);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await dbContext.AppUsers
            .Include(item => item.ApplicantProfile)
            .SingleOrDefaultAsync(
                item => item.Id == userId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var normalized = Normalize(request);
        var duplicateNationalId = await dbContext.ApplicantProfiles
            .AsNoTracking()
            .AnyAsync(
                item => item.NationalId == normalized.NationalId &&
                    item.UserId != userId,
                cancellationToken);

        if (duplicateNationalId)
        {
            return ConflictProblem(
                nameof(request.NationalId),
                "This National ID is already associated with an account.");
        }

        var now = DateTimeOffset.UtcNow;
        var profile = user.ApplicantProfile;
        if (profile is null)
        {
            profile = new ApplicantProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                CreatedAt = now,
                IsProfileLocked = false
            };
            dbContext.ApplicantProfiles.Add(profile);
            user.ApplicantProfile = profile;
        }
        else if (profile.IsProfileLocked &&
            ProtectedFieldsChanged(profile, normalized))
        {
            return ConflictProblem(
                "profile",
                "Eligibility and document-verification fields are " +
                "permanently locked after application submission.");
        }

        if (!profile.IsProfileLocked)
        {
            profile.FullNameEn = normalized.FullNameEn;
            profile.FullNameAr = normalized.FullNameAr;
            profile.NationalId = normalized.NationalId;
            profile.Nationality = normalized.Nationality;
            profile.DateOfBirth = normalized.DateOfBirth;
            profile.Gender = normalized.Gender;
            profile.University = normalized.University;
            profile.Faculty = normalized.Faculty;
            profile.DegreeLevel = normalized.DegreeLevel;
            profile.Major = normalized.Major;
            profile.GraduationYear = normalized.GraduationYear;
            profile.CumulativeGrade = normalized.CumulativeGrade;
            profile.MilitaryStatus = normalized.MilitaryStatus;
        }

        profile.Address = normalized.Address;
        profile.Governorate = normalized.Governorate;
        profile.UpdatedAt = now;

        user.PhoneNumber = normalized.PhoneNumber;
        user.PreferredLanguage = normalized.PreferredLanguage;
        user.GmailNotificationsEnabled =
            normalized.GmailNotificationsEnabled;
        user.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse( User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private void ValidateBusinessRules(UpdateProfileRequest request)
    {
        var requiredStrings = new (string Key, string Value)[]
        {
            (nameof(request.FullNameEn), request.FullNameEn),
            (nameof(request.FullNameAr), request.FullNameAr),
            (nameof(request.Nationality), request.Nationality),
            (nameof(request.University), request.University),
            (nameof(request.Faculty), request.Faculty),
            (nameof(request.DegreeLevel), request.DegreeLevel),
            (nameof(request.Major), request.Major)
        };

        foreach (var (key, value) in requiredStrings)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ModelState.AddModelError(key, "The field cannot be blank.");
            }
        }

        if (request.DateOfBirth == default ||
            request.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(
                nameof(request.DateOfBirth),
                "Date of birth must be in the past.");
        }

        var currentYear = DateTime.UtcNow.Year;
        if (request.GraduationYear < 1900 ||
            request.GraduationYear > currentYear)
        {
            ModelState.AddModelError(
                nameof(request.GraduationYear),
                $"Graduation year must be between 1900 and {currentYear}.");
        }

        if (request.Gender is not (Gender.Male or Gender.Female))
        {
            ModelState.AddModelError(
                nameof(request.Gender),
                "Gender must be Male or Female.");
        }

        if (request.CumulativeGrade is not (
            CumulativeGrade.Acceptable or
            CumulativeGrade.Good or
            CumulativeGrade.VeryGood or
            CumulativeGrade.Excellent))
        {
            ModelState.AddModelError(
                nameof(request.CumulativeGrade),
                "Cumulative grade is invalid.");
        }

        if (request.Gender == Gender.Female &&
            request.MilitaryStatus is not null)
        {
            ModelState.AddModelError(
                nameof(request.MilitaryStatus),
                "Military status must be omitted for female applicants.");
        }

        if (request.Gender == Gender.Male &&
            request.MilitaryStatus is not (
                MilitaryStatus.Completed or
                MilitaryStatus.Exempted or
                MilitaryStatus.Postponed or
                MilitaryStatus.CurrentlyServing))
        {
            ModelState.AddModelError(
                nameof(request.MilitaryStatus),
                "A valid military status is required for male applicants.");
        }
    }

    private static NormalizedProfileRequest Normalize(UpdateProfileRequest request) =>
        new(
            request.FullNameEn.Trim(),
            request.FullNameAr.Trim(),
            request.NationalId.Trim(),
            request.Nationality.Trim().ToUpperInvariant(),
            request.DateOfBirth,
            request.Gender,
            TrimOrNull(request.Address),
            TrimOrNull(request.Governorate),
            request.University.Trim(),
            request.Faculty.Trim(),
            request.DegreeLevel.Trim(),
            request.Major.Trim(),
            request.GraduationYear,
            request.CumulativeGrade,
            request.MilitaryStatus,
            TrimOrNull(request.PhoneNumber),
            request.PreferredLanguage,
            request.GmailNotificationsEnabled);

    private static bool ProtectedFieldsChanged(ApplicantProfile profile,NormalizedProfileRequest request) =>
        profile.FullNameEn != request.FullNameEn ||
        profile.FullNameAr != request.FullNameAr ||
        profile.NationalId != request.NationalId ||
        profile.Nationality != request.Nationality ||
        profile.DateOfBirth != request.DateOfBirth ||
        profile.Gender != request.Gender ||
        profile.University != request.University ||
        profile.Faculty != request.Faculty ||
        profile.DegreeLevel != request.DegreeLevel ||
        profile.Major != request.Major ||
        profile.GraduationYear != request.GraduationYear ||
        profile.CumulativeGrade != request.CumulativeGrade ||
        profile.MilitaryStatus != request.MilitaryStatus;

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ActionResult ConflictProblem(string key, string error) =>
        Conflict(new ValidationProblemDetails(
            new Dictionary<string, string[]>
            {
                [key] = [error]
            })
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Profile update conflict"
        });

    private static ProfileResponse ToResponse(AppUser user)
    {
        var profile = user.ApplicantProfile;
        return new ProfileResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            user.PreferredLanguage,
            user.GmailNotificationsEnabled,
            IsComplete: profile is not null,
            IsProfileLocked: profile?.IsProfileLocked ?? false,
            profile?.FullNameEn,
            profile?.FullNameAr,
            profile?.NationalId,
            profile?.Nationality,
            profile is null ? null : profile.DateOfBirth,
            profile?.Gender.ToString(),
            profile?.Address,
            profile?.Governorate,
            profile?.University,
            profile?.Faculty,
            profile?.DegreeLevel,
            profile?.Major,
            profile is null ? null : profile.GraduationYear,
            profile?.CumulativeGrade.ToString(),
            profile?.MilitaryStatus?.ToString(),
            profile?.CreatedAt,
            profile?.UpdatedAt);
    }

    private sealed record NormalizedProfileRequest(
        string FullNameEn,
        string FullNameAr,
        string NationalId,
        string Nationality,
        DateOnly DateOfBirth,
        Gender Gender,
        string? Address,
        string? Governorate,
        string University,
        string Faculty,
        string DegreeLevel,
        string Major,
        int GraduationYear,
        CumulativeGrade CumulativeGrade,
        MilitaryStatus? MilitaryStatus,
        string? PhoneNumber,
        string PreferredLanguage,
        bool GmailNotificationsEnabled);
}