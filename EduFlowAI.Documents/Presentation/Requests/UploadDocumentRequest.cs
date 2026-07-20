using EduFlowAI.Documents.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace EduFlowAI.Documents.Presentation.Requests;

public sealed class UploadDocumentRequest
{
    public IFormFile? File { get; init; }
    public DocumentType DocumentType { get; init; }
}
