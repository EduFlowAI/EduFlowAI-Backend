using System;

namespace EduFlowAI.AI.Application.DTOs;

public class RetrievedChunkDto
{
    public string Content { get; set; } = string.Empty;

    public string SourceTitle { get; set; } = string.Empty;

    public Guid DocumentId { get; set; }
}
