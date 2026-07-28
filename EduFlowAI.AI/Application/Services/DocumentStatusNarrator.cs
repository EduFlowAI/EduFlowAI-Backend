using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.Interfaces;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Domain.Enums;

namespace EduFlowAI.AI.Application.Services;

// Hybrid document-status answer: the statuses are deterministic facts (never decided by the model);
// the model only phrases them warmly in the applicant's language. Falls back to the plain facts if
// the model returns nothing or is unavailable, so a status question always gets a truthful answer.
public sealed class DocumentStatusNarrator : IDocumentStatusNarrator
{
    private readonly IGeminiChatClient _chatClient;

    public DocumentStatusNarrator(IGeminiChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    public async Task<string> NarrateAsync(
        IEnumerable<ApplicantDocumentDto> documents,
        string language,
        CancellationToken cancellationToken = default)
    {
        var docs = documents?.ToList() ?? new List<ApplicantDocumentDto>();

        // No documents needs no model call - a warm, fixed line is enough.
        if (docs.Count == 0)
            return language == "ar"
                ? "لا توجد مستندات مرفوعة على حسابك حتى الآن. يمكنك رفعها من حسابك في أي وقت."
                : "You haven't uploaded any documents yet. You can upload them from your account whenever you're ready.";

        var facts = BuildFacts(docs);
        var languageLine = language == "ar" ? "Respond in Arabic only." : "Respond in English only.";

        // English is the canonical fact language; the model translates and phrases, never re-decides.
        var systemPrompt = $"""
# ROLE
You are ITI's admission assistant, replying warmly and briefly to an applicant who asked about their
uploaded documents.

# THE FACTS (authoritative - this is the applicant's real record)
{facts}

# RULES
- Report exactly these statuses. Never add, change, soften, upgrade or downgrade a status.
- Never invent documents, dates, timelines or next steps that are not stated above.
- If something is rejected or still needs review, say so plainly but kindly.
- Keep it to one short, natural paragraph or a short list. No filler, no emoji.
- {languageLine}
""";

        string? answer;
        try
        {
            answer = await _chatClient.GenerateAsync(
                systemPrompt, "Tell me the status of my documents.", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            answer = null; // model unavailable (e.g. quota) - fall back to the plain facts below
        }

        return string.IsNullOrWhiteSpace(answer) ? PlainSummary(docs, language) : answer;
    }

    // The deterministic source of truth, handed to the model and used verbatim if the model can't reply.
    private static string BuildFacts(IEnumerable<ApplicantDocumentDto> docs) =>
        string.Join("\n", docs
            .OrderBy(d => d.DocumentType)
            .Select(d => $"- {TypeLabel(d.DocumentType)}: {StatusLabel(d.Status)}"));

    private static string PlainSummary(IReadOnlyList<ApplicantDocumentDto> docs, string language)
    {
        var intro = language == "ar" ? "حالة مستنداتك:" : "Your document status:";
        var lines = docs
            .OrderBy(d => d.DocumentType)
            .Select(d => $"• {TypeLabel(d.DocumentType)}: {StatusLabel(d.Status)}");
        return intro + "\n" + string.Join("\n", lines);
    }

    private static string TypeLabel(DocumentType type) => type switch
    {
        DocumentType.NationalId => "National ID",
        DocumentType.BirthCertificate => "Birth Certificate",
        DocumentType.GraduationCertificate => "Graduation Certificate",
        DocumentType.MilitaryCertificate => "Military Certificate",
        _ => "Document"
    };

    private static string StatusLabel(DocumentStatus status) => status switch
    {
        DocumentStatus.Uploaded => "Received (awaiting review)",
        DocumentStatus.Verifying => "Under verification",
        DocumentStatus.Approved => "Approved",
        DocumentStatus.NeedsHumanReview => "Under manual review",
        DocumentStatus.Rejected => "Rejected (needs re-upload)",
        _ => "Pending"
    };
}
