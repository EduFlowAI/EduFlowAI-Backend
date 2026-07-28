using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.AI.Application.Services;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Domain.Enums;

namespace EduFlowAI.AI.Tests;

public class DocumentStatusNarratorTests
{
    [Fact]
    public async Task No_documents_returns_a_warm_line_without_calling_the_model()
    {
        var chat = new StubChat();
        var narrator = new DocumentStatusNarrator(chat);

        var en = await narrator.NarrateAsync(new List<ApplicantDocumentDto>(), "en");
        var ar = await narrator.NarrateAsync(new List<ApplicantDocumentDto>(), "ar");

        Assert.Contains("haven't uploaded", en);
        Assert.Contains("لا توجد مستندات", ar);
        Assert.Null(chat.LastSystemPrompt);   // the empty case never needs the model
    }

    [Fact]
    public async Task Documents_are_narrated_with_the_exact_statuses_as_grounded_facts()
    {
        var chat = new StubChat { Response = "Your transcript is approved and your ID is under review." };
        var narrator = new DocumentStatusNarrator(chat);

        var result = await narrator.NarrateAsync(Docs(), "en");

        Assert.Equal("Your transcript is approved and your ID is under review.", result);  // model's phrasing wins
        Assert.NotNull(chat.LastSystemPrompt);
        // the authoritative facts are handed to the model verbatim...
        Assert.Contains("Graduation Certificate: Approved", chat.LastSystemPrompt);
        Assert.Contains("National ID: Under manual review", chat.LastSystemPrompt);
        // ...with an instruction never to change them
        Assert.Contains("Never add, change", chat.LastSystemPrompt);
    }

    [Fact]
    public async Task Falls_back_to_the_plain_facts_when_the_model_returns_nothing()
    {
        var chat = new StubChat { Response = "" };
        var narrator = new DocumentStatusNarrator(chat);

        var result = await narrator.NarrateAsync(Docs(), "en");

        // never an empty reply - the deterministic facts still reach the applicant
        Assert.Contains("Graduation Certificate: Approved", result);
        Assert.Contains("National ID: Under manual review", result);
    }

    [Fact]
    public async Task Falls_back_to_the_plain_facts_when_the_model_is_unavailable()
    {
        var chat = new StubChat { Throw = true };   // e.g. quota exhausted
        var narrator = new DocumentStatusNarrator(chat);

        var result = await narrator.NarrateAsync(Docs(), "en");

        Assert.Contains("Graduation Certificate: Approved", result);
        Assert.Contains("National ID: Under manual review", result);
    }

    private static List<ApplicantDocumentDto> Docs() => new()
    {
        new(Guid.NewGuid(), DocumentType.GraduationCertificate, "grad.pdf", DocumentStatus.Approved, DateTimeOffset.UtcNow),
        new(Guid.NewGuid(), DocumentType.NationalId, "id.pdf", DocumentStatus.NeedsHumanReview, DateTimeOffset.UtcNow)
    };

    private sealed class StubChat : IGeminiChatClient
    {
        public string Response { get; set; } = "narrated";
        public bool Throw { get; set; }
        public string? LastSystemPrompt { get; private set; }

        public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        {
            LastSystemPrompt = systemPrompt;
            if (Throw)
                throw new System.Net.Http.HttpRequestException("model unavailable");
            return Task.FromResult(Response);
        }

        public Task<string> GenerateJsonAsync(string systemPrompt, string userMessage, object responseSchema, CancellationToken ct = default)
            => Task.FromResult(string.Empty);

        public Task<string> GenerateFromDocumentAsync(string instruction, byte[] document, string mimeType, CancellationToken ct = default)
            => Task.FromResult(string.Empty);
    }
}
