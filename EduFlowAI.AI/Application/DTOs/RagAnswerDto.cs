using System.Collections.Generic;

namespace EduFlowAI.AI.Application.DTOs;

/// <summary>
/// A grounded answer plus the documents it drew on. Sources let the caller show provenance
/// (the Angular assistant has a source-citation component) and make answers auditable.
/// </summary>
public class RagAnswerDto
{
    public string Answer { get; set; } = string.Empty;

    public List<string> Sources { get; set; } = new();
}
