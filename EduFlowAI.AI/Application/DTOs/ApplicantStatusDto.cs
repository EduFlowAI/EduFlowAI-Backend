using System;

namespace EduFlowAI.AI.Application.DTOs;

// What the status reader port hands back. Status is the Admission ApplicationStatus enum name.
public class ApplicantStatusDto
{
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset LastUpdatedAt { get; set; }
}
