using EduFlowAI.Admission.Application.DbContextAbstraction;
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
    public class ReplacementRequestService(IDocumentDbContext dbContext, UserManager<AppUser> userManager, IAdmissionDbContext admissionDbContext) : IReplacementRequestService
    {
        public async Task<Result<string>> SendReplacementRequest(ReplacementRequestDto request, string requestedById)
        {
            try
            {
                var doc = await dbContext.ApplicantDocuments.FirstOrDefaultAsync(d => d.Id == request.DocumentId);
                if (doc == null)
                {
                    return Result<string>.Failure(404, "Document not found");
                }

                var isAuthorizedUser = await IsUserAuthorizedAsync(requestedById);
                if (!isAuthorizedUser)
                {
                    return Result<string>.Failure(403, "User is not authorized to send replacement requests");
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
                await dbContext.SaveChangesAsync();

                // may send notification/email to applicant about replacement request here

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
        private async Task<bool> IsUserAuthorizedAsync(string userId)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }
            return await userManager.IsInRoleAsync(user, "Admin") || await userManager.IsInRoleAsync(user, "Operation Manager");
        }
    }
}
