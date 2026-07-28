using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Shared.Messaging.Contracts.Documents.V1;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Wolverine.Attributes;

namespace EduFlowAI.Documents.Application.Messaging;

public sealed class ApplicantDocumentVerificationFailedV1Handler
{
    [Transactional(typeof(IDocumentDbContext))]
    public static async Task Handle(
        ApplicantDocumentVerificationFailedV1 message,
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

        // Concurrency check: Ensure a replacement wasn't uploaded during the crash
        if (!string.Equals(
                document.StorageKey,
                message.SourceStorageKey,
                StringComparison.Ordinal))
        {
            return;
        }

        // ADDED: Business Idempotency Guard
        // Do not let a delayed failure message overwrite a document that is already 
        // successfully approved or already flagged for human review.
        if (document.Status is DocumentStatus.Approved or DocumentStatus.NeedsHumanReview)
        {
            return;
        }

        // Apply the failure state
        document.Status = DocumentStatus.NeedsHumanReview;
        document.VerifiedAt = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        document.VerificationDetailsJson = JsonSerializer.Serialize(new
        {
            message.ErrorCode,
            message.SafeErrorMessage,
            message.AttemptCount,
            message.FailedAtUtc
        });

        // Notify Halim's Application State Machine about the technical failure
        var reviewResult = new DocumentReviewResultDto
        {
            ReviewerType = ReviewerType.AI,
            IsAccepted = false,
            IsAgentUncertain = true
        };

        await statusCoordinator.ProcessDocumentReviewAsync(document.ApplicationId, reviewResult);
    }
}
