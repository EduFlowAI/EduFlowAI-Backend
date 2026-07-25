using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.Admission.Application.Services;

// Fills the AI module's status port by delegating to this module's status query service.
public sealed class ApplicationStatusReader : IApplicationStatusReader
{
    private readonly IApplicationStatusQueryService _statusQuery;

    public ApplicationStatusReader(IApplicationStatusQueryService statusQuery)
    {
        ArgumentNullException.ThrowIfNull(statusQuery);
        _statusQuery = statusQuery;
    }

    public async Task<ApplicantStatusDto?> GetCurrentStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        var status = await _statusQuery.GetCurrentApplicationStatusForApplicantAsync(userId, cancellationToken);
        if (status is null)
            return null;

        return new ApplicantStatusDto
        {
            Status = status.CurrentStatus,
            LastUpdatedAt = status.LastUpdatedAt
        };
    }
}
