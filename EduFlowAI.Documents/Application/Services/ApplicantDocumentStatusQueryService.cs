using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EduFlowAI.Documents.Application.Services;

public sealed class ApplicantDocumentStatusQueryService
    : IApplicantDocumentStatusQueryService
{
    private readonly IDocumentDbContext _documentDbContext;
    private readonly IApplicationStatusQueryService
        _applicationStatusQueryService;

    public ApplicantDocumentStatusQueryService(
        IDocumentDbContext documentDbContext,
        IApplicationStatusQueryService applicationStatusQueryService)
    {
        _documentDbContext = documentDbContext;
        _applicationStatusQueryService = applicationStatusQueryService;
    }

    public async Task<ApplicantDocumentStatusDto?>
        GetCurrentDocumentStatusForApplicantAsync(
            string applicantUserId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantUserId);

        var application =
            await _applicationStatusQueryService
                .GetCurrentApplicationStatusForApplicantAsync(
                    applicantUserId,
                    cancellationToken);

        if (application is null)
        {
            return null;
        }

        var documents = await _documentDbContext
            .ApplicantDocuments
            .AsNoTracking()
            .Where(document =>
                document.ApplicationId == application.ApplicationId)
            .OrderBy(document => document.DocumentType)
            .Select(document => new DocumentStatusItemDto
            {
                DocumentId = document.Id,
                DocumentType = document.DocumentType.ToString(),
                Status = document.Status.ToString(),
                StatusMessage = CreateStatusMessage(document.Status)
            })
            .ToListAsync(cancellationToken);

        var lastUpdatedAt = await _documentDbContext
            .ApplicantDocuments
            .AsNoTracking()
            .Where(document =>
                document.ApplicationId == application.ApplicationId)
            .Select(document => (DateTimeOffset?)document.UpdatedAt)
            .MaxAsync(cancellationToken);

        return new ApplicantDocumentStatusDto
        {
            ApplicationId = application.ApplicationId,
            OverallStatus = CalculateOverallStatus(documents),
            Documents = documents,
            LastUpdatedAt =
                lastUpdatedAt ?? application.LastUpdatedAt
        };
    }

    private static string CalculateOverallStatus(
        IReadOnlyCollection<DocumentStatusItemDto> documents)
    {
        if (documents.Count == 0)
        {
            return "NotUploaded";
        }

        if (documents.Any(document =>
                document.Status ==
                DocumentStatus.Rejected.ToString()))
        {
            return DocumentStatus.Rejected.ToString();
        }

        if (documents.Any(document =>
                document.Status ==
                DocumentStatus.NeedsHumanReview.ToString()))
        {
            return DocumentStatus.NeedsHumanReview.ToString();
        }

        if (documents.Any(document =>
                document.Status ==
                DocumentStatus.Verifying.ToString()))
        {
            return DocumentStatus.Verifying.ToString();
        }

        if (documents.All(document =>
                document.Status ==
                DocumentStatus.Approved.ToString()))
        {
            return DocumentStatus.Approved.ToString();
        }

        return DocumentStatus.Uploaded.ToString();
    }

    private static string CreateStatusMessage(
        DocumentStatus status)
    {
        return status switch
        {
            DocumentStatus.Uploaded =>
                "The document has been uploaded.",

            DocumentStatus.Verifying =>
                "The document is currently being verified.",

            DocumentStatus.Approved =>
                "The document has been approved.",

            DocumentStatus.NeedsHumanReview =>
                "The document is pending manual review.",

            DocumentStatus.Rejected =>
                "The document was rejected. Please review the confirmed reason.",

            _ =>
                "No document status is available."
        };
    }
}