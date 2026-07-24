using EduFlowAI.Identity.Application.DTOs;
using EduFlowAI.Identity.Application.Services;
using EduFlowAI.Identity.Domain.Entities;
using EduFlowAI.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Identity.Tests;

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task Put_RejectsProtectedFieldChangesAfterLock()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedLockedUserAsync(dbContext);
        var service = new ProfileService(dbContext);
        var request = CreateMatchingRequest() with
        {
            FullNameEn = "Changed Name",
            Address = "New address"
        };

        var result = await service.UpdateAsync(
            user.Id,
            request,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            IdentityFailureKind.Conflict,
            result.Failure!.Kind);
        var profile = await dbContext.ApplicantProfiles.SingleAsync();
        Assert.Equal("Karim Test", profile.FullNameEn);
        Assert.Equal("Old address", profile.Address);
    }

    [Fact]
    public async Task Put_AllowsNonProtectedChangesAfterLock()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedLockedUserAsync(dbContext);
        var service = new ProfileService(dbContext);
        var request = CreateMatchingRequest() with
        {
            Address = "New address",
            Governorate = "Giza",
            PhoneNumber = "+201001234567",
            PreferredLanguage = "ar",
            GmailNotificationsEnabled = true
        };

        var result = await service.UpdateAsync(
            user.Id,
            request,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var savedUser = await dbContext.AppUsers
            .Include(item => item.ApplicantProfile)
            .SingleAsync();
        Assert.Equal("New address", savedUser.ApplicantProfile!.Address);
        Assert.Equal("Giza", savedUser.ApplicantProfile.Governorate);
        Assert.Equal("+201001234567", savedUser.PhoneNumber);
        Assert.Equal("ar", savedUser.PreferredLanguage);
        Assert.True(savedUser.GmailNotificationsEnabled);
        Assert.True(savedUser.ApplicantProfile.IsProfileLocked);
    }

    private static TestIdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestIdentityDbContext(options);
    }

    private static async Task<AppUser> SeedLockedUserAsync(
        TestIdentityDbContext dbContext)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "karim@example.com",
            Email = "karim@example.com",
            IsActive = true,
            PreferredLanguage = "en",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var profile = ApplicantProfileLockerTests.CreateProfile(user.Id);
        profile.Address = "Old address";
        profile.IsProfileLocked = true;
        profile.User = user;
        user.ApplicantProfile = profile;

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static UpdateProfileRequest CreateMatchingRequest() => new()
    {
        FullNameEn = "Karim Test",
        FullNameAr = "كريم تيست",
        NationalId = "30001010123456",
        Nationality = "EG",
        DateOfBirth = new DateOnly(2000, 1, 1),
        Gender = Gender.Male,
        Address = "Old address",
        University = "Test University",
        Faculty = "Engineering",
        DegreeLevel = "Bachelor",
        Major = "Computer Engineering",
        GraduationYear = 2024,
        CumulativeGrade = CumulativeGrade.VeryGood,
        MilitaryStatus = MilitaryStatus.Completed,
        PreferredLanguage = "en"
    };
}
