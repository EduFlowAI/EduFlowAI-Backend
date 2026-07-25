using System;
using Microsoft.AspNetCore.Http;
using EduFlowAI.Documents.Domain.Enums;

namespace EduFlowAI.Documents.Application.DTOs
{
    public sealed record UploadDocumentDto(
        Guid ApplicationId,
        DocumentType DocumentType,
        IFormFile File
    );
}