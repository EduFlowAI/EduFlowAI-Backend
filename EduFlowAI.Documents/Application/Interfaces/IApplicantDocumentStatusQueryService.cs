using EduFlowAI.Documents.Application.DTOs;

namespace EduFlowAI.Documents.Application.Interfaces;

public interface IApplicantDocumentStatusQueryService
{
    Task<ApplicantDocumentStatusDto?>
        GetCurrentDocumentStatusForApplicantAsync(
            string applicantUserId,
            CancellationToken cancellationToken = default);
}