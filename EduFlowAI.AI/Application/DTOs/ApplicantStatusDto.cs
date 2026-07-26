using System;

namespace EduFlowAI.AI.Application.DTOs;

// Status is the ApplicationStatus enum name.
public class ApplicantStatusDto
{
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset LastUpdatedAt { get; set; }
}
