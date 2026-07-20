using EduFlowAI.Documents.Application.DTOs;
using EduFlowAI.Documents.Domain.Enums;
using EduFlowAI.Shared.Kernel.Common;
using Microsoft.AspNetCore.Http;

namespace EduFlowAI.Documents.Application.Interfaces;

public interface IApplicantDocumentService
{
    Task<Result<ApplicationDocumentsDto>> ListAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<Result<UploadDocumentDto>> UploadAsync(
        Guid applicationId,
        DocumentType documentType,
        IFormFile file,
        CancellationToken cancellationToken);

    Task<Result<SubmitDocumentsDto>> SubmitAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<Result<DocumentFileDto>> OpenFileAsync(Guid documentId, CancellationToken cancellationToken);
}
