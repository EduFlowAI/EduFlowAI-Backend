using EduFlowAI.Identity.Application.DbContextAbstraction;
using EduFlowAI.Identity.Application.DTOs;
using EduFlowAI.Identity.Application.Interfaces;
using EduFlowAI.Identity.Domain.Entities;
using EduFlowAI.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Identity.Application.Services;

internal sealed class ProfileService(
    IIdentityDbContext dbContext) : IProfileService
{
    public async Task<IdentityOperationResult<ProfileResponse>> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AppUsers
            .AsNoTracking()
            .Include(item => item.ApplicantProfile)
            .SingleOrDefaultAsync(
                item => item.Id == userId,
                cancellationToken);

        return user is null || !user.IsActive
            ? Unauthorized()
            : IdentityOperationResult<ProfileResponse>.Success(
                ToResponse(user));
    }

    public async Task<IdentityOperationResult<ProfileResponse>> UpdateAsync(
        string userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return IdentityOperationResult<ProfileResponse>.Fail(
                new IdentityFailure(
                    IdentityFailureKind.Validation,
                    "Profile validation failed",
                    Errors: validationErrors));
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
            return Conflict(
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
            return Conflict(
                "profile",
                "Eligibility and document-verification fields are " +
                "permanently locked after application submission.");
        }

        if (!profile.IsProfileLocked)
        {
            UpdateProtectedFields(profile, normalized);
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
        return IdentityOperationResult<ProfileResponse>.Success(
            ToResponse(user));
    }

    private static Dictionary<string, string[]> Validate(
        UpdateProfileRequest request)
    {
        var errors = new Dictionary<string, List<string>>();
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
                AddError(errors, key, "The field cannot be blank.");
            }
        }

        if (request.DateOfBirth == default ||
            request.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            AddError(
                errors,
                nameof(request.DateOfBirth),
                "Date of birth must be in the past.");
        }

        var currentYear = DateTime.UtcNow.Year;
        if (request.GraduationYear < 1900 ||
            request.GraduationYear > currentYear)
        {
            AddError(
                errors,
                nameof(request.GraduationYear),
                $"Graduation year must be between 1900 and {currentYear}.");
        }

        if (request.Gender is not (Gender.Male or Gender.Female))
        {
            AddError(
                errors,
                nameof(request.Gender),
                "Gender must be Male or Female.");
        }

        if (request.CumulativeGrade is not (
            CumulativeGrade.Acceptable or
            CumulativeGrade.Good or
            CumulativeGrade.VeryGood or
            CumulativeGrade.Excellent))
        {
            AddError(
                errors,
                nameof(request.CumulativeGrade),
                "Cumulative grade is invalid.");
        }

        if (request.Gender == Gender.Female &&
            request.MilitaryStatus is not null)
        {
            AddError(
                errors,
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
            AddError(
                errors,
                nameof(request.MilitaryStatus),
                "A valid military status is required for male applicants.");
        }

        return errors.ToDictionary(
            item => item.Key,
            item => item.Value.ToArray());
    }

    private static void AddError(
        IDictionary<string, List<string>> errors,
        string key,
        string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }

    private static void UpdateProtectedFields(
        ApplicantProfile profile,
        NormalizedProfileRequest request)
    {
        profile.FullNameEn = request.FullNameEn;
        profile.FullNameAr = request.FullNameAr;
        profile.NationalId = request.NationalId;
        profile.Nationality = request.Nationality;
        profile.DateOfBirth = request.DateOfBirth;
        profile.Gender = request.Gender;
        profile.University = request.University;
        profile.Faculty = request.Faculty;
        profile.DegreeLevel = request.DegreeLevel;
        profile.Major = request.Major;
        profile.GraduationYear = request.GraduationYear;
        profile.CumulativeGrade = request.CumulativeGrade;
        profile.MilitaryStatus = request.MilitaryStatus;
    }

    private static NormalizedProfileRequest Normalize(
        UpdateProfileRequest request) =>
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

    private static bool ProtectedFieldsChanged(
        ApplicantProfile profile,
        NormalizedProfileRequest request) =>
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

    private static IdentityOperationResult<ProfileResponse> Unauthorized() =>
        IdentityOperationResult<ProfileResponse>.Fail(new IdentityFailure(
            IdentityFailureKind.Unauthorized,
            "Authentication required",
            "A valid active account is required."));

    private static IdentityOperationResult<ProfileResponse> Conflict(
        string key,
        string error) =>
        IdentityOperationResult<ProfileResponse>.Fail(new IdentityFailure(
            IdentityFailureKind.Conflict,
            "Profile update conflict",
            Errors: new Dictionary<string, string[]>
            {
                [key] = [error]
            }));

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
