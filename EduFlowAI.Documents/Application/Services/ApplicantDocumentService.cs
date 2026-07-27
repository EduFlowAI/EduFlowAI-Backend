// Cross-module contracts (from Halim, Karim, and Abdallah)
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Enums;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Application.Mapping;
using EduFlowAI.Documents.Domain.Entities;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Identity.Application.Interfaces;
using EduFlowAI.Shared.Kernel.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EduFlowAI.Documents.Application.Services;

public sealed class ApplicantDocumentService : IApplicantDocumentService
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationAcessReader _applicationAccessReader;
    private readonly IApplicantProfileReader _applicantProfileReader; // Karim's contract
    private readonly IProgramRequirementReader _programRequirementReader; // Abdallah's contract
    private readonly IValidator<UploadDocumentDto> _validator;
    private readonly ILogger<ApplicantDocumentService> _logger;
    private readonly IProfileService _profileService;

    public ApplicantDocumentService(
        IDocumentDbContext dbContext,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IApplicationAcessReader applicationAccessReader,
        IApplicantProfileReader applicantProfileReader,
        IProgramRequirementReader programRequirementReader,
        IValidator<UploadDocumentDto> validator,
        ILogger<ApplicantDocumentService> logger,
        IProfileService profileService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _applicationAccessReader = applicationAccessReader ?? throw new ArgumentNullException(nameof(applicationAccessReader));
        _applicantProfileReader = applicantProfileReader ?? throw new ArgumentNullException(nameof(applicantProfileReader));
        _programRequirementReader = programRequirementReader ?? throw new ArgumentNullException(nameof(programRequirementReader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
    }

    public async Task<Result<Guid>> UploadDocumentAsync(
        UploadDocumentDto dto,
        CancellationToken cancellationToken)
    {
        // PHASE 1: Validation (Fail Fast)
        var validationResult = await _validator.ValidateAsync(dto, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));

            _logger.LogWarning(
                "Upload rejected: Validation failed for Application {ApplicationId}. Errors: {Errors}",
                dto.ApplicationId,
                errors);

            return Result<Guid>.Failure(400, errors);
        }

        // PHASE 2: Cross-Module Business Rules & Security
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Upload rejected: Unauthenticated user attempted an upload.");
            return Result<Guid>.Failure(401, "User is not authenticated.");
        }

        // 1. Ask Halim: Does the user own this and is it open for uploads?
        var hasAccess = await _applicationAccessReader.CanUploadDocumentsAsync(
            dto.ApplicationId,
            userId,
            cancellationToken);

        if (!hasAccess)
        {
            _logger.LogWarning(
                "Upload rejected: User {UserId} does not have access to upload documents for Application {ApplicationId}",
                userId,
                dto.ApplicationId);
            return Result<Guid>.Failure(403, "User does not have access to upload documents for this application.");
        }

        // 2. Ask Halim: What is the ProgramId for this Application?
        var programId = await _applicationAccessReader.GetProgramIdAsync(dto.ApplicationId, cancellationToken);
        if (programId == Guid.Empty)
        {
            _logger.LogError("Upload failed: Could not resolve ProgramId for Application {ApplicationId}", dto.ApplicationId);
            return Result<Guid>.Failure(404, "Application program context could not be found.");
        }

        // 3. Ask Karim: What is this user's gender?
        //var applicantGender = await _applicantProfileReader.GetApplicantGenderAsync(userId, cancellationToken);
        var applicant = await _profileService.GetAsync(userId, cancellationToken);
        var applicantGender = GenderMapper.ToGender(applicant?.Value?.Gender) ?? Gender.None;


        // 4. Ask Abdallah: Is this specific document required for this Program/Gender?
        var requirementSet = await _programRequirementReader.ResolveAsync(
            programId: programId,
            applicantGender: applicantGender,
            cancellationToken: cancellationToken);

        if (requirementSet is null || !requirementSet.DocumentTypes.Contains(dto.DocumentType))
        {
            _logger.LogWarning(
                "Upload rejected: Document type {DocType} is not required for Application {ApplicationId} (Program: {ProgramId}).",
                dto.DocumentType,
                dto.ApplicationId,
                programId);

            return Result<Guid>.Failure(400, $"The document type '{dto.DocumentType}' is not required for your application.");
        }

        // PHASE 3: Physical File Save
        string storageKey;
        try
        {
            storageKey = await _fileStorageService.SaveFileAsync(dto.File, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save physical file to disk for Application {ApplicationId}", dto.ApplicationId);
            return Result<Guid>.Failure(500, "An error occurred while securely storing the file. Please try again.");
        }

        // PHASE 4 & 5: Database Save & Compensating Action
        try
        {
            var document = new ApplicantDocument
            {
                ApplicationId = dto.ApplicationId,
                DocumentType = dto.DocumentType,
                StorageKey = storageKey,
                OriginalFileName = dto.File.FileName,
                Status = DocumentStatus.Uploaded,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ApplicantDocuments.Add(document);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Database save failed for Application {ApplicationId}. Rolling back physical file {StorageKey}",
                dto.ApplicationId,
                storageKey);

            // Compensating Action: Delete the physical file so we don't have orphan files taking up disk space
            try
            {
                await _fileStorageService.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch (Exception ex_2)
            {
                _logger.LogError(
                    ex_2,
                    "Faild to delete the orphan file. Storage key {StorageKey}",
                    storageKey
                    );

            }

            return Result<Guid>.Failure(500, "Failed to save document metadata. The upload was rolled back.");
        }
    }



    public async Task<Result<IEnumerable<ApplicantDocumentDto>>> GetDocumentsByApplicationIdAsync(
    Guid applicationId,
    CancellationToken cancellationToken = default)
    {
        // 1. Fetch the user ID directly from your injected current user service
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<IEnumerable<ApplicantDocumentDto>>.Failure(401, "User is not authenticated.");
        }

        // 2. Validate authorization using Halim's contract
        bool hasAccess = await _applicationAccessReader.CanUploadDocumentsAsync(
            applicationId,
            userId,
            cancellationToken);

        if (!hasAccess)
        {
            _logger.LogWarning(
                "Security: User {UserId} attempted to view documents for application {ApplicationId} without authorization.",
                userId,
                applicationId);

            return Result<IEnumerable<ApplicantDocumentDto>>.Failure(403, "You do not have permission to view documents for this application.");
        }

        // 3. Query the database using highly optimized EF Core projection
        var documents = await _dbContext.ApplicantDocuments
            .AsNoTracking() 
            .Where(d => d.ApplicationId == applicationId)
            .Select(d => new ApplicantDocumentDto(
                d.Id,
                d.DocumentType,
                d.OriginalFileName,
                d.Status,
                d.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<IEnumerable<ApplicantDocumentDto>>.Success(documents);
    }








    public async Task<Result<FileDownloadDto>> DownloadDocumentAsync(
    Guid documentId,
    CancellationToken cancellationToken = default)
    {
        // 1. Authenticate
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<FileDownloadDto>.Failure(401, "User is not authenticated.");
        }

        // 2. Fetch the document metadata from the database
        var document = await _dbContext.ApplicantDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            return Result<FileDownloadDto>.Failure(404, "Document not found.");
        }

        // 3. Prevent IDOR: Check if the current user owns the parent application
        bool hasAccess = await _applicationAccessReader.CanUploadDocumentsAsync(
            document.ApplicationId,
            userId,
            cancellationToken);

        if (!hasAccess)
        {
            _logger.LogWarning(
                "Security IDOR attempt: User {UserId} tried to download document {DocumentId} belonging to application {ApplicationId}.",
                userId, documentId, document.ApplicationId);

            return Result<FileDownloadDto>.Failure(403, "You do not have permission to download this document.");
        }

        // 4. Retrieve the physical file stream
        var stream = await _fileStorageService.OpenReadAsync(document.StorageKey, cancellationToken);

        // Note: We don't check for null stream here because if it fails, 
        // IFileStorageService will throw an exception which your global ExceptionHandler will catch (500).

        var dto = new FileDownloadDto(stream, document.OriginalFileName);
        return Result<FileDownloadDto>.Success(dto);
    }
}