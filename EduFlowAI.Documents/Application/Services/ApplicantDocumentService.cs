using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Domain.Entities;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Identity.Application.DbContextAbstraction;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DocumentStatus = EduFlowAI.Documents.Domain.Enums.DocumentStatus;
using DocumentType = EduFlowAI.Documents.Domain.Enums.DocumentType;

namespace EduFlowAI.Documents.Application.Services;

public sealed class ApplicantDocumentService(
    IDocumentDbContext documentDbContext,
    IAdmissionDbContext admissionDbContext,
    IIdentityDbContext identityDbContext,
    IProgramRequirementReader programRequirementReader,
    IFileStorageService fileStorageService,
    ICurrentUserService currentUserService,
    ILogger<ApplicantDocumentService> logger) : IApplicantDocumentService
{
    public async Task<Result<ApplicationDocumentsDto>> ListAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var access = await GetOwnedApplicationAsync(applicationId, false, cancellationToken);
        if (!access.IsSuccess)
            return Result<ApplicationDocumentsDto>.Failure(access.StatusCode, access.Message);

        var requirements = await GetRequirementsAsync(applicationId, access.UserId!, cancellationToken);
        if (!requirements.IsSuccess)
            return Result<ApplicationDocumentsDto>.Failure(requirements.StatusCode, requirements.Message);

        var uploaded = await documentDbContext.ApplicantDocuments
            .AsNoTracking()
            .Where(document => document.ApplicationId == applicationId)
            .OrderBy(document => document.DocumentType)
            .ToListAsync(cancellationToken);

        var required = requirements.Types!;
        var items = uploaded
            .Select(document => ToListItem(document, required.Contains(document.DocumentType)))
            .Concat(required
                .Where(type => uploaded.All(document => document.DocumentType != type))
                .Select(type => new DocumentListItemDto(
                    null, type, true, false, null, null, null, null)))
            .OrderBy(item => item.DocumentType)
            .ToList();

        return Result<ApplicationDocumentsDto>.Success(
            new ApplicationDocumentsDto(applicationId, items));
    }

    public async Task<Result<UploadDocumentDto>> UploadAsync(
        Guid applicationId,
        DocumentType documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Result<UploadDocumentDto>.Failure(400, "File cannot be null or empty.");

        if (documentType == DocumentType.None || !Enum.IsDefined(documentType))
            return Result<UploadDocumentDto>.Failure(400, "The document type is invalid.");

        var access = await GetOwnedApplicationAsync(applicationId, true, cancellationToken);
        if (!access.IsSuccess)
            return Result<UploadDocumentDto>.Failure(access.StatusCode, access.Message);

        if (!IsEditable(access.Application!.Status))
            return Result<UploadDocumentDto>.Failure(409, "Documents cannot be uploaded in the current application state.");

        var requirements = await GetRequirementsAsync(applicationId, access.UserId!, cancellationToken);
        if (!requirements.IsSuccess)
            return Result<UploadDocumentDto>.Failure(requirements.StatusCode, requirements.Message);

        if (!requirements.Types!.Contains(documentType))
            return Result<UploadDocumentDto>.Failure(400, "The document type is not required for this application.");

        var existing = await documentDbContext.ApplicantDocuments
            .SingleOrDefaultAsync(
                document => document.ApplicationId == applicationId && document.DocumentType == documentType,
                cancellationToken);

        if (existing is not null && existing.Status != DocumentStatus.Uploaded)
            return Result<UploadDocumentDto>.Failure(409, "This document can no longer be replaced.");

        string newStorageKey;
        try
        {
            newStorageKey = await fileStorageService.SaveFileAsync(file, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Result<UploadDocumentDto>.Failure(400, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Result<UploadDocumentDto>.Failure(400, exception.Message);
        }

        var now = DateTimeOffset.UtcNow;
        var safeOriginalFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeOriginalFileName))
            safeOriginalFileName = $"document{Path.GetExtension(newStorageKey)}";

        var oldStorageKey = existing?.StorageKey;
        var document = existing ?? new ApplicantDocument
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            DocumentType = documentType,
            CreatedAt = now
        };

        document.StorageKey = newStorageKey;
        document.OriginalFileName = safeOriginalFileName;
        document.Status = DocumentStatus.Uploaded;
        document.VerificationDetailsJson = null;
        document.VerifiedAt = null;
        document.UpdatedAt = now;

        if (existing is null)
            await documentDbContext.ApplicantDocuments.AddAsync(document, cancellationToken);

        try
        {
            await documentDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await TryDeleteAsync(newStorageKey, "database rollback", cancellationToken);
            return Result<UploadDocumentDto>.Failure(409, "The document could not be saved because it changed concurrently.");
        }
        catch
        {
            await TryDeleteAsync(newStorageKey, "database rollback", cancellationToken);
            throw;
        }

        if (oldStorageKey is not null && !string.Equals(oldStorageKey, newStorageKey, StringComparison.Ordinal))
            await TryDeleteAsync(oldStorageKey, "replacement cleanup", cancellationToken);

        return Result<UploadDocumentDto>.Success(
            new UploadDocumentDto(
                document.Id,
                document.ApplicationId,
                document.DocumentType,
                document.Status,
                document.OriginalFileName,
                document.CreatedAt,
                document.UpdatedAt),
            existing is null ? 201 : 200,
            existing is null ? "Document uploaded." : "Document replaced.");
    }

    public async Task<Result<SubmitDocumentsDto>> SubmitAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var access = await GetOwnedApplicationAsync(applicationId, true, cancellationToken);
        if (!access.IsSuccess)
            return Result<SubmitDocumentsDto>.Failure(access.StatusCode, access.Message);

        if (access.Application!.Status == ApplicationStatus.UnderDocumentVerification)
            return Result<SubmitDocumentsDto>.Failure(409, "The document package has already been submitted.");

        if (access.Application.Status != ApplicationStatus.DocumentsRequired)
            return Result<SubmitDocumentsDto>.Failure(409, "The application is not ready for document submission.");

        var requirements = await GetRequirementsAsync(applicationId, access.UserId!, cancellationToken);
        if (!requirements.IsSuccess)
            return Result<SubmitDocumentsDto>.Failure(requirements.StatusCode, requirements.Message);

        var documents = await documentDbContext.ApplicantDocuments
            .Where(document => document.ApplicationId == applicationId)
            .ToListAsync(cancellationToken);
        var missing = requirements.Types!
            .Where(type => documents.All(document => document.DocumentType != type))
            .OrderBy(type => type)
            .ToList();

        if (missing.Count > 0)
        {
            return Result<SubmitDocumentsDto>.Failure(
                new SubmitDocumentsDto(applicationId, false, missing),
                409,
                "Required documents are missing.");
        }

        if (documents.Any(document => document.Status != DocumentStatus.Uploaded))
            return Result<SubmitDocumentsDto>.Failure(409, "One or more documents are not in a submittable state.");

        // The repository maps Wolverine envelope storage, but does not register a Wolverine host,
        // a transactional outbox writer, or a transport destination. Fail closed so state is never
        // transitioned without the required atomic message write.
        return Result<SubmitDocumentsDto>.Failure(
            new SubmitDocumentsDto(applicationId, false, []),
            StatusCodes.Status503ServiceUnavailable,
            "Document submission is unavailable until the transactional outbox is configured.");
    }

    public async Task<Result<DocumentFileDto>> OpenFileAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await documentDbContext.ApplicantDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        if (document is null)
            return Result<DocumentFileDto>.Failure(404, "Document not found.");

        var access = await GetOwnedApplicationAsync(document.ApplicationId, false, cancellationToken);
        if (!access.IsSuccess)
            return Result<DocumentFileDto>.Failure(access.StatusCode, access.Message);

        try
        {
            var stream = await fileStorageService.OpenReadAsync(document.StorageKey, cancellationToken);
            var safeDownloadName = Path.GetFileName(document.OriginalFileName);
            if (string.IsNullOrWhiteSpace(safeDownloadName))
                safeDownloadName = "document";

            return Result<DocumentFileDto>.Success(new DocumentFileDto(
                stream,
                GetContentType(document.StorageKey),
                safeDownloadName));
        }
        catch (FileNotFoundException)
        {
            return Result<DocumentFileDto>.Failure(404, "The stored document file was not found.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<DocumentFileDto>.Failure(404, "The stored document file was not found.");
        }
    }

    private async Task<AccessResult> GetOwnedApplicationAsync(
        Guid applicationId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new AccessResult(false, 401, "The authenticated user identifier is missing.", null, null);

        var query = admissionDbContext.Applications.AsQueryable();
        if (!tracked)
            query = query.AsNoTracking();

        var application = await query.SingleOrDefaultAsync(item => item.Id == applicationId, cancellationToken);
        if (application is null)
            return new AccessResult(false, 404, "Application not found.", userId, null);

        if (!string.Equals(application.ApplicantUserId, userId, StringComparison.Ordinal))
            return new AccessResult(false, 403, "You do not have access to this application.", userId, null);

        return new AccessResult(true, 200, string.Empty, userId, application);
    }

    private async Task<RequirementsResult> GetRequirementsAsync(
        Guid applicationId,
        string userId,
        CancellationToken cancellationToken)
    {
        var gender = await identityDbContext.ApplicantProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.Gender.ToString())
            .SingleOrDefaultAsync(cancellationToken);
        if (gender is null)
            return new RequirementsResult(false, 409, "The applicant profile is required before documents can be processed.", null);

        var programIds = await (
            from preference in admissionDbContext.ApplicationPreferences.AsNoTracking()
            join offering in admissionDbContext.TrackBranchOfferings.AsNoTracking()
                on preference.TrackBranchOfferingId equals offering.Id
            join track in admissionDbContext.Tracks.AsNoTracking()
                on offering.TrackId equals track.Id
            where preference.ApplicationId == applicationId
            select track.ProgramId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (programIds.Count == 0)
            return new RequirementsResult(false, 409, "At least one application preference is required before documents can be processed.", null);

        var requiredTypes = new HashSet<DocumentType>();
        foreach (var programId in programIds)
        {
            requiredTypes.UnionWith(await programRequirementReader.GetRequiredTypesAsync(
                programId,
                gender,
                cancellationToken));
        }

        return new RequirementsResult(true, 200, string.Empty, requiredTypes);
    }

    private async Task TryDeleteAsync(string storageKey, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await fileStorageService.DeleteAsync(storageKey, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Document file cleanup failed during {Operation}.", operation);
        }
    }

    private static bool IsEditable(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.DocumentsRequired;

    private static DocumentListItemDto ToListItem(ApplicantDocument document, bool isRequired) =>
        new(
            document.Id,
            document.DocumentType,
            isRequired,
            true,
            document.Status,
            document.OriginalFileName,
            document.CreatedAt,
            document.UpdatedAt);

    private static string GetContentType(string storageKey) =>
        Path.GetExtension(storageKey).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

    private sealed record AccessResult(
        bool IsSuccess,
        int StatusCode,
        string Message,
        string? UserId,
        EduFlowAI.Admission.Domain.Entities.Application? Application);

    private sealed record RequirementsResult(
        bool IsSuccess,
        int StatusCode,
        string Message,
        IReadOnlySet<DocumentType>? Types);
}
