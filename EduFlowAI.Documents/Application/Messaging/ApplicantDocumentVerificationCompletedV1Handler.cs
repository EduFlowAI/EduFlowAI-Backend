using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Shared.Messaging.Contracts.Documents.V1;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Wolverine.Attributes;
// Ensure you have using statements for Halim's namespace here:
 //using EduFlowAI.Admission.Application.Services;
// using EduFlowAI.Admission.Application.DTOs;

namespace EduFlowAI.Documents.Application.Messaging;

public sealed class ApplicantDocumentVerificationCompletedV1Handler
{
    [Transactional(typeof(IDocumentDbContext))]
    public static async Task Handle(
        ApplicantDocumentVerificationCompletedV1 message,
        IDocumentDbContext dbContext,
        IStatusTransitionCoordinator statusCoordinator, // <-- Injected Halim's Coordinator
        CancellationToken cancellationToken)
    {
        var document = await dbContext.ApplicantDocuments
            .SingleOrDefaultAsync(
                x => x.Id == message.DocumentId,
                cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException(
                $"ApplicantDocument {message.DocumentId} was not found.");
        }

        // 1. Concurrency/Replacement check: A replacement was uploaded while AI was processing.
        if (!string.Equals(
                document.StorageKey,
                message.SourceStorageKey,
                StringComparison.Ordinal))
        {
            return; // Ignore stale result 
        }

        // 2. Business idempotency in addition to Wolverine Inbox .
        if (document.VerifiedAt is not null &&
            document.Status is
                DocumentStatus.Approved or
                DocumentStatus.NeedsHumanReview)
        {
            return;
        }

        // 3. Update the Document entity 
        document.VerificationDetailsJson = JsonSerializer.Serialize(message.Details);
        document.VerifiedAt = message.VerifiedAtUtc;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        // AI never rejects automatically .
        bool isExactMatch = message.Outcome == DocumentVerificationOutcomeV1.ExactMatch;
        document.Status = isExactMatch
            ? DocumentStatus.Approved
            : DocumentStatus.NeedsHumanReview;

        // 4. Notify Halim's Application State Machine
        var reviewResult = new DocumentReviewResultDto
        {
            ReviewerType = ReviewerType.AI,
            IsAccepted = isExactMatch,
            IsAgentUncertain = !isExactMatch
        };

        await statusCoordinator.ProcessDocumentReviewAsync(document.ApplicationId, reviewResult);
    }
}