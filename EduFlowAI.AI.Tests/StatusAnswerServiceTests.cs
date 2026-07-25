using System;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;

namespace EduFlowAI.AI.Tests;

public class StatusAnswerServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Guest_is_asked_to_sign_in_and_the_reader_is_never_called(string? userId)
    {
        var reader = new FakeReader();
        var service = new StatusAnswerService(reader);

        var result = await service.AnswerStatusAsync(userId, "en");

        Assert.True(result.RequiresSignIn);
        Assert.Contains("sign in", result.Answer);
        Assert.False(reader.WasCalled);
    }

    [Fact]
    public async Task Signed_in_with_no_application_is_told_to_apply()
    {
        var service = new StatusAnswerService(new FakeReader { Status = null });

        var result = await service.AnswerStatusAsync("user-1", "en");

        Assert.False(result.RequiresSignIn);
        Assert.Contains("don't have an application", result.Answer);
    }

    [Fact]
    public async Task The_reader_is_called_with_the_given_user_id()
    {
        var reader = new FakeReader { Status = "Admitted" };
        var service = new StatusAnswerService(reader);

        await service.AnswerStatusAsync("user-42", "en");

        Assert.Equal("user-42", reader.LastUserId);
    }

    [Theory]
    [InlineData("Admitted", "admitted")]
    [InlineData("UnderDocumentVerification", "verified")]
    [InlineData("DocumentsRequired", "documents")]
    [InlineData("NotSelected", "apply again")]
    [InlineData("Waitlisted", "waiting list")]
    public async Task A_known_status_gets_its_specific_english_message(string status, string expectedFragment)
    {
        var service = new StatusAnswerService(new FakeReader { Status = status });

        var result = await service.AnswerStatusAsync("user-1", "en");

        Assert.Contains(expectedFragment, result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Arabic_is_answered_in_arabic()
    {
        var service = new StatusAnswerService(new FakeReader { Status = "Admitted" });

        var result = await service.AnswerStatusAsync("user-1", "ar");

        Assert.Contains("مبروك", result.Answer);
    }

    [Fact]
    public async Task Status_lookup_is_case_insensitive()
    {
        var service = new StatusAnswerService(new FakeReader { Status = "admitted" });

        var result = await service.AnswerStatusAsync("user-1", "en");

        Assert.Contains("admitted", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    // A renamed or brand-new status must never throw - it falls back to the neutral message.
    [Fact]
    public async Task An_unknown_status_falls_back_to_the_default()
    {
        var service = new StatusAnswerService(new FakeReader { Status = "SomeFutureStatus" });

        var en = await service.AnswerStatusAsync("user-1", "en");
        var ar = await service.AnswerStatusAsync("user-1", "ar");

        Assert.Contains("being processed", en.Answer);
        Assert.Contains("قيد المعالجة", ar.Answer);
    }

    private sealed class FakeReader : IApplicationStatusReader
    {
        public string? Status { get; set; } = "Admitted";
        public bool WasCalled { get; private set; }
        public string? LastUserId { get; private set; }

        public Task<ApplicantStatusDto?> GetCurrentStatusAsync(string userId, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastUserId = userId;
            var dto = Status is null ? null : new ApplicantStatusDto { Status = Status };
            return Task.FromResult(dto);
        }
    }
}
