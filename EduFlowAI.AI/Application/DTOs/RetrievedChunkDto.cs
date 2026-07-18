using System;

namespace EduFlowAI.AI.Application.DTOs;

/// <summary>
/// A retrieved knowledge-base chunk together with the document it came from.
///
/// The source matters: without it the model receives anonymous text and cannot answer questions
/// about what the knowledge base actually covers (e.g. "which tracks are available?" was answered
/// with "no list of tracks found", even though the only indexed document *was* a track brochure).
/// It also gives the frontend the data it needs to cite sources.
/// </summary>
public class RetrievedChunkDto
{
    public string Content { get; set; } = string.Empty;

    /// <summary>Human-readable document name (the uploaded file name).</summary>
    public string SourceTitle { get; set; } = string.Empty;

    public Guid DocumentId { get; set; }
}
