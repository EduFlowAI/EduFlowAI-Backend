namespace EduFlowAI.Documents.Application.DTOs;

public sealed record ApplicantDocumentStatusDto
{
    public Guid ApplicationId { get; init; }

    public string OverallStatus { get; init; } = string.Empty;

    public IReadOnlyList<DocumentStatusItemDto> Documents { get; init; }
        = [];

    public DateTimeOffset LastUpdatedAt { get; init; }
}

public sealed record DocumentStatusItemDto
{
    public Guid DocumentId { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? StatusMessage { get; init; }
}