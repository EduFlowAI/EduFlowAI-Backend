using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Communication.Application.Interfaces;
using EduFlowAI.Communication.Application.Interfaces.Emails;
using EduFlowAI.Communication.Domain.Enums;
using EduFlowAI.Documents.Application.DbContextAbstraction;
using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Application.Emails;
using EduFlowAI.Documents.Application.Interfaces;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Identity.Application.Interfaces;
using EduFlowAI.Identity.Domain.Entities;
using EduFlowAI.Shared.Kernel.Common;
using EduFlowAI.Shared.Kernel.Common.Pagination;
using EduFlowAI.Shared.Kernel.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EduFlowAI.Documents.Application.Services
{
    public class ReplacementRequestService(IDocumentDbContext dbContext, IUserContactInfoReader userContactInfoReader, 
        IAdmissionDbContext admissionDbContext, INotificationService notificationService, IEmailDispatcher emailDispatcher, IOutboxPublisher outboxPublisher) 
        : IReplacementRequestService
    {
        public async Task<Result<string>> SendReplacementRequest(ReplacementRequestDto request, string requestedById, CancellationToken cancellationToken)
        {
            try
            {
                var doc = await dbContext.ApplicantDocuments.FirstOrDefaultAsync(d => d.Id == request.DocumentId);
                if (doc == null)
                {
                    return Result<string>.Failure(404, "Document not found");
                }

                var replacementRequest = new Domain.Entities.DocumentReplacementRequest
                {
                    Id = Guid.NewGuid(),
                    DocumentId = request.DocumentId,
                    RequestedByUserId = requestedById,
                    Reason = request.Reason,
                    Status = ReplacementRequestStatus.Open,
                    RequestedAt = DateTimeOffset.UtcNow
                };

                await dbContext.DocumentReplacementRequests.AddAsync(replacementRequest);

                var applicant = await GetApplicant(doc.ApplicationId);
                // send notification
                await notificationService.NotifyAsync(new CreateNotificationRequest(
                    UserId: applicant.UserId,
                    ApplicationId: doc.ApplicationId,
                    Type: NotificationType.DocumentReplacementRequested,
                    Message: $"Your {doc.DocumentType.ToString()} needs to be replaced!"),
                    cancellationToken);
                // send email
                await emailDispatcher.RequestEmailAsync(
                    EmailType.DocumentReplacementRequested,
                    applicant.Email!,
                    new DocumentReplacementRequestEmailData(doc.DocumentType.ToString(), request.Reason),
                    cancellationToken);

                // save transaction
                await outboxPublisher.SaveChangesAndFlushMessagesAsync(cancellationToken);

                return Result<string>.Success("Replacement request sent successfully");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(500, $"An error occurred while sending the replacement request: {ex.Message}");
            }
        }

        public async Task<Result<PaginatedResult<ReplacementDto>>> GetAllReplacemntRequestsAsync(string ApplicantId, QueryParameters queryParameters)
        {
            try
            {
                var apps = await admissionDbContext.Applications.AsNoTracking()
                    .Where(a => a.ApplicantUserId == ApplicantId).ToListAsync();

                if (!apps.Any() || apps is null)
                {
                    Result<PaginatedResult<ReplacementDto>>.Failure(400, "User does not have any applications");
                }

                var query = dbContext.DocumentReplacementRequests.AsNoTracking()
                    .Where(r=>r.Status == ReplacementRequestStatus.Open)
                    .Include(r => r.Document)
                    .Where(r => apps.Any(a => a.Id == r.Document.ApplicationId));

                if(!string.IsNullOrEmpty(queryParameters.Search))
                {
                    query = query.Where(r => r.Reason.Contains(queryParameters.Search));
                }
                if(!string.IsNullOrEmpty(queryParameters.Status)) {
                    query = query.Where(r => r.Status.ToString().Contains(queryParameters.Status));
                }
                else
                {
                    query = query.OrderBy(r => r.RequestedAt);
                }

                var totalCount = await query.CountAsync();
                var items = await query
                    .Skip((queryParameters.Page - 1) * queryParameters.PageSize)
                    .Take(queryParameters.PageSize)
                    .Select(r => new ReplacementDto(
                        r.Id,
                        r.DocumentId,
                        r.Reason,
                        r.Status,
                        r.RequestedAt
                    ))
                    .ToListAsync();
                var paginatedResult = new PaginatedResult<ReplacementDto>
                {
                    Data = items,
                    PageSize = queryParameters.PageSize,
                    CurrentPage = queryParameters.Page,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)queryParameters.PageSize),
                    TotalCount = totalCount
                };

                return Result<PaginatedResult<ReplacementDto>>.Success(paginatedResult);
            }
            catch (Exception ex)
            {
                return Result<PaginatedResult<ReplacementDto>>.Failure(500, $"An error occurred while retrieving replacement requests: {ex.Message}");
            }
        }

        public async Task<Result<ReplacementDto>> GetReplacemntRequestAsync(Guid requestId)
        {
            try
            {
                var req = await dbContext.DocumentReplacementRequests.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if(req is null)
                {
                    return Result<ReplacementDto>.Failure(404, "Request not found");
                }

                var dto = new ReplacementDto
                    (
                        requestId,
                        req.DocumentId,
                        req.Reason,
                        req.Status,
                        req.RequestedAt
                    );

                return Result<ReplacementDto>.Success(dto);
            }
            catch (Exception ex)
            {
                return Result<ReplacementDto>.Failure(500, $"An error occurred while retrieving the replacement request: {ex.Message}");
            }
        }

        // helpers
        private async Task<UserContactInfo> GetApplicant(Guid applicationId)
        {
            var id = await admissionDbContext.Applications
                .Where(a => a.Id == applicationId)
                .Select(a => a.ApplicantUserId)
                .FirstOrDefaultAsync();

            var user = await userContactInfoReader.GetContactInfoAsync(id!, default);

            return user;
        }
    }
}
