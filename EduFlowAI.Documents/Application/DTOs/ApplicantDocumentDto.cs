using System;
using EduFlowAI.Documents.Domain.Enums;

namespace EduFlowAI.Documents.Application.DTOs;

public sealed record ApplicantDocumentDto(
    Guid Id,
    DocumentType DocumentType,
    string OriginalFileName,
    DocumentStatus Status,
    DateTimeOffset CreatedAt
);