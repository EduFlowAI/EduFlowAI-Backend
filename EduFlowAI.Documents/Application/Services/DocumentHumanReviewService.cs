using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Communication.Application.Interfaces;
using EduFlowAI.Communication.Domain.Enums;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Identity.Domain.Entities;
using EduFlowAI.Shared.Kernel.Common;
using EduFlowAI.Shared.Kernel.Common.Pagination;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Documents.Application.Services
{
    public class DocumentHumanReviewService(IDocumentDbContext dbContext, UserManager<AppUser> userManager, 
        IAdmissionDbContext admissionDbContext, IFileStorageService fileStorageService, INotificationService notificationService)
        : IDocumentHumanReviewService
    {
        public async Task<Result<PaginatedResult<HumanReviewDto>>> GetAllDocumentsReviewsAsync(QueryParameters queryParameters)
        {
            try
            {
                var query = dbContext.ApplicantDocuments.AsNoTracking()
                    .Where(d => d.Status == DocumentStatus.NeedsHumanReview)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(queryParameters.Search)) {
                    query = query.Where(d => d.OriginalFileName.Contains(queryParameters.Search));
                }
                if (!string.IsNullOrEmpty(queryParameters.Status))
                {
                    query = query.Where(d => d.Status.ToString().Contains(queryParameters.Status));
                }
                if (!string.IsNullOrEmpty(queryParameters.Search))
                {
                    query = query.Where(d => d.DocumentType.ToString().Contains(queryParameters.Type!));
                }
                else
                {
                    query = query.OrderBy(d => d.CreatedAt);
                }

                var totalCount = await query.CountAsync();
                var docReviews = await query.Skip((queryParameters.Page - 1) * queryParameters.PageSize)
                    .Take(queryParameters.PageSize)
                    .Select(d => new HumanReviewDto(
                        DocumentId: d.Id,
                        DocumentType: d.DocumentType,
                        StorageKey: d.StorageKey,
                        OriginalFileName: d.OriginalFileName,
                        Status: d.Status,
                        VerificationDetailsJson: d.VerificationDetailsJson
                    ))
                    .ToListAsync();

                var data = new PaginatedResult<HumanReviewDto>
                {
                    Data = docReviews,
                    CurrentPage = queryParameters.Page,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)queryParameters.PageSize),
                    PageSize = queryParameters.PageSize,
                    TotalCount = totalCount
                };
                return Result<PaginatedResult<HumanReviewDto>>.Success(data);
            }
            catch (Exception ex)
            {
                return Result<PaginatedResult<HumanReviewDto>>.Failure(500, $"An error occurred while retrieving document reviews: {ex.Message}");
            }
        }

        public async Task<Result<DocumentReviewDto>> GetDocumentReviewAsync(Guid documentId)
        {
            try
            {
                var doc = await dbContext.ApplicantDocuments.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == documentId);
                if (doc == null)
                {
                    return Result<DocumentReviewDto>.Failure(404, "Document not found");
                }

                var dto = new DocumentReviewDto
                (
                    DocumentId: doc.Id,
                    ApplicationId: doc.ApplicationId,
                    ApplicantId: await GetApplicantId(doc.ApplicationId),
                    DocumentType: doc.DocumentType,
                    StorageKey: doc.StorageKey,
                    OriginalFileName: doc.OriginalFileName,
                    Status: doc.Status,
                    VerificationDetailsJson: doc.VerificationDetailsJson
                );

                return Result<DocumentReviewDto>.Success(dto);
            }
            catch (Exception ex)
            {
                return Result<DocumentReviewDto>.Failure(500, $"An error occurred while retrieving the document review: {ex.Message}");
            }
        }

        public async Task<Result<string>> ApproveReviewAsync(Guid documentId, string approvedById, CancellationToken cancellationToken)
        {
            try
            {
                var doc = await dbContext.ApplicantDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
                if (doc == null)
                {
                    return Result<string>.Failure(404, "Document not found");
                }

                var isAuthorizedUser = await IsUserAuthorizedAsync(approvedById);
                if (!isAuthorizedUser)
                {
                    return Result<string>.Failure(403, "User is not authorized to approve reviews");
                }


                doc.Status = DocumentStatus.Approved;
                await dbContext.SaveChangesAsync();

                var applicantId = await GetApplicantId(doc.ApplicationId);
                // send notification
                await notificationService.NotifyAsync(new CreateNotificationRequest(
                    UserId: applicantId,
                    ApplicationId: doc.ApplicationId,
                    Type: NotificationType.DocumentApproved,
                    Message: $"Your {doc.DocumentType.ToString()} approved successfully."),
                    cancellationToken);

                return Result<string>.Success("Document approved successfully");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(500, $"An error occurred while approving the document review: {ex.Message}");
            }
        }

        public async Task<Result<string>> RejectReviewAsync(Guid documentId, string rejectedById, CancellationToken cancellationToken)
        {
            try
            {
                var doc = await dbContext.ApplicantDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
                if (doc == null)
                {
                    return Result<string>.Failure(404, "Document not found");
                }

                var isAuthorizedUser = await IsUserAuthorizedAsync(rejectedById);
                if (!isAuthorizedUser)
                {
                    return Result<string>.Failure(403, "User is not authorized to reject reviews");
                }

                doc.Status = DocumentStatus.Rejected;
                await dbContext.SaveChangesAsync();

                var applicantId = await GetApplicantId(doc.ApplicationId);
                // send notification
                await notificationService.NotifyAsync(new CreateNotificationRequest(
                    UserId: applicantId,
                    ApplicationId: doc.ApplicationId,
                    Type: NotificationType.DocumentRejected,
                    Message: $"Unfortunately, your {doc.DocumentType.ToString()} rejected, see details."),
                    cancellationToken);

                return Result<string>.Success("Document rejected successfully");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(500, $"An error occurred while rejecting the document review: {ex.Message}");
            }
        }

        public async Task<Result<DocumentFileDto>> GetDocumentFileAsync(Guid documentId)
        {
            try
            {
                var document = await dbContext.ApplicantDocuments.AsNoTracking()
                    .SingleOrDefaultAsync(d => d.Id == documentId);

                if (document is null)
                {
                    return Result<DocumentFileDto>.Failure(404, "Document not found");
                }

                if (string.IsNullOrWhiteSpace(document.StorageKey))
                {
                    return Result<DocumentFileDto>.Failure(400, "The document does not have a stored file.");
                }

                var stream = await fileStorageService.OpenReadAsync(document.StorageKey);

                var contentType = GetContentType(document.StorageKey);

                var fileName = string.IsNullOrWhiteSpace(
                    document.OriginalFileName)
                        ? Path.GetFileName(document.StorageKey)
                        : document.OriginalFileName;

                return Result<DocumentFileDto>.Success(new DocumentFileDto
                (
                    Content: stream,
                    ContentType: contentType,
                    FileName: fileName
                ));
            }
            catch (Exception ex)
            {
                return Result<DocumentFileDto>.Failure(500, $"An error occurred while retrieving the document file: {ex.Message}");
            }
        }

        // helpers
        #region Helpers
        private async Task<bool> IsUserAuthorizedAsync(string userId)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }
            return await userManager.IsInRoleAsync(user, "Admin") || await userManager.IsInRoleAsync(user, "Operation Manager");
        }

        private async Task<string> GetApplicantId(Guid applicationId)
        {
            var id = await admissionDbContext.Applications
                .Where(a => a.Id == applicationId)
                .Select(a => a.ApplicantUserId)
                .FirstOrDefaultAsync();
            return id ?? string.Empty;
        }

        private static string GetContentType(string storageKey)
        {
            var extension = Path
                .GetExtension(storageKey)
                .ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
        }
        #endregion
    }
}
