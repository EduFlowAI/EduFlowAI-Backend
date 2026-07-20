using EduFlowAI.Documents.Domain.Enums;

namespace EduFlowAI.Documents.Application.DTOs;

public sealed record DocumentListItemDto(
    Guid? Id,
    DocumentType DocumentType,
    bool IsRequired,
    bool IsUploaded,
    DocumentStatus? Status,
    string? OriginalFileName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ApplicationDocumentsDto(
    Guid ApplicationId,
    IReadOnlyList<DocumentListItemDto> Documents);

public sealed record UploadDocumentDto(
    Guid Id,
    Guid ApplicationId,
    DocumentType DocumentType,
    DocumentStatus Status,
    string OriginalFileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SubmitDocumentsDto(
    Guid ApplicationId,
    bool Submitted,
    IReadOnlyList<DocumentType> MissingDocumentTypes);

public sealed record DocumentFileDto(
    Stream Stream,
    string ContentType,
    string DownloadFileName);
