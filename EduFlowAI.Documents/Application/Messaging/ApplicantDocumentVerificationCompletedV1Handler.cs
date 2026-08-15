using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.Services;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Shared.Messaging.Contracts.Documents.V1;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Wolverine.Attributes;

namespace EduFlowAI.Documents.Application.Messaging;

public sealed class ApplicantDocumentVerificationCompletedV1Handler
{
    [Transactional(typeof(IDocumentDbContext))]
    public static async Task Handle(
        ApplicantDocumentVerificationCompletedV1 message,
        IDocumentDbContext dbContext,
        IStatusTransitionCoordinator statusCoordinator,
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

        var reviewResult = await DocumentPackageReviewResolver.ResolveAsync(
            dbContext, document.ApplicationId,
            document.Id, document.Status,
            cancellationToken);

        if(reviewResult is not null)
        {
            var transition= await statusCoordinator.ProcessDocumentReviewAsync(document.ApplicationId, reviewResult);
            if (!transition.IsSuccess)
            {
                throw new InvalidOperationException(transition.ErrorMessage);
            }
        }

    }
}